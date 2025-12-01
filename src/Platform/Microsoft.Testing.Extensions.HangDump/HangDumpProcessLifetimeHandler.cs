// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

#if NETCOREAPP
using Microsoft.Diagnostics.NETCore.Client;
#endif

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer, IDataProducer,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly IMessageBus _messageBus;
    private readonly OutputDeviceWriter _outputDisplay;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IProcessHandler _processHandler;
    private readonly IClock _clock;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly bool _traceEnabled;
    private readonly ILogger<HangDumpProcessLifetimeHandler> _logger;
    private readonly ManualResetEventSlim _waitConsumerPipeName = new(false);
    private readonly List<string> _dumpFiles = [];

    private TimeSpan? _activityTimerValue;
    private Timer? _activityTimer;
    private Task? _waitConnectionTask;
    private Task? _activityIndicatorTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private string _dumpType = "Full";
    private string? _dumpFileNamePattern;
    private ITestHostProcessInformation? _testHostProcessInformation;
    private NamedPipeClient? _namedPipeClient;

    public HangDumpProcessLifetimeHandler(
        PipeNameDescription pipeNameDescription,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ICommandLineOptions commandLineOptions,
        ITask task,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IProcessHandler processHandler,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpProcessLifetimeHandler>();
        _traceEnabled = _logger.IsEnabled(LogLevel.Trace);
        _pipeNameDescription = pipeNameDescription;
        _messageBus = messageBus;
        _outputDisplay = new OutputDeviceWriter(outputDevice, this);
        _commandLineOptions = commandLineOptions;
        _task = task;
        _environment = environment;
        _configuration = configuration;
        _processHandler = processHandler;
        _clock = clock;
    }

    public string Uid => nameof(HangDumpProcessLifetimeHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName));

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        _activityTimerValue = _commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : TimeSpan.FromMinutes(30);

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeOptionName, out string[]? dumpType))
        {
            _dumpType = dumpType[0];
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpFileNameOptionName, out string[]? fileName))
        {
            _dumpFileNamePattern = fileName[0];
        }

        await _logger.LogInformationAsync($"Hang dump timeout setup {_activityTimerValue}.").ConfigureAwait(false);

        _waitConnectionTask = _task.Run(
            async () =>
        {
            _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
            _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _singleConnectionNamedPipeServer.RegisterSerializer(new SessionEndSerializerRequestSerializer(), typeof(SessionEndSerializerRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new ActivitySignalRequestSerializer(), typeof(ActivitySignalRequest));
            await _logger.LogDebugAsync($"Waiting for connection to {_singleConnectionNamedPipeServer.PipeName.Name}").ConfigureAwait(false);
            await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is SessionEndSerializerRequest)
        {
            await _logger.LogDebugAsync("Session end received by the test host").ConfigureAwait(false);

#pragma warning disable IDE0031 // Null check can be simplified - analyzer doesn't consider multigating. But we can fix when https://github.com/dotnet/csharplang/issues/8631 is implemented.
            if (_activityTimer is not null)
            {
#if NETCOREAPP
                await _activityTimer.DisposeAsync().ConfigureAwait(false);
#else
                _activityTimer.Dispose();
#endif
            }

            _namedPipeClient?.Dispose();
            return VoidResponse.CachedInstance;
        }
        else if (request is ConsumerPipeNameRequest consumerPipeNameRequest)
        {
            await _logger.LogDebugAsync($"Consumer pipe name received '{consumerPipeNameRequest.PipeName}'").ConfigureAwait(false);
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName, _environment);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _namedPipeClient.RegisterSerializer(new ExitSignalActivityIndicatorTaskRequestSerializer(), typeof(ExitSignalActivityIndicatorTaskRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _waitConsumerPipeName.Set();
            return VoidResponse.CachedInstance;
        }
        else if (request is ActivitySignalRequest)
        {
            if (_traceEnabled)
            {
                _logger.LogTrace($"Activity signal received by the test host '{_clock.UtcNow}'");
            }

            _activityTimer?.Change(_activityTimerValue!.Value, TimeSpan.FromMilliseconds(-1));
            return VoidResponse.CachedInstance;
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request));
        }
    }

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_waitConnectionTask is not null);
        ApplicationStateGuard.Ensure(_singleConnectionNamedPipeServer is not null);
        try
        {
            _testHostProcessInformation = testHostProcessInformation;

            await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{_singleConnectionNamedPipeServer.PipeName.Name}'").ConfigureAwait(false);
            await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
            ApplicationStateGuard.Ensure(_namedPipeClient is not null);
            await _namedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'").ConfigureAwait(false);

            _activityTimer = new Timer(
                _ => _activityIndicatorTask = TakeDumpOfTreeAsync(cancellationToken),
                null,
                _activityTimerValue!.Value,
                TimeSpan.FromMilliseconds(-1));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string GetDiskInfo()
    {
        var builder = new StringBuilder();
        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo d in allDrives)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Drive {d.Name}");
            if (d.IsReady)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Available free space: {d.AvailableFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total free space: {d.TotalFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total size: {d.TotalSize} bytes");
            }
        }

        return builder.ToString();
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!testHostProcessInformation.HasExitedGracefully)
        {
            _logger.LogDebug($"Testhost didn't exit gracefully '{testHostProcessInformation.ExitCode}')");
        }

        foreach (string dumpFile in _dumpFiles)
        {
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), ExtensionResources.HangDumpArtifactDisplayName, ExtensionResources.HangDumpArtifactDescription)).ConfigureAwait(false);
        }
    }

    private async Task TakeDumpOfTreeAsync(CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);

        await _logger.LogInformationAsync($"Hang dump timeout({_activityTimerValue}) expired.").ConfigureAwait(false);
        await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)), cancellationToken).ConfigureAwait(false);

        using IProcess process = _processHandler.GetProcessById(_testHostProcessInformation.PID);
        var processTree = (await process.GetProcessTreeAsync(_logger, _outputDisplay, cancellationToken).ConfigureAwait(false)).Where(p => p.Process?.Name is not null and not "conhost" and not "WerFault").ToList();
        IEnumerable<IProcess> bottomUpTree = processTree.OrderByDescending(t => t.Level).Select(t => t.Process).OfType<IProcess>();

        try
        {
            if (processTree.Count > 1)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(ExtensionResources.DumpingProcessTree), cancellationToken).ConfigureAwait(false);

                foreach (ProcessTreeNode? p in processTree.OrderBy(t => t.Level))
                {
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"{(p.Level != 0 ? " + " : " > ")}{new string('-', p.Level)} {p.Process!.Id} - {p.Process.Name}"), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.DumpingProcess, process.Id, process.Name)), cancellationToken).ConfigureAwait(false);
            }

            await _logger.LogInformationAsync($"Hang dump timeout({_activityTimerValue}) expired.").ConfigureAwait(false);

            // Do not suspend processes with NetClient dumper it stops the diagnostic thread running in
            // them and hang dump request will get stuck forever, because the process is not co-operating.
            // Instead we start one task per dump asynchronously, and hope that the parent process will start dumping
            // before the child process is done dumping. This way if the parent is waiting for the children to exit,
            // we will be dumping it before it observes the child exiting and we get a more accurate results. If we did not
            // do this, then parent that is awaiting child might exit before we get to dumping it.
            foreach (IProcess p in bottomUpTree)
            {
                try
                {
                    await TakeDumpAsync(p, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // exceptions.Add(new InvalidOperationException($"Error while taking dump of process {p.Name} {p.Id}", e));
                    await _logger.LogErrorAsync($"Error while taking dump of process {p.Id} - {p.Name}", e).ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, p.Id, p.Name, e)), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            NotifyCrashDumpServiceIfEnabled();

            // Some of the processes might crashed, which breaks the process tree (on windows it is just an illusion),
            // so try extra hard to kill all the known processes in the tree, since we already spent a bunch of time getting
            // to know which processes are involved.
            foreach (ProcessTreeNode node in processTree)
            {
                IProcess? p = node.Process;
                if (p == null)
                {
                    continue;
                }

                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        await p.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    await _logger.LogErrorAsync($"Problem killing {p.Id} - {p.Name}", e).ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorKillingProcess, p.Id, p.Name, e)), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task TakeDumpAsync(IProcess process, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);
        ApplicationStateGuard.Ensure(_dumpType is not null);

        string finalDumpFileName = (_dumpFileNamePattern ?? $"{process.Name}_%p_hang.dmp").Replace("%p", process.Id.ToString(CultureInfo.InvariantCulture));
        finalDumpFileName = Path.Combine(_configuration.GetTestResultDirectory(), finalDumpFileName);

        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        GetInProgressTestsResponse tests = await _namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), cancellationToken).ConfigureAwait(false);
        await _namedPipeClient.RequestReplyAsync<ExitSignalActivityIndicatorTaskRequest, VoidResponse>(new ExitSignalActivityIndicatorTaskRequest(), cancellationToken).ConfigureAwait(false);
        if (tests.Tests.Length > 0)
        {
            string hangTestsFileName = Path.Combine(_configuration.GetTestResultDirectory(), Path.ChangeExtension(Path.GetFileName(finalDumpFileName), ".log"));
            using (FileStream fs = File.OpenWrite(hangTestsFileName))
            using (StreamWriter sw = new(fs))
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(ExtensionResources.RunningTestsWhileDumping), cancellationToken).ConfigureAwait(false);
                foreach ((string testName, int seconds) in tests.Tests)
                {
                    await sw.WriteLineAsync($"[{TimeSpan.FromSeconds(seconds)}] {testName}").ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"[{TimeSpan.FromSeconds(seconds)}] {testName}"), cancellationToken).ConfigureAwait(false);
                }
            }

            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(hangTestsFileName), ExtensionResources.HangTestListArtifactDisplayName, ExtensionResources.HangTestListArtifactDescription)).ConfigureAwait(false);
        }

        await _logger.LogInformationAsync($"Creating dump filename {finalDumpFileName}").ConfigureAwait(false);

        await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.CreatingDumpFile, finalDumpFileName)), cancellationToken).ConfigureAwait(false);

#if NETCOREAPP
        DiagnosticsClient diagnosticsClient = new(process.Id);
        DumpType dumpType = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => DumpType.Normal,
            "heap" => DumpType.WithHeap,
            "triage" => DumpType.Triage,
            "full" => DumpType.Full,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        // Wrap the dump path into "" when it has space in it, this is a workaround for this runtime issue: https://github.com/dotnet/diagnostics/issues/5020
        // It only affects windows. Otherwise the dump creation fails with: [createdump] The pid argument is no longer supported
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && finalDumpFileName.Contains(' '))
        {
            finalDumpFileName = $"\"{finalDumpFileName}\"";
        }

        try
        {
            diagnosticsClient.WriteDump(dumpType, finalDumpFileName, logDumpGeneration: false);
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }

#else
        MiniDumpWriteDump.MiniDumpTypeOption miniDumpTypeOption = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => MiniDumpWriteDump.MiniDumpTypeOption.Mini,
            "heap" => MiniDumpWriteDump.MiniDumpTypeOption.Heap,
            "full" => MiniDumpWriteDump.MiniDumpTypeOption.Full,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        try
        {
            MiniDumpWriteDump.CollectDumpUsingMiniDumpWriteDump(process.Id, finalDumpFileName, miniDumpTypeOption);
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }
#endif

        _dumpFiles.Add(finalDumpFileName);
    }

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");

    public void Dispose()
    {
        if (_activityIndicatorTask is not null)
        {
            bool waitResult;
            try
            {
                waitResult = _activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
            }
            catch (Exception e)
            {
                _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).GetAwaiter().GetResult();
                throw;
            }

            if (!waitResult)
            {
                throw new InvalidOperationException($"_activityIndicatorTask didn't exit in {TimeoutHelper.DefaultHangTimeSpanTimeout} seconds");
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_activityIndicatorTask is not null)
        {
            try
            {
                await _activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }
#endif
}

internal sealed class OutputDeviceWriter
{
    private readonly IOutputDevice _outputDevice;
    private readonly IOutputDeviceDataProducer _outputDeviceDataProducer;

    public OutputDeviceWriter(IOutputDevice outputDevice, IOutputDeviceDataProducer outputDeviceDataProducer)
    {
        _outputDevice = outputDevice;
        _outputDeviceDataProducer = outputDeviceDataProducer;
    }

    /// <summary>
    /// Displays the output data asynchronously, using the stored producer.
    /// </summary>
    /// <param name="data">The output data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisplayAsync(IOutputDeviceData data, CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(_outputDeviceDataProducer, data, cancellationToken).ConfigureAwait(false);
}

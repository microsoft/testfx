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
using Microsoft.Testing.Platform.Services;

#if NETCOREAPP
using Microsoft.Diagnostics.NETCore.Client;
#endif

namespace Microsoft.Testing.Extensions.Diagnostics;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
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

    // Guards the "take the dump only once" gate (_dumpTaken) together with publishing the running
    // dump task (_activityIndicatorTask), so disposal always observes and awaits the winning dump.
#if NET9_0_OR_GREATER
    private readonly Lock _dumpLock = new();
#else
    private readonly object _dumpLock = new();
#endif

    private TimeSpan? _activityTimerValue;
    private Timer? _activityTimer;
    private DateTimeOffset? _deadlineDumpAt;
    private Timer? _deadlineTimer;

    /// <summary>
    /// <see cref="Timer"/> throws for due times above ~49.7 days (its internal limit is
    /// <see cref="uint.MaxValue"/> milliseconds). A deadline that far out is effectively "never"
    /// for a test run, so we clamp to this maximum instead of throwing during setup.
    /// </summary>
    private static readonly TimeSpan MaxTimerDueTime = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private int _dumpTaken;
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

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(HangDumpOptions.IsEnabled(_commandLineOptions));

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        _activityTimerValue = _commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : TimeSpan.FromMinutes(30);

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeOptionName, out string[]? dumpType))
        {
            _dumpType = dumpType[0];
        }
        else if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, out string[]? dumpTypeIfSupported))
        {
            // The "-if-supported" variant accepts the full set of dump types regardless of TFM
            // (see HangDumpCommandLineProvider.ValidateOptionArgumentsAsync). When the user
            // requests a value that the current runtime cannot honor, fall back to the closest
            // supported value (see MapToSupportedDumpType) and emit a single informational
            // message so the CI log makes the substitution visible without breaking the run.
            string requested = dumpTypeIfSupported[0];
            _dumpType = HangDumpCommandLineProvider.MapToSupportedDumpType(requested);
            if (!string.Equals(_dumpType, requested, StringComparison.OrdinalIgnoreCase))
            {
                await _outputDisplay.DisplayAsync(
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeIfSupportedFallbackInfoMessage, requested, _dumpType)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpFileNameOptionName, out string[]? fileName))
        {
            _dumpFileNamePattern = fileName[0];
        }

        await _logger.LogInformationAsync($"Hang dump timeout setup {_activityTimerValue}.").ConfigureAwait(false);

        // In addition to the inactivity timeout above, honor an absolute CI deadline (if provided).
        // We compute the wall-clock instant at which we should start taking the dump so that the dump
        // has a chance to complete before the CI runner hard-kills the process.
        if (DeadlineHelper.TryGetDeadline(_environment, out DateTimeOffset deadline))
        {
            _deadlineDumpAt = DeadlineHelper.SubtractSaturating(deadline, DeadlineHelper.GetDumpMargin(_environment));
            await _logger.LogInformationAsync($"Hang dump deadline setup {_deadlineDumpAt:o}.").ConfigureAwait(false);
        }

        _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
        _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ActivitySignalRequestSerializer(), typeof(ActivitySignalRequest));

        _waitConnectionTask = _task.Run(
            async () =>
            {
                await _logger.LogDebugAsync($"Waiting for connection to {_singleConnectionNamedPipeServer.PipeName.Name}").ConfigureAwait(false);
                await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is ConsumerPipeNameRequest consumerPipeNameRequest)
        {
            await _logger.LogDebugAsync($"Consumer pipe name received '{consumerPipeNameRequest.PipeName}'").ConfigureAwait(false);
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName, _environment);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
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

        _testHostProcessInformation = testHostProcessInformation;

        // Arm the absolute CI deadline timer as early as possible, before we block on the pipe
        // handshake below. If the test host wedges during startup (never connects back over the
        // pipe), those waits would otherwise block well past the deadline and the deadline dump/kill
        // would never be armed, which defeats the purpose of the deadline. The dump path only needs
        // the test host PID, which we already have here; the in-progress-test list (which needs the
        // consumer pipe) is best-effort and skipped when the pipe never connected.
        if (_deadlineDumpAt is { } deadlineDumpAt)
        {
            TimeSpan dueTime = deadlineDumpAt - _clock.UtcNow;
            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }
            else if (dueTime > MaxTimerDueTime)
            {
                // Clamp far-future deadlines so the Timer ctor does not throw. The run (and this
                // timer) is disposed long before the clamped due time elapses, so it never fires early.
                dueTime = MaxTimerDueTime;
            }

            _deadlineTimer = new Timer(
                _ => TriggerDumpOnce(cancellationToken, triggeredByDeadline: true),
                null,
                dueTime,
                TimeSpan.FromMilliseconds(-1));
        }

        await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{_singleConnectionNamedPipeServer.PipeName.Name}'").ConfigureAwait(false);
        await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        await _namedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'").ConfigureAwait(false);

        // The inactivity timer only makes sense once the host has connected and can send activity
        // signals; before that there is nothing to reset it. The deadline timer above is independent.
        _activityTimer = new Timer(
            _ => TriggerDumpOnce(cancellationToken, triggeredByDeadline: false),
            null,
            _activityTimerValue!.Value,
            TimeSpan.FromMilliseconds(-1));
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
        cancellationToken.ThrowIfCancellationRequested();

        if (_activityTimer is not null)
        {
#if NETCOREAPP
            await _activityTimer.DisposeAsync().ConfigureAwait(false);
#else
            _activityTimer.Dispose();
#endif
        }

        if (_deadlineTimer is not null)
        {
#if NETCOREAPP
            await _deadlineTimer.DisposeAsync().ConfigureAwait(false);
#else
            _deadlineTimer.Dispose();
#endif
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

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private void TriggerDumpOnce(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        // The inactivity timer and the deadline timer can both fire, and disposal can run
        // concurrently. Claim the gate and publish the running dump task under the same lock, so both
        // disposal paths (which take the lock, claim the gate, and capture _activityIndicatorTask)
        // always observe and await the winning dump instead of tearing down the pipes underneath it.
        lock (_dumpLock)
        {
            if (_dumpTaken != 0)
            {
                return;
            }

            _dumpTaken = 1;
            _activityIndicatorTask = TakeDumpOfTreeAsync(cancellationToken, triggeredByDeadline);
        }
    }

    private async Task TakeDumpOfTreeAsync(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);

        string dumpReason = triggeredByDeadline
            ? $"CI deadline approaching (dump scheduled at {_deadlineDumpAt:o})"
            : $"Hang dump timeout({_activityTimerValue}) expired";

        await _logger.LogInformationAsync($"{dumpReason}. Taking hang dump.").ConfigureAwait(false);
        await _outputDisplay.DisplayAsync(
            new ErrorMessageOutputDeviceData(triggeredByDeadline
                ? ExtensionResources.HangDumpDeadlineApproaching
                : string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)),
            cancellationToken).ConfigureAwait(false);

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

            await _logger.LogInformationAsync($"{dumpReason}.").ConfigureAwait(false);

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
                        await p.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
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

        string processId = process.Id.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(process.Name, processId, _clock.UtcNow);

        string pattern = _dumpFileNamePattern ?? $"{process.Name}_%p_hang.dmp";

        // First resolve {placeholder} templates, then handle legacy %p pattern for backward compatibility.
        string finalDumpFileName = ArtifactNamingHelper.ResolveTemplate(pattern, replacements)
            .Replace("%p", processId);
        string resultsDirectory = Path.GetFullPath(_configuration.GetTestResultDirectory());
        finalDumpFileName = Path.GetFullPath(Path.Combine(resultsDirectory, finalDumpFileName));

        // Reject resolved paths that escape the results directory (e.g. rooted paths or ".." segments).
        // Append a trailing separator to prevent sibling-directory bypass (e.g. "/tmp/results" vs "/tmp/results-evil").
        // Use case-insensitive comparison on Windows where paths are case-insensitive.
        StringComparison pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string separatorStr = Path.DirectorySeparatorChar.ToString();
        string resultsDirectoryGuard = resultsDirectory.EndsWith(separatorStr, StringComparison.Ordinal)
            ? resultsDirectory
            : resultsDirectory + separatorStr;
        if (!finalDumpFileName.StartsWith(resultsDirectoryGuard, pathComparison))
        {
            throw new InvalidOperationException($"The resolved dump file path '{finalDumpFileName}' is outside the results directory '{resultsDirectory}'. Ensure --hangdump-filename is a relative path without '..' segments.");
        }

        // Ensure the destination directory exists (templates may include directory separators, e.g. {asm}/{pname}).
        Directory.CreateDirectory(Path.GetDirectoryName(finalDumpFileName)!);

        // The consumer pipe is only usable once the test host connected back over it. A non-null
        // client is not enough: it is created when the host sends its pipe name but only connected
        // later, so a deadline dump firing in that window (or a host that wedged during startup)
        // would hit an unconnected pipe. Treat the in-progress-test list as best-effort: any query
        // failure is logged and swallowed so it can never block taking the dump and killing the tree.
        if (_namedPipeClient is not null)
        {
            try
            {
                GetInProgressTestsResponse tests = await _namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), cancellationToken).ConfigureAwait(false);
                if (tests.Tests.Length > 0)
                {
                    string hangTestsFileName = Path.ChangeExtension(finalDumpFileName, ".log");
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
            }
            catch (Exception ex)
            {
                await _logger.LogDebugAsync($"Could not collect the in-progress tests before dumping (the consumer pipe may not be connected). Continuing with the dump. {ex}").ConfigureAwait(false);
            }
        }

        await _logger.LogInformationAsync($"Creating dump filename {finalDumpFileName}").ConfigureAwait(false);

        await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.CreatingDumpFile, finalDumpFileName)), cancellationToken).ConfigureAwait(false);

#if NETCOREAPP
        DiagnosticsClient diagnosticsClient = new(process.Id);
        DumpType? dumpType = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => DumpType.Normal,
            "heap" => DumpType.WithHeap,
            "triage" => DumpType.Triage,
            "full" => DumpType.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        DumpFileNames dumpFileNames = GetDumpFileNames(finalDumpFileName);

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (dumpType.HasValue)
            {
                diagnosticsClient.WriteDump(dumpType.Value, dumpFileNames.WriteDumpFileName, logDumpGeneration: false);
                _dumpFiles.Add(dumpFileNames.ArtifactDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }

#else
        MiniDumpWriteDump.MiniDumpTypeOption? miniDumpTypeOption = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => MiniDumpWriteDump.MiniDumpTypeOption.Mini,
            "heap" => MiniDumpWriteDump.MiniDumpTypeOption.Heap,
            "full" => MiniDumpWriteDump.MiniDumpTypeOption.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (miniDumpTypeOption.HasValue)
            {
                MiniDumpWriteDump.CollectDumpUsingMiniDumpWriteDump(process.Id, finalDumpFileName, miniDumpTypeOption.Value);
                _dumpFiles.Add(finalDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    // Wrap the dump path into "" when it has space in it, this is a workaround for this runtime issue: https://github.com/dotnet/diagnostics/issues/5020
    // It only affects windows. Otherwise the dump creation fails with: [createdump] The pid argument is no longer supported
    internal static DumpFileNames GetDumpFileNames(string dumpFileName)
        => new(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && dumpFileName.Contains(' ')
                ? $"\"{dumpFileName}\""
                : dumpFileName,
            dumpFileName);

    internal readonly record struct DumpFileNames(string WriteDumpFileName, string ArtifactDumpFileName);

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");

    public void Dispose()
    {
        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Claim the gate so no timer callback can start a new dump once we begin tearing down the
            // pipes, and capture any dump already in flight so we wait for it below.
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            bool waitResult;
            try
            {
                waitResult = activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
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
        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Claim the gate so no timer callback can start a new dump once we begin tearing down the
            // pipes, and capture any dump already in flight so we await it below.
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            try
            {
                await activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
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

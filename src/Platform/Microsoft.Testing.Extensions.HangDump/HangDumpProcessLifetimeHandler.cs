// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
#if NETCOREAPP
using System.Runtime.InteropServices;
#endif

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform;
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

internal sealed class HangDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer, IDataProducer,
#if NETCOREAPP
    IAsyncDisposable
#else
    IDisposable
#endif
{
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDisplay;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IProcessHandler _processHandler;
    private readonly IClock _clock;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly bool _traceEnabled;
    private readonly ILogger<HangDumpProcessLifetimeHandler> _logger;
    private readonly ManualResetEventSlim _mutexNameReceived = new(false);
    private readonly ManualResetEventSlim _waitConsumerPipeName = new(false);

    private TimeSpan _activityTimerValue = TimeSpan.FromMinutes(30);
    private Task? _waitConnectionTask;
    private Task? _activityIndicatorTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private string? _activityTimerMutexName;
    private bool _exitActivityIndicatorTask;
    private string _dumpType = "Full";
    private string _dumpFileNamePattern;
    private Mutex? _activityIndicatorMutex;
    private ITestHostProcessInformation? _testHostProcessInformation;
    private string _dumpFileTaken = string.Empty;
    private NamedPipeClient? _namedPipeClient;

    public HangDumpProcessLifetimeHandler(
        PipeNameDescription pipeNameDescription,
        IMessageBus messageBus,
        IOutputDevice outputDisplay,
        ICommandLineOptions commandLineOptions,
        ITask task,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IConfiguration configuration,
        IProcessHandler processHandler,
        IServiceProvider serviceProvider,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpProcessLifetimeHandler>();
        _traceEnabled = _logger.IsEnabled(LogLevel.Trace);
        _pipeNameDescription = pipeNameDescription;
        _messageBus = messageBus;
        _outputDisplay = outputDisplay;
        _commandLineOptions = commandLineOptions;
        _task = task;
        _environment = environment;
        _configuration = configuration;
        _processHandler = processHandler;
        _clock = clock;
        _testApplicationCancellationTokenSource = serviceProvider.GetTestApplicationCancellationTokenSource();
        _dumpFileNamePattern = $"{Path.GetFileNameWithoutExtension(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}_%p_hang.dmp";
    }

    public string Uid => nameof(HangDumpProcessLifetimeHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName) &&
        !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey));

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout))
        {
            _activityTimerValue = TimeSpanParser.Parse(timeout[0]);
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeOptionName, out string[]? dumpType))
        {
            _dumpType = dumpType[0];
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpFileNameOptionName, out string[]? fileName))
        {
            _dumpFileNamePattern = fileName[0];
        }

        await _logger.LogInformationAsync($"Hang dump timeout setup {_activityTimerValue}.");

        _waitConnectionTask = _task.Run(
            async () =>
        {
            _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
            _singleConnectionNamedPipeServer.RegisterSerializer(new ActivityIndicatorMutexNameRequestSerializer(), typeof(ActivityIndicatorMutexNameRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _singleConnectionNamedPipeServer.RegisterSerializer(new SessionEndSerializerRequestSerializer(), typeof(SessionEndSerializerRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
            await _logger.LogDebugAsync($"Waiting for connection to {_singleConnectionNamedPipeServer.PipeName.Name}");
            await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken);
        }, cancellationToken);
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is ActivityIndicatorMutexNameRequest activityIndicatorMutexNameRequest)
        {
            await _logger.LogDebugAsync($"Mutex name received by the test host, '{activityIndicatorMutexNameRequest.MutexName}'");
            _activityTimerMutexName = activityIndicatorMutexNameRequest.MutexName;
            _mutexNameReceived.Set();
            return VoidResponse.CachedInstance;
        }
        else if (request is SessionEndSerializerRequest)
        {
            await _logger.LogDebugAsync($"Session end received by the test host");
            _exitActivityIndicatorTask = true;
#if NET
            if (_namedPipeClient is not null)
            {
                await _namedPipeClient.DisposeAsync();
            }
#else
            _namedPipeClient?.Dispose();
#endif
            return VoidResponse.CachedInstance;
        }
        else if (request is ConsumerPipeNameRequest consumerPipeNameRequest)
        {
            await _logger.LogDebugAsync($"Consumer pipe name received '{consumerPipeNameRequest.PipeName}'");
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _namedPipeClient.RegisterSerializer(new ExitSignalActivityIndicatorTaskRequestSerializer(), typeof(ExitSignalActivityIndicatorTaskRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _waitConsumerPipeName.Set();
            return VoidResponse.CachedInstance;
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request));
        }
    }

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        ApplicationStateGuard.Ensure(_waitConnectionTask is not null);
        ApplicationStateGuard.Ensure(_singleConnectionNamedPipeServer is not null);
        try
        {
            _testHostProcessInformation = testHostProcessInformation;

            await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{_singleConnectionNamedPipeServer.PipeName.Name}'");
            await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
            _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
            ApplicationStateGuard.Ensure(_namedPipeClient is not null);
            await _namedPipeClient.ConnectAsync(cancellation).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'");

            // Keep the custom thread to avoid to waste one from thread pool.
            _activityIndicatorTask = _task.RunLongRunning(ActivityTimerAsync, "[HangDump] ActivityTimerAsync", cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        if (!testHostProcessInformation.HasExitedGracefully)
        {
            _logger.LogDebug($"Testhost didn't exit gracefully '{testHostProcessInformation.ExitCode}', disposing _activityIndicatorMutex(is null: '{_activityIndicatorMutex is null}')");
            _activityIndicatorMutex?.Dispose();
        }

        if (!RoslynString.IsNullOrEmpty(_dumpFileTaken))
        {
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(_dumpFileTaken), ExtensionResources.HangDumpArtifactDisplayName, ExtensionResources.HangDumpArtifactDescription));
        }
    }

    private async Task ActivityTimerAsync()
    {
        _logger.LogDebug($"Wait for mutex name from the test host");

        if (!_mutexNameReceived.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.MutexNameReceptionTimeoutErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }

        ApplicationStateGuard.Ensure(_activityTimerMutexName is not null);

        _logger.LogDebug($"Open activity mutex '{_activityTimerMutexName}'");

        if (!Mutex.TryOpenExisting(_activityTimerMutexName, out _activityIndicatorMutex))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.MutexDoesNotExistErrorMessage, _activityTimerMutexName));
        }

        bool timeoutFired = false;
        try
        {
            // Don't wait in async in the while, we need thread affinity for the mutex
            while (true)
            {
                if (_traceEnabled)
                {
                    _logger.LogTrace($"Wait for activity signal");
                }

                if (!_activityIndicatorMutex.WaitOne(_activityTimerValue))
                {
                    timeoutFired = true;
                    break;
                }

                if (_traceEnabled)
                {
                    _logger.LogTrace($"Activity signal received by the test host '{_clock.UtcNow}'");
                }

                // We don't release in case of exit because we will release after the timeout check to unblock the client and exit the task
                if (_exitActivityIndicatorTask)
                {
                    break;
                }
                else
                {
                    _activityIndicatorMutex.ReleaseMutex();
                }
            }

            if (_traceEnabled)
            {
                _logger.LogTrace($"Exit 'ActivityTimerAsync'");
            }
        }
        catch (AbandonedMutexException)
        {
            // If the mutex is abandoned from the test host crash we will get an exception
            _logger.LogDebug($"Mutex '{_activityTimerMutexName}' is abandoned");
        }
        catch (ObjectDisposedException)
        {
            // If test host exit in a non gracefully way on process exit we dispose the mutex to unlock the activity timer.
            // In this way we release also the dispose.
            _logger.LogDebug($"Mutex '{_activityTimerMutexName}' is disposed");
        }

        if (!timeoutFired)
        {
            try
            {
                _logger.LogDebug($"Timeout is not fired release activity mutex handle to allow test host to close");
                _activityIndicatorMutex.ReleaseMutex();
            }
            catch (AbandonedMutexException)
            {
                // If the mutex is abandoned from the test host crash we will get an exception
                _logger.LogDebug($"Mutex '{_activityTimerMutexName}' is abandoned, during last release");
            }
            catch (ObjectDisposedException)
            {
                // If test host exit in a non gracefully way on process exit we dispose the mutex to unlock the activity timer.
                _logger.LogDebug($"Mutex '{_activityTimerMutexName}' is disposed, during last release");
            }
        }

        _activityIndicatorMutex.Dispose();
        _logger.LogDebug($"Activity indicator disposed");

        if (timeoutFired)
        {
            await TakeDumpAsync();
        }
    }

    private async Task TakeDumpAsync()
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);
        ApplicationStateGuard.Ensure(_dumpType is not null);

        await _logger.LogInformationAsync($"Hang dump timeout({_activityTimerValue}) expired.");
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)));

        string finalDumpFileName = _dumpFileNamePattern.Replace("%p", _testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
        finalDumpFileName = Path.Combine(_configuration.GetTestResultDirectory(), finalDumpFileName);

        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        GetInProgressTestsResponse tests = await _namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), _testApplicationCancellationTokenSource.CancellationToken);
        await _namedPipeClient.RequestReplyAsync<ExitSignalActivityIndicatorTaskRequest, VoidResponse>(new ExitSignalActivityIndicatorTaskRequest(), _testApplicationCancellationTokenSource.CancellationToken);
        if (tests.Tests.Length > 0)
        {
            string hangTestsFileName = Path.Combine(_configuration.GetTestResultDirectory(), Path.ChangeExtension(Path.GetFileName(finalDumpFileName), ".log"));
            using (FileStream fs = File.OpenWrite(hangTestsFileName))
            using (StreamWriter sw = new(fs))
            {
                await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(ExtensionResources.RunningTestsWhileDumping));
                foreach ((string testName, int seconds) in tests.Tests)
                {
                    await sw.WriteLineAsync($"[{TimeSpan.FromSeconds(seconds)}] {testName}");
                    await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData($"[{TimeSpan.FromSeconds(seconds)}] {testName}"));
                }
            }

            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(hangTestsFileName), ExtensionResources.HangTestListArtifactDisplayName, ExtensionResources.HangTestListArtifactDescription));
        }

        await _logger.LogInformationAsync($"Creating dump filename {finalDumpFileName}");

        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.CreatingDumpFile, finalDumpFileName)));

#if NETCOREAPP
        DiagnosticsClient diagnosticsClient = new(_testHostProcessInformation.PID);
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

        diagnosticsClient.WriteDump(dumpType, finalDumpFileName, true);
#else
        MiniDumpWriteDump.MiniDumpTypeOption miniDumpTypeOption = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => MiniDumpWriteDump.MiniDumpTypeOption.Mini,
            "heap" => MiniDumpWriteDump.MiniDumpTypeOption.Heap,
            "full" => MiniDumpWriteDump.MiniDumpTypeOption.Full,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        MiniDumpWriteDump.CollectDumpUsingMiniDumpWriteDump(_testHostProcessInformation.PID, finalDumpFileName, miniDumpTypeOption);
#endif

        NotifyCrashDumpServiceIfEnabled();
        using IProcess process = _processHandler.GetProcessById(_testHostProcessInformation.PID);
        process.Kill();
        await process.WaitForExitAsync();
        _dumpFileTaken = finalDumpFileName;
    }

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");

    public void Dispose()
    {
        if (_activityIndicatorTask is not null)
        {
            if (!_activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                throw new InvalidOperationException($"_activityIndicatorTask didn't exit in {TimeoutHelper.DefaultHangTimeSpanTimeout} seconds");
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _mutexNameReceived.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
        _pipeNameDescription.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_activityIndicatorTask is not null)
        {
            await _activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _mutexNameReceived.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
        _pipeNameDescription.Dispose();
    }
#endif
}

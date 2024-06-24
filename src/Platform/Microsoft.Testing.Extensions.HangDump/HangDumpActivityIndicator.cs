// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpActivityIndicator : IDataConsumer, ITestSessionLifetimeHandler,
#if NETCOREAPP
    IAsyncDisposable
#else
    IDisposable
#endif
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger<HangDumpActivityIndicator> _logger;
    private readonly NamedPipeClient? _namedPipeClient;
    private readonly ManualResetEventSlim _signalActivity = new(false);
    private readonly ManualResetEventSlim _mutexCreated = new(false);
    private readonly bool _traceLevelEnabled;
    private readonly ConcurrentDictionary<string, (Type, DateTimeOffset)> _testsCurrentExecutionState = new();

    private Task? _signalActivityIndicatorTask;
    private Mutex? _activityIndicatorMutex;
    private string? _mutexName;
    private bool _exitSignalActivityIndicatorAsync;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private PipeNameDescription? _pipeNameDescription;
    private bool _sessionEndCalled;

    public HangDumpActivityIndicator(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        ITask task,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpActivityIndicator>();
        _traceLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _task = task;
        _clock = clock;
        if (_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName) &&
            !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey))
        {
            string namedPipeSuffix = _environment.GetEnvironmentVariable(HangDumpConfiguration.MutexNameSuffix) ?? throw new InvalidOperationException($"Expected {HangDumpConfiguration.MutexNameSuffix} environment variable set.");
            string pipeNameEnvironmentVariable = $"{HangDumpConfiguration.PipeName}_{FNV_1aHashHelper.ComputeStringHash(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}_{namedPipeSuffix}";
            string namedPipeName = _environment.GetEnvironmentVariable(pipeNameEnvironmentVariable) ?? throw new InvalidOperationException($"Expected {pipeNameEnvironmentVariable} environment variable set.");
            _namedPipeClient = new NamedPipeClient(namedPipeName);
            _namedPipeClient.RegisterSerializer(new ActivityIndicatorMutexNameRequestSerializer(), typeof(ActivityIndicatorMutexNameRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _namedPipeClient.RegisterSerializer(new SessionEndSerializerRequestSerializer(), typeof(SessionEndSerializerRequest));
            _namedPipeClient.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
        }
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(HangDumpActivityIndicator);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName) &&
        !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey));

    public async Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_namedPipeClient is not null);

        if (!await IsEnabledAsync() || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            // Connect to the named pipe server
            await _logger.LogTraceAsync($"Connecting to the process lifetime handler {_namedPipeClient.PipeName}");
            await _namedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            await _logger.LogTraceAsync("Connected to the process lifetime handler");
            _activityIndicatorMutex = new Mutex(true);
            _mutexName = $"{HangDumpConfiguration.MutexName}_{Guid.NewGuid():N}";

            // Keep the custom thread to avoid to waste one from thread pool.
            _signalActivityIndicatorTask = _task.RunLongRunning(SignalActivityIndicatorAsync, "[HangDump] SignalActivityIndicatorAsync", cancellationToken);
            await _logger.LogTraceAsync($"Wait for mutex '{_mutexName}' creation");
            _mutexCreated.Wait(cancellationToken);
            await _logger.LogTraceAsync($"Mutex '{_mutexName}' created");
            await _namedPipeClient.RequestReplyAsync<ActivityIndicatorMutexNameRequest, VoidResponse>(new ActivityIndicatorMutexNameRequest(_mutexName), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            await _logger.LogTraceAsync($"Mutex '{_mutexName}' sent to the process lifetime handler");

            // Setup the server channel with the testhost controller
            _pipeNameDescription = NamedPipeServer.GetPipeName($"HangDumpActivityIndicator_{Guid.NewGuid():N}");
            _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
            _singleConnectionNamedPipeServer.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _singleConnectionNamedPipeServer.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new ExitSignalActivityIndicatorTaskRequestSerializer(), typeof(ExitSignalActivityIndicatorTaskRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            await _logger.LogTraceAsync($"Send consumer pipe name to the test controller '{_pipeNameDescription.Name}'");
            await _namedPipeClient.RequestReplyAsync<ConsumerPipeNameRequest, VoidResponse>(new ConsumerPipeNameRequest(_pipeNameDescription.Name), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

            // Wait the connection from the testhost controller
            await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            await _logger.LogTraceAsync($"Test host controller connected");
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Do nothing, we're stopping
        }
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is GetInProgressTestsRequest)
        {
            await _logger.LogDebugAsync($"Received '{nameof(GetInProgressTestsRequest)}'");
            return new GetInProgressTestsResponse(_testsCurrentExecutionState.Select(x => (x.Key, (int)_clock.UtcNow.Subtract(x.Value.Item2).TotalSeconds)).ToArray());
        }
        else if (request is ExitSignalActivityIndicatorTaskRequest)
        {
            await _logger.LogDebugAsync($"Received '{nameof(ExitSignalActivityIndicatorTaskRequest)}'");
            await ExitSignalActivityIndicatorTaskAsync();
            return VoidResponse.CachedInstance;
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request.GetType().FullName));
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || value is not TestNodeUpdateMessage nodeChangedMessage)
        {
            return;
        }

        TestNodeStateProperty? state = nodeChangedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is InProgressTestNodeStateProperty)
        {
            if (_traceLevelEnabled)
            {
                await _logger.LogTraceAsync($"New in-progress test '{nodeChangedMessage.TestNode.DisplayName}'");
            }

            _testsCurrentExecutionState.TryAdd(nodeChangedMessage.TestNode.DisplayName, (typeof(InProgressTestNodeStateProperty), _clock.UtcNow));
        }
        else if (state is PassedTestNodeStateProperty or ErrorTestNodeStateProperty or CancelledTestNodeStateProperty
            or FailedTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty
            && _testsCurrentExecutionState.TryRemove(nodeChangedMessage.TestNode.DisplayName, out (Type, DateTimeOffset) record)
            && _traceLevelEnabled)
        {
            await _logger.LogTraceAsync($"Test removed from in-progress list '{nodeChangedMessage.TestNode.DisplayName}' after '{_clock.UtcNow.Subtract(record.Item2)}', total in-progress '{_testsCurrentExecutionState.Count}'");
        }

        // Optimization, we're interested in test progression and eventually in the discovery progression
        if (state is not InProgressTestNodeStateProperty)
        {
            if (_traceLevelEnabled)
            {
                await _logger.LogTraceAsync($"Signal for action node {nodeChangedMessage.TestNode.DisplayName} - '{state}'");
            }

            // Signal the activity if it's not set
            if (!_signalActivity.IsSet)
            {
                _signalActivity.Set();
            }
        }
    }

    private Task SignalActivityIndicatorAsync()
    {
        _activityIndicatorMutex = new Mutex(true, _mutexName);
        _mutexCreated.Set();

        while (!_exitSignalActivityIndicatorAsync)
        {
            // Wait for the signal
            // We don't add the timeout here because depends on the user value specified with the --hangdump-timeout option
            _signalActivity.Wait();

            if (_traceLevelEnabled)
            {
                _logger.LogTrace($"Signal process lifetime handler, exitSignalActivityIndicatorAsync {_exitSignalActivityIndicatorAsync}");
            }

            _activityIndicatorMutex.ReleaseMutex();
            _activityIndicatorMutex.WaitOne(TimeoutHelper.DefaultHangTimeSpanTimeout);

            if (_traceLevelEnabled)
            {
                _logger.LogTrace($"Signaled by process lifetime handler, exitSignalActivityIndicatorAsync {_exitSignalActivityIndicatorAsync}");
            }

            // Reset the signal
            _signalActivity.Reset();
        }

        _logger.LogDebug($"Exit 'SignalActivityIndicatorAsync'");

        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        ApplicationStateGuard.Ensure(_activityIndicatorMutex is not null);

        if (!await IsEnabledAsync())
        {
            return;
        }

        await _namedPipeClient.RequestReplyAsync<SessionEndSerializerRequest, VoidResponse>(new SessionEndSerializerRequest(), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        await _logger.LogDebugAsync($"Signal for test session end'");
        await ExitSignalActivityIndicatorTaskAsync();

        await _logger.LogTraceAsync($"Signaled by process for it's exit");
        _sessionEndCalled = true;
    }

    private async Task ExitSignalActivityIndicatorTaskAsync()
    {
        if (_exitSignalActivityIndicatorAsync)
        {
            return;
        }

        ApplicationStateGuard.Ensure(_signalActivityIndicatorTask is not null);
        _exitSignalActivityIndicatorAsync = true;
        _signalActivity.Set();
        await _signalActivityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await DisposeHelper.DisposeAsync(_namedPipeClient);

        // If the OnTestSessionFinishingAsync is not called means that something unhandled happened
        // and we didn't correctly coordinate the shutdown with the HangDumpProcessLifetimeHandler.
        // If we go do wait for the server we will hang.
        if (_sessionEndCalled)
        {
            await DisposeHelper.DisposeAsync(_singleConnectionNamedPipeServer);
        }

        _pipeNameDescription?.Dispose();
        _mutexCreated.Dispose();
        _signalActivity.Dispose();
        _activityIndicatorMutex?.Dispose();
    }
#else
    public void Dispose()
    {
        _namedPipeClient?.Dispose();

        // If the OnTestSessionFinishingAsync is not called means that something unhandled happened
        // and we didn't correctly coordinate the shutdown with the HangDumpProcessLifetimeHandler.
        // If we go do wait for the server we will hang.
        if (_sessionEndCalled)
        {
            _singleConnectionNamedPipeServer?.Dispose();
        }

        _pipeNameDescription?.Dispose();
        _mutexCreated.Dispose();
        _signalActivity.Dispose();
        _activityIndicatorMutex?.Dispose();
    }
#endif
}

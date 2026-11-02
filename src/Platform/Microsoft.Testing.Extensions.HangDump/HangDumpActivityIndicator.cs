// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("wasi")]
internal sealed class HangDumpActivityIndicator : IDataConsumer, ITestSessionLifetimeHandler,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger<HangDumpActivityIndicator> _logger;
    private readonly NamedPipeClient? _namedPipeClient;
    private readonly bool _traceLevelEnabled;
    private readonly ConcurrentDictionary<TestNodeUid, (string Name, Type Type, DateTimeOffset StartTime)> _testsCurrentExecutionState = new();
    private static readonly TimeSpan ActivitySignalShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _activitySignalInterval;

    private volatile bool _exitSignalActivityIndicatorAsync;
    private int _activitySignalPending;
    private CancellationTokenSource? _activitySignalCancellation;
    private Task? _activitySignalTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private PipeNameDescription? _pipeNameDescription;

    public HangDumpActivityIndicator(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        ITask task,
        ILoggerFactory loggerFactory,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpActivityIndicator>();
        _traceLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _task = task;
        _clock = clock;
        if (HangDumpOptions.IsEnabled(_commandLineOptions))
        {
            string namedPipeName = _environment.GetEnvironmentVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName)
                ?? throw new InvalidOperationException($"Expected {HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName} environment variable set.");
            _namedPipeClient = new NamedPipeClient(namedPipeName, _environment);
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _namedPipeClient.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
            _namedPipeClient.RegisterSerializer(new ActivitySignalRequestSerializer(), typeof(ActivitySignalRequest));
            _activitySignalInterval = HangDumpOptions.GetActivitySignalInterval(HangDumpOptions.GetTimeout(_commandLineOptions));
        }
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(HangDumpActivityIndicator);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(HangDumpOptions.IsEnabled(_commandLineOptions));

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Connect to the named pipe server
            await _logger.LogTraceAsync($"Connecting to the process lifetime handler {_namedPipeClient.PipeName}").ConfigureAwait(false);
            await _namedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            await _logger.LogTraceAsync("Connected to the process lifetime handler").ConfigureAwait(false);

            // Setup the server channel with the testhost controller
            _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
            await _logger.LogTraceAsync($"Hang dump pipe name: '{_pipeNameDescription.Name}'").ConfigureAwait(false);
            _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
            _singleConnectionNamedPipeServer.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _singleConnectionNamedPipeServer.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            await _logger.LogTraceAsync($"Send consumer pipe name to the test controller '{_pipeNameDescription.Name}'").ConfigureAwait(false);
            await _namedPipeClient.RequestReplyAsync<ConsumerPipeNameRequest, VoidResponse>(new ConsumerPipeNameRequest(_pipeNameDescription.Name), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);

            // Wait the connection from the testhost controller
            await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            await _logger.LogTraceAsync("Test host controller connected").ConfigureAwait(false);

            _activitySignalCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken stopToken = _activitySignalCancellation.Token;
            _activitySignalTask = _task.Run(() => SignalActivityLoopAsync(stopToken, cancellationToken), CancellationToken.None);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Do nothing, we're stopping
        }
    }

    private async Task SignalActivityLoopAsync(CancellationToken stopToken, CancellationToken sessionCancellationToken)
    {
        try
        {
            while (true)
            {
                await _task.Delay(_activitySignalInterval, stopToken).ConfigureAwait(false);

                // The stop token only interrupts the wait. A request in flight completes, so the pipe is never
                // left with a half-read reply when the session ends.
                await SendPendingActivitySignalAsync(sessionCancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            // Stopped by the end of the session or by cancellation.
        }
        catch (Exception ex)
        {
            // Log now: without activity signals the controller can take a hang dump before the session ends
            // and rethrows this failure.
            await _logger.LogErrorAsync("Failed to send a hang dump activity signal to the test host controller; activity signals stopped.", ex).ConfigureAwait(false);
            throw;
        }
    }

    private async Task StopActivitySignalLoopAsync()
    {
        // Idempotent: whoever takes the fields stops the loop, so session end and disposal never race on them.
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _activitySignalCancellation, null);
        Task? loop = Interlocked.Exchange(ref _activitySignalTask, null);
        if (cancellation is null)
        {
            return;
        }

        try
        {
#if NET
            await cancellation.CancelAsync().ConfigureAwait(false);
#else
            cancellation.Cancel();
#endif
            if (loop is not null)
            {
                await loop.ConfigureAwait(false);
            }
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task SendPendingActivitySignalAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _activitySignalPending, 0) == 0 || _exitSignalActivityIndicatorAsync)
        {
            return;
        }

        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        await _namedPipeClient.RequestReplyAsync<ActivitySignalRequest, VoidResponse>(ActivitySignalRequest.Instance, cancellationToken).ConfigureAwait(false);

        if (_traceLevelEnabled)
        {
            await _logger.LogTraceAsync("Activity signal sent.").ConfigureAwait(false);
        }
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is GetInProgressTestsRequest)
        {
            await _logger.LogDebugAsync($"Received '{nameof(GetInProgressTestsRequest)}'").ConfigureAwait(false);
            _exitSignalActivityIndicatorAsync = true;
            return new GetInProgressTestsResponse([.. _testsCurrentExecutionState.Select(x => (x.Value.Name, (int)_clock.UtcNow.Subtract(x.Value.StartTime).TotalSeconds))]);
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request.GetType().FullName));
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not TestNodeUpdateMessage nodeChangedMessage)
        {
            return;
        }

        TestNodeStateProperty? state = nodeChangedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        bool executionCompleted = nodeChangedMessage.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>();
        if (state is InProgressTestNodeStateProperty)
        {
            if (_traceLevelEnabled)
            {
                // Stryker disable once Boolean: continuation scheduling does not change the trace diagnostic.
                await _logger.LogTraceAsync($"New in-progress test '{nodeChangedMessage.TestNode.DisplayName}'").ConfigureAwait(false);
            }

            _testsCurrentExecutionState.TryAdd(nodeChangedMessage.TestNode.Uid, (nodeChangedMessage.TestNode.DisplayName, typeof(InProgressTestNodeStateProperty), _clock.UtcNow));
        }
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
        else if ((executionCompleted
            || state is PassedTestNodeStateProperty or ErrorTestNodeStateProperty or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            or FailedTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty)
            // A test framework that retries in-process reports a terminal update per attempt but only one
            // in-progress update for the test. Dropping it from the in-progress set on a superseded attempt would
            // leave the test untracked while its next attempt is still running, so a hang during that attempt
            // would not be attributed to it in the hang dump's in-progress list.
            && !nodeChangedMessage.TestNode.IsSupersededRetryAttempt()
            && _testsCurrentExecutionState.TryRemove(nodeChangedMessage.TestNode.Uid, out (string Name, Type Type, DateTimeOffset StartTime) record)
            && _traceLevelEnabled)
        {
            // Stryker disable once Boolean: continuation scheduling does not change the trace diagnostic.
            await _logger.LogTraceAsync($"Test removed from in-progress list '{record.Name}' after '{_clock.UtcNow.Subtract(record.StartTime)}', total in-progress '{_testsCurrentExecutionState.Count}'").ConfigureAwait(false);
        }

        // Optimization, we're interested in test progression and eventually in the discovery progression
        if (state is not InProgressTestNodeStateProperty)
        {
            if (_traceLevelEnabled)
            {
                // Stryker disable once Boolean: continuation scheduling does not change the trace diagnostic.
                await _logger.LogTraceAsync($"Signal for action node {nodeChangedMessage.TestNode.DisplayName} - '{state}'. _exitSignalActivityIndicatorAsync: {_exitSignalActivityIndicatorAsync}").ConfigureAwait(false);
            }

            // Record the activity; the signal loop forwards it to the controller.
            Volatile.Write(ref _activitySignalPending, 1);
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (Volatile.Read(ref _activitySignalTask) is null)
        {
            return;
        }

        // Stop the loop before sending the last pending signal, so two requests never share the pipe.
        await StopActivitySignalLoopAsync().ConfigureAwait(false);
        await SendPendingActivitySignalAsync(testSessionContext.CancellationToken).ConfigureAwait(false);
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        // Wait (bounded) for the loop before disposing the pipe client it may be using. This also covers runs
        // where session finishing was skipped because execution or a lifecycle handler threw.
        Task stop = StopActivitySignalLoopAsync();
        try
        {
            Task completed = await Task.WhenAny(stop, Task.Delay(ActivitySignalShutdownTimeout)).ConfigureAwait(false);
            if (completed == stop)
            {
                await stop.ConfigureAwait(false);
            }
            else
            {
                ObserveLateStopFailure(stop);
            }
        }
        catch (Exception ex)
        {
            await _logger.LogDebugAsync($"Hang dump activity signal loop failed during disposal: {ex.Message}").ConfigureAwait(false);
        }

        _namedPipeClient?.Dispose();
    }
#endif

    public void Dispose()
    {
        // Wait (bounded) for the loop before disposing the pipe client it may be using.
        Task stop = StopActivitySignalLoopAsync();
        try
        {
            if (!stop.Wait(ActivitySignalShutdownTimeout))
            {
                ObserveLateStopFailure(stop);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Hang dump activity signal loop failed during disposal: {ex.Message}");
        }

        _namedPipeClient?.Dispose();
    }

    private void ObserveLateStopFailure(Task stop)
    {
        _logger.LogDebug("Hang dump activity signal loop did not stop within the shutdown timeout during disposal.");
        _ = stop.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }
}

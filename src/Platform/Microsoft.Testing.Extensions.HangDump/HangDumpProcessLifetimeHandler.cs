// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions.Diagnostics;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal sealed partial class HangDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer, IDataProducer,
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
    private readonly IServiceProvider _serviceProvider;
    private readonly NamedPipeServerEndpoint _endpoint;
    private readonly bool _traceEnabled;
    private readonly ILogger<HangDumpProcessLifetimeHandler> _logger;
    private readonly ManualResetEventSlim _waitConsumerPipeName = new(false);
    private readonly ConcurrentQueue<string> _dumpFiles = [];

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
    private bool _hostExited;

    /// <summary>
    /// Upper bound for the optional in-progress-test query before taking a dump. A connected but
    /// wedged host never answers the request/reply, and the application token is not cancelled while
    /// the run is still "in progress" (which is exactly when the deadline dump fires), so an unbounded
    /// query would block the dump and kill indefinitely and consume the whole dump margin. The query
    /// is issued once per dump of the tree, not once per process, so this is the total worst case for
    /// the whole tree and stays a small slice of the default 30s dump margin however many processes
    /// are dumped; the healthy path answers in milliseconds.
    /// </summary>
    private static readonly TimeSpan InProgressTestsQueryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BestEffortDiagnosticsTimeout = TimeSpan.FromSeconds(1);

    private int _dumpTaken;
    private Task? _waitConnectionTask;
    private Task? _activityIndicatorTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private string _dumpType = "Full";
    private string? _dumpFileNamePattern;
    private ITestHostProcessInformation? _testHostProcessInformation;
    private NamedPipeClient? _namedPipeClient;

    // Cancels the pipe handshake in OnTestHostProcessStartedAsync when a dump wins the race. Read and
    // written under _dumpLock, and null whenever no handshake is in flight. Cancelled outside the lock,
    // because cancellation runs the waiters' continuations inline and those continue into the handshake,
    // which takes _dumpLock again on its way out.
    private CancellationTokenSource? _handshakeCancellationTokenSource;

    public HangDumpProcessLifetimeHandler(
        NamedPipeServerEndpoint endpoint,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ICommandLineOptions commandLineOptions,
        ITask task,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IProcessHandler processHandler,
        IClock clock,
        IServiceProvider serviceProvider)
    {
        _logger = loggerFactory.CreateLogger<HangDumpProcessLifetimeHandler>();
        _traceEnabled = _logger.IsEnabled(LogLevel.Trace);
        _endpoint = endpoint;
        _messageBus = messageBus;
        _outputDisplay = new OutputDeviceWriter(outputDevice, this);
        _commandLineOptions = commandLineOptions;
        _task = task;
        _environment = environment;
        _configuration = configuration;
        _processHandler = processHandler;
        _clock = clock;
        _serviceProvider = serviceProvider;
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

        _singleConnectionNamedPipeServer = NamedPipeServerFactory.CreateAndBind(
            _endpoint,
            CallbackAsync,
            _environment,
            _logger,
            _task,
            _serviceProvider,
            cancellationToken);
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

            // exitProcessOnConnectionLoss: false, because this is an auxiliary channel. It carries nothing but
            // the best-effort in-progress-test query used to annotate a dump, and the peer is a test host we
            // are often about to dump and kill -- so a disconnect here is expected rather than fatal. With the
            // default (true) a host that drops while the query is in flight would call IEnvironment.Exit on
            // this controller, killing the very process that still has to take and publish the dump, and the
            // catch in QueryInProgressTestsWithTimeoutAsync would never run. Surfacing it as an exception lets
            // the query fall back to an empty list and the dump continue.
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName, _environment, exitProcessOnConnectionLoss: false);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _waitConsumerPipeName.Set();
            return VoidResponse.CachedInstance;
        }
        else if (request is ActivitySignalRequest)
        {
            _activityTimer?.Change(_activityTimerValue!.Value, TimeSpan.FromMilliseconds(-1));

            if (_traceEnabled)
            {
                await _logger.LogTraceAsync($"Activity signal received by the test host '{_clock.UtcNow}'").ConfigureAwait(false);
            }

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

        // Read the pipe server once, here, where the guard above proves it is set. The dereference is in a
        // nested try several awaits down, and a field can in principle change under those awaits, so a local
        // is what makes the null-safety contract hold at the point it is actually used.
        NamedPipeServer singleConnectionNamedPipeServer = _singleConnectionNamedPipeServer;

        _testHostProcessInformation = testHostProcessInformation;

        // The pipe handshake below must be interruptible by a dump. Killing the test host does not
        // complete this process's own WaitConnectionAsync -- that pipe is waiting for a client that will
        // now never connect -- so without this token a host that wedged before connecting keeps Started
        // blocked for DefaultHangTimeSpanTimeout (five minutes), far past the default 30s dump margin and
        // the CI hard deadline, and OnTestHostProcessExitedAsync never runs to publish the dump that was
        // taken. Published before the deadline timer is armed so a timer that fires immediately (a
        // deadline already in the past) still finds it.
        var handshakeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_dumpLock)
        {
            _handshakeCancellationTokenSource = handshakeCancellation;
        }

        CancellationToken handshakeToken = handshakeCancellation.Token;

        try
        {
            // Arm the absolute CI deadline timer as early as possible, before we block on the pipe
            // handshake below. If the test host wedges during startup (never connects back over the
            // pipe), those waits would otherwise block well past the deadline and the deadline dump/kill
            // would never be armed, which defeats the purpose of the deadline. The dump path only needs
            // the test host PID, which we already have here; the in-progress-test list (which needs the
            // consumer pipe) is best-effort and skipped when the pipe never connected.
            if (_deadlineDumpAt is { } deadlineDumpAt)
            {
                _deadlineTimer = new Timer(
                    _ => OnDeadlineTimerElapsed(cancellationToken),
                    null,
                    Timeout.InfiniteTimeSpan,
                    TimeSpan.FromMilliseconds(-1));
                _deadlineTimer.Change(DeadlineHelper.GetTimerDueTime(deadlineDumpAt, _clock.UtcNow), Timeout.InfiniteTimeSpan);
            }

            // Once a dump has started, the test host is being dumped and killed out from under this
            // handshake, so the pipe waits below will throw (cancellation, timeout, or a torn-down pipe).
            // Let Started return normally in that case so the lifetime handler still receives
            // OnTestHostProcessExitedAsync, which is where the dump files are published; otherwise a
            // deadline dump would be taken but never surfaced as an artifact.
            try
            {
                await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{singleConnectionNamedPipeServer.PipeName.Name}'").ConfigureAwait(false);
                await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, handshakeToken).ConfigureAwait(false);
                using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
                using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(handshakeToken, timeout.Token);
                _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
                ApplicationStateGuard.Ensure(_namedPipeClient is not null);
                await _namedPipeClient.ConnectAsync(handshakeToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, handshakeToken).ConfigureAwait(false);
                await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'").ConfigureAwait(false);

                // The inactivity timer only makes sense once the host has connected and can send activity
                // signals; before that there is nothing to reset it. The deadline timer above is independent.
                _activityTimer = new Timer(
                    _ => TriggerDumpOnce(cancellationToken, triggeredByDeadline: false),
                    null,
                    _activityTimerValue!.Value,
                    TimeSpan.FromMilliseconds(-1));
            }
            catch (Exception ex) when (Volatile.Read(ref _dumpTaken) != 0)
            {
                // A dump is already in progress; the failed handshake is expected. Return normally so
                // OnTestHostProcessExitedAsync runs and publishes the dump that is being taken.
                await RunBestEffortDiagnosticAsync(
                    () => _logger.LogDebugAsync($"Test host handshake failed after the dump started; continuing so the dump can be published. {ex}"),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
        }
        finally
        {
            // Unpublish before disposing, so a later dump reads null instead of a disposed source.
            lock (_dumpLock)
            {
                _handshakeCancellationTokenSource = null;
            }

            handshakeCancellation.Dispose();
        }
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

        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Timer.DisposeAsync waits for the timer callback, but TriggerDumpOnce returns as soon as it
            // publishes the actual dump task. Capture and await that task before enumerating its artifacts.
            _hostExited = true;
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            try
            {
                await activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await RunBestEffortDiagnosticAsync(
                    () => _logger.LogErrorAsync("The hang dump operation failed while the test host was exiting.", ex),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                await RunBestEffortDiagnosticAsync(
                    () => _outputDisplay.DisplayAsync(
                        new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, ex, GetDiskInfo())),
                        CancellationToken.None),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
        }

        if (!testHostProcessInformation.HasExitedGracefully)
        {
            await _logger.LogDebugAsync($"Testhost didn't exit gracefully '{testHostProcessInformation.ExitCode}')").ConfigureAwait(false);
        }

        foreach (string dumpFile in _dumpFiles)
        {
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), ExtensionResources.HangDumpArtifactDisplayName, ExtensionResources.HangDumpArtifactDescription)).ConfigureAwait(false);
        }
    }

    private void OnDeadlineTimerElapsed(CancellationToken cancellationToken)
    {
        TimeSpan dueTime = DeadlineHelper.GetTimerDueTime(_deadlineDumpAt!.Value, _clock.UtcNow);
        if (dueTime > TimeSpan.Zero)
        {
            try
            {
                _deadlineTimer!.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Teardown won the race with this timer callback.
            }

            return;
        }

        TriggerDumpOnce(cancellationToken, triggeredByDeadline: true);
    }
}

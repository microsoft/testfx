// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Reacts to a CI-imposed hard-cancel deadline (see <see cref="DeadlineHelper"/>): a short margin
/// before the deadline it asks the test framework to gracefully stop scheduling new tests, so the
/// in-flight session can end normally and all reporters (TRX/HTML/AzDO live run) get to finalize
/// before the CI runner hard-kills the process.
/// </summary>
/// <remarks>
/// This is a prototype. It is timer-driven (the deadline is an absolute instant, so the timer is
/// armed at construction). It implements <see cref="IDataConsumer"/> so the message bus keeps a live
/// reference to it for the duration of the run (which also keeps its timer alive); it consumes no
/// message types, so the bus keeps that reference without routing any message to it. It also implements
/// <see cref="ITestSessionLifetimeHandler"/> as a backstop and is registered as a service so the host can call
/// <see cref="NotifyTestExecutionCompleted"/> the moment the test framework invoker returns. That early signal
/// prevents a timer firing while reporters finalize an already-finished run from marking it deadline-truncated.
/// </remarks>
internal sealed class AbortAtDeadlineExtension : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IGracefulStopTestExecutionCapability? _capability;
    private readonly IStopPoliciesService _policiesService;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger _logger;
    private readonly IClock _clock;
    private readonly List<string> _startupWarnings = [];
    private readonly DateTimeOffset? _stopAt;
    private readonly Timer? _timer;

    // How long a single best-effort diagnostic may take before it is abandoned. Injectable only so a test
    // can exercise the bound without waiting DefaultReportTimeout for it; production always uses the default.
    private readonly TimeSpan _reportTimeout;

    // Serializes publishing _handleDeadlineTask against Dispose reading it, so the timer callback and
    // disposal cannot interleave in a way that starts the handler after Dispose has already returned.
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private int _handled;
    private int _startupWarningsDisplayed;
    private volatile bool _disposed;

    // Which of "test execution finished" and "the deadline took the run" happened first. Both transitions are
    // made under _lock and only out of Running, so they are mutually exclusive: whichever takes the lock first
    // wins and the other becomes a no-op. Read without the lock on the timer callback's fast-path, so it is
    // volatile.
    private volatile RunState _state;
    private Task? _handleDeadlineTask;

    /// <summary>
    /// Bounded wait applied on disposal to let an in-flight deadline handler finish reporting before
    /// the host tears down, without letting a wedged stop hang disposal forever.
    /// </summary>
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Bounded wait applied to each best-effort diagnostic on the deadline path, so a logger or output
    /// device that never completes cannot hold up the graceful stop it precedes.
    /// </summary>
    /// <remarks>
    /// Generous enough that a healthy provider never hits it, and short relative to the margin the
    /// deadline leaves for the stop to take effect.
    /// </remarks>
    private static readonly TimeSpan DefaultReportTimeout = TimeSpan.FromSeconds(10);

    public AbortAtDeadlineExtension(
        IEnvironment environment,
        IClock clock,
        IGracefulStopTestExecutionCapability? capability,
        IStopPoliciesService policiesService,
        ITestApplicationCancellationTokenSource cancellationTokenSource,
        IOutputDevice outputDevice,
        ILoggerFactory loggerFactory,
        TimeSpan? reportTimeout = null,
        bool isHangDumpEnabled = false)
    {
        _capability = capability;
        _policiesService = policiesService;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));
        _clock = clock;
        _reportTimeout = reportTimeout ?? DefaultReportTimeout;

        if (!DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadline))
        {
            // Distinguish "opt-in is off" (variable unset) from "set but malformed". The former is the
            // normal case and stays silent; the latter is a configuration mistake worth a warning.
            string? raw = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE);
            if (!RoslynString.IsNullOrWhiteSpace(raw))
            {
                TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set to '{raw}' but could not be parsed as an absolute ISO 8601 instant. Deadline-aware cancellation is disabled."));
                _startupWarnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.AbortAtDeadlineInvalidDeadlineWarning,
                    EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
                    raw));
            }

            return;
        }

        TimeSpan stopMargin = DeadlineHelper.GetStopMargin(environment);
        TimeSpan dumpMargin = DeadlineHelper.GetDumpMargin(environment);
        DateTimeOffset stopAt = DeadlineHelper.SubtractSaturating(deadline, stopMargin);
        bool areMarginsInverted = isHangDumpEnabled && dumpMargin >= stopMargin;
        if (areMarginsInverted)
        {
            _startupWarnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.AbortAtDeadlineInvalidMarginOrderWarning,
                dumpMargin,
                stopMargin));
        }

        // Log the resolved instants and margins so any misconfiguration is visible.
        TryLog(() =>
        {
            _logger.LogInformation($"Deadline-aware cancellation: deadline={deadline:o}, stopMargin={stopMargin}, dumpMargin={dumpMargin}, graceful stop scheduled at {stopAt:o} (UTC).");

            // stopMargin is meant to be larger than dumpMargin so the graceful stop is attempted before
            // the hang dump. Warn when the ordering is inverted rather than silently misbehaving.
            if (areMarginsInverted)
            {
                _logger.LogWarning($"Deadline dump margin ({dumpMargin}) is greater than or equal to the stop margin ({stopMargin}). The graceful stop is meant to run before the hang dump; with these margins the hang dump may fire first.");
            }
        });

        if (capability is null)
        {
            // A deadline is configured but this framework cannot stop gracefully, so nothing is armed.
            // Surface it rather than silently doing nothing.
            TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set but the test framework does not support graceful stop ('{nameof(IGracefulStopTestExecutionCapability)}'); the platform cannot stop early at the deadline."));
            _startupWarnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.AbortAtDeadlineCapabilityUnavailableWarning,
                EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
                nameof(IGracefulStopTestExecutionCapability)));
            return;
        }

        _stopAt = stopAt;

        // Timer cannot represent delays above ~49.7 days. Arm it in bounded chunks and re-check the
        // absolute instant on every callback so a far-future deadline never fires early.
        _timer = new Timer(static state => ((AbortAtDeadlineExtension)state!).OnTimerElapsed(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(DeadlineHelper.GetTimerDueTime(stopAt, clock.UtcNow), Timeout.InfiniteTimeSpan);
    }

    private static void TryLog(Action logAction)
    {
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Construction-time diagnostics are best-effort: a logger failure must never break test
            // framework construction.
        }
    }

    private void OnTimerElapsed()
    {
        TimeSpan dueTime = DeadlineHelper.GetTimerDueTime(_stopAt!.Value, _clock.UtcNow);
        if (dueTime > TimeSpan.Zero)
        {
            lock (_lock)
            {
                if (!_disposed && _state == RunState.Running)
                {
                    _timer!.Change(dueTime, Timeout.InfiniteTimeSpan);
                }
            }

            return;
        }

        OnDeadlineReached();
    }

    // No message types are consumed: this extension implements IDataConsumer only to keep a live
    // reference on the message bus (see the remark on the class). Returning an empty list avoids the
    // bus routing every test result to a no-op ConsumeAsync, which would be O(test-count) overhead.
    public Type[] DataTypesConsumed { get; } = [];

    /// <inheritdoc />
    public string Uid => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

    /// <inheritdoc />
    public string DisplayName => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.AbortAtDeadlineDescription;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync()
    {
        if (Interlocked.Exchange(ref _startupWarningsDisplayed, 1) == 0)
        {
            foreach (string warning in _startupWarnings)
            {
                await TryReportAsync(
                    () => _outputDevice.DisplayAsync(
                        this,
                        new WarningMessageOutputDeviceData(warning),
                        _cancellationTokenSource.CancellationToken),
                    "Failed to display a deadline configuration warning.").ConfigureAwait(false);
            }
        }

        return _stopAt.HasValue && _capability is not null;
    }

    /// <inheritdoc />
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        NotifyTestExecutionCompleted();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disarms the deadline because test execution has finished.
    /// </summary>
    /// <remarks>
    /// The host calls this as soon as the test framework invoker returns, before end-of-session draining and
    /// reporting begin. That instant matters: from here on the deadline is moot, because every test that was
    /// going to run has run. Doing it through <see cref="ITestSessionLifetimeHandler"/> instead would be far
    /// too late -- session-end notification first drains the message bus, then runs the non-consumer handlers,
    /// then the consumer handlers in registration order, and this extension is a consumer appended after the
    /// reporters. A deadline reached anywhere in that window would still mark a fully-executed run as
    /// truncated (exit code 15).
    /// The transition is made under the same lock that claims the deadline, and only out of
    /// <see cref="RunState.Running"/>, so it is atomic against claiming the stop. Completion after the deadline
    /// claim is recorded separately so a rejected stop can restore the completed state without invoking
    /// framework code while holding the lock.
    /// The timer itself is left to be disposed at host teardown; the state is what makes any late fire a no-op.
    /// </remarks>
    public void NotifyTestExecutionCompleted()
    {
        lock (_lock)
        {
            if (_state == RunState.Running)
            {
                _state = RunState.Completed;
            }
            else if (_state == RunState.DeadlineClaimed)
            {
                _state = RunState.DeadlineClaimedAndCompleted;
            }
        }
    }

    /// <summary>
    /// Waits until a deadline stop request that raced test-execution completion has resolved its verdict.
    /// </summary>
    internal Task WaitForDeadlineHandlingAsync()
    {
        lock (_lock)
        {
            return _handleDeadlineTask ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// Atomically claims the deadline while test execution is still running.
    /// </summary>
    /// <remarks>
    /// Framework code is invoked only after releasing the lock. Completion that occurs after the claim is
    /// tracked by <see cref="RunState.DeadlineClaimedAndCompleted"/> and reconciled if the stop is rejected.
    /// </remarks>
    private bool TryClaimDeadline()
    {
        lock (_lock)
        {
            if (_disposed || _state != RunState.Running || _policiesService.IsTestExecutionCompleted)
            {
                return false;
            }

            _state = RunState.DeadlineClaimed;
            return true;
        }
    }

    private static async Task<bool> RequestGracefulStopAsync(
        IGracefulStopTestExecutionCapability capability,
        CancellationToken cancellationToken)
    {
        await capability.StopTestExecutionAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void OnDeadlineReached()
    {
        // Do not start deadline handling once we are tearing down, or once test execution has already
        // completed (cheap fast-path; both are re-checked under the lock below to actually close the race
        // with Dispose and NotifyTestExecutionCompleted).
        if (_disposed || _state != RunState.Running || _policiesService.IsTestExecutionCompleted)
        {
            return;
        }

        // Ensure we react only once.
        if (Interlocked.Exchange(ref _handled, 1) != 0)
        {
            return;
        }

        // Publish the handler task under the same lock Dispose uses so the two cannot interleave:
        // either we set _handleDeadlineTask before Dispose captures it (Dispose then drains it), or
        // Dispose sets _disposed first and we observe it here and never start the handler against
        // torn-down services. Without this, the timer callback could pass the _disposed check above,
        // Dispose could run to completion (seeing _handleDeadlineTask still null, so draining
        // nothing), and only then would the handler start -- after disposal already returned.
        lock (_lock)
        {
            // Bail if disposal started, or if test execution finished between the fast-path check above and
            // acquiring the lock (the timer fired while the reporters are finalizing a fully-completed run). In
            // either case there is nothing left to stop, and marking the run deadline-truncated would wrongly
            // force exit code 15 on a run that actually completed. This is only an early-out: the handler
            // claims the deadline under this same lock before invoking the graceful-stop capability.
            if (_disposed || _state != RunState.Running || _policiesService.IsTestExecutionCompleted)
            {
                return;
            }

            // Start the handler and capture its task while holding the lock so it cannot interleave with
            // Dispose. HandleDeadlineAsync yields immediately (await Task.Yield()), so it returns an incomplete
            // task here and the lock is released before the handler body starts.
            _handleDeadlineTask = HandleDeadlineAsync();
        }
    }

    private async Task HandleDeadlineAsync()
    {
        // This method is started synchronously inside _lock (see OnDeadlineReached), which also publishes
        // the returned task into _handleDeadlineTask. An async method does NOT necessarily yield at its
        // first await: the logger and output device can return already-completed tasks, which would otherwise
        // run the handler synchronously before _handleDeadlineTask is published. Yield first so control returns
        // to the caller, _handleDeadlineTask is observed, and _lock is released before the handler starts.
        // The deadline is later claimed under _lock, but framework code is invoked after releasing the lock.
        // Task.Yield is cooperative and safe even on a single-threaded runtime (browser/WASI).
        await Task.Yield();

        if (_capability is not { } capability)
        {
            return;
        }

        if (!TryClaimDeadline())
        {
            await TryReportAsync(
                () => _logger.LogDebugAsync("Test execution completed while the approaching deadline was being reported; abandoning the graceful stop."),
                "Failed to report the abandoned deadline stop.").ConfigureAwait(false);
            return;
        }

        Task<bool> stopTask;
        try
        {
            stopTask = capability is IGracefulStopTestExecutionResultCapability resultCapability
                ? resultCapability.TryStopTestExecutionAsync(_cancellationTokenSource.CancellationToken)
                : RequestGracefulStopAsync(capability, _cancellationTokenSource.CancellationToken);
        }
        catch (Exception ex)
        {
            ReleaseDeadlineClaim();
            await TryReportAsync(
                () => _logger.LogErrorAsync("Failed to request graceful stop at deadline.", ex),
                "Failed to report the graceful-stop failure.").ConfigureAwait(false);
            return;
        }

        // Diagnostics are best-effort and run only after the framework has received the stop request. A wedged
        // logger therefore cannot consume the remaining deadline margin before graceful shutdown starts.
        await TryReportAsync(
            () => _logger.LogInformationAsync($"Deadline approaching (stop scheduled at {_stopAt:o}). Requesting graceful stop of test execution."),
            "Failed to report the approaching deadline.").ConfigureAwait(false);

        bool stopAccepted = false;
        try
        {
            await stopTask.TimeoutAfterAsync(DisposeDrainTimeout).ConfigureAwait(false);
            stopAccepted = await stopTask.ConfigureAwait(false);
            if (!stopAccepted)
            {
                if (!await _policiesService.TryExecuteDeadlineStopFallbackAsync().ConfigureAwait(false))
                {
                    return;
                }

                stopAccepted = true;
            }

            // Commit only after the framework accepted the stop. The host awaits this handler after the
            // invoker returns and before reporters run, so committing here cannot be missed by exit-code
            // consumers, while an asynchronously rejected stop is resolved before they inspect the verdict.
            await _policiesService.ExecuteDeadlineCallbacksAsync().ConfigureAwait(false);

            // Only now tell the user. It is written after the stop is accepted rather than before it for the
            // window above, and it is reached only when the deadline actually won the race, so the message is
            // never printed for a run that finished on its own or for a stop the framework rejected. The
            // framework has been asked to stop but in-flight tests are still finishing, so this still lands
            // before the end-of-run summary.
            await TryReportAsync(
                () => _outputDevice.DisplayAsync(
                    this,
                    new SessionMessageOutputDeviceData(PlatformResources.AbortAtDeadlineMessage),
                    _cancellationTokenSource.CancellationToken),
                "Failed to report the approaching deadline.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // An asynchronous stop failure leaves the verdict unset and releases the claim below. If a
            // deadline callback failed instead, ExecuteDeadlineCallbacksAsync already set the verdict
            // synchronously, so the accepted stop remains correctly classified.
            string message = stopAccepted
                ? "The deadline stop was accepted, but a deadline callback failed."
                : "Failed to request graceful stop at deadline.";
            await TryReportAsync(
                () => _logger.LogErrorAsync(message, ex),
                "Failed to report the graceful-stop failure.").ConfigureAwait(false);
        }
        finally
        {
            if (!stopAccepted)
            {
                ReleaseDeadlineClaim();
            }
        }
    }

    private void ReleaseDeadlineClaim()
    {
        lock (_lock)
        {
            if (_state == RunState.DeadlineClaimed)
            {
                _state = RunState.Running;
            }
            else if (_state == RunState.DeadlineClaimedAndCompleted)
            {
                _state = RunState.Completed;
            }
        }
    }

    /// <summary>
    /// Runs a best-effort diagnostic, swallowing anything it throws and giving up on it if it does not
    /// complete promptly, so it can never skip or delay the graceful stop.
    /// </summary>
    private async Task TryReportAsync(Func<Task> report, string failureMessage)
    {
        try
        {
            // Swallowing faults is not enough on its own. A wedged logger or output device does not throw,
            // it hands back a task that never completes. Before the claim that would stop the handler ever
            // reaching the graceful stop, so the deadline would pass with nothing done; after the stop it
            // would keep the handler task alive until disposal gave up on draining it. Bound the wait:
            // TimeoutAfterAsync abandons the task and keeps observing it, so a fault arriving later cannot
            // resurface as an unobserved task exception.
            await report().TimeoutAfterAsync(_reportTimeout).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Even this failure log is best-effort, and bounded for the same reason: the logger may be
            // exactly what threw or wedged, so reporting the failure must not re-throw or block the stop.
            try
            {
                await _logger.LogErrorAsync(failureMessage, ex).TimeoutAfterAsync(_reportTimeout).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore: the graceful stop is the only thing that must happen.
            }
        }
    }

    public void Dispose()
    {
        // Capture the in-flight handler task under the lock so we either observe the task the timer
        // callback published (and drain it below) or set _disposed first (so the callback never
        // starts the handler). See OnDeadlineReached.
        Task? handleDeadlineTask;
        lock (_lock)
        {
            _disposed = true;
            handleDeadlineTask = _handleDeadlineTask;
        }

        _timer?.Dispose();
#if !NETCOREAPP
        // netstandard2.0 has no ValueTask/IAsyncDisposable, so there is no async drain path (see
        // DisposeAsync below, which is netcoreapp-only). Fall back to a bounded blocking drain so an
        // in-flight deadline handler can finish reporting before teardown, without letting a wedged
        // graceful stop hang disposal. HandleDeadlineAsync swallows its own failures, so this wait
        // never observes a fault.
        try
        {
            handleDeadlineTask?.Wait(DisposeDrainTimeout);
        }
        catch (Exception)
        {
            // Best-effort drain: disposal must never throw.
        }
#endif
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        // Capture the in-flight handler task under the lock so we either observe the task the timer
        // callback published (and drain it below) or set _disposed first (so the callback never
        // starts the handler). See OnDeadlineReached.
        Task? handleTask;
        lock (_lock)
        {
            _disposed = true;
            handleTask = _handleDeadlineTask;
        }

        _timer?.Dispose();

        // Drain an in-flight deadline handler so its reporting can finish before the host tears down,
        // but bound the wait so a wedged graceful stop cannot hang disposal. HandleDeadlineAsync
        // swallows its own failures, so awaiting the completed task here never throws.
        if (handleTask is not null)
        {
            Task completed = await Task.WhenAny(handleTask, Task.Delay(DisposeDrainTimeout)).ConfigureAwait(false);
            if (completed == handleTask)
            {
                await handleTask.ConfigureAwait(false);
            }
        }
    }
#endif

    /// <summary>
    /// Which of test execution finishing and the deadline firing took the run. The winner is claimed only out
    /// of <see cref="Running"/> and under the extension's lock; completion during a deadline claim is recorded
    /// separately so a rejected stop can restore the completed state.
    /// </summary>
    private enum RunState
    {
        /// <summary>
        /// Test execution is in progress, so the deadline still applies.
        /// </summary>
        Running,

        /// <summary>
        /// The test framework invoker returned: every test that was going to run has run, so a deadline
        /// reached from here on must not mark the run as truncated.
        /// </summary>
        Completed,

        /// <summary>
        /// The deadline fired while tests were still running and owns the verdict. Test execution completing
        /// afterwards is the stop taking effect, so it must not take the verdict back.
        /// </summary>
        DeadlineClaimed,

        /// <summary>
        /// Test execution completed after the deadline claim but before the framework accepted the stop. If the
        /// framework rejects the stop, the run returns to <see cref="Completed"/> rather than <see cref="Running"/>.
        /// </summary>
        DeadlineClaimedAndCompleted,
    }
}

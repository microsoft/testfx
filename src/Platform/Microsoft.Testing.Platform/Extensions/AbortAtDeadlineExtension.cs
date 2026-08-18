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

    /// <summary>
    /// <see cref="Timer"/> throws for due times above ~49.7 days (its internal limit is
    /// <see cref="uint.MaxValue"/> milliseconds). A deadline that far out is effectively "never"
    /// for a test run, so we clamp to this maximum instead of throwing at construction.
    /// </summary>
    private static readonly TimeSpan MaxTimerDueTime = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public AbortAtDeadlineExtension(
        IEnvironment environment,
        IClock clock,
        IGracefulStopTestExecutionCapability? capability,
        IStopPoliciesService policiesService,
        ITestApplicationCancellationTokenSource cancellationTokenSource,
        IOutputDevice outputDevice,
        ILoggerFactory loggerFactory,
        TimeSpan? reportTimeout = null)
    {
        _capability = capability;
        _policiesService = policiesService;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));
        _reportTimeout = reportTimeout ?? DefaultReportTimeout;

        if (!DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadline))
        {
            // Distinguish "opt-in is off" (variable unset) from "set but malformed". The former is the
            // normal case and stays silent; the latter is a configuration mistake worth a warning.
            string? raw = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE);
            if (!RoslynString.IsNullOrWhiteSpace(raw))
            {
                TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set to '{raw}' but could not be parsed as an absolute ISO 8601 instant. Deadline-aware cancellation is disabled."));
            }

            return;
        }

        TimeSpan stopMargin = DeadlineHelper.GetStopMargin(environment);
        TimeSpan dumpMargin = DeadlineHelper.GetDumpMargin(environment);
        DateTimeOffset stopAt = DeadlineHelper.SubtractSaturating(deadline, stopMargin);

        // A deadline given without an offset is parsed as UTC (AssumeUniversal), which is easy to get
        // wrong. Log the resolved instants and margins so a misconfigured offset is visible.
        TryLog(() =>
        {
            _logger.LogInformation($"Deadline-aware cancellation: deadline={deadline:o}, stopMargin={stopMargin}, dumpMargin={dumpMargin}, graceful stop scheduled at {stopAt:o} (UTC).");

            // stopMargin is meant to be larger than dumpMargin so the graceful stop is attempted before
            // the hang dump. Warn when the ordering is inverted rather than silently misbehaving.
            if (dumpMargin >= stopMargin)
            {
                _logger.LogWarning($"Deadline dump margin ({dumpMargin}) is greater than or equal to the stop margin ({stopMargin}). The graceful stop is meant to run before the hang dump; with these margins the hang dump may fire first.");
            }
        });

        if (capability is null)
        {
            // A deadline is configured but this framework cannot stop gracefully, so nothing is armed.
            // Surface it rather than silently doing nothing.
            TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set but the test framework does not support graceful stop ('{nameof(IGracefulStopTestExecutionCapability)}'); the platform cannot stop early at the deadline."));
            return;
        }

        _stopAt = stopAt;

        // The deadline is absolute wall-clock time, so we can arm a one-shot timer now. If the
        // computed instant is already in the past, fire as soon as possible. If it is farther
        // out than a Timer can represent, clamp it: the run (and this timer) will be disposed
        // long before the clamped due time elapses, so the timer never fires early in practice.
        TimeSpan dueTime = stopAt - clock.UtcNow;
        if (dueTime < TimeSpan.Zero)
        {
            dueTime = TimeSpan.Zero;
        }
        else if (dueTime > MaxTimerDueTime)
        {
            dueTime = MaxTimerDueTime;
        }

        _timer = new Timer(static state => ((AbortAtDeadlineExtension)state!).OnDeadlineReached(), this, dueTime, Timeout.InfiniteTimeSpan);
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
    public Task<bool> IsEnabledAsync() => Task.FromResult(_stopAt.HasValue && _capability is not null);

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
    /// The transition is made under the same lock <see cref="TryClaimDeadline"/> uses, and only out of
    /// <see cref="RunState.Running"/>, so it is atomic against the deadline handler committing its verdict:
    /// either this call wins and the handler then abandons the stop, or the handler claimed the run first and
    /// this call leaves that verdict alone (a real truncation must not be un-marked by the completion that the
    /// requested stop itself produced).
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
        }
    }

    /// <summary>
    /// Takes the run for the deadline, if test execution has not finished (or disposal started) first.
    /// </summary>
    /// <remarks>
    /// This is the commit point of the deadline verdict, and it is deliberately the last thing before the
    /// verdict is recorded: the handler yields and reports before getting here, and the test framework invoker
    /// can return during that window. Checking completion only before starting the handler would therefore
    /// still let a run that executed every test be reported as deadline-truncated.
    /// </remarks>
    /// <returns><see langword="true"/> when the deadline still applies and this call owns the verdict.</returns>
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

    /// <summary>
    /// Gives the run back after a claimed deadline turned out not to stop it, so a completion arriving later
    /// is recorded normally.
    /// </summary>
    private void ReleaseDeadlineClaim()
    {
        lock (_lock)
        {
            if (_state == RunState.DeadlineClaimed)
            {
                _state = RunState.Running;
            }
        }
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
            // re-checks with TryClaimDeadline right before committing the verdict, which is what actually
            // closes the race.
            if (_disposed || _state != RunState.Running || _policiesService.IsTestExecutionCompleted)
            {
                return;
            }

            // Start the handler and capture its task while holding the lock so it cannot interleave with
            // Dispose. HandleDeadlineAsync yields immediately (await Task.Yield()), so none of its work runs
            // while the lock is held: it returns an incomplete task here, we store it, and the lock is
            // released before the handler body executes. That keeps the lock's scope to just publishing the
            // task and avoids running the graceful-stop callback synchronously under the lock -- which could
            // deadlock if the stop waits on session finishing/disposal (both of which take this lock).
            _handleDeadlineTask = HandleDeadlineAsync();
        }
    }

    private async Task HandleDeadlineAsync()
    {
        // This method is started synchronously inside _lock (see OnDeadlineReached), which also publishes
        // the returned task into _handleDeadlineTask. An async method does NOT necessarily yield at its
        // first await: the logger, output device, the empty deadline-callback queue and the framework's
        // graceful-stop capability can all return already-completed tasks, which would run this whole
        // handler synchronously while _lock is held. If the graceful stop then synchronously waited on
        // session finishing or disposal (both take _lock), that would deadlock. Yield first so control
        // returns to the caller, _handleDeadlineTask is observed, and _lock is released before any handler
        // work runs. Task.Yield posts the continuation back to the current context, so it is cooperative
        // and safe even on a single-threaded runtime (browser/WASI) -- unlike Task.Run, which needs another
        // thread to pick the work up.
        await Task.Yield();

        if (_capability is not { } capability)
        {
            return;
        }

        // Diagnostics are best-effort and must never prevent the graceful stop below: MTP's logger
        // and output device both propagate provider exceptions, so a failure here would otherwise
        // skip the deadline handling entirely.
        await TryReportAsync(
            () => _logger.LogInformationAsync($"Deadline approaching (stop scheduled at {_stopAt:o}). Requesting graceful stop of test execution."),
            "Failed to report the approaching deadline.").ConfigureAwait(false);

        // Take the run for the deadline, atomically against NotifyTestExecutionCompleted. Everything above --
        // the yield that got us off the timer callback, and the logging -- runs while the test framework
        // invoker may still return, so this is the first point at which committing the verdict is safe.
        if (!TryClaimDeadline())
        {
            // Test execution finished (or disposal started) while we were getting here. There is nothing left
            // to stop and the run was not truncated, so neither the verdict nor the stop request may happen:
            // both would report a deadline stop for a run that executed every test.
            await TryReportAsync(
                () => _logger.LogDebugAsync("Test execution completed while the approaching deadline was being reported; abandoning the graceful stop."),
                "Failed to report the abandoned deadline stop.").ConfigureAwait(false);
            return;
        }

        bool stopAccepted = false;
        try
        {
            // Commit the verdict with nothing awaited between it and the claim above. The claim closes the
            // door on NotifyTestExecutionCompleted -- once the run is claimed, a completion can no longer
            // disarm the deadline -- so any await in between would be a window where the run finishes on its
            // own and we still go on to report it as truncated. Setting the flag is synchronous inside
            // ExecuteDeadlineCallbacksAsync, so claim and commit are effectively one step.
            //
            // It also has to happen BEFORE the stop is requested. StopTestExecutionAsync can unblock the
            // framework and let it finalize the session (and compute the process exit code) right away, so
            // setting the flag afterwards would race that finalization and the deadline exit code could be
            // missed. The graceful stop does not cancel the token, so unlike abort this flag is not set by a
            // token-registered callback; set it here first.
            await _policiesService.ExecuteDeadlineCallbacksAsync().ConfigureAwait(false);

            // Only now tell the user. This sits after the commit rather than before it so that the claim is
            // not held across this await, and it is still reached only when the deadline actually won the
            // race, so the message is never printed for a run that finished on its own.
            await TryReportAsync(
                () => _outputDevice.DisplayAsync(
                    this,
                    new FormattedTextOutputDeviceData(PlatformResources.AbortAtDeadlineMessage),
                    _cancellationTokenSource.CancellationToken),
                "Failed to report the approaching deadline.").ConfigureAwait(false);

            await capability.StopTestExecutionAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
            stopAccepted = true;
        }
        catch (Exception ex)
        {
            // The verdict above was committed on the assumption that the stop would be accepted. It was not,
            // so the framework was never asked to stop and the run carries on to completion -- leaving the
            // verdict set would report TestExecutionStoppedAtDeadline for a run that executed every test.
            // Take it back. This cannot resurrect the finalization race the early commit avoids: that race
            // only exists once the stop is accepted, and here it never was.
            // Best-effort: never let the timer callback crash the process during teardown. The error
            // log is itself best-effort (the logger may be what threw), so guard it too.
            try
            {
                await _logger.LogErrorAsync("Failed to request graceful stop at deadline.", ex).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore: nothing else can be done here.
            }
        }
        finally
        {
            if (!stopAccepted)
            {
                // The stop request was rejected (StopTestExecutionAsync is a [TPEXP] extensibility point, so a
                // framework can throw anything), which means the platform truncated nothing. Take the outcome
                // back, otherwise a run that carries on and executes every test still exits with
                // ExitCode.TestExecutionStoppedAtDeadline and fails a job that actually completed. If the run
                // is instead hard-killed at the real deadline, the process never reports an exit code at all,
                // so reverting cannot mask a genuine truncation.
                _policiesService.RevertDeadlineTriggered();
                ReleaseDeadlineClaim();
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
            // it hands back a task that never completes, and every call to this sits on the path to
            // StopTestExecutionAsync -- the one after the claim runs when the verdict is already committed.
            // Awaiting such a task there would leave the run recorded as stopped at the deadline while the
            // stop was never actually requested, which is the exact split this extension exists to prevent.
            // Bound the wait: TimeoutAfterAsync abandons the task and keeps observing it, so a fault
            // arriving later cannot resurface as an unobserved task exception.
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
    /// Which of test execution finishing and the deadline firing took the run. Transitions happen only out of
    /// <see cref="Running"/> and only under the extension's lock, so the two are mutually exclusive.
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
    }
}

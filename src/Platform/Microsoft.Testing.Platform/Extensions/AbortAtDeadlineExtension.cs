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
/// <see cref="ITestSessionLifetimeHandler"/> as a backstop for the completion gate, but the gate that actually
/// matters is <see cref="IStopPoliciesService.IsTestExecutionCompleted"/>, which the host sets as soon as the test
/// framework invoker returns: a timer that fired while the reporters finalize an already-finished run would
/// otherwise wrongly mark the run as deadline-truncated.
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

    // Serializes publishing _handleDeadlineTask against Dispose reading it, so the timer callback and
    // disposal cannot interleave in a way that starts the handler after Dispose has already returned.
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private int _handled;
    private volatile bool _disposed;

    // Set once test execution has completed (OnTestSessionFinishingAsync). After this the deadline must not
    // fire: the run already finished, so marking it deadline-truncated would be wrong. Written under _lock and
    // read on the timer callback's fast-path, so it is volatile.
    private volatile bool _executionCompleted;
    private Task? _handleDeadlineTask;

    /// <summary>
    /// Bounded wait applied on disposal to let an in-flight deadline handler finish reporting before
    /// the host tears down, without letting a wedged stop hang disposal forever.
    /// </summary>
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(30);

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
        ILoggerFactory loggerFactory)
    {
        _capability = capability;
        _policiesService = policiesService;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));

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
        // Backstop for the completion gate. The host already signalled completion through
        // IStopPoliciesService.NotifyTestExecutionCompleted the moment the test framework invoker returned, which is
        // what actually protects the reporting window: this callback runs at the END of the session-end
        // notification, because the extension is an IDataConsumer and consumer lifetime handlers are invoked last
        // (after the initial drain, every non-consumer handler and another drain). Setting the flag here as well
        // costs nothing and keeps the extension correct on any host path that does not signal completion. Set it
        // under the same lock OnDeadlineReached/Dispose use, so a timer firing right now is gated rather than
        // half-applied. This never un-marks a real truncation: if the timer already fired during execution,
        // IsDeadlineTriggered is already set and setting this flag now is harmless. The timer itself is left to be
        // disposed at host teardown; the flag is what makes any late fire a no-op.
        lock (_lock)
        {
            _executionCompleted = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a value indicating whether test execution has finished, so a deadline firing from now on must be ignored.
    /// </summary>
    /// <remarks>
    /// The authoritative signal is <see cref="IStopPoliciesService.IsTestExecutionCompleted"/>, which the host sets
    /// the moment the test framework invoker returns -- before the session-end notification drains the message bus
    /// and runs the reporters. <see cref="OnTestSessionFinishingAsync"/> also sets the local flag, but that runs at
    /// the very end of that phase (this extension is an <see cref="IDataConsumer"/>, and consumer lifetime handlers
    /// run last), so on its own it would leave the whole reporting window exposed. It is kept as a backstop for host
    /// paths that never signal completion. Both flags are monotonic (false -> true only), so reading them outside the
    /// lock cannot observe a value that is later retracted.
    /// </remarks>
    private bool IsExecutionCompleted => _executionCompleted || _policiesService.IsTestExecutionCompleted;

    private void OnDeadlineReached()
    {
        // Do not start deadline handling once we are tearing down, or once test execution has already
        // completed (cheap fast-path; both are re-checked under the lock below to actually close the race
        // with Dispose and the completion signal).
        if (_disposed || IsExecutionCompleted)
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
            // force exit code 15 on a run that actually completed.
            if (_disposed || IsExecutionCompleted)
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
        try
        {
            await _logger.LogInformationAsync($"Deadline approaching (stop scheduled at {_stopAt:o}). Requesting graceful stop of test execution.").ConfigureAwait(false);
            await _outputDevice.DisplayAsync(
                this,
                new FormattedTextOutputDeviceData(PlatformResources.AbortAtDeadlineMessage),
                _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Even this failure log is best-effort. If the logger itself is what threw, logging
            // again could re-throw and skip the stop below, so swallow anything here.
            try
            {
                await _logger.LogErrorAsync("Failed to report the approaching deadline.", ex).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore: the graceful stop below is the only thing that must happen.
            }
        }

        bool stopAccepted = false;
        try
        {
            // Mark the run as deadline-triggered BEFORE requesting the stop. StopTestExecutionAsync can
            // unblock the framework and let it finalize the session (and compute the process exit code)
            // right away, so setting the flag afterwards would race that finalization and the deadline
            // exit code could be missed. The graceful stop does not cancel the token, so unlike abort
            // this flag is not set by a token-registered callback; set it here first. Setting it is
            // synchronous inside ExecuteDeadlineCallbacksAsync, so it is visible before the stop begins.
            await _policiesService.ExecuteDeadlineCallbacksAsync().ConfigureAwait(false);

            await capability.StopTestExecutionAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
            stopAccepted = true;
        }
        catch (Exception ex)
        {
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
}

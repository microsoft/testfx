// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Extensions;

internal sealed partial class AbortAtDeadlineExtension
{
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

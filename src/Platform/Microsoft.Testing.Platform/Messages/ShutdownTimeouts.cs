// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Bounds applied during canceled-run shutdown.
/// </summary>
internal static class ShutdownTimeouts
{
    /// <summary>
    /// Smallest override we accept: the downstream waits work in whole milliseconds, so anything below this
    /// truncates to a zero timeout.
    /// </summary>
    private static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Default value of <see cref="GetCanceledConsumerCompletion(IEnvironment)"/>.
    /// </summary>
    public static readonly TimeSpan DefaultCanceledConsumerCompletion = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default value of <see cref="GetControllerFinalization(IEnvironment)"/>.
    /// </summary>
    public static readonly TimeSpan DefaultControllerFinalization = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the budget for completing the consumer handshake once the run has already been canceled. It bounds
    /// the shutdown as a whole rather than each consumer: <see cref="AsynchronousMessageBus"/> completes its
    /// processors sequentially, so a per-consumer budget would let N uncooperative consumers multiply it. Can
    /// be overridden through
    /// <see cref="EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS"/>.
    /// </summary>
    /// <remarks>
    /// Only the canceled path is bounded. On a normal run the wait stays unbounded, because a consumer that
    /// legitimately takes a long time to flush its final results (a large TRX or coverage report) must not be
    /// cut off. On an aborted run the trade-off flips: a cooperative consumer unwinds as soon as it observes
    /// the cancellation token, so anything still running after this budget is ignoring the token, and letting
    /// it block the abort indefinitely would hang the cancellations that have no second escape hatch
    /// ('--timeout', '--maximum-failed-tests'), unlike an interactive Ctrl+C which can be pressed again.
    /// </remarks>
    public static TimeSpan GetCanceledConsumerCompletion(IEnvironment environment)
    {
        string? value = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS);
        return GetConfiguredTimeout(value, DefaultCanceledConsumerCompletion);
    }

    /// <summary>
    /// Gets the total budget for test-host controller callbacks, output reporting, and service disposal after
    /// the child exits from an aborted run.
    /// </summary>
    public static TimeSpan GetControllerFinalization(IEnvironment environment)
    {
        string? value = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS);
        return GetConfiguredTimeout(value, DefaultControllerFinalization);
    }

    private static TimeSpan GetConfiguredTimeout(string? value, TimeSpan defaultTimeout)
    {
        // The upper bound is the strictest downstream API: SemaphoreSlim.WaitAsync(TimeSpan) caps at
        // Int32.MaxValue milliseconds, which is tighter than the Task.WaitAsync / CancelAfter limit. Without it
        // a syntactically valid but absurd value ('1e300', or anything past ~24.8 days) would parse and then
        // throw out of TimeSpan or the wait itself, so an optional override could break the message bus or the
        // blocking-consumer shutdown instead of simply being ignored.
        //
        // Written as a conjunction on purpose: 'NaN' parses successfully and every comparison against it is
        // false, so requiring the checks to pass rejects it, whereas negated guards would let it through.
        bool isUsable = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            && seconds > 0
            && seconds * 1000 <= int.MaxValue;

        if (!isUsable)
        {
            return defaultTimeout;
        }

        // A positive value can still degenerate into "no wait at all": anything below TimeSpan's resolution
        // (1e-300, say) rounds to zero, and the downstream waits work in whole milliseconds, so a
        // sub-millisecond value such as 0.0001 truncates to a zero timeout in SemaphoreSlim.WaitAsync. Either
        // would make an aborted run abandon every consumer instantly rather than honoring a budget, so require
        // at least a millisecond.
        var timeout = TimeSpan.FromSeconds(seconds);
        return timeout >= OneMillisecond ? timeout : defaultTimeout;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Bounds applied to the message bus shutdown handshake.
/// </summary>
internal static class ShutdownTimeouts
{
    /// <summary>
    /// Default value of <see cref="GetCanceledConsumerCompletion(IEnvironment)"/>.
    /// </summary>
    public static readonly TimeSpan DefaultCanceledConsumerCompletion = TimeSpan.FromSeconds(30);

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

        // The upper bound is what Task.WaitAsync and CancellationTokenSource.CancelAfter accept. Without it a
        // syntactically valid but absurd value such as '1e300' would parse and then throw out of TimeSpan or the
        // wait itself, so an optional override could break the message bus instead of being ignored.
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            && seconds > 0
            && seconds * 1000 <= Helpers.TaskExtensions.MaxSupportedTimeoutMs
            ? TimeSpan.FromSeconds(seconds)
            : DefaultCanceledConsumerCompletion;
    }
}

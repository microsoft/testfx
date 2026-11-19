// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Extensions;

internal sealed partial class AbortAtDeadlineExtension
{
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
}

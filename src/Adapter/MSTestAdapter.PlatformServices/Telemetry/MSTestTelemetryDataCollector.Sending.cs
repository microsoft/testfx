// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestTelemetryDataCollector
{
    /// <summary>
    /// Sends the accumulated discovery telemetry via the provided sender delegate and clears the
    /// discovery state by resetting the current collector. Safe to call when no sender is available
    /// (the collector is still cleared so state does not leak across sessions).
    /// </summary>
    /// <param name="telemetrySender">Optional delegate to send telemetry. If null, telemetry is silently discarded.</param>
    internal static async Task SendDiscoveryTelemetryAndResetAsync(Func<string, IDictionary<string, object>, Task>? telemetrySender)
    {
        try
        {
            MSTestTelemetryDataCollector? collector = Current;
            if (collector is null || telemetrySender is null)
            {
                return;
            }

            // Defense in depth: re-check opt-out at send time in case the env var was set after
            // EnsureInitialized but before this point.
            if (IsTelemetryOptedOut())
            {
                return;
            }

            Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();
            if (metrics.Count > 0)
            {
                await telemetrySender("dotnet/testingplatform/mstest/discovery", metrics).ConfigureAwait(false);

                // Mark that the discovery payload (settings + attribute_usage + custom_test_*_types
                // + config_source) has shipped in this process so a subsequent execution event in
                // the same session does not duplicate it.
                Interlocked.Exchange(ref s_discoveryEventEmitted, 1);
            }
        }
        catch (Exception)
        {
            // Telemetry should never cause test failures
        }
        finally
        {
            // Clear the current collector so a subsequent execution accumulates settings/config
            // anew (settings are static-per-process so re-population is cheap and keeps each
            // event self-contained).
            Current = null;
        }
    }

    /// <summary>
    /// Sends the accumulated execution telemetry via the provided sender delegate, drains the
    /// static assertion counters, and clears the current collector. Safe to call when no sender
    /// is available (the counters and collector are still drained/cleared so state does not leak
    /// across sessions).
    /// </summary>
    /// <param name="telemetrySender">Optional delegate to send telemetry. If null, telemetry is silently discarded.</param>
    internal static async Task SendExecutionTelemetryAndResetAsync(Func<string, IDictionary<string, object>, Task>? telemetrySender)
    {
        try
        {
            // Always drain the static assertion counters so they don't leak across sessions,
            // even when no sender is wired (VSTest mode) or no collector was initialized
            // (e.g. telemetry opted out before EnsureInitialized was called).
            Dictionary<string, long> assertionCounts = TelemetryCollector.DrainAssertionCallCounts();

            MSTestTelemetryDataCollector? collector = Current;
            if (collector is null || telemetrySender is null)
            {
                return;
            }

            // Defense in depth: re-check opt-out at send time in case the env var was set after
            // EnsureInitialized but before this point.
            if (IsTelemetryOptedOut())
            {
                return;
            }

            // If the discovery event already shipped the settings/attribute payload during this
            // process, do not duplicate it in the sessionexit event. The flag is reset below in
            // the finally block so each process can still ship a fresh payload after a full
            // session reset.
            bool includeDiscoveryPayload = Interlocked.CompareExchange(ref s_discoveryEventEmitted, 0, 0) == 0;

            Dictionary<string, object> metrics = collector.BuildExecutionMetrics(assertionCounts, includeDiscoveryPayload);
            if (metrics.Count > 0)
            {
                await telemetrySender("dotnet/testingplatform/mstest/sessionexit", metrics).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Telemetry should never cause test failures
        }
        finally
        {
            Current = null;
            Interlocked.Exchange(ref s_discoveryEventEmitted, 0);
        }
    }
}
#endif

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Pure decision logic for the history-driven slow-test threshold feature. Kept free of any
/// platform dependency so it can be exercised directly by unit tests.
/// </summary>
internal static class AzureDevOpsSlowTestThresholds
{
    /// <summary>
    /// Returns the effective slow-test threshold for a test. When sufficient history is available the
    /// threshold is lowered to <c>p99 * multiplier</c>, but never raised above the static default.
    /// </summary>
    /// <param name="staticThreshold">The static fallback threshold (e.g. 60s).</param>
    /// <param name="stats">The historical duration statistics, when available.</param>
    /// <param name="hasStats">Whether <paramref name="stats"/> holds usable history.</param>
    /// <param name="multiplier">The multiplier applied to the historical p99.</param>
    /// <param name="minimumSampleCount">The minimum number of samples before history is trusted.</param>
    public static TimeSpan ComputeThreshold(TimeSpan staticThreshold, in DurationHistoryStats stats, bool hasStats, double multiplier, int minimumSampleCount)
    {
        if (!hasStats || stats.SampleCount < minimumSampleCount || stats.P99Milliseconds <= 0 || multiplier <= 0)
        {
            return staticThreshold;
        }

        double historyThresholdMs = stats.P99Milliseconds * multiplier;
        if (double.IsNaN(historyThresholdMs) || double.IsInfinity(historyThresholdMs) || historyThresholdMs < 0)
        {
            // A pathological multiplier (or overflow to infinity) would make TimeSpan.FromMilliseconds throw;
            // fall back to the static threshold instead of silently disabling slow-test detection.
            return staticThreshold;
        }

        var historyThreshold = TimeSpan.FromMilliseconds(historyThresholdMs);
        return historyThreshold < staticThreshold ? historyThreshold : staticThreshold;
    }

    /// <summary>
    /// Indicates whether the history decoration should be emitted for the given statistics.
    /// </summary>
    public static bool HasUsableHistory(in DurationHistoryStats stats, bool hasStats, int minimumSampleCount)
        => hasStats && stats.SampleCount >= minimumSampleCount && stats.P99Milliseconds > 0;

    /// <summary>
    /// Formats a duration (in milliseconds) into a compact human-readable string such as <c>2s</c>, <c>2.5s</c>, or <c>500ms</c>.
    /// </summary>
    public static string FormatDuration(double milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        return milliseconds >= 60_000
            ? TrimTrailingZero(milliseconds / 60_000.0) + "m"
            : milliseconds >= 1_000
                ? TrimTrailingZero(milliseconds / 1_000.0) + "s"
                : ((long)Math.Round(milliseconds)).ToString(CultureInfo.InvariantCulture) + "ms";
    }

    private static string TrimTrailingZero(double value)
    {
        // One decimal place is enough resolution for a heartbeat line; drop a trailing ".0".
        double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        long whole = (long)Math.Round(rounded, 0, MidpointRounding.AwayFromZero);
        return Math.Abs(rounded - whole) < 1e-9
            ? whole.ToString(CultureInfo.InvariantCulture)
            : rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

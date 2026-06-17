// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Immutable per-test historical duration statistics derived from Azure DevOps test result history.
/// </summary>
internal readonly struct DurationHistoryStats
{
    public DurationHistoryStats(double p95Milliseconds, double p99Milliseconds, int sampleCount)
    {
        P95Milliseconds = p95Milliseconds;
        P99Milliseconds = p99Milliseconds;
        SampleCount = sampleCount;
    }

    public double P95Milliseconds { get; }

    public double P99Milliseconds { get; }

    public int SampleCount { get; }

    /// <summary>
    /// Builds the statistics from a collection of per-run durations (in milliseconds).
    /// </summary>
    /// <param name="durationsMilliseconds">The per-run durations. Non-positive values are ignored.</param>
    /// <param name="stats">The computed statistics when at least one positive sample is present.</param>
    /// <returns><see langword="true"/> when at least one positive sample is present; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(IReadOnlyList<double> durationsMilliseconds, out DurationHistoryStats stats)
    {
        var samples = new List<double>(durationsMilliseconds.Count);
        foreach (double duration in durationsMilliseconds)
        {
            if (duration > 0)
            {
                samples.Add(duration);
            }
        }

        if (samples.Count == 0)
        {
            stats = default;
            return false;
        }

        samples.Sort();
        stats = new DurationHistoryStats(
            PercentileCalculator.Compute(samples, 95),
            PercentileCalculator.Compute(samples, 99),
            samples.Count);
        return true;
    }
}

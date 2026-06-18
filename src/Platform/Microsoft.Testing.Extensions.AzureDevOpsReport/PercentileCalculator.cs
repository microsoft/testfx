// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Computes percentiles from a set of samples using the nearest-rank method.
/// Azure DevOps Analytics does not expose percentile metrics directly, so the slow-test
/// history feature fetches raw per-run durations and computes p95/p99 client-side.
/// </summary>
internal static class PercentileCalculator
{
    /// <summary>
    /// Computes the percentile value using the nearest-rank method.
    /// </summary>
    /// <param name="sortedSamples">Samples sorted in ascending order. Must not be empty.</param>
    /// <param name="percentile">The percentile to compute, in the inclusive range (0, 100].</param>
    /// <returns>The sample at the nearest rank for the requested percentile.</returns>
    public static double Compute(IReadOnlyList<double> sortedSamples, double percentile)
    {
        if (sortedSamples.Count == 0)
        {
            throw new ArgumentException("At least one sample is required.", nameof(sortedSamples));
        }

        if (percentile is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be in the range (0, 100].");
        }

        // Nearest-rank: rank = ceil(p/100 * n), 1-based; index = rank - 1, clamped to the array bounds.
        int rank = (int)Math.Ceiling(percentile / 100.0 * sortedSamples.Count);
        int index = Math.Min(sortedSamples.Count - 1, Math.Max(0, rank - 1));
        return sortedSamples[index];
    }
}

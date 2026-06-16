// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsSlowTestHistoryTests
{
    [TestMethod]
    public void Percentile_NearestRank_ReturnsExpectedSamples()
    {
        double[] samples = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

        Assert.AreEqual(100.0, PercentileCalculator.Compute(samples, 100));
        Assert.AreEqual(100.0, PercentileCalculator.Compute(samples, 99));
        Assert.AreEqual(100.0, PercentileCalculator.Compute(samples, 95));
        Assert.AreEqual(50.0, PercentileCalculator.Compute(samples, 50));
        Assert.AreEqual(10.0, PercentileCalculator.Compute(samples, 1));
    }

    [TestMethod]
    public void Percentile_SingleSample_ReturnsThatSample()
        => Assert.AreEqual(42.0, PercentileCalculator.Compute([42], 95));

    [TestMethod]
    public void Percentile_EmptySamples_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() =>
        {
            _ = PercentileCalculator.Compute([], 95);
        });

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(100.1)]
    public void Percentile_OutOfRange_Throws(double percentile)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
        {
            _ = PercentileCalculator.Compute([1, 2, 3], percentile);
        });

    [TestMethod]
    public void DurationHistoryStats_TryCreate_IgnoresNonPositiveSamples()
    {
        Assert.IsTrue(DurationHistoryStats.TryCreate([0, -5, 100, 200, 300], out DurationHistoryStats stats));
        Assert.AreEqual(3, stats.SampleCount);
        Assert.AreEqual(300.0, stats.P99Milliseconds);
    }

    [TestMethod]
    public void DurationHistoryStats_TryCreate_WithNoPositiveSamples_ReturnsFalse()
        => Assert.IsFalse(DurationHistoryStats.TryCreate([0, -1], out _));

    [TestMethod]
    public void ComputeThreshold_WithoutHistory_UsesStaticThreshold()
    {
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), default, hasStats: false, multiplier: 3.0, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(60), threshold);
    }

    [TestMethod]
    public void ComputeThreshold_BelowMinimumSampleCount_UsesStaticThreshold()
    {
        var stats = new DurationHistoryStats(2000, 3000, sampleCount: 5);
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), stats, hasStats: true, multiplier: 3.0, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(60), threshold);
    }

    [TestMethod]
    public void ComputeThreshold_WithShortHistory_LowersThreshold()
    {
        // p99 = 3s, multiplier 3 => 9s, which is below the 60s static default.
        var stats = new DurationHistoryStats(2000, 3000, sampleCount: 120);
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), stats, hasStats: true, multiplier: 3.0, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(9), threshold);
    }

    [TestMethod]
    public void ComputeThreshold_WhenHistoryExceedsStatic_KeepsStaticThreshold()
    {
        // p99 = 30s, multiplier 3 => 90s, which is above the 60s static default.
        var stats = new DurationHistoryStats(25000, 30000, sampleCount: 120);
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), stats, hasStats: true, multiplier: 3.0, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(60), threshold);
    }

    [TestMethod]
    public void ComputeThreshold_WhenMultiplierOverflowsToInfinity_UsesStaticThreshold()
    {
        // A pathological multiplier would overflow p99 * multiplier to +Infinity, which would throw inside
        // TimeSpan.FromMilliseconds; the guard must fall back to the static threshold instead.
        var stats = new DurationHistoryStats(25000, 30000, sampleCount: 120);
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), stats, hasStats: true, multiplier: double.MaxValue, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(60), threshold);
    }

    [TestMethod]
    public void ComputeThreshold_WhenHistoryExceedsTimeSpanMax_UsesStaticThreshold()
    {
        // A large *finite* history threshold (neither NaN nor Infinity) can still exceed
        // TimeSpan.MaxValue.TotalMilliseconds and throw inside TimeSpan.FromMilliseconds. Because the result is
        // never below the static threshold, the method must short-circuit to the static one without converting.
        double hugeButFinite = TimeSpan.MaxValue.TotalMilliseconds * 2;
        var stats = new DurationHistoryStats(hugeButFinite, hugeButFinite, sampleCount: 120);
        TimeSpan threshold = AzureDevOpsSlowTestThresholds.ComputeThreshold(
            TimeSpan.FromSeconds(60), stats, hasStats: true, multiplier: 1.0, minimumSampleCount: 10);

        Assert.AreEqual(TimeSpan.FromSeconds(60), threshold);
    }

    [TestMethod]
    public void HasUsableHistory_RespectsMinimumSampleCount()
    {
        var stats = new DurationHistoryStats(2000, 3000, sampleCount: 9);
        Assert.IsFalse(AzureDevOpsSlowTestThresholds.HasUsableHistory(stats, hasStats: true, minimumSampleCount: 10));
        Assert.IsTrue(AzureDevOpsSlowTestThresholds.HasUsableHistory(new DurationHistoryStats(2000, 3000, 10), hasStats: true, minimumSampleCount: 10));
        Assert.IsFalse(AzureDevOpsSlowTestThresholds.HasUsableHistory(stats, hasStats: false, minimumSampleCount: 1));
    }

    [TestMethod]
    [DataRow(2000.0, "2s")]
    [DataRow(2500.0, "2.5s")]
    [DataRow(500.0, "500ms")]
    [DataRow(120000.0, "2m")]
    [DataRow(90000.0, "1.5m")]
    [DataRow(-10.0, "0ms")]
    public void FormatDuration_ProducesCompactStrings(double milliseconds, string expected)
        => Assert.AreEqual(expected, AzureDevOpsSlowTestThresholds.FormatDuration(milliseconds));
}

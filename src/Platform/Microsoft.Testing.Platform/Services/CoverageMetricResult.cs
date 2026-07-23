// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// A single correlated coverage measurement (one metric for one scope) exposed through
/// <see cref="ITestCoverageResult"/>. Carries counts; percentage is derived.
/// </summary>
public sealed class CoverageMetricResult
{
    private readonly CoverageCounts _coverageCounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageMetricResult"/> class.
    /// </summary>
    /// <param name="metric">The metric being measured.</param>
    /// <param name="coveredCount">The number of covered units.</param>
    /// <param name="coverableCount">The number of coverable units.</param>
    /// <param name="producerId">The collector that produced this measurement; part of the correlation key.</param>
    /// <param name="customMetricName">The custom metric name; set only when <paramref name="metric"/> is <see cref="CoverageMetric.Custom"/>.</param>
    public CoverageMetricResult(
        CoverageMetric metric,
        long coveredCount,
        long coverableCount,
        string producerId,
        string? customMetricName = null)
    {
        _coverageCounts = new CoverageCounts(coveredCount, coverableCount);
        CoverageMetricHelper.ValidateProducerId(producerId);
        CoverageMetricHelper.ValidateCustomMetricName(metric, customMetricName);

        Metric = metric;
        ProducerId = producerId;
        CustomMetricName = customMetricName;
    }

    /// <summary>Gets the metric being measured.</summary>
    public CoverageMetric Metric { get; }

    /// <summary>Gets the custom metric name; set only when <see cref="Metric"/> is <see cref="CoverageMetric.Custom"/>.</summary>
    public string? CustomMetricName { get; }

    /// <summary>Gets the collector that produced this measurement; part of the correlation key.</summary>
    public string ProducerId { get; }

    /// <summary>Gets the number of covered units.</summary>
    public long CoveredCount => _coverageCounts.CoveredCount;

    /// <summary>Gets the number of coverable units.</summary>
    public long CoverableCount => _coverageCounts.CoverableCount;

    /// <summary>Gets a value indicating whether there is any coverable data.</summary>
    public bool HasCoverableData => _coverageCounts.HasCoverableData;

    /// <summary>Gets the coverage as a percentage in the range 0–100; 0 when nothing is coverable.</summary>
    public double Percentage => _coverageCounts.Percentage;
}

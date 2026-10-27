// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Reports a single code-coverage measurement (one metric for one scope) as counts. The percentage is
/// derived, never stored.
/// </summary>
public sealed class TestCoverageMessage : DataWithSessionUid
{
    private readonly CoverageCounts _coverageCounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageMessage"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this measurement belongs to.</param>
    /// <param name="scope">The scope the measurement describes.</param>
    /// <param name="metric">The metric being measured.</param>
    /// <param name="coveredCount">The number of covered units.</param>
    /// <param name="coverableCount">The number of coverable units.</param>
    /// <param name="producerId">A stable, non-empty id of the collector that produced the measurement.</param>
    /// <param name="customMetricName">The custom metric name; required when <paramref name="metric"/> is <see cref="CoverageMetric.Custom"/> and forbidden otherwise.</param>
    public TestCoverageMessage(
        SessionUid sessionUid,
        CoverageScope scope,
        CoverageMetric metric,
        long coveredCount,
        long coverableCount,
        string producerId,
        string? customMetricName = null)
        : base("Test coverage", "Reports a code coverage measurement for a scope.", sessionUid)
    {
        _coverageCounts = new CoverageCounts(coveredCount, coverableCount);
        CoverageMetricHelper.ValidateProducerId(producerId);
        CoverageMetricHelper.ValidateCustomMetricName(metric, customMetricName);

        Scope = scope;
        Metric = metric;
        ProducerId = producerId;
        CustomMetricName = customMetricName;
    }

    /// <summary>Gets the scope the measurement describes.</summary>
    public CoverageScope Scope { get; }

    /// <summary>Gets the metric being measured.</summary>
    public CoverageMetric Metric { get; }

    /// <summary>
    /// Gets the id of the collector that produced this measurement (e.g. "microsoft-code-coverage").
    /// <b>Required and non-empty</b>: it is part of the correlation key, so results from multiple
    /// collectors are never silently merged, and it ties a measurement back to its
    /// <see cref="TestCoverageReportMessage"/>. A collector that does not care about multi-collector
    /// scenarios should still pass a stable constant (e.g. its extension Uid).
    /// </summary>
    public string ProducerId { get; }

    /// <summary>Gets the custom metric name; set only when <see cref="Metric"/> is <see cref="CoverageMetric.Custom"/>.</summary>
    public string? CustomMetricName { get; }

    /// <summary>Gets the number of covered units.</summary>
    public long CoveredCount => _coverageCounts.CoveredCount;

    /// <summary>Gets the number of coverable units.</summary>
    public long CoverableCount => _coverageCounts.CoverableCount;

    /// <summary>Gets a value indicating whether there is any coverable data.</summary>
    public bool HasCoverableData => _coverageCounts.HasCoverableData;

    /// <summary>Gets the coverage as a percentage in the range 0–100; 0 when nothing is coverable.</summary>
    public double Percentage => _coverageCounts.Percentage;
}

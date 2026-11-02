// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>Reports the result of a coverage threshold evaluation.</summary>
public sealed class TestCoverageThresholdMessage : DataWithSessionUid
{
    private readonly bool? _countBasedPassed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageThresholdMessage"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this evaluation belongs to.</param>
    /// <param name="scope">The scope the threshold applies to.</param>
    /// <param name="metric">The metric being evaluated.</param>
    /// <param name="aggregation">How per-scope values were combined.</param>
    /// <param name="actualPercentage">The actual coverage, 0–100 (normalized to 0 when there is no coverable data).</param>
    /// <param name="requiredPercentage">The required coverage threshold, 0–100.</param>
    /// <param name="hasCoverableData">Whether the evaluated scope had any coverable data.</param>
    /// <param name="producerId">A stable, non-empty id of the collector that produced the evaluation.</param>
    /// <param name="aggregatedOver">The child scope level the aggregation was computed over; non-null iff <paramref name="aggregation"/> is not <see cref="CoverageAggregation.None"/>.</param>
    /// <param name="treatNoDataAsFailure">The no-data policy: whether an evaluation with no coverable data counts as failed.</param>
    /// <param name="customMetricName">The custom metric name; required when <paramref name="metric"/> is <see cref="CoverageMetric.Custom"/> and forbidden otherwise.</param>
    public TestCoverageThresholdMessage(
        SessionUid sessionUid,
        CoverageScope scope,
        CoverageMetric metric,
        CoverageAggregation aggregation,
        double actualPercentage,
        double requiredPercentage,
        bool hasCoverableData,
        string producerId,
        CoverageScopeLevel? aggregatedOver = null,
        bool treatNoDataAsFailure = true,
        string? customMetricName = null)
        : base("Test coverage threshold", "Reports the result of a coverage threshold evaluation.", sessionUid)
    {
        ValidatePercentage(requiredPercentage, nameof(requiredPercentage));

        // ActualPercentage is only meaningful when there is coverable data; otherwise it is normalized
        // to a stable 0 so it can never leak NaN/Infinity into renderers/consumers that display it
        // without checking HasCoverableData.
        if (hasCoverableData)
        {
            ValidatePercentage(actualPercentage, nameof(actualPercentage));
        }
        else
        {
            actualPercentage = 0d;
        }

        CoverageMetricHelper.ValidateProducerId(producerId);
        CoverageMetricHelper.ValidateCustomMetricName(metric, customMetricName);

        // An aggregate must name the population it aggregated over; a non-aggregate must not.
        if (aggregation is CoverageAggregation.None && aggregatedOver is not null)
        {
            throw new ArgumentException("A non-aggregate evaluation must not specify a population.", nameof(aggregatedOver));
        }

        if (aggregation is not CoverageAggregation.None
            && aggregatedOver is null or CoverageScopeLevel.Overall)
        {
            throw new ArgumentException("An aggregate evaluation must specify a non-overall aggregated population.", nameof(aggregatedOver));
        }

        Scope = scope;
        Metric = metric;
        Aggregation = aggregation;
        AggregatedOver = aggregatedOver;
        ActualPercentage = actualPercentage;
        RequiredPercentage = requiredPercentage;
        HasCoverableData = hasCoverableData;
        TreatNoDataAsFailure = treatNoDataAsFailure;
        ProducerId = producerId;
        CustomMetricName = customMetricName;

        static void ValidatePercentage(double value, string paramName)
        {
            if (double.IsNaN(value) || value < 0d || value > 100d)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Percentage must be a number in the range 0–100.");
            }
        }
    }

    internal TestCoverageThresholdMessage(
        SessionUid sessionUid,
        CoverageMetric metric,
        decimal actualPercentage,
        decimal requiredPercentage,
        bool hasCoverableData,
        string producerId,
        bool passed)
        : this(sessionUid, CoverageScope.Overall, metric, CoverageAggregation.None, (double)actualPercentage,
            (double)requiredPercentage, hasCoverableData, producerId)
    {
        _countBasedPassed = passed;
        ExactActualPercentage = actualPercentage;
        ExactRequiredPercentage = requiredPercentage;
    }

    internal decimal? ExactActualPercentage { get; }

    internal decimal? ExactRequiredPercentage { get; }

    /// <summary>Gets the scope the threshold applies to (enables per-scope thresholds).</summary>
    public CoverageScope Scope { get; }

    /// <summary>Gets the metric being evaluated.</summary>
    public CoverageMetric Metric { get; }

    /// <summary>Gets the custom metric name; set only when <see cref="Metric"/> is <see cref="CoverageMetric.Custom"/>.</summary>
    public string? CustomMetricName { get; }

    /// <summary>Gets the id of the collector that produced this evaluation (required, correlation key).</summary>
    public string ProducerId { get; }

    /// <summary>Gets how per-scope values were combined.</summary>
    public CoverageAggregation Aggregation { get; }

    /// <summary>
    /// Gets the child scope level that <see cref="Aggregation"/> was computed over (e.g. the minimum
    /// across <see cref="CoverageScopeLevel.Module"/> vs. across <see cref="CoverageScopeLevel.File"/>).
    /// Non-null iff <see cref="Aggregation"/> is not <see cref="CoverageAggregation.None"/>, so a
    /// (scope, metric, aggregation) triple is unambiguous.
    /// </summary>
    public CoverageScopeLevel? AggregatedOver { get; }

    /// <summary>Gets the actual coverage, 0–100; normalized to 0 when <see cref="HasCoverableData"/> is false.</summary>
    public double ActualPercentage { get; }

    /// <summary>Gets the required coverage threshold, 0–100.</summary>
    public double RequiredPercentage { get; }

    /// <summary>
    /// Gets a value indicating whether the evaluated scope had any coverable data. <see langword="false"/>
    /// when the scope had nothing coverable (an empty module, generated-only code, …). This is kept
    /// distinct from a genuine 0% so a no-data policy can be applied explicitly rather than being
    /// conflated with real 0% coverage.
    /// </summary>
    public bool HasCoverableData { get; }

    /// <summary>Gets a value indicating whether an evaluation with no coverable data counts as failed (the no-data policy).</summary>
    public bool TreatNoDataAsFailure { get; }

    /// <summary>
    /// Gets a value indicating whether the threshold is satisfied. With no coverable data the outcome
    /// follows <see cref="TreatNoDataAsFailure"/>. Platform-configured thresholds retain the exact
    /// count-based comparison rather than comparing rounded percentage representations.
    /// </summary>
    public bool Passed => _countBasedPassed ?? (HasCoverableData
        ? ActualPercentage >= RequiredPercentage
        : !TreatNoDataAsFailure);
}

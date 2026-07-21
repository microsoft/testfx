// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Identifies a code-coverage metric. This is a closed but append-only set: consumers MUST treat
/// unrecognized values non-exhaustively (always handle a default case). New well-known members may
/// be added in future versions without being a breaking change.
/// </summary>
public enum CoverageMetric
{
    /// <summary>Line coverage.</summary>
    Line = 0,

    /// <summary>Statement coverage.</summary>
    Statement = 1,

    /// <summary>Branch coverage.</summary>
    Branch = 2,

    /// <summary>Method coverage.</summary>
    Method = 3,

    /// <summary>Function coverage.</summary>
    Function = 4,

    /// <summary>Block coverage (Microsoft.CodeCoverage / dotnet-coverage primary metric).</summary>
    Block = 5,

    /// <summary>Instruction coverage (JaCoCo).</summary>
    Instruction = 6,

    /// <summary>Region coverage (llvm-cov).</summary>
    Region = 7,

    /// <summary>Class coverage (JaCoCo / PHPUnit entity counter).</summary>
    Class = 8,

    /// <summary>Condition coverage.</summary>
    Condition = 9,

    /// <summary>Cyclomatic complexity (JaCoCo, count-based rather than a percentage).</summary>
    Complexity = 10,

    /// <summary>
    /// A collector-specific metric not covered by the well-known members. When set,
    /// <see cref="TestCoverageMessage.CustomMetricName"/> carries the metric identifier. This is the
    /// single escape hatch for proprietary / safety-critical metrics (e.g. MC/DC).
    /// </summary>
    Custom = 255,
}

/// <summary>
/// How per-scope values are combined for a threshold evaluation. Append-only. The population being
/// aggregated is identified separately by <see cref="TestCoverageThresholdMessage.AggregatedOver"/> —
/// e.g. <see cref="Minimum"/> over <see cref="CoverageScopeLevel.Module"/> vs. over
/// <see cref="CoverageScopeLevel.File"/> are distinct evaluations.
/// </summary>
public enum CoverageAggregation
{
    /// <summary>Not an aggregate — a single scope's own value (use with an exact scope).</summary>
    None = 0,

    /// <summary>Aggregate covered / aggregate coverable across the population.</summary>
    Total = 1,

    /// <summary>Worst scope in the population.</summary>
    Minimum = 2,

    /// <summary>Mean of per-scope percentages in the population.</summary>
    Average = 3,

    /// <summary>Best scope in the population.</summary>
    Maximum = 4,
}

/// <summary>The entity a coverage / threshold entry describes. Append-only.</summary>
public enum CoverageScopeLevel
{
    /// <summary>The whole run.</summary>
    Overall = 0,

    /// <summary>A module (assembly file on disk).</summary>
    Module = 1,

    /// <summary>An assembly.</summary>
    Assembly = 2,

    /// <summary>A namespace.</summary>
    Namespace = 3,

    /// <summary>A type.</summary>
    Type = 4,

    /// <summary>A source file.</summary>
    File = 5,
}

/// <summary>
/// Identifies the entity a coverage measurement or threshold describes. Scope levels form
/// overlapping dimensions rather than a single parent hierarchy (a file can contain many types and a
/// partial type can span many files), so this models scope identity, not a forced tree.
/// </summary>
public readonly struct CoverageScope : IEquatable<CoverageScope>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageScope"/> struct.
    /// </summary>
    /// <param name="level">The granularity of the scope.</param>
    /// <param name="name">The scope identifier (module path, type name, file path…); must be null for <see cref="CoverageScopeLevel.Overall"/> and non-empty otherwise.</param>
    /// <param name="containerHint">Optional, non-authoritative grouping hint for UIs.</param>
    public CoverageScope(CoverageScopeLevel level, string? name = null, string? containerHint = null)
    {
        // Invariant: only Overall is unnamed; every other level requires a stable identifier.
        if (level == CoverageScopeLevel.Overall)
        {
            if (name is not null)
            {
                throw new ArgumentException("The Overall scope must not have a name.", nameof(name));
            }
        }
        else if (RoslynString.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"A name is required for scope level '{level}'.", nameof(name));
        }

        Level = level;
        Name = name;
        ContainerHint = containerHint;
    }

    /// <summary>Gets the granularity of this scope.</summary>
    public CoverageScopeLevel Level { get; }

    /// <summary>Gets the scope identifier (module path, type name, file path…); null only for <see cref="CoverageScopeLevel.Overall"/>.</summary>
    public string? Name { get; }

    /// <summary>
    /// Gets an optional, non-authoritative grouping hint for UIs (e.g. the assembly of a type, or a
    /// representative file of a type). It is deliberately <b>not</b> a tree edge: a file can contain
    /// many types and a partial type can span many files, so scope levels form overlapping dimensions
    /// rather than a single parent hierarchy. Consumers that need exact containment should read the
    /// referenced report artifact (see <see cref="TestCoverageReportMessage"/>), which carries the
    /// authoritative structure.
    /// </summary>
    public string? ContainerHint { get; }

    /// <summary>Gets the whole-run scope.</summary>
    public static CoverageScope Overall => new(CoverageScopeLevel.Overall);

    /// <summary>Compares two <see cref="CoverageScope"/> values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the two values are equal.</returns>
    public static bool operator ==(CoverageScope left, CoverageScope right) => left.Equals(right);

    /// <summary>Compares two <see cref="CoverageScope"/> values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the two values are not equal.</returns>
    public static bool operator !=(CoverageScope left, CoverageScope right) => !left.Equals(right);

    /// <inheritdoc/>
    // Identity is (Level, Name) only. ContainerHint is a non-authoritative display hint and is
    // intentionally excluded so two collectors reporting the same scope with different hints are not
    // split into separate entries by the correlator.
    public bool Equals(CoverageScope other)
        => Level == other.Level
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CoverageScope scope && Equals(scope);

    /// <inheritdoc/>
    public override int GetHashCode() => RoslynHashCode.Combine(Level, Name);
}

/// <summary>
/// Reports a single code-coverage measurement (one metric for one scope) as counts. The percentage is
/// derived, never stored.
/// </summary>
public sealed class TestCoverageMessage : DataWithSessionUid
{
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
        CoverageMetricHelper.ValidateCounts(coveredCount, coverableCount);
        CoverageMetricHelper.ValidateProducerId(producerId);
        CoverageMetricHelper.ValidateCustomMetricName(metric, customMetricName);

        Scope = scope;
        Metric = metric;
        CoveredCount = coveredCount;
        CoverableCount = coverableCount;
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
    public long CoveredCount { get; }

    /// <summary>Gets the number of coverable units.</summary>
    public long CoverableCount { get; }

    /// <summary>Gets a value indicating whether there is any coverable data.</summary>
    public bool HasCoverableData => CoverableCount > 0;

    /// <summary>Gets the coverage as a percentage in the range 0–100; 0 when nothing is coverable.</summary>
    public double Percentage => HasCoverableData ? (double)CoveredCount / CoverableCount * 100d : 0d;
}

/// <summary>Reports the result of a coverage threshold evaluation.</summary>
public sealed class TestCoverageThresholdMessage : DataWithSessionUid
{
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

        if (aggregation is not CoverageAggregation.None && aggregatedOver is null)
        {
            throw new ArgumentException("An aggregate evaluation must specify the aggregated population.", nameof(aggregatedOver));
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
    /// follows <see cref="TreatNoDataAsFailure"/>; otherwise it is a plain numeric comparison.
    /// </summary>
    public bool Passed => HasCoverableData
        ? ActualPercentage >= RequiredPercentage
        : !TreatNoDataAsFailure;
}

/// <summary>The on-disk format of a coverage report artifact. Append-only.</summary>
public enum CoverageReportFormat
{
    /// <summary>An unknown / unspecified format.</summary>
    Unknown = 0,

    /// <summary>Cobertura XML.</summary>
    Cobertura = 1,

    /// <summary>OpenCover XML.</summary>
    OpenCover = 2,

    /// <summary>lcov text.</summary>
    Lcov = 3,

    /// <summary>Microsoft.CodeCoverage XML.</summary>
    CoverageXml = 4,

    /// <summary>A collector-specific format identified by <see cref="TestCoverageReportMessage.CustomFormatName"/>.</summary>
    Custom = 255,
}

/// <summary>
/// References a coverage report artifact so deep consumers (HTML/UI generators) can parse full-fidelity
/// per-line data that the summary intentionally does not carry.
/// </summary>
public sealed class TestCoverageReportMessage : DataWithSessionUid
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageReportMessage"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this report belongs to.</param>
    /// <param name="reportPath">The path to the report artifact.</param>
    /// <param name="format">The on-disk format of the report.</param>
    /// <param name="producerId">A stable, non-empty id of the collector that produced the report.</param>
    /// <param name="customFormatName">The custom format name; required when <paramref name="format"/> is <see cref="CoverageReportFormat.Custom"/> and forbidden otherwise.</param>
    public TestCoverageReportMessage(
        SessionUid sessionUid,
        string reportPath,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
        : base("Test coverage report", "References a coverage report artifact.", sessionUid)
    {
        CoverageReportHelper.Validate(reportPath, nameof(reportPath), format, producerId, customFormatName);

        ReportPath = reportPath;
        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    /// <summary>Gets the path to the report artifact.</summary>
    public string ReportPath { get; }

    /// <summary>Gets the on-disk format of the report.</summary>
    public CoverageReportFormat Format { get; }

    /// <summary>Gets the custom format name; set if and only if <see cref="Format"/> is <see cref="CoverageReportFormat.Custom"/>.</summary>
    public string? CustomFormatName { get; }

    /// <summary>Gets the id of the collector that produced the report (required, correlation key).</summary>
    public string ProducerId { get; }
}

/// <summary>Shared validation for the custom-metric escape hatch.</summary>
internal static class CoverageMetricHelper
{
    public static void ValidateCounts(long coveredCount, long coverableCount)
    {
        if (coverableCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coverableCount));
        }

        if (coveredCount < 0 || coveredCount > coverableCount)
        {
            throw new ArgumentOutOfRangeException(nameof(coveredCount));
        }
    }

    public static void ValidateProducerId(string producerId)
    {
        if (RoslynString.IsNullOrEmpty(producerId))
        {
            throw new ArgumentException("A stable, non-empty producer id is required.", nameof(producerId));
        }
    }

    public static void ValidateCustomMetricName(CoverageMetric metric, string? customMetricName)
    {
        if (metric == CoverageMetric.Custom && RoslynString.IsNullOrWhiteSpace(customMetricName))
        {
            throw new ArgumentException("A custom metric name is required when metric is Custom.", nameof(customMetricName));
        }

        if (metric != CoverageMetric.Custom && customMetricName is not null)
        {
            throw new ArgumentException("A custom metric name is only valid when metric is Custom.", nameof(customMetricName));
        }
    }
}

internal static class CoverageReportHelper
{
    public static void Validate(
        string path,
        string pathParameterName,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName)
    {
        if (RoslynString.IsNullOrEmpty(path))
        {
            throw new ArgumentException("A report path is required.", pathParameterName);
        }

        CoverageMetricHelper.ValidateProducerId(producerId);

        if (format == CoverageReportFormat.Custom && RoslynString.IsNullOrWhiteSpace(customFormatName))
        {
            throw new ArgumentException("A custom format name is required when format is Custom.", nameof(customFormatName));
        }

        if (format != CoverageReportFormat.Custom && customFormatName is not null)
        {
            throw new ArgumentException("A custom format name is only valid when format is Custom.", nameof(customFormatName));
        }
    }
}

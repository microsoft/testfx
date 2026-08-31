// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal static class CiCoverageSummary
{
    public static CiCoverageSummaryData Create(ITestCoverageResult coverageResult, SessionUid sessionUid)
    {
        CoverageScopeSummary[] scopes =
        [
            .. coverageResult.Scopes.Where(scope =>
                string.Equals(scope.SessionUid.Value, sessionUid.Value, StringComparison.Ordinal)),
        ];
        HashSet<(string ProducerId, CoverageMetric Metric, string? CustomMetricName)> overallMetricKeys =
        [
            .. scopes
                .Where(scope => scope.Scope.Level == CoverageScopeLevel.Overall)
                .SelectMany(scope => scope.Metrics)
                .Select(metric => (metric.ProducerId, metric.Metric, metric.CustomMetricName)),
        ];
        CiCoverageMetric[] metrics =
        [
            .. scopes
                .Where(scope => scope.Scope.Level is CoverageScopeLevel.Overall or CoverageScopeLevel.Module)
                .SelectMany(scope => scope.Metrics
                    .Where(metric =>
                        scope.Scope.Level == CoverageScopeLevel.Overall
                        || !overallMetricKeys.Contains((metric.ProducerId, metric.Metric, metric.CustomMetricName)))
                    .Select(metric => new CiCoverageMetric
                    {
                        ScopeLevel = scope.Scope.Level,
                        ScopeName = scope.Scope.Name ?? string.Empty,
                        Metric = metric.Metric,
                        CustomMetricName = metric.CustomMetricName ?? string.Empty,
                        ProducerId = metric.ProducerId,
                        CoveredCount = metric.CoveredCount,
                        CoverableCount = metric.CoverableCount,
                    })),
        ];
        CiCoverageThreshold[] thresholds =
        [
            .. coverageResult.Thresholds
                .Where(threshold => string.Equals(threshold.SessionUid.Value, sessionUid.Value, StringComparison.Ordinal))
                .Select(threshold => new CiCoverageThreshold
                {
                    ScopeLevel = threshold.Scope.Level,
                    ScopeName = threshold.Scope.Name ?? string.Empty,
                    Metric = threshold.Metric,
                    CustomMetricName = threshold.CustomMetricName ?? string.Empty,
                    ProducerId = threshold.ProducerId,
                    Aggregation = threshold.Aggregation,
                    AggregatedOver = threshold.AggregatedOver,
                    ActualPercentage = threshold.ActualPercentage,
                    RequiredPercentage = threshold.RequiredPercentage,
                    HasCoverableData = threshold.HasCoverableData,
                    Passed = threshold.Passed,
                }),
        ];

        return new CiCoverageSummaryData
        {
            Metrics = metrics,
            Thresholds = thresholds,
            ReportingModuleCount = metrics.Length > 0 || thresholds.Length > 0 ? 1 : 0,
            TotalModuleCount = 1,
        };
    }

    public static CiCoverageSummaryData Aggregate(IReadOnlyList<CiRunSummaryModule> modules, int totalModuleCount)
    {
        var metrics = new Dictionary<(string ProducerId, CoverageMetric Metric, string CustomMetricName), (long Covered, long Coverable)>();
        var metricOrder = new List<(string ProducerId, CoverageMetric Metric, string CustomMetricName)>();
        var thresholds = new List<CiCoverageThreshold>();
        int reportingModuleCount = 0;

        foreach (CiRunSummaryModule module in modules)
        {
            if (module.Coverage.Metrics.Length > 0 || module.Coverage.Thresholds.Length > 0)
            {
                reportingModuleCount++;
            }

            foreach (CiCoverageMetric metric in module.Coverage.Metrics)
            {
                (string ProducerId, CoverageMetric Metric, string CustomMetricName) key =
                    (metric.ProducerId, metric.Metric, metric.CustomMetricName);
                if (!metrics.TryGetValue(key, out (long Covered, long Coverable) counts))
                {
                    metricOrder.Add(key);
                }

                metrics[key] = (
                    checked(counts.Covered + metric.CoveredCount),
                    checked(counts.Coverable + metric.CoverableCount));
            }

            foreach (CiCoverageThreshold threshold in module.Coverage.Thresholds)
            {
                thresholds.Add(new CiCoverageThreshold
                {
                    Source = $"{module.AssemblyName} ({module.TargetFramework})",
                    ScopeLevel = threshold.ScopeLevel,
                    ScopeName = threshold.ScopeName,
                    Metric = threshold.Metric,
                    CustomMetricName = threshold.CustomMetricName,
                    ProducerId = threshold.ProducerId,
                    Aggregation = threshold.Aggregation,
                    AggregatedOver = threshold.AggregatedOver,
                    ActualPercentage = threshold.ActualPercentage,
                    RequiredPercentage = threshold.RequiredPercentage,
                    HasCoverableData = threshold.HasCoverableData,
                    Passed = threshold.Passed,
                });
            }
        }

        return new CiCoverageSummaryData
        {
            Metrics =
            [
                .. metricOrder.Select(key => new CiCoverageMetric
                {
                    ScopeLevel = CoverageScopeLevel.Overall,
                    Metric = key.Metric,
                    CustomMetricName = key.CustomMetricName,
                    ProducerId = key.ProducerId,
                    CoveredCount = metrics[key].Covered,
                    CoverableCount = metrics[key].Coverable,
                }),
            ],
            Thresholds = [.. thresholds],
            ReportingModuleCount = reportingModuleCount,
            TotalModuleCount = totalModuleCount,
        };
    }

    public static void AppendMarkdown(StringBuilder builder, CiCoverageSummaryData coverage, int headingLevel)
    {
        if (coverage.Metrics.Length == 0 && coverage.Thresholds.Length == 0)
        {
            return;
        }

        string heading = new('#', headingLevel);
        builder.Append(heading).Append(" Code coverage\n\n");
        if (coverage.TotalModuleCount > 1 && coverage.ReportingModuleCount < coverage.TotalModuleCount)
        {
            builder.Append("> Coverage data was reported by ")
                .Append(coverage.ReportingModuleCount.ToString(CultureInfo.InvariantCulture))
                .Append(" of ")
                .Append(coverage.TotalModuleCount.ToString(CultureInfo.InvariantCulture))
                .Append(" test modules.\n\n");
        }

        if (coverage.Metrics.Length > 0)
        {
            Dictionary<(CoverageScopeLevel ScopeLevel, string ScopeName, CoverageMetric Metric, string CustomMetricName), int> metricCounts = [];
            foreach (CiCoverageMetric metric in coverage.Metrics)
            {
                (CoverageScopeLevel ScopeLevel, string ScopeName, CoverageMetric Metric, string CustomMetricName) key =
                    (metric.ScopeLevel, metric.ScopeName, metric.Metric, metric.CustomMetricName);
                metricCounts.TryGetValue(key, out int count);
                metricCounts[key] = count + 1;
            }

            builder.Append("| Scope | Metric | Covered | Total | Coverage |\n");
            builder.Append("| --- | --- | ---: | ---: | ---: |\n");
            foreach (CiCoverageMetric metric in coverage.Metrics)
            {
                string metricLabel = GetMetricLabel(metric.Metric, metric.CustomMetricName);
                if (metricCounts[(metric.ScopeLevel, metric.ScopeName, metric.Metric, metric.CustomMetricName)] > 1)
                {
                    metricLabel = $"{metricLabel} ({metric.ProducerId})";
                }

                builder.Append("| ").Append(EscapeCell(GetScopeLabel(metric.ScopeLevel, metric.ScopeName)))
                    .Append(" | ").Append(EscapeCell(metricLabel))
                    .Append(" | ").Append(metric.CoveredCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(metric.CoverableCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(metric.CoverableCount > 0
                        ? ((double)metric.CoveredCount / metric.CoverableCount * 100d).ToString("F1", CultureInfo.InvariantCulture) + "%"
                        : "No data")
                    .Append(" |\n");
            }

            builder.Append('\n');
        }

        if (coverage.Thresholds.Length > 0)
        {
            Dictionary<(string Source, CoverageScopeLevel ScopeLevel, string ScopeName, CoverageMetric Metric, string CustomMetricName, CoverageAggregation Aggregation, CoverageScopeLevel? AggregatedOver), int> thresholdCounts = [];
            foreach (CiCoverageThreshold threshold in coverage.Thresholds)
            {
                (string Source, CoverageScopeLevel ScopeLevel, string ScopeName, CoverageMetric Metric, string CustomMetricName, CoverageAggregation Aggregation, CoverageScopeLevel? AggregatedOver) key =
                    (threshold.Source, threshold.ScopeLevel, threshold.ScopeName, threshold.Metric, threshold.CustomMetricName, threshold.Aggregation, threshold.AggregatedOver);
                thresholdCounts.TryGetValue(key, out int count);
                thresholdCounts[key] = count + 1;
            }

            builder.Append(heading).Append("# Coverage thresholds\n\n");
            builder.Append("| Scope | Metric | Actual | Required | Result |\n");
            builder.Append("| --- | --- | ---: | ---: | --- |\n");
            foreach (CiCoverageThreshold threshold in coverage.Thresholds)
            {
                string scope = GetScopeLabel(threshold.ScopeLevel, threshold.ScopeName);
                if (!RoslynString.IsNullOrEmpty(threshold.Source))
                {
                    scope = $"{threshold.Source} — {scope}";
                }

                string actual = threshold.HasCoverableData
                    ? FormatPercentage(threshold.ActualPercentage, threshold.RequiredPercentage, threshold.Passed)
                    : "No data";
                string thresholdLabel = GetThresholdLabel(threshold);
                if (thresholdCounts[(threshold.Source, threshold.ScopeLevel, threshold.ScopeName, threshold.Metric, threshold.CustomMetricName, threshold.Aggregation, threshold.AggregatedOver)] > 1)
                {
                    thresholdLabel = $"{thresholdLabel} ({threshold.ProducerId})";
                }

                builder.Append("| ").Append(EscapeCell(scope))
                    .Append(" | ").Append(EscapeCell(thresholdLabel))
                    .Append(" | ").Append(actual)
                    .Append(" | ").Append(threshold.RequiredPercentage.ToString("F1", CultureInfo.InvariantCulture)).Append("%")
                    .Append(" | ").Append(threshold.Passed ? "✅ Passed" : "❌ Failed")
                    .Append(" |\n");
            }

            builder.Append('\n');
        }
    }

    private static string FormatPercentage(double actual, double required, bool passed)
    {
        string formattedActual = actual.ToString("F1", CultureInfo.InvariantCulture);
        if (!passed && string.Equals(formattedActual, required.ToString("F1", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            formattedActual = actual.ToString("G17", CultureInfo.InvariantCulture);
        }

        return formattedActual + "%";
    }

    private static string GetThresholdLabel(CiCoverageThreshold threshold)
        => threshold.Aggregation == CoverageAggregation.None
            ? GetMetricLabel(threshold.Metric, threshold.CustomMetricName)
            : threshold.AggregatedOver is { } aggregatedOver
                ? $"{GetMetricLabel(threshold.Metric, threshold.CustomMetricName)} ({threshold.Aggregation} over {aggregatedOver})"
                : $"{GetMetricLabel(threshold.Metric, threshold.CustomMetricName)} ({threshold.Aggregation})";

    private static string GetMetricLabel(CoverageMetric metric, string customMetricName)
        => metric == CoverageMetric.Custom && !RoslynString.IsNullOrEmpty(customMetricName)
            ? customMetricName
            : metric.ToString();

    private static string GetScopeLabel(CoverageScopeLevel level, string scopeName)
        => level == CoverageScopeLevel.Overall
            ? "Overall"
            : RoslynString.IsNullOrEmpty(scopeName) ? level.ToString() : scopeName;

    private static string EscapeCell(string value)
        => System.Net.WebUtility.HtmlEncode(value)
            .Replace("|", "\\|")
            .Replace("\r", string.Empty)
            .Replace("\n", "<br>");
}

#pragma warning restore RS0051

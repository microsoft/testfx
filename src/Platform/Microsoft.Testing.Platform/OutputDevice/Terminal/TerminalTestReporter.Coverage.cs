// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void AppendCoverageSummary(IReadOnlyList<CoverageScopeSummary> scopes, IReadOnlyList<TestCoverageThresholdMessage> thresholds)
        => _terminalWithProgress.WriteToTerminal(terminal => AppendCoverageSummary(terminal, scopes, thresholds));

    private static void AppendCoverageSummary(ITerminal terminal, IReadOnlyList<CoverageScopeSummary> scopes, IReadOnlyList<TestCoverageThresholdMessage> thresholds)
    {
        CoverageScopeSummary[] summaryScopes =
            [.. scopes.Where(scope => scope.Scope.Level is CoverageScopeLevel.Overall or CoverageScopeLevel.Module)];
        if (summaryScopes.Length == 0 && thresholds.Count == 0)
        {
            return;
        }

        terminal.AppendLine();

        if (summaryScopes.Length > 0)
        {
            terminal.AppendLine($"{SingleIndentation}{TerminalResources.CodeCoverageSummary}");

            foreach (CoverageScopeSummary scope in summaryScopes)
            {
                string scopeLabel = GetCoverageScopeLabel(scope.Scope);
                Dictionary<(CoverageMetric Metric, string? CustomMetricName), int> metricCounts = [];
                foreach (CoverageMetricResult metric in scope.Metrics)
                {
                    (CoverageMetric Metric, string? CustomMetricName) metricKey = (metric.Metric, metric.CustomMetricName);
                    metricCounts.TryGetValue(metricKey, out int count);
                    metricCounts[metricKey] = count + 1;
                }

                foreach (CoverageMetricResult metric in scope.Metrics)
                {
                    terminal.Append(DoubleIndentation);
                    string coverageValue = metric.HasCoverableData
                        ? $"{metric.Percentage.ToString("F1", CultureInfo.InvariantCulture)}%"
                        : TerminalResources.CoverageNoData;
                    string metricLabel = GetCoverageMetricLabel(metric.Metric, metric.CustomMetricName);
                    if (metricCounts[(metric.Metric, metric.CustomMetricName)] > 1)
                    {
                        metricLabel = string.Format(
                            CultureInfo.InvariantCulture,
                            TerminalResources.CoverageMetricWithProducer,
                            metricLabel,
                            MakeControlCharactersVisible(metric.ProducerId, true));
                    }

                    terminal.AppendLine($"{scopeLabel} - {metricLabel}: {coverageValue}");
                }
            }
        }

        if (thresholds.Count > 0)
        {
            // Only separate from the coverage block above when it was actually rendered; otherwise the
            // unconditional blank line at the top of the method already provides the single leading blank.
            if (summaryScopes.Length > 0)
            {
                terminal.AppendLine();
            }

            terminal.AppendLine($"{SingleIndentation}{TerminalResources.CoverageThresholdResults}");

            Dictionary<(CoverageScope Scope, CoverageMetric Metric, string? CustomMetricName, CoverageAggregation Aggregation, CoverageScopeLevel? AggregatedOver), int> thresholdCounts = [];
            foreach (TestCoverageThresholdMessage threshold in thresholds)
            {
                (CoverageScope Scope, CoverageMetric Metric, string? CustomMetricName, CoverageAggregation Aggregation, CoverageScopeLevel? AggregatedOver) thresholdKey =
                    (threshold.Scope, threshold.Metric, threshold.CustomMetricName, threshold.Aggregation, threshold.AggregatedOver);
                thresholdCounts.TryGetValue(thresholdKey, out int count);
                thresholdCounts[thresholdKey] = count + 1;
            }

            foreach (TestCoverageThresholdMessage threshold in thresholds)
            {
                bool passed = threshold.Passed;
                terminal.SetColor(passed ? TerminalColor.DarkGreen : TerminalColor.DarkRed);
                terminal.Append(DoubleIndentation);
                string comparison;
                if (threshold.HasCoverableData)
                {
                    (string actual, string required) = FormatThresholdPercentages(threshold);
                    comparison = string.Format(
                        CultureInfo.InvariantCulture,
                        passed ? TerminalResources.CoverageThresholdPassed : TerminalResources.CoverageThresholdFailed,
                        actual,
                        required);
                }
                else
                {
                    comparison = passed
                        ? TerminalResources.CoverageThresholdNoDataPassed
                        : TerminalResources.CoverageThresholdNoDataFailed;
                }

                string thresholdLabel = GetCoverageThresholdLabel(threshold);
                if (thresholdCounts[(threshold.Scope, threshold.Metric, threshold.CustomMetricName, threshold.Aggregation, threshold.AggregatedOver)] > 1)
                {
                    thresholdLabel = string.Format(
                        CultureInfo.InvariantCulture,
                        TerminalResources.CoverageMetricWithProducer,
                        thresholdLabel,
                        MakeControlCharactersVisible(threshold.ProducerId, true));
                }

                terminal.AppendLine($"{thresholdLabel}: {comparison}");
                terminal.ResetColor();
            }
        }
    }

    private static (string Actual, string Required) FormatThresholdPercentages(TestCoverageThresholdMessage threshold)
    {
        string actual = threshold.ActualPercentage.ToString("F1", CultureInfo.InvariantCulture);
        string required = threshold.RequiredPercentage.ToString("F1", CultureInfo.InvariantCulture);

        // Rounding a failed near-boundary result to one decimal can make both operands look equal.
        // Increase precision only for that ambiguous case so the rendered comparison remains truthful.
        if (!threshold.Passed && string.Equals(actual, required, StringComparison.Ordinal))
        {
            actual = threshold.ActualPercentage.ToString("G17", CultureInfo.InvariantCulture);
            required = threshold.RequiredPercentage.ToString("G17", CultureInfo.InvariantCulture);
        }

        return (actual, required);
    }

    // Renders the scope identity used in the coverage summary; the whole-run (Overall) scope is shown
    // with a localized "Total" label, every other scope uses its own name.
    private static string GetCoverageScopeLabel(CoverageScope scope)
        => scope.Level == CoverageScopeLevel.Overall
            ? TerminalResources.CoverageScopeOverall
            : MakeControlCharactersVisible(scope.Name ?? GetCoverageScopeLevelLabel(scope.Level), true);

    // Maps the CoverageMetric enum to a localized label so localized runs don't render the English enum
    // identifier; falls back to the identifier (or custom name) for any member without a resource.
    private static string GetCoverageMetricLabel(CoverageMetric metric, string? customMetricName)
        => metric switch
        {
            CoverageMetric.Line => TerminalResources.CoverageMetricLine,
            CoverageMetric.Statement => TerminalResources.CoverageMetricStatement,
            CoverageMetric.Branch => TerminalResources.CoverageMetricBranch,
            CoverageMetric.Method => TerminalResources.CoverageMetricMethod,
            CoverageMetric.Function => TerminalResources.CoverageMetricFunction,
            CoverageMetric.Block => TerminalResources.CoverageMetricBlock,
            CoverageMetric.Instruction => TerminalResources.CoverageMetricInstruction,
            CoverageMetric.Region => TerminalResources.CoverageMetricRegion,
            CoverageMetric.Class => TerminalResources.CoverageMetricClass,
            CoverageMetric.Condition => TerminalResources.CoverageMetricCondition,
            CoverageMetric.Complexity => TerminalResources.CoverageMetricComplexity,
            CoverageMetric.Custom => MakeControlCharactersVisible(customMetricName ?? nameof(CoverageMetric.Custom), true),
            _ => metric.ToString(),
        };

    private static string GetCoverageThresholdLabel(TestCoverageThresholdMessage threshold)
    {
        string scope = GetCoverageScopeLabel(threshold.Scope);
        string metric = GetCoverageMetricLabel(threshold.Metric, threshold.CustomMetricName);
        if (threshold.Aggregation == CoverageAggregation.None)
        {
            return $"{scope} - {metric}";
        }

        string aggregation = GetCoverageAggregationLabel(threshold.Aggregation);
        string aggregatedOver = GetCoverageScopeLevelLabel(threshold.AggregatedOver.GetValueOrDefault());
        string aggregationLabel = string.Format(
            CultureInfo.InvariantCulture,
            TerminalResources.CoverageAggregationOver,
            aggregation,
            aggregatedOver);

        return $"{scope} - {metric} ({aggregationLabel})";
    }

    private static string GetCoverageAggregationLabel(CoverageAggregation aggregation)
        => aggregation switch
        {
            CoverageAggregation.Total => TerminalResources.CoverageAggregationTotal,
            CoverageAggregation.Minimum => TerminalResources.CoverageAggregationMinimum,
            CoverageAggregation.Average => TerminalResources.CoverageAggregationAverage,
            CoverageAggregation.Maximum => TerminalResources.CoverageAggregationMaximum,
            _ => aggregation.ToString(),
        };

    private static string GetCoverageScopeLevelLabel(CoverageScopeLevel level)
        => level switch
        {
            CoverageScopeLevel.Overall => TerminalResources.CoverageScopeOverall,
            CoverageScopeLevel.Module => TerminalResources.CoverageScopeModule,
            CoverageScopeLevel.Assembly => TerminalResources.CoverageScopeAssembly,
            CoverageScopeLevel.Namespace => TerminalResources.CoverageScopeNamespace,
            CoverageScopeLevel.Type => TerminalResources.CoverageScopeType,
            CoverageScopeLevel.File => TerminalResources.CoverageScopeFile,
            _ => level.ToString(),
        };
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

internal static class CoverageThresholdPolicy
{
    public static (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) Evaluate(
        ICommandLineOptions commandLineOptions,
        IReadOnlyList<CoverageScopeSummary> scopes,
        SessionUid? sessionUid)
    {
        List<TestCoverageThresholdMessage> thresholds = [];
        List<string> errors = [];
        CoverageScopeSummary[] overallScopes =
            [.. scopes.Where(scope => scope.Scope.Level == CoverageScopeLevel.Overall
                && (sessionUid is null || scope.SessionUid.Equals(sessionUid.Value)))];
        CoverageScopeSummary? overall = overallScopes.Length == 1 ? overallScopes[0] : null;
        bool ambiguous = overallScopes.Length > 1
            || overall?.Metrics.Select(metric => metric.ProducerId).Distinct(StringComparer.Ordinal).Skip(1).Any() == true;

        EvaluateMetric(PlatformCommandLineProvider.CoverageThresholdLineOptionKey, CoverageMetric.Line);
        EvaluateMetric(PlatformCommandLineProvider.CoverageThresholdBranchOptionKey, CoverageMetric.Branch);

        return (thresholds, errors);

        void EvaluateMetric(string optionName, CoverageMetric metric)
        {
            if (!commandLineOptions.TryGetOptionArgumentList(optionName, out string[]? arguments))
            {
                return;
            }

            decimal required = decimal.Parse(arguments[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            CoverageMetricResult? measurement = ambiguous ? null : overall?[metric];
            string? error = ambiguous
                ? PlatformResources.CoverageThresholdAmbiguous
                : measurement is null
                    ? PlatformResources.CoverageThresholdMissingMeasurement
                    : !measurement.HasCoverableData
                        ? PlatformResources.CoverageThresholdEmptyMeasurement
                        : null;

            double actualPercentage = measurement?.Percentage ?? 0;
            double requiredPercentage = (double)required;

            thresholds.Add(new TestCoverageThresholdMessage(
                sessionUid ?? overall?.SessionUid ?? new SessionUid("coverage"),
                CoverageScope.Overall,
                metric,
                CoverageAggregation.None,
                actualPercentage,
                requiredPercentage,
                hasCoverableData: measurement?.HasCoverableData == true,
                producerId: nameof(CoverageThresholdPolicy)));

            if (error is not null)
            {
                errors.Add(string.Format(CultureInfo.InvariantCulture, error, optionName));
            }
            else if (actualPercentage < requiredPercentage)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CoverageThresholdNotMet,
                    optionName,
                    actualPercentage.ToString("G17", CultureInfo.InvariantCulture),
                    required.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}

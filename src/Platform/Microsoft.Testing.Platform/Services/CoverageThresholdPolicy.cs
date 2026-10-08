// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

internal static class CoverageThresholdPolicy
{
    public static bool IsDeferredToController(ICommandLineOptions commandLineOptions, IEnvironment environment)
        => (commandLineOptions.IsOptionSet(PlatformCommandLineProvider.CoverageThresholdLineOptionKey)
                || commandLineOptions.IsOptionSet(PlatformCommandLineProvider.CoverageThresholdBranchOptionKey))
            && commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, out string[]? controllerPid)
            && controllerPid is [string pid]
            && environment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_COVERAGEPOLICY}_{pid}") == "1";

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
            CoverageMetricResult[] measurements = overall is null ? [] : [.. overall.Metrics.Where(result => result.Metric == metric)];
            bool ambiguous = overallScopes.Length > 1 || measurements.Length > 1;
            CoverageMetricResult? measurement = !ambiguous && measurements.Length == 1 ? measurements[0] : null;
            string? error = ambiguous
                ? PlatformResources.CoverageThresholdAmbiguous
                : measurement is null
                    ? PlatformResources.CoverageThresholdMissingMeasurement
                    : !measurement.HasCoverableData
                        ? PlatformResources.CoverageThresholdEmptyMeasurement
                        : null;

            bool hasCoverableData = measurement is { HasCoverableData: true };
            decimal actualPercentage = hasCoverableData
                ? 100m * measurement!.CoveredCount / measurement.CoverableCount
                : 0;
            bool passed = hasCoverableData && MeetsThreshold(measurement!.CoveredCount, measurement.CoverableCount, required);
            TestCoverageThresholdMessage threshold = new(
                sessionUid ?? overall?.SessionUid ?? new SessionUid("coverage"),
                metric,
                actualPercentage,
                required,
                hasCoverableData,
                nameof(CoverageThresholdPolicy),
                passed);
            thresholds.Add(threshold);

            if (error is not null)
            {
                errors.Add(string.Format(CultureInfo.InvariantCulture, error, optionName));
            }
            else if (!threshold.Passed)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CoverageThresholdNotMet,
                    optionName,
                    actualPercentage.ToString(CultureInfo.InvariantCulture),
                    required.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private static bool MeetsThreshold(long coveredCount, long coverableCount, decimal required)
    {
        int[] bits = decimal.GetBits(required);
        int scale = (bits[3] >> 16) & 0xFF;
        BigInteger significand = ((BigInteger)(uint)bits[2] << 64)
            | ((BigInteger)(uint)bits[1] << 32)
            | (uint)bits[0];

        // Cross-multiply integers so neither division nor decimal/double rounding can change
        // an inclusive boundary, even with long-sized counts and 28 decimal places.
        return (BigInteger)coveredCount * 100 * BigInteger.Pow(10, scale) >= significand * coverableCount;
    }
}

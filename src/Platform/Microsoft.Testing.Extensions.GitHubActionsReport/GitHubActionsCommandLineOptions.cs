// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static class GitHubActionsCommandLineOptions
{
    public const string GitHubActionsOptionName = "report-gh";
    public const string GitHubActionsGroups = "report-gh-groups";
    public const string GitHubActionsHistory = "report-gh-history";
    public const string GitHubActionsHistoryWindow = "report-gh-history-window";
    public const string GitHubActionsAnnotations = "report-gh-annotations";
    public const string GitHubActionsStepSummary = "report-gh-step-summary";
    public const string GitHubActionsStepSummarySections = "report-gh-step-summary-sections";
    public const string GitHubActionsFailureDetails = "report-gh-failure-details";
    public const string GitHubActionsSlowTestNotices = "report-gh-slow-test-notices";
    public const string GitHubActionsSlowTestThreshold = "report-gh-slow-test-threshold";

    public const string StepSummaryOnFailureValue = "on-failure";
    public const int HistoryWindowDefaultDays = 30;
    public const int SlowTestThresholdDefaultSeconds = 60;
}

[Flags]
internal enum GitHubActionsStepSummarySections
{
    None = 0,
    TestResults = 1,
    SlowTests = 2,
    Coverage = 4,
    All = TestResults | SlowTests | Coverage,
}

internal static class GitHubActionsStepSummarySectionsParser
{
    internal const string AllSectionName = "all";
    internal const string CoverageSectionName = "coverage";
    internal const string SlowTestsSectionName = "slow-tests";
    internal const string TestResultsSectionName = "test-results";

    public static GitHubActionsStepSummarySections GetSections(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.TryGetOptionArgumentList(
                GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections,
                out string[]? arguments)
            ? GitHubActionsStepSummarySections.All
            : TryParse(arguments, out GitHubActionsStepSummarySections sections, out _, out _)
            ? sections
            : throw new InvalidOperationException("The GitHub Actions step-summary sections were not validated.");

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out GitHubActionsStepSummarySections sections,
        out string? invalidValue,
        out bool hasEmptyValue)
    {
        sections = GitHubActionsStepSummarySections.None;
        invalidValue = null;
        hasEmptyValue = false;

        foreach (string argument in arguments)
        {
            string[] values = argument.Split(',');
            foreach (string normalizedValue in values.Select(static value => value.Trim()))
            {
                if (normalizedValue.Length == 0)
                {
                    hasEmptyValue = true;
                    return false;
                }

                switch (normalizedValue.ToLowerInvariant())
                {
                    case TestResultsSectionName:
                        sections |= GitHubActionsStepSummarySections.TestResults;
                        break;
                    case SlowTestsSectionName:
                        sections |= GitHubActionsStepSummarySections.SlowTests;
                        break;
                    case CoverageSectionName:
                        sections |= GitHubActionsStepSummarySections.Coverage;
                        break;
                    case AllSectionName:
                        sections = GitHubActionsStepSummarySections.All;
                        break;
                    default:
                        invalidValue = normalizedValue;
                        return false;
                }
            }
        }

        if (sections == GitHubActionsStepSummarySections.None)
        {
            hasEmptyValue = true;
            return false;
        }

        return true;
    }

    public static string[] ToPersistedValues(GitHubActionsStepSummarySections sections)
    {
        var values = new List<string>(2);
        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            values.Add(TestResultsSectionName);
        }

        if ((sections & GitHubActionsStepSummarySections.SlowTests) != 0)
        {
            values.Add(SlowTestsSectionName);
        }

        if ((sections & GitHubActionsStepSummarySections.Coverage) != 0)
        {
            values.Add(CoverageSectionName);
        }

        return [.. values];
    }

    public static GitHubActionsStepSummarySections GetAggregateSections(IReadOnlyList<CiRunSummaryModule> modules)
    {
        if (modules.Count == 0)
        {
            return GitHubActionsStepSummarySections.All;
        }

        GitHubActionsStepSummarySections sections = GitHubActionsStepSummarySections.None;
        foreach (CiRunSummaryModule module in modules)
        {
            string[]? persistedValues = module.GitHubActionsStepSummarySections;
            if (persistedValues is null
                || !TryParse(persistedValues, out GitHubActionsStepSummarySections moduleSections, out _, out _))
            {
                return GitHubActionsStepSummarySections.All;
            }

            sections |= moduleSections;
            if (sections == GitHubActionsStepSummarySections.All)
            {
                return sections;
            }
        }

        return sections;
    }
}

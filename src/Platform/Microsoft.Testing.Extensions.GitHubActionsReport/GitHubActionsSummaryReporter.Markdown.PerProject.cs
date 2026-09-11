// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    private static string BuildMarkdownCore(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int exitCode,
        CiCoverageSummaryData coverage,
        GitHubActionsStepSummarySections sections,
        bool includeFailureDetails,
        SummaryBudget budget)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int flaky = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failures = new List<TestRecord>();
        var flakyTests = new List<TestRecord>();

        foreach (TestRecord record in records)
        {
            totalDuration += record.Duration;
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    if (failures.Count < MaxFailures)
                    {
                        failures.Add(record);
                    }

                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }

            if (record.IsFlaky)
            {
                flaky++;
                flakyTests.Add(record);
            }
        }

        // Reflect the process verdict, not just the failed-test count: a run can end in failure with zero failed
        // tests (e.g. zero tests discovered or a --minimum-expected-tests violation), which must not show ✅.
        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        string statusIcon = runFailed ? "❌" : "✅";

        var builder = new StringBuilder();
        builder.Append(ProjectSectionMarker).Append('\n');
        builder.Append("## ").Append(statusIcon).Append(" Test Run Summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            builder.Append("| Total | Passed | Failed | Skipped | Flaky | Duration |\n");
            builder.Append("|---:|---:|---:|---:|---:|---:|\n");
            builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(passed.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(failed.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(skipped.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(flaky.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatDuration(totalDuration)).Append(" |\n\n");
        }

        if ((sections & GitHubActionsStepSummarySections.Coverage) != 0)
        {
            CiCoverageSummary.AppendMarkdown(builder, coverage, headingLevel: 3);
        }

        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            // Surface a non-test-result failure that this reporter can observe once the session has finished
            // (zero tests, --minimum-expected-tests, --maximum-failed-tests, test-adapter session failure) as a
            // GitHub alert callout. Plain pass / at-least-one-failed outcomes are already conveyed by the totals
            // table and the failures section, so no callout is added for them.
            if (!GitHubActionsExitCode.IsTestResultOutcome(exitCode))
            {
                string calloutText = string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.ExitCodeCallout,
                    exitCode.ToString(CultureInfo.InvariantCulture),
                    GitHubActionsExitCode.GetName(exitCode),
                    GitHubActionsExitCode.GetReason(exitCode));
                builder.Append("> [!WARNING]\n> ").Append(EscapeInlineCode(calloutText)).Append("\n\n");
            }

            if (failures.Count > 0)
            {
                GitHubActionsFailureDetails.AppendFailuresSection(
                    builder,
                    "###",
                    [.. failures.Select(static failure => new GitHubActionsFailureEntry(
                        failure.FullyQualifiedName,
                        failure.Duration,
                        failure.Failure?.Message,
                        failure.Failure?.ExceptionType,
                        failure.Failure?.StackTrace,
                        failure.Failure?.FilePath,
                        failure.Failure?.LineNumber ?? 0))],
                    failed,
                    includeFailureDetails,
                    budget);
            }

            AppendFlakyTests(builder, flakyTests.Select(static test => test.FullyQualifiedName), flaky);
        }

        if ((sections & GitHubActionsStepSummarySections.SlowTests) != 0)
        {
            IEnumerable<TestRecord> slowest = records
                .Where(static r => r.Duration > TimeSpan.Zero)
                .OrderByDescending(static r => r.Duration)
                .Take(MaxSlowestTests);

            bool slowestEmitted = false;
            foreach (TestRecord record in slowest)
            {
                if (!slowestEmitted)
                {
                    builder.Append("### ⏱ Slowest tests\n\n");
                    slowestEmitted = true;
                }

                builder.Append("- `").Append(EscapeInlineCode(record.FullyQualifiedName)).Append("` — ").Append(FormatDuration(record.Duration)).Append('\n');
            }

            if (slowestEmitted)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a single-line verdict for this test project. Used only when the shared summary file is already
    /// near GitHub's cap, where the few kilobytes of a normal section would be the thing that overflows it.
    /// </summary>
    internal static /* for testing */ string BuildMinimalMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        foreach (TestRecord record in records)
        {
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        return BuildCondensedLine(
            assemblyName,
            targetFrameworkMoniker,
            records.Count,
            passed,
            failed,
            skipped,
            failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode));
    }

    /// <summary>
    /// Renders one module's section, returning the number of its listed failures whose diagnostics did not fit
    /// the shared budget.
    /// </summary>
    private static int AppendModuleMarkdown(
        StringBuilder builder,
        CiRunSummaryModule module,
        int headingLevel,
        GitHubActionsStepSummarySections sections,
        bool includeFailureDetails,
        SummaryBudget budget)
    {
        string heading = new('#', headingLevel);
        bool runFailed = module.FailedTests > 0 || GitHubActionsExitCode.IndicatesFailure(module.ExitCode);
        builder.Append(heading).Append(' ').Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeInlineCode(module.AssemblyName)).Append("\n\n");
        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            builder.Append("| Total | Passed | Failed | Skipped | Flaky | Test duration |\n");
            builder.Append("|---:|---:|---:|---:|---:|---:|\n");
            builder.Append("| ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.FlakyTests.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n\n");
        }

        if ((sections & GitHubActionsStepSummarySections.Coverage) != 0)
        {
            CiCoverageSummary.AppendMarkdown(builder, module.Coverage, headingLevel + 1);
        }

        int omittedDetails = 0;
        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            if (!GitHubActionsExitCode.IsTestResultOutcome(module.ExitCode))
            {
                builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("` (")
                    .Append(EscapeInlineCode(GitHubActionsExitCode.GetName(module.ExitCode))).Append(")\n\n");
            }

            if (module.Failures.Length > 0)
            {
                omittedDetails = GitHubActionsFailureDetails.AppendFailuresSection(
                    builder,
                    heading + "#",
                    [.. module.Failures.Select(static failure => new GitHubActionsFailureEntry(
                        failure.FullyQualifiedName,
                        TimeSpan.FromTicks(failure.DurationTicks),
                        failure.ErrorMessage,
                        failure.ErrorType,
                        failure.StackTrace,
                        failure.FilePath,
                        failure.LineNumber ?? 0))],
                    module.FailedTests,
                    includeFailureDetails,
                    budget);
            }

            AppendFlakyTests(
                builder,
                module.FlakyTests.Select(static test => test.FullyQualifiedName),
                module.FlakyTests.Length,
                headingLevel + 1);
        }

        if ((sections & GitHubActionsStepSummarySections.SlowTests) != 0
            && module.SlowestTests.Length > 0)
        {
            builder.Append(heading).Append("# ⏱ Slowest tests\n\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("- `").Append(EscapeInlineCode(test.FullyQualifiedName)).Append("` — ")
                    .Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append('\n');
            }

            builder.Append('\n');
        }

        return omittedDetails;
    }

    private static void AppendFlakyTests(
        StringBuilder builder,
        IEnumerable<string> testNames,
        int count,
        int headingLevel = 3)
    {
        if (count == 0)
        {
            return;
        }

        builder.Append(new string('#', headingLevel)).Append(" ⚠️ Flaky tests (")
            .Append(count.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
        foreach (string testName in testNames.Take(MaxListedFlakyTests))
        {
            builder.Append("- `").Append(EscapeInlineCode(testName)).Append("`\n");
        }

        if (count > MaxListedFlakyTests)
        {
            builder.Append("- … and ")
                .Append((count - MaxListedFlakyTests).ToString(CultureInfo.InvariantCulture))
                .Append(" more\n");
        }

        builder.Append('\n');
    }
}

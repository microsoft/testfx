// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    private const int MaxListedFlakyTests = 20;

    /// <summary>
    /// Marks a full test project section, so the truncation note can state how many test projects got their
    /// results into the summary without depending on the localized heading text.
    /// </summary>
    internal const string ProjectSectionMarker = "<!-- microsoft-testing-platform:github:project-section -->";

    /// <summary>
    /// Marks the start of the truncation note in the shared summary file so a later test project can find the
    /// note it (or a sibling) already wrote and replace it, rather than appending a second copy.
    /// </summary>
    internal const string TruncationNoticeMarker = "<!-- microsoft-testing-platform:github:summary-truncated -->";

    /// <summary>
    /// Marks the end of the truncation note. The note carries a project count, so its length varies and it
    /// cannot be located by its text alone.
    /// </summary>
    internal const string TruncationNoticeEndMarker = "<!-- /microsoft-testing-platform:github:summary-truncated -->";

    /// <summary>
    /// The note that says expanded diagnostics were dropped. The weaker of the two: every project and every
    /// failing test is still named.
    /// </summary>
    internal const int DetailsOmittedNoticeStrength = 1;

    /// <summary>
    /// The note that says whole test project sections were reduced to a one-line verdict, or dropped. Stronger
    /// than <see cref="DetailsOmittedNoticeStrength"/>, because results are missing rather than merely shortened.
    /// </summary>
    internal const int SectionsRemovedNoticeStrength = 2;

    /// <summary>
    /// Records how much the summary gave up, so a later writer can tell whether the note already in the file
    /// still describes the worst loss.
    /// </summary>
    /// <remarks>
    /// The two notices share a start marker so a summary can never carry two contradictory warnings, but they do
    /// not describe the same loss. Without this token the first note written would win outright: an aggregate
    /// step that only dropped diagnostics would suppress a later project's "whole sections were removed" note,
    /// and the summary would never say that results are missing.
    /// </remarks>
    internal static string BuildNoticeStrengthToken(int strength)
        => $"<!-- microsoft-testing-platform:github:truncation-strength:{strength.ToString(CultureInfo.InvariantCulture)} -->";

    /// <summary>
    /// What a combined <c>dotnet test</c> rendering produced, and what it had to give up to fit GitHub's cap.
    /// </summary>
    internal readonly struct AggregateRenderResult
    {
        internal AggregateRenderResult(string markdown, int modulesWithOmittedDetails, int condensedModules, int unlistedModules)
        {
            Markdown = markdown;
            ModulesWithOmittedDetails = modulesWithOmittedDetails;
            CondensedModules = condensedModules;
            UnlistedModules = unlistedModules;
        }

        /// <summary>Gets the rendered summary.</summary>
        internal string Markdown { get; }

        /// <summary>Gets the number of test projects that kept their section but lost their expanded diagnostics.</summary>
        internal int ModulesWithOmittedDetails { get; }

        /// <summary>Gets the number of test projects reduced to a one-line verdict.</summary>
        internal int CondensedModules { get; }

        /// <summary>Gets the number of test projects that did not fit at all, reported only as a count.</summary>
        internal int UnlistedModules { get; }

        /// <summary>
        /// Returns how many of <paramref name="totalModules"/> got a full section, which is what the top-of-file
        /// note quotes — so it excludes both the condensed and the unlisted.
        /// </summary>
        internal int FullyReportedModules(int totalModules)
            => totalModules - CondensedModules - UnlistedModules;
    }

    internal static /* for testing */ string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int exitCode,
        GitHubActionsStepSummarySections sections = GitHubActionsStepSummarySections.All)
        => BuildMarkdown(records, assemblyName, targetFrameworkMoniker, exitCode, new CiCoverageSummaryData(), sections);

    internal static /* for testing */ string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int exitCode,
        bool includeFailureDetails,
        SummaryBudget? budget = null)
        => BuildMarkdownCore(
            records,
            assemblyName,
            targetFrameworkMoniker,
            exitCode,
            new CiCoverageSummaryData(),
            GitHubActionsStepSummarySections.All,
            includeFailureDetails,
            budget ?? SummaryBudget.ForProject(0));

    internal static /* for testing */ string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int exitCode,
        CiCoverageSummaryData coverage,
        GitHubActionsStepSummarySections sections,
        bool includeFailureDetails,
        SummaryBudget? budget = null)
        => BuildMarkdownCore(records, assemblyName, targetFrameworkMoniker, exitCode, coverage, sections, includeFailureDetails, budget ?? SummaryBudget.ForProject(0));

    private static string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int exitCode,
        CiCoverageSummaryData coverage,
        GitHubActionsStepSummarySections sections)
        => BuildMarkdownCore(records, assemblyName, targetFrameworkMoniker, exitCode, coverage, sections, includeFailureDetails: true, SummaryBudget.ForProject(0));

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

    internal static AggregateRenderResult BuildAggregateMarkdown(
        CiRunSummaryAggregate aggregate,
        bool includeFailureDetails = true,
        bool condenseAllModules = false,
        long alreadyWrittenBytes = 0)
    {
        GitHubActionsStepSummarySections sections = GitHubActionsStepSummarySectionsParser.GetAggregateSections(aggregate.Modules);
        bool failed = aggregate.ExitCode is int exitCode
            ? GitHubActionsExitCode.IndicatesFailure(exitCode)
            : aggregate.FailedTests > 0;
        string statusIcon = failed
            ? "❌"
            : aggregate.IsPartial || !aggregate.HasAuthoritativeRunSummary
                ? "⚠️"
                : "✅";
        string duration = aggregate.Duration is { } value ? FormatDuration(value) : "Unavailable";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Overall Test Run Summary\n\n");
        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            builder.Append("| Total | Passed | Failed | Skipped | Flaky | Duration |\n");
            builder.Append("|---:|---:|---:|---:|---:|---:|\n");
            builder.Append("| ").Append(aggregate.TotalTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(aggregate.PassedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(aggregate.FailedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(aggregate.SkippedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(aggregate.FlakyTests.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(duration).Append(" |\n\n");
        }

        if ((sections & GitHubActionsStepSummarySections.Coverage) != 0)
        {
            CiCoverageSummary.AppendMarkdown(builder, aggregate.Coverage, headingLevel: 3);
        }

        if ((sections & GitHubActionsStepSummarySections.TestResults) != 0)
        {
            if (aggregate.IsPartial)
            {
                builder.Append("> [!WARNING]\n> This summary is partial because the test run was truncated.\n\n");
            }
            else if (!aggregate.HasAuthoritativeRunSummary)
            {
                builder.Append("> [!NOTE]\n> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
            }

            if (aggregate.ExitCode is int authoritativeExitCode
                && !GitHubActionsExitCode.IsTestResultOutcome(authoritativeExitCode))
            {
                string calloutText = string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.ExitCodeCallout,
                    authoritativeExitCode.ToString(CultureInfo.InvariantCulture),
                    GitHubActionsExitCode.GetName(authoritativeExitCode),
                    GitHubActionsExitCode.GetReason(authoritativeExitCode));
                builder.Append("> [!WARNING]\n> ").Append(EscapeInlineCode(calloutText)).Append("\n\n");
            }

            AppendFlakyTests(
                builder,
                aggregate.FlakyTests.Select(static test => test.FullyQualifiedName),
                aggregate.FlakyTests.Count);
        }

        // One shared budget, byte-denominated, drives every decision below: which shape each module is rendered
        // in, and how much expanded detail it may spend. Only the newly appended chars are measured each round,
        // so the loop stays linear.
        int moduleCount = aggregate.Modules.Count;
        int measuredChars = builder.Length;
        var budget = SummaryBudget.ForAggregate(alreadyWrittenBytes + Encoding.UTF8.GetByteCount(builder.ToString()), moduleCount);
        int modulesWithOmittedDetails = 0;
        int condensedModules = 0;
        int listedModules = 0;

        foreach (CiRunSummaryModule module in aggregate.Modules)
        {
            if (builder.Length > measuredChars)
            {
                budget.Consume(Encoding.UTF8.GetByteCount(builder.ToString(measuredChars, builder.Length - measuredChars)));
                measuredChars = builder.Length;
            }

            // The stop-listing bound applies even in the condensed fallback: it is the rendering the
            // post-processor reaches *after* the full one was refused for size, so if it too rendered a line per
            // project without bound, a large enough run would have both refused and contribute nothing at all.
            SummaryStage stage = budget.Stage;
            if (condenseAllModules && stage != SummaryStage.Unlisted)
            {
                stage = SummaryStage.Condensed;
            }

            // Condensing bounds what a module costs, not how many of them there are: a run with thousands of test
            // projects overruns the cap on verdict lines alone. Past this point the listing stops entirely and the
            // remainder is reported as a count, which is the only rendering whose size does not grow with the run.
            if (stage == SummaryStage.Unlisted)
            {
                break;
            }

            // Dividing the detail budget bounds the diagnostics, but every module still costs a heading, a totals
            // table and its failure lines whether or not any budget is left. A run with enough test projects
            // therefore overruns GitHub's cap on that overhead alone, and an oversized summary is discarded in
            // full — so past this point a module reports only its verdict, exactly as the per-project path does.
            if (stage == SummaryStage.Condensed)
            {
                AppendCondensedModuleLine(builder, module);
                condensedModules++;
                listedModules++;
                continue;
            }

            bool needsDiscriminator = HasDuplicateModuleIdentity(aggregate.Modules, module);

            // A full module is a test project whose results are all here, which is exactly what the marker counts.
            // Emitting it lets a direct per-project writer sharing this file count these modules too, so a note it
            // writes later states how many projects the whole summary reports rather than only its own.
            builder.Append(ProjectSectionMarker).Append('\n');
            builder.Append("<details>\n<summary>")
                .Append(HtmlEncode(module.AssemblyName))
                .Append(" (").Append(HtmlEncode(module.TargetFramework)).Append(", ")
                .Append(HtmlEncode(module.Architecture));
            if (needsDiscriminator)
            {
                builder.Append(", attempt ").Append(module.AttemptNumber.ToString(CultureInfo.InvariantCulture))
                    .Append(", session ").Append(HtmlEncode(module.SessionUid));
            }

            builder.Append(")</summary>\n\n");

            // Hand this module its share, keeping whatever earlier modules left unspent.
            budget.GrantModuleShare(moduleCount - listedModules);
            if (AppendModuleMarkdown(builder, module, headingLevel: 3, sections, includeFailureDetails, budget) > 0)
            {
                modulesWithOmittedDetails++;
            }

            builder.Append("</details>\n\n");
            listedModules++;
        }

        int unlistedModules = moduleCount - listedModules;
        if (unlistedModules > 0)
        {
            builder.Append("> [!WARNING]\n> ")
                .Append(EscapeInlineCode(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.ModulesNotListed,
                    unlistedModules.ToString(CultureInfo.InvariantCulture))))
                .Append("\n\n");
        }

        return new AggregateRenderResult(builder.ToString(), modulesWithOmittedDetails, condensedModules, unlistedModules);
    }

    /// <summary>
    /// Renders the one-line verdict a test project is reduced to when the shared summary file is near GitHub's
    /// cap. Both writing modes reach this: the aggregate path condenses a module, and the per-project path
    /// condenses itself, so they render through one method rather than two that must be kept in step.
    /// </summary>
    private static string BuildCondensedLine(string assemblyName, string targetFramework, long total, long passed, long failed, long skipped, bool runFailed)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0} `{1}` ({2}): {3} total, {4} passed, {5} failed, {6} skipped — {7}\n\n",
            runFailed ? "❌" : "✅",
            EscapeInlineCode(assemblyName),
            EscapeInlineCode(targetFramework),
            total.ToString(CultureInfo.InvariantCulture),
            passed.ToString(CultureInfo.InvariantCulture),
            failed.ToString(CultureInfo.InvariantCulture),
            skipped.ToString(CultureInfo.InvariantCulture),
            GitHubActionsResources.SummaryCondensed);

    /// <summary>
    /// Renders one module as a single-line verdict, for when the combined summary has grown too large to give it
    /// a section of its own.
    /// </summary>
    private static void AppendCondensedModuleLine(StringBuilder builder, CiRunSummaryModule module)
        => builder.Append(BuildCondensedLine(
            module.AssemblyName,
            module.TargetFramework,
            module.TotalTests,
            module.PassedTests,
            module.FailedTests,
            module.SkippedTests,
            module.FailedTests > 0 || GitHubActionsExitCode.IndicatesFailure(module.ExitCode)));

    /// <summary>
    /// Renders the top-of-file warning for a combined <c>dotnet test</c> summary whose modules lost their expanded
    /// diagnostics.
    /// </summary>
    /// <remarks>
    /// This shares its marker with the per-project warning deliberately. Only one of the two writing modes runs in
    /// a given test process, but a workflow is free to mix them across steps, and two warnings in one summary —
    /// each describing a different kind of loss — would be worse than either alone. The shared marker means
    /// whichever is written first is the only one, and neither writer adds a second.
    /// </remarks>
    internal static /* for testing */ string BuildAggregateTruncationNotice(int modulesWithOmittedDetails, int totalModules)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.ModuleDetailsOmitted,
            modulesWithOmittedDetails.ToString(CultureInfo.InvariantCulture),
            totalModules.ToString(CultureInfo.InvariantCulture));

        return $"{TruncationNoticeMarker}\n{BuildNoticeStrengthToken(DetailsOmittedNoticeStrength)}\n> [!WARNING]\n> {message}\n{TruncationNoticeEndMarker}\n\n";
    }

    /// <summary>
    /// Renders the note that tells the reader the summary was shortened on purpose, and how many test projects
    /// did get their full results in before that happened.
    /// </summary>
    /// <param name="reportedProjectCount">
    /// The number of test projects whose full results are in the summary, counted from the file itself. Each
    /// test project runs in its own process and cannot know how many siblings will follow, so this is whatever
    /// is visible when the note is written.
    /// </param>
    /// <remarks>
    /// Without this note the report is silently incomplete: the reader sees one-line verdicts with no indication
    /// that anything was left out. The note is deliberately small — it is written while the file is already well
    /// into GitHub's cap, so it has to cost a few hundred bytes, not a few thousand.
    /// </remarks>
    internal static /* for testing */ string BuildTruncationNotice(int reportedProjectCount)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.SummaryTruncatedNotice,
            reportedProjectCount.ToString(CultureInfo.InvariantCulture));

        return $"{TruncationNoticeMarker}\n{BuildNoticeStrengthToken(SectionsRemovedNoticeStrength)}\n> [!WARNING]\n> {message}\n{TruncationNoticeEndMarker}\n\n";
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

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");

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

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value)
            ? value
            : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

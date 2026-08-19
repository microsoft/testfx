// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode, bool includeFailureDetails = true, int detailsBudget = GitHubActionsFailureDetails.MaxTotalDetailsLength)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failures = new List<TestRecord>();

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
        }

        // Reflect the process verdict, not just the failed-test count: a run can end in failure with zero failed
        // tests (e.g. zero tests discovered or a --minimum-expected-tests violation), which must not show ✅.
        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        string statusIcon = runFailed ? "❌" : "✅";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Test Run Summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(passed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(failed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(skipped.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(totalDuration)).Append(" |\n\n");

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
            int remainingBudget = detailsBudget;
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
                ref remainingBudget);
        }

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

        return builder.ToString();
    }

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate, bool includeFailureDetails = true)
    {
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
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(aggregate.TotalTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.PassedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.FailedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.SkippedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(duration).Append(" |\n\n");

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

        // The 1 MiB cap applies to the whole file, so the budget is shared across every module rather than
        // granted per module. Reserve each module's non-detail overhead (heading, tables, failure lines) up
        // front so the rendered file lands near MaxSummaryLength rather than that much detail *plus* overhead,
        // then divide the rest so an early module with many large failures cannot starve the later ones.
        int moduleCount = Math.Max(1, aggregate.Modules.Count);
        int overheadReserve = moduleCount * GitHubActionsFailureDetails.PerProjectOverheadReserve;
        int detailsBudget = Math.Max(0, GitHubActionsFailureDetails.MaxSummaryLength - overheadReserve);
        int perModuleBudget = detailsBudget / moduleCount;
        int remainingBudget = 0;
        int modulesWithOmittedDetails = 0;

        foreach (CiRunSummaryModule module in aggregate.Modules)
        {
            bool needsDiscriminator = HasDuplicateModuleIdentity(aggregate.Modules, module);
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

            // Top up with this module's share, keeping whatever earlier modules left unspent.
            remainingBudget += perModuleBudget;
            if (AppendModuleMarkdown(builder, module, headingLevel: 3, includeFailureDetails, ref remainingBudget) > 0)
            {
                modulesWithOmittedDetails++;
            }

            builder.Append("</details>\n\n");
        }

        // Surface budget exhaustion at the file level too. A per-module note is easy to miss when it is buried
        // inside one of dozens of collapsed module sections, and the reader needs to know the summary as a whole
        // is not showing everything it collected.
        if (modulesWithOmittedDetails > 0)
        {
            builder.Append("> [!NOTE]\n> ")
                .Append(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.ModuleDetailsOmitted,
                    modulesWithOmittedDetails.ToString(CultureInfo.InvariantCulture),
                    aggregate.Modules.Count.ToString(CultureInfo.InvariantCulture)))
                .Append("\n\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Marks the closing truncation note in the shared summary file so a later test project can find the note
    /// it (or a sibling) already wrote instead of appending a second copy.
    /// </summary>
    internal const string TruncationNoticeMarker = "<!-- microsoft-testing-platform:github:summary-truncated -->";

    /// <summary>
    /// Renders the closing note that tells the reader the summary stops short on purpose: GitHub discards an
    /// oversized job summary in full, so this extension condenses and then stops rather than losing everything.
    /// </summary>
    /// <remarks>
    /// Without this note the summary simply ends, which is indistinguishable from the reporter crashing or the
    /// run being cut short. The note is deliberately small — it is written while the file is already close to
    /// the cap, so it has to cost a few hundred bytes, not a few thousand.
    /// </remarks>
    internal static /* for testing */ string BuildTruncationNotice()
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.SummaryTruncatedNotice,
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture));

        return $"{TruncationNoticeMarker}\n> [!WARNING]\n> {message}\n\n";
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

        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} `{1}` ({2}): {3} total, {4} passed, {5} failed, {6} skipped — {7}\n\n",
            runFailed ? "❌" : "✅",
            EscapeInlineCode(assemblyName),
            EscapeInlineCode(targetFrameworkMoniker),
            records.Count.ToString(CultureInfo.InvariantCulture),
            passed.ToString(CultureInfo.InvariantCulture),
            failed.ToString(CultureInfo.InvariantCulture),
            skipped.ToString(CultureInfo.InvariantCulture),
            GitHubActionsResources.SummaryCondensed);
    }

    /// <summary>
    /// Renders one module's section, returning the number of its listed failures whose diagnostics did not fit
    /// the shared budget.
    /// </summary>
    private static int AppendModuleMarkdown(StringBuilder builder, CiRunSummaryModule module, int headingLevel, bool includeFailureDetails, ref int remainingBudget)
    {
        string heading = new('#', headingLevel);
        bool runFailed = module.FailedTests > 0 || GitHubActionsExitCode.IndicatesFailure(module.ExitCode);
        builder.Append(heading).Append(' ').Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeInlineCode(module.AssemblyName)).Append("\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Test duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n\n");

        if (!GitHubActionsExitCode.IsTestResultOutcome(module.ExitCode))
        {
            builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("` (")
                .Append(EscapeInlineCode(GitHubActionsExitCode.GetName(module.ExitCode))).Append(")\n\n");
        }

        int omittedDetails = 0;
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
                ref remainingBudget);
        }

        if (module.SlowestTests.Length > 0)
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

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode)
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
            builder.Append("### ❌ Failures (").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
            foreach (TestRecord failure in failures)
            {
                builder.Append("- `").Append(EscapeInlineCode(failure.FullyQualifiedName)).Append("`\n");
            }

            builder.Append('\n');
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

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate)
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
            AppendModuleMarkdown(builder, module, headingLevel: 3);
            builder.Append("</details>\n\n");
        }

        return builder.ToString();
    }

    private static void AppendModuleMarkdown(StringBuilder builder, CiRunSummaryModule module, int headingLevel)
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

        if (module.Failures.Length > 0)
        {
            builder.Append(heading).Append("# ❌ Failures (").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
            foreach (CiRunSummaryTest failure in module.Failures)
            {
                builder.Append("- `").Append(EscapeInlineCode(failure.FullyQualifiedName)).Append("`\n");
            }

            builder.Append('\n');
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

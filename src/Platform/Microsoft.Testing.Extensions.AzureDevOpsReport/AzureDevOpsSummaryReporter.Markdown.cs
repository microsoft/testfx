// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    internal static /* for testing */ string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker)
        => BuildMarkdown(records, assemblyName, targetFrameworkMoniker, new CiCoverageSummaryData());

    private static string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        CiCoverageSummaryData coverage)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failingByClass = new Dictionary<string, int>(StringComparer.Ordinal);
        var failingFqns = new List<string>();

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
                    if (failingFqns.Count < MaxFirstFailingFqns)
                    {
                        failingFqns.Add(record.FullyQualifiedName);
                    }

                    string className = GetClassName(record.FullyQualifiedName);
                    failingByClass[className] = failingByClass.TryGetValue(className, out int count) ? count + 1 : 1;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        var builder = new StringBuilder();
        builder.Append("# Test summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(total.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Total duration | ").Append(FormatDuration(totalDuration)).Append(" |\n");
        builder.Append('\n');
        CiCoverageSummary.AppendMarkdown(builder, coverage, headingLevel: 2);

        if (failingByClass.Count > 0)
        {
            builder.Append("## Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (KeyValuePair<string, int> pair in failingByClass.OrderByDescending(static p => p.Value).ThenBy(static p => p.Key, StringComparer.Ordinal).Take(MaxTopFailingClasses))
            {
                builder.Append("| ").Append(EscapeCell(pair.Key)).Append(" | ").Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        if (failingFqns.Count > 0)
        {
            builder.Append("## First failing tests\n\n");
            foreach (string fqn in failingFqns)
            {
                builder.Append("- ").Append(EscapeCell(fqn)).Append('\n');
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
                builder.Append("## Slowest tests\n\n");
                builder.Append("| Test | Duration |\n");
                builder.Append("| --- | ---: |\n");
                slowestEmitted = true;
            }

            builder.Append("| ").Append(EscapeCell(record.DisplayName)).Append(" | ").Append(FormatDuration(record.Duration)).Append(" |\n");
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate)
    {
        var builder = new StringBuilder();
        builder.Append("# Overall test summary\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(aggregate.TotalTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(aggregate.PassedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(aggregate.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(aggregate.SkippedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Duration | ")
            .Append(aggregate.Duration is { } duration ? FormatDuration(duration) : "Unavailable")
            .Append(" |\n");
        if (aggregate.ExitCode is int exitCode)
        {
            builder.Append("| Exit code | ").Append(exitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        }

        builder.Append('\n');
        CiCoverageSummary.AppendMarkdown(builder, aggregate.Coverage, headingLevel: 2);
        if (aggregate.IsPartial)
        {
            builder.Append("> **Partial summary:** the test run was truncated.\n\n");
        }
        else if (!aggregate.HasAuthoritativeRunSummary)
        {
            builder.Append("> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
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
            AppendModuleMarkdown(builder, module);
            builder.Append("</details>\n\n");
        }

        return builder.ToString();
    }

    private static void AppendModuleMarkdown(StringBuilder builder, CiRunSummaryModule module)
    {
        builder.Append("## ").Append(EscapeCell(module.AssemblyName)).Append("\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Test duration | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n");
        builder.Append("| Module exit code | ").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n\n");
        CiCoverageSummary.AppendMarkdown(builder, module.Coverage, headingLevel: 3);

        if (module.TopFailingClasses.Length > 0)
        {
            builder.Append("### Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryFailingClass item in module.TopFailingClasses)
            {
                builder.Append("| ").Append(EscapeCell(item.ClassName)).Append(" | ")
                    .Append(item.FailureCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        if (module.Failures.Length > 0)
        {
            builder.Append("### First failing tests\n\n");
            foreach (CiRunSummaryTest failure in module.Failures.Take(MaxFirstFailingFqns))
            {
                builder.Append("- ").Append(EscapeCell(failure.FullyQualifiedName)).Append('\n');
            }

            builder.Append('\n');
        }

        if (module.SlowestTests.Length > 0)
        {
            builder.Append("### Slowest tests\n\n");
            builder.Append("| Test | Duration |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("| ").Append(EscapeCell(test.DisplayName)).Append(" | ")
                    .Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append(" |\n");
            }

            builder.Append('\n');
        }
    }

    private static string GetClassName(string fullyQualifiedName)
    {
        if (RoslynString.IsNullOrEmpty(fullyQualifiedName))
        {
            return "(unknown)";
        }

        int lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot <= 0 ? "(unknown)" : fullyQualifiedName.Substring(0, lastDot);
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0:D2}:{1:D2}", "{0}:{1:D2}:{2:D2}");

    private static string EscapeCell(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '|':
                    sb.Append("\\|");
                    break;
                case '`':
                    sb.Append("\\`");
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append("<br>");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

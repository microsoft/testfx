// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    private const int MaxHistoricalTests = 20;
    private const int MaxHistoricalDurations = 10;
    private const int MaxDependencies = 50;
    private const int MaxAggregateFailureDetails = 20;

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
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            assemblyName,
            targetFrameworkMoniker,
            "unknown",
            executionId: null,
            sessionUid: "direct",
            attemptNumber: 1,
            exitCode: 0,
            coverage: coverage);
        return BuildMarkdown(module);
    }

    private static string BuildMarkdown(CiRunSummaryModule module)
    {
        var builder = new StringBuilder();
        builder.Append("# Test summary — ").Append(EscapeCell(module.AssemblyName)).Append(" (")
            .Append(EscapeCell(module.TargetFramework)).Append(")\n\n");
        _ = AppendModuleMarkdown(
            builder,
            module,
            headingLevel: 2,
            includeHeading: false,
            includeInsights: true,
            failureDetailLimit: MaxFirstFailingFqns);
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
        builder.Append("| Flaky | ").Append(aggregate.FlakyTests.Count.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Pass rate | ").Append(FormatRate(aggregate.PassedTests, aggregate.TotalTests)).Append(" |\n");
        builder.Append("| Duration | ")
            .Append(aggregate.Duration is { } duration ? FormatDuration(duration) : "Unavailable")
            .Append(" |\n");
        if (aggregate.ExitCode is int exitCode)
        {
            builder.Append("| Exit code | ").Append(exitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        }

        builder.Append('\n');
        CiCoverageSummary.AppendMarkdown(builder, aggregate.Coverage, headingLevel: 2);
        AppendHistoryMarkdown(builder, aggregate.Modules, headingLevel: 2);
        AppendDependenciesMarkdown(builder, aggregate.Modules, headingLevel: 2);
        if (aggregate.IsPartial)
        {
            builder.Append("> **Partial summary:** the test run was truncated.\n\n");
        }
        else if (!aggregate.HasAuthoritativeRunSummary)
        {
            builder.Append("> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
        }

        int remainingFailureDetails = MaxAggregateFailureDetails;
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
            remainingFailureDetails -= AppendModuleMarkdown(
                builder,
                module,
                headingLevel: 2,
                includeHeading: true,
                includeInsights: false,
                failureDetailLimit: remainingFailureDetails);
            builder.Append("</details>\n\n");
        }

        return builder.ToString();
    }

    private static int AppendModuleMarkdown(
        StringBuilder builder,
        CiRunSummaryModule module,
        int headingLevel,
        bool includeHeading,
        bool includeInsights,
        int failureDetailLimit)
    {
        string heading = new('#', headingLevel);
        if (includeHeading)
        {
            builder.Append(heading).Append(' ').Append(EscapeCell(module.AssemblyName)).Append("\n\n");
        }

        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Flaky | ").Append(module.FlakyTests.Length.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Pass rate | ").Append(FormatRate(module.PassedTests, module.TotalTests)).Append(" |\n");
        builder.Append(includeHeading ? "| Test duration | " : "| Total duration | ")
            .Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n");
        if (includeHeading)
        {
            builder.Append("| Module exit code | ").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        }

        builder.Append('\n');
        CiCoverageSummary.AppendMarkdown(builder, module.Coverage, headingLevel + 1);

        if (module.TopFailingClasses.Length > 0)
        {
            builder.Append(heading).Append("# Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryFailingClass item in module.TopFailingClasses)
            {
                builder.Append("| ").Append(EscapeCell(item.ClassName)).Append(" | ")
                    .Append(item.FailureCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        CiRunSummaryTest[] detailedFailures =
        [
            .. module.Failures
                .Where(HasFailureDetails)
                .Take(failureDetailLimit),
        ];
        int renderedFailureDetails = detailedFailures.Length;
        if (renderedFailureDetails > 0)
        {
            builder.Append(heading).Append("# Failure details\n\n");
            foreach (CiRunSummaryTest failure in detailedFailures)
            {
                builder.Append("<details>\n<summary>")
                    .Append(HtmlEncode(failure.FullyQualifiedName))
                    .Append(" — ")
                    .Append(FormatDuration(TimeSpan.FromTicks(failure.DurationTicks)))
                    .Append("</summary>\n\n");
                if (!RoslynString.IsNullOrWhiteSpace(failure.ErrorType))
                {
                    builder.Append("**Exception:** `").Append(EscapeInlineCode(failure.ErrorType!)).Append("`\n\n");
                }

                if (!RoslynString.IsNullOrWhiteSpace(failure.ErrorMessage))
                {
                    builder.Append("**Message:**\n\n");
                    AppendBlockQuote(builder, failure.ErrorMessage!);
                }

                builder.Append("</details>\n\n");
            }
        }

        if (module.Failures.Length > renderedFailureDetails)
        {
            builder.Append(heading).Append("# Additional failing tests\n\n");
            foreach (CiRunSummaryTest failure in module.Failures
                .Where(failure => !detailedFailures.Contains(failure))
                .Take(MaxFirstFailingFqns))
            {
                builder.Append("- `").Append(EscapeInlineCode(failure.FullyQualifiedName)).Append("`\n");
            }

            builder.Append('\n');
        }

        if (module.FlakyTests.Length > 0)
        {
            builder.Append(heading).Append("# Flaky tests\n\n");
            foreach (CiRunSummaryTest flaky in module.FlakyTests.Take(MaxHistoricalTests))
            {
                builder.Append("- `").Append(EscapeInlineCode(flaky.FullyQualifiedName)).Append("`\n");
            }

            if (module.FlakyTests.Length > MaxHistoricalTests)
            {
                builder.Append("- … and ")
                    .Append((module.FlakyTests.Length - MaxHistoricalTests).ToString(CultureInfo.InvariantCulture))
                    .Append(" more\n");
            }

            builder.Append('\n');
        }

        if (includeInsights)
        {
            AppendHistoryMarkdown(builder, [module], headingLevel + 1);
            AppendDependenciesMarkdown(builder, [module], headingLevel + 1);
        }

        if (module.SlowestTests.Length > 0)
        {
            builder.Append(heading).Append("# Slowest tests\n\n");
            builder.Append("| Test | Duration |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("| ").Append(EscapeCell(test.DisplayName)).Append(" | ")
                    .Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append(" |\n");
            }

            builder.Append('\n');
        }

        return renderedFailureDetails;
    }

    private static bool HasFailureDetails(CiRunSummaryTest failure)
        => !RoslynString.IsNullOrWhiteSpace(failure.ErrorMessage)
            || !RoslynString.IsNullOrWhiteSpace(failure.ErrorType);

    private static void AppendHistoryMarkdown(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        int headingLevel)
    {
        (CiRunSummaryModule Module, CiRunSummaryHistoryTest Test)[] flakyHistory =
        [
            .. modules
                .SelectMany(module => module.HistoryTests.Select(test => (Module: module, Test: test)))
                .Where(static item => item.Test.HistoricalPassCount > 0 && item.Test.HistoricalFailCount > 0)
                .OrderByDescending(static item => GetHistoricalFailureRate(item.Test))
                .ThenByDescending(static item => item.Test.HistoricalFailCount)
                .ThenBy(static item => item.Test.FullyQualifiedName, StringComparer.Ordinal)
                .Take(MaxHistoricalTests),
        ];
        (CiRunSummaryModule Module, CiRunSummaryHistoryTest Test)[] durationHistory =
        [
            .. modules
                .SelectMany(module => module.HistoryTests.Select(test => (Module: module, Test: test)))
                .Where(static item => item.Test.DurationSampleCount > 0)
                .OrderByDescending(static item => GetDurationRatio(item.Test))
                .ThenByDescending(static item => item.Test.DurationTicks)
                .ThenBy(static item => item.Test.FullyQualifiedName, StringComparer.Ordinal)
                .Take(MaxHistoricalDurations),
        ];

        string heading = new('#', headingLevel);
        if (flakyHistory.Length > 0)
        {
            builder.Append(heading).Append(" Flakiness history\n\n");
            builder.Append("| Test | Current | Historical results | Passed | Failed | Failure rate |\n");
            builder.Append("| --- | --- | ---: | ---: | ---: | ---: |\n");
            foreach ((CiRunSummaryModule module, CiRunSummaryHistoryTest test) in flakyHistory)
            {
                int total = test.HistoricalPassCount + test.HistoricalFailCount;
                builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules.Count, module, test.FullyQualifiedName)))
                    .Append(" | ").Append(EscapeCell(test.Outcome))
                    .Append(" | ").Append(total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(test.HistoricalPassCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(test.HistoricalFailCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append((GetHistoricalFailureRate(test) * 100d).ToString("F1", CultureInfo.InvariantCulture)).Append("%");
                if (test.HistoryWindowInDays > 0)
                {
                    builder.Append(" (").Append(test.HistoryWindowInDays.ToString(CultureInfo.InvariantCulture)).Append("d)");
                }

                builder.Append(" |\n");
            }

            builder.Append('\n');
        }

        if (durationHistory.Length > 0)
        {
            builder.Append(heading).Append(" Duration history\n\n");
            builder.Append("| Test | Current | Historical p95 | Historical p99 | Samples | vs p95 |\n");
            builder.Append("| --- | ---: | ---: | ---: | ---: | ---: |\n");
            foreach ((CiRunSummaryModule module, CiRunSummaryHistoryTest test) in durationHistory)
            {
                builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules.Count, module, test.FullyQualifiedName)))
                    .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks)))
                    .Append(" | ").Append(FormatHistoricalDuration(test.P95DurationMilliseconds))
                    .Append(" | ").Append(FormatHistoricalDuration(test.P99DurationMilliseconds))
                    .Append(" | ").Append(test.DurationSampleCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(GetDurationRatio(test).ToString("F2", CultureInfo.InvariantCulture)).Append("× |\n");
            }

            builder.Append('\n');
        }
    }

    private static void AppendDependenciesMarkdown(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        int headingLevel)
    {
        (CiRunSummaryModule Module, CiRunSummaryDependency Dependency)[] dependencies =
        [
            .. modules
                .SelectMany(module => module.Dependencies.Select(dependency => (Module: module, Dependency: dependency)))
                .OrderBy(static item => item.Dependency.DependentFullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static item => item.Dependency.Prerequisite, StringComparer.Ordinal)
                .Take(MaxDependencies),
        ];
        if (dependencies.Length == 0)
        {
            return;
        }

        builder.Append(new string('#', headingLevel)).Append(" Test dependencies\n\n");
        builder.Append("| Test | Prerequisite | If prerequisite fails |\n");
        builder.Append("| --- | --- | --- |\n");
        foreach ((CiRunSummaryModule module, CiRunSummaryDependency dependency) in dependencies)
        {
            builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules.Count, module, dependency.DependentFullyQualifiedName)))
                .Append(" | ").Append(EscapeCell(dependency.Prerequisite))
                .Append(" | ").Append(dependency.ProceedOnFailure ? "Continue" : "Skip")
                .Append(" |\n");
        }

        int totalDependencies = modules.Sum(static module => module.Dependencies.Length);
        if (totalDependencies > dependencies.Length)
        {
            builder.Append("\n> ")
                .Append((totalDependencies - dependencies.Length).ToString(CultureInfo.InvariantCulture))
                .Append(" additional dependency edges were omitted.\n");
        }

        builder.Append('\n');
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0:D2}:{1:D2}", "{0}:{1:D2}:{2:D2}");

    private static string FormatHistoricalDuration(double milliseconds)
        => milliseconds <= TimeSpan.MaxValue.TotalMilliseconds
            ? FormatDuration(TimeSpan.FromMilliseconds(milliseconds))
            : milliseconds.ToString("G3", CultureInfo.InvariantCulture) + "ms";

    private static string FormatRate(long value, long total)
        => total > 0
            ? ((double)value / total * 100d).ToString("F1", CultureInfo.InvariantCulture) + "%"
            : "No tests";

    private static double GetHistoricalFailureRate(CiRunSummaryHistoryTest test)
    {
        int total = test.HistoricalPassCount + test.HistoricalFailCount;
        return total > 0 ? (double)test.HistoricalFailCount / total : 0;
    }

    private static double GetDurationRatio(CiRunSummaryHistoryTest test)
        => test.P95DurationMilliseconds > 0
            ? TimeSpan.FromTicks(test.DurationTicks).TotalMilliseconds / test.P95DurationMilliseconds
            : 0;

    private static string GetQualifiedTestLabel(int moduleCount, CiRunSummaryModule module, string testName)
        => moduleCount > 1 ? $"{module.AssemblyName}: {testName}" : testName;

    private static void AppendBlockQuote(StringBuilder builder, string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            builder.Append("> ").Append(EscapeCell(line)).Append('\n');
        }

        builder.Append('\n');
    }

    private static string EscapeInlineCode(string value)
        => value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string EscapeCell(string value)
        => RoslynString.IsNullOrEmpty(value)
            ? value
            : HtmlEncode(value)
                .Replace("|", "\\|")
                .Replace("`", "\\`")
                .Replace("\r", string.Empty)
                .Replace("\n", "<br>");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

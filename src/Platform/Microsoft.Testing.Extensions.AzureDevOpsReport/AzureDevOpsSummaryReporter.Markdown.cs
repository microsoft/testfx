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
    private const int MaxSlowestTests = 10;

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
        bool runFailed = module.FailedTests > 0 || module.ExitCode != 0;
        var builder = new StringBuilder();
        builder.Append("## ").Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeCell(module.AssemblyName)).Append(" (")
            .Append(EscapeCell(module.TargetFramework)).Append(")\n\n");
        AppendStatusStrip(
            builder,
            module.TotalTests,
            module.PassedTests,
            module.FailedTests,
            module.SkippedTests,
            module.FlakyTests.Length,
            TimeSpan.FromTicks(module.TestDurationTicks),
            module.ExitCode);
        CiCoverageSummary.AppendMarkdown(builder, module.Coverage, headingLevel: 3);
        _ = AppendModuleDiagnostics(
            builder,
            module,
            headingLevel: 3,
            includeInsights: true,
            failureDetailLimit: MaxFirstFailingFqns);
        return builder.ToString();
    }

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate)
    {
        bool runFailed = aggregate.ExitCode is int exitCode
            ? exitCode != 0
            : aggregate.Modules.Any(static module => module.FailedTests > 0 || module.ExitCode != 0);
        var builder = new StringBuilder();
        builder.Append("## ").Append(runFailed ? "❌" : "✅").Append(" Overall test results\n\n");
        AppendStatusStrip(
            builder,
            aggregate.TotalTests,
            aggregate.PassedTests,
            aggregate.FailedTests,
            aggregate.SkippedTests,
            aggregate.FlakyTests.Count,
            aggregate.Duration,
            aggregate.ExitCode);
        if (aggregate.IsPartial)
        {
            builder.Append("> **Partial summary:** the test run was truncated.\n\n");
        }
        else if (!aggregate.HasAuthoritativeRunSummary)
        {
            builder.Append("> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
        }

        CiCoverageSummary.AppendMarkdown(builder, aggregate.Coverage, headingLevel: 3);
        AppendHistoryMarkdown(builder, aggregate.Modules, headingLevel: 3);
        AppendDependenciesMarkdown(builder, aggregate.Modules, headingLevel: 3);

        builder.Append("### Test modules\n\n");
        builder.Append("| Result | Test module | Total | Passed | Failed | Skipped | Flaky | Duration |\n");
        builder.Append("| :---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");
        CiRunSummaryModule[] orderedModules =
        [
            .. aggregate.Modules
                .OrderByDescending(static module => module.FailedTests > 0 || module.ExitCode != 0)
                .ThenBy(static module => module.AssemblyName, StringComparer.Ordinal)
                .ThenBy(static module => module.TargetFramework, StringComparer.Ordinal)
                .ThenBy(static module => module.Architecture, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static module => module.AttemptNumber),
        ];
        foreach (CiRunSummaryModule module in orderedModules)
        {
            bool moduleFailed = module.FailedTests > 0 || module.ExitCode != 0;
            builder.Append("| ").Append(moduleFailed ? "❌" : "✅").Append(" | ");
            AppendModuleIdentity(builder, aggregate.Modules, module);
            builder.Append(" | ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.FlakyTests.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks)))
                .Append(" |\n");
        }

        builder.Append('\n');

        int remainingFailureDetails = MaxAggregateFailureDetails;
        CiRunSummaryModule[] modulesWithFailures =
        [
            .. orderedModules.Where(static module => module.FailedTests > 0 || module.ExitCode != 0),
        ];
        if (modulesWithFailures.Length > 0)
        {
            builder.Append("### ❌ Failures\n\n");
            foreach (CiRunSummaryModule module in modulesWithFailures)
            {
                builder.Append("#### ");
                AppendModuleIdentity(builder, aggregate.Modules, module);
                builder.Append("\n\n");
                remainingFailureDetails -= AppendModuleDiagnostics(
                    builder,
                    module,
                    headingLevel: 4,
                    includeInsights: false,
                    failureDetailLimit: remainingFailureDetails);
            }
        }

        (CiRunSummaryModule Module, CiRunSummaryTest Test)[] slowest =
        [
            .. aggregate.Modules
                .SelectMany(static module => module.SlowestTests.Select(test => (Module: module, Test: test)))
                .OrderByDescending(static item => item.Test.DurationTicks)
                .ThenBy(static item => item.Test.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.AssemblyName, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.TargetFramework, StringComparer.Ordinal)
                .Take(MaxSlowestTests),
        ];
        if (slowest.Length > 0)
        {
            builder.Append("### ⏱ Slowest tests\n\n");
            foreach ((CiRunSummaryModule module, CiRunSummaryTest test) in slowest)
            {
                builder.Append("- **").Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append("** — ")
                    .Append(FormatTestName(test.DisplayName))
                    .Append(" — ");
                AppendModuleIdentity(builder, aggregate.Modules, module);
                builder.Append('\n');
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static int AppendModuleDiagnostics(
        StringBuilder builder,
        CiRunSummaryModule module,
        int headingLevel,
        bool includeInsights,
        int failureDetailLimit)
    {
        string heading = new('#', headingLevel);
        if (module.ExitCode != 0)
        {
            builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("`\n\n");
        }

        if (module.TopFailingClasses.Length > 0)
        {
            builder.Append(heading).Append(" Top failing classes\n\n");
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
            builder.Append(heading).Append(" Failure details\n\n");
            foreach (CiRunSummaryTest failure in detailedFailures)
            {
                builder.Append("**").Append(FormatTestName(failure.FullyQualifiedName))
                    .Append(" — ").Append(FormatDuration(TimeSpan.FromTicks(failure.DurationTicks))).Append("**\n\n");
                if (!RoslynString.IsNullOrWhiteSpace(failure.ErrorType))
                {
                    builder.Append("**Exception:** ").Append(FormatTestName(failure.ErrorType!)).Append("\n\n");
                }

                if (!RoslynString.IsNullOrWhiteSpace(failure.ErrorMessage))
                {
                    builder.Append("**Message:**\n\n");
                    AppendBlockQuote(builder, failure.ErrorMessage!);
                }
            }
        }

        if (module.Failures.Length > renderedFailureDetails)
        {
            builder.Append(heading).Append(" Additional failing tests\n\n");
            foreach (CiRunSummaryTest failure in module.Failures
                .Where(failure => !detailedFailures.Contains(failure))
                .Take(MaxFirstFailingFqns))
            {
                builder.Append("- ").Append(FormatTestName(failure.FullyQualifiedName)).Append('\n');
            }

            builder.Append('\n');
        }

        if (module.FlakyTests.Length > 0)
        {
            builder.Append(heading).Append(" Flaky tests\n\n");
            foreach (CiRunSummaryTest flaky in module.FlakyTests.Take(MaxHistoricalTests))
            {
                builder.Append("- ").Append(FormatTestName(flaky.FullyQualifiedName)).Append('\n');
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
            AppendHistoryMarkdown(builder, [module], headingLevel);
            AppendDependenciesMarkdown(builder, [module], headingLevel);
        }

        if (module.SlowestTests.Length > 0)
        {
            builder.Append(heading).Append(" ⏱ Slowest tests\n\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("- **").Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append("** — ")
                    .Append(FormatTestName(test.DisplayName)).Append('\n');
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
                long total = (long)test.HistoricalPassCount + test.HistoricalFailCount;
                builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules, module, test.FullyQualifiedName)))
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
                builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules, module, test.FullyQualifiedName)))
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
            builder.Append("| ").Append(EscapeCell(GetQualifiedTestLabel(modules, module, dependency.DependentFullyQualifiedName)))
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

    private static void AppendStatusStrip(
        StringBuilder builder,
        long total,
        long passed,
        long failed,
        long skipped,
        long flaky,
        TimeSpan? duration,
        int? exitCode)
    {
        builder.Append("**").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(total == 1 ? " test**" : " tests**")
            .Append(" · ✅ **").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" passed**")
            .Append(" · ❌ **").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" failed**")
            .Append(" · ⏭ **").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped**");
        if (flaky > 0)
        {
            builder.Append(" · ⚠ **").Append(flaky.ToString(CultureInfo.InvariantCulture)).Append(" flaky**");
        }

        builder.Append(" · ⏱ **").Append(duration is { } value ? FormatDuration(value) : "Unavailable").Append("**");
        if (exitCode is int processExitCode && processExitCode != 0)
        {
            builder.Append(" · exit code **").Append(processExitCode.ToString(CultureInfo.InvariantCulture)).Append("**");
        }

        builder.Append("\n\n");
    }

    private static string FormatHistoricalDuration(double milliseconds)
        => milliseconds <= TimeSpan.MaxValue.TotalMilliseconds
            ? FormatDuration(TimeSpan.FromMilliseconds(milliseconds))
            : milliseconds.ToString("G3", CultureInfo.InvariantCulture) + "ms";

    private static double GetHistoricalFailureRate(CiRunSummaryHistoryTest test)
    {
        long total = (long)test.HistoricalPassCount + test.HistoricalFailCount;
        return total > 0 ? (double)test.HistoricalFailCount / total : 0;
    }

    private static double GetDurationRatio(CiRunSummaryHistoryTest test)
        => test.P95DurationMilliseconds > 0
            ? TimeSpan.FromTicks(test.DurationTicks).TotalMilliseconds / test.P95DurationMilliseconds
            : 0;

    private static string GetQualifiedTestLabel(
        IReadOnlyList<CiRunSummaryModule> modules,
        CiRunSummaryModule module,
        string testName)
    {
        if (modules.Count == 1)
        {
            return testName;
        }

        string discriminator = $"{module.AssemblyName} ({module.TargetFramework}, {module.Architecture}";
        if (HasDuplicateModuleIdentity(modules, module))
        {
            discriminator += $", attempt {module.AttemptNumber.ToString(CultureInfo.InvariantCulture)}, session {module.SessionUid}";
        }

        return $"{discriminator}): {testName}";
    }

    private static void AppendBlockQuote(StringBuilder builder, string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            builder.Append("> ").Append(EscapeCell(line)).Append('\n');
        }

        builder.Append('\n');
    }

    private static string FormatTestName(string value)
    {
        string flattenedValue = value
            .Replace("\r", " ")
            .Replace("\n", " ");
        if (flattenedValue.Length == 0)
        {
            return string.Empty;
        }

        int maxBacktickRun = 0;
        int currentBacktickRun = 0;
        foreach (char character in flattenedValue)
        {
            if (character == '`')
            {
                currentBacktickRun++;
                maxBacktickRun = Math.Max(maxBacktickRun, currentBacktickRun);
            }
            else
            {
                currentBacktickRun = 0;
            }
        }

        string delimiter = new('`', maxBacktickRun + 1);
        bool requiresPadding = flattenedValue.StartsWith("`", StringComparison.Ordinal)
            || flattenedValue.EndsWith("`", StringComparison.Ordinal)
            || flattenedValue.StartsWith(" ", StringComparison.Ordinal)
            || flattenedValue.EndsWith(" ", StringComparison.Ordinal);
        return requiresPadding
            ? $"{delimiter} {flattenedValue} {delimiter}"
            : $"{delimiter}{flattenedValue}{delimiter}";
    }

    private static void AppendModuleIdentity(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        CiRunSummaryModule module)
    {
        builder.Append(EscapeCell(module.AssemblyName))
            .Append(" (").Append(EscapeCell(module.TargetFramework)).Append(", ")
            .Append(EscapeCell(module.Architecture)).Append(')');
        if (HasDuplicateModuleIdentity(modules, module))
        {
            builder.Append(" — attempt ").Append(module.AttemptNumber.ToString(CultureInfo.InvariantCulture))
                .Append(", session ").Append(EscapeCell(module.SessionUid));
        }
    }

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

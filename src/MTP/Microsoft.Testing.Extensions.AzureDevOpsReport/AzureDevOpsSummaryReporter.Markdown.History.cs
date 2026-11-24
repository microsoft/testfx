// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
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
        if (durationHistory.Length == 0 && modules.Any(static module => module.TotalTests > 0))
        {
            builder.Append("> Duration history unavailable for the observed tests; current timings are not a historical baseline.\n\n");
        }

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
}

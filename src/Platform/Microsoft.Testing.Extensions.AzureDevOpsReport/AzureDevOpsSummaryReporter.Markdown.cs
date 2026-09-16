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
        => BuildMarkdown(records, assemblyName, targetFrameworkMoniker, exitCode: null, coverage: new CiCoverageSummaryData());

    private static string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        int? exitCode,
        CiCoverageSummaryData coverage)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failingByClass = new Dictionary<string, int>(StringComparer.Ordinal);

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
                    string className = GetClassName(record.FullyQualifiedName);
                    failingByClass[className] = failingByClass.TryGetValue(className, out int count) ? count + 1 : 1;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        bool runFailed = failed > 0 || (exitCode is int processExitCode && processExitCode != 0);
        var builder = new StringBuilder();
        builder.Append("## ").Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeCell(assemblyName)).Append(" (").Append(EscapeCell(targetFrameworkMoniker)).Append(")\n\n");
        AppendStatusStrip(builder, total, passed, failed, skipped, totalDuration, exitCode);
        CiCoverageSummary.AppendMarkdown(builder, coverage, headingLevel: 3);

        if (failingByClass.Count > 0)
        {
            builder.Append("### ❌ Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (KeyValuePair<string, int> pair in failingByClass.OrderByDescending(static p => p.Value).ThenBy(static p => p.Key, StringComparer.Ordinal).Take(MaxTopFailingClasses))
            {
                builder.Append("| ").Append(EscapeCell(pair.Key)).Append(" | ").Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        string[] failingFqns =
        [
            .. records
                .Where(static record => record.Kind == TerminalKind.Failed)
                .Select(static record => record.FullyQualifiedName)
                .OrderBy(static fullyQualifiedName => fullyQualifiedName, StringComparer.Ordinal)
                .Take(MaxFirstFailingFqns),
        ];
        if (failingFqns.Length > 0)
        {
            builder.Append("### ❌ Failed tests\n\n");
            foreach (string fqn in failingFqns)
            {
                builder.Append("- ").Append(FormatTestName(fqn)).Append('\n');
            }

            if (failed > failingFqns.Length)
            {
                builder.Append("- … and ")
                    .Append((failed - failingFqns.Length).ToString(CultureInfo.InvariantCulture))
                    .Append(" more\n");
            }

            builder.Append('\n');
        }

        IEnumerable<TestRecord> slowest = records
            .Where(static r => r.Duration > TimeSpan.Zero)
            .OrderByDescending(static r => r.Duration)
            .ThenBy(static r => r.FullyQualifiedName, StringComparer.Ordinal)
            .Take(MaxSlowestTests);

        bool slowestEmitted = false;
        foreach (TestRecord record in slowest)
        {
            if (!slowestEmitted)
            {
                builder.Append("### ⏱ Slowest tests\n\n");
                slowestEmitted = true;
            }

            builder.Append("- **").Append(FormatDuration(record.Duration)).Append("** — ")
                .Append(FormatTestName(record.DisplayName)).Append('\n');
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

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

        builder.Append("### Test modules\n\n");
        builder.Append("| Result | Test module | Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("| :---: | --- | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (CiRunSummaryModule module in aggregate.Modules
            .OrderByDescending(static module => module.FailedTests > 0 || module.ExitCode != 0)
            .ThenBy(static module => module.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static module => module.TargetFramework, StringComparer.Ordinal)
            .ThenBy(static module => module.Architecture, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static module => module.AttemptNumber))
        {
            bool moduleFailed = module.FailedTests > 0 || module.ExitCode != 0;
            builder.Append("| ").Append(moduleFailed ? "❌" : "✅").Append(" | ");
            AppendModuleIdentity(builder, aggregate.Modules, module);
            builder.Append(" | ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks)))
                .Append(" |\n");
        }

        builder.Append('\n');

        CiRunSummaryModule[] failedModules =
        [
            .. aggregate.Modules
                .Where(static module =>
                    module.FailedTests > 0
                    || module.ExitCode != 0)
                .OrderByDescending(static module => module.FailedTests)
                .ThenBy(static module => module.AssemblyName, StringComparer.Ordinal)
                .ThenBy(static module => module.TargetFramework, StringComparer.Ordinal)
                .ThenBy(static module => module.Architecture, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static module => module.AttemptNumber),
        ];
        if (failedModules.Length > 0)
        {
            builder.Append("### ❌ Failures\n\n");
            foreach (CiRunSummaryModule module in failedModules)
            {
                AppendModuleFailuresMarkdown(builder, aggregate.Modules, module);
            }
        }

        (CiRunSummaryModule Module, CiRunSummaryTest Test)[] slowest = aggregate.Modules
            .SelectMany(static module => module.SlowestTests.Select(test => (Module: module, Test: test)))
            .OrderByDescending(static item => item.Test.DurationTicks)
            .ThenBy(static item => item.Test.FullyQualifiedName, StringComparer.Ordinal)
            .ThenBy(static item => item.Module.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static item => item.Module.TargetFramework, StringComparer.Ordinal)
            .Take(MaxSlowestTests)
            .ToArray();
        if (slowest.Length > 0)
        {
            builder.Append("### ⏱ Slowest tests\n\n");
            foreach ((CiRunSummaryModule Module, CiRunSummaryTest Test) item in slowest)
            {
                builder.Append("- **").Append(FormatDuration(TimeSpan.FromTicks(item.Test.DurationTicks))).Append("** — ")
                    .Append(FormatTestName(item.Test.DisplayName))
                    .Append(" — ");
                AppendModuleIdentity(builder, aggregate.Modules, item.Module);
                builder.Append('\n');
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendModuleFailuresMarkdown(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        CiRunSummaryModule module)
    {
        builder.Append("#### ");
        AppendModuleIdentity(builder, modules, module);
        builder.Append("\n\n");
        if (module.ExitCode != 0)
        {
            builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("`\n\n");
        }

        if (module.TopFailingClasses.Length > 0)
        {
            builder.Append("**Top failing classes**\n\n");
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
            builder.Append("**Failed tests**\n\n");
            foreach (CiRunSummaryTest failure in module.Failures.Take(MaxFirstFailingFqns))
            {
                builder.Append("- ").Append(FormatTestName(failure.FullyQualifiedName)).Append('\n');
            }

            if (module.FailedTests > MaxFirstFailingFqns)
            {
                builder.Append("- … and ")
                    .Append((module.FailedTests - MaxFirstFailingFqns).ToString(CultureInfo.InvariantCulture))
                    .Append(" more\n");
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

    private static void AppendStatusStrip(
        StringBuilder builder,
        long total,
        long passed,
        long failed,
        long skipped,
        TimeSpan? duration,
        int? exitCode)
    {
        builder.Append("**").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(total == 1 ? " test**" : " tests**")
            .Append(" · ✅ **").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" passed**")
            .Append(" · ❌ **").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" failed**")
            .Append(" · ⏭ **").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped**")
            .Append(" · ⏱ **").Append(duration is { } value ? FormatDuration(value) : "Unavailable").Append("**");
        if (exitCode is int processExitCode && processExitCode != 0)
        {
            builder.Append(" · exit code **").Append(processExitCode.ToString(CultureInfo.InvariantCulture)).Append("**");
        }

        builder.Append("\n\n");
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
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        string encodedValue = System.Net.WebUtility.HtmlEncode(value);
        var sb = new StringBuilder(encodedValue.Length);
        foreach (char c in encodedValue)
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

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

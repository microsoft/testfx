// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate)
    {
        bool runFailed = aggregate.FailedTests > 0
            || aggregate.Modules.Any(static module => module.FailedTests > 0 || module.ExitCode != 0)
            || (aggregate.ExitCode is int exitCode && exitCode != 0);
        string statusIcon = runFailed
            ? "❌"
            : (aggregate.IsPartial || !aggregate.HasAuthoritativeRunSummary ? "⚠️" : "✅");
        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Overall test results\n\n");
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
                    includeExitCode: true,
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
        bool includeExitCode,
        int failureDetailLimit)
    {
        string heading = new('#', headingLevel);
        if (includeExitCode && module.ExitCode != 0)
        {
            builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("` (")
                .Append(CiRunSummaryExitCode.GetName(module.ExitCode)).Append(" — ")
                .Append(CiRunSummaryExitCode.GetReason(module.ExitCode, AzureDevOpsResources.ResourceManager)).Append(")\n\n");
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
}

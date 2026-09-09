// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
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


    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}

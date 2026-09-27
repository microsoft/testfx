// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}

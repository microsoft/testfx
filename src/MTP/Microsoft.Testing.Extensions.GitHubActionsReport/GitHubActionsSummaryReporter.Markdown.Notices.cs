// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    /// <summary>
    /// Records how much the summary gave up, so a later writer can tell whether the note already in the file
    /// still describes the worst loss.
    /// </summary>
    /// <remarks>
    /// The two notices share a start marker so a summary can never carry two contradictory warnings, but they do
    /// not describe the same loss. Without this token the first note written would win outright: an aggregate
    /// step that only dropped diagnostics would suppress a later project's "whole sections were removed" note,
    /// and the summary would never say that results are missing.
    /// </remarks>
    internal static string BuildNoticeStrengthToken(int strength)
        => $"<!-- microsoft-testing-platform:github:truncation-strength:{strength.ToString(CultureInfo.InvariantCulture)} -->";

    /// <summary>
    /// Renders the top-of-file warning for a combined <c>dotnet test</c> summary whose modules lost their expanded
    /// diagnostics.
    /// </summary>
    /// <remarks>
    /// This shares its marker with the per-project warning deliberately. Only one of the two writing modes runs in
    /// a given test process, but a workflow is free to mix them across steps, and two warnings in one summary —
    /// each describing a different kind of loss — would be worse than either alone. The shared marker means
    /// whichever is written first is the only one, and neither writer adds a second.
    /// </remarks>
    internal static /* for testing */ string BuildAggregateTruncationNotice(int modulesWithOmittedDetails, int totalModules)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.ModuleDetailsOmitted,
            modulesWithOmittedDetails.ToString(CultureInfo.InvariantCulture),
            totalModules.ToString(CultureInfo.InvariantCulture));

        return $"{TruncationNoticeMarker}\n{BuildNoticeStrengthToken(DetailsOmittedNoticeStrength)}\n> [!WARNING]\n> {message}\n{TruncationNoticeEndMarker}\n\n";
    }

    /// <summary>
    /// Renders the note that tells the reader the summary was shortened on purpose, and how many test projects
    /// did get their full results in before that happened.
    /// </summary>
    /// <param name="reportedProjectCount">
    /// The number of test projects whose full results are in the summary, counted from the file itself. Each
    /// test project runs in its own process and cannot know how many siblings will follow, so this is whatever
    /// is visible when the note is written.
    /// </param>
    /// <remarks>
    /// Without this note the report is silently incomplete: the reader sees one-line verdicts with no indication
    /// that anything was left out. The note is deliberately small — it is written while the file is already well
    /// into GitHub's cap, so it has to cost a few hundred bytes, not a few thousand.
    /// </remarks>
    internal static /* for testing */ string BuildTruncationNotice(int reportedProjectCount)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.SummaryTruncatedNotice,
            reportedProjectCount.ToString(CultureInfo.InvariantCulture));

        return $"{TruncationNoticeMarker}\n{BuildNoticeStrengthToken(SectionsRemovedNoticeStrength)}\n> [!WARNING]\n> {message}\n{TruncationNoticeEndMarker}\n\n";
    }
}

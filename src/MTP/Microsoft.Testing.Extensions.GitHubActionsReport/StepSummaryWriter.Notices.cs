// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class StepSummaryWriter
{
    /// <summary>
    /// Indicates whether the shared summary file already opens with the truncation notice.
    /// </summary>
    /// <remarks>
    /// The notice is only ever placed at the very start of the file, so that is the only position that counts.
    /// Failing tests' messages are copied verbatim into the summary, so a test whose diagnostics contain the
    /// marker text would otherwise be mistaken for the notice — suppressing the real warning and leaving a
    /// shortened summary that never says it was shortened. This reporter's own test suite refers to the marker
    /// by value, which makes that a live case rather than a hypothetical one.
    /// </remarks>
    internal static bool HasLeadingTruncationNotice(string summary)
        => summary.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, StringComparison.Ordinal);

    /// <summary>
    /// Returns how much loss the note at the top of <paramref name="summary"/> describes, or <c>0</c> when there
    /// is no note. A note written before strengths were recorded reads as the weakest.
    /// </summary>
    internal static int GetLeadingNoticeStrength(string summary)
    {
        if (!HasLeadingTruncationNotice(summary))
        {
            return 0;
        }

        int end = summary.IndexOf(GitHubActionsSummaryReporter.TruncationNoticeEndMarker, StringComparison.Ordinal);
        string head = end < 0 ? summary : summary.Substring(0, end);
        for (int strength = GitHubActionsSummaryReporter.SectionsRemovedNoticeStrength; strength >= GitHubActionsSummaryReporter.DetailsOmittedNoticeStrength; strength--)
        {
            if (head.IndexOf(GitHubActionsSummaryReporter.BuildNoticeStrengthToken(strength), StringComparison.Ordinal) >= 0)
            {
                return strength;
            }
        }

        return GitHubActionsSummaryReporter.DetailsOmittedNoticeStrength;
    }

    /// <summary>
    /// Removes the note at the top of <paramref name="summary"/>, so a stronger one can take its place.
    /// </summary>
    private static string StripLeadingTruncationNotice(string summary)
    {
        if (!HasLeadingTruncationNotice(summary))
        {
            return summary;
        }

        int end = summary.IndexOf(GitHubActionsSummaryReporter.TruncationNoticeEndMarker, StringComparison.Ordinal);
        if (end < 0)
        {
            return summary;
        }

        int after = end + GitHubActionsSummaryReporter.TruncationNoticeEndMarker.Length;
        while (after < summary.Length && (summary[after] == '\n' || summary[after] == '\r'))
        {
            after++;
        }

        return summary.Substring(after);
    }
}

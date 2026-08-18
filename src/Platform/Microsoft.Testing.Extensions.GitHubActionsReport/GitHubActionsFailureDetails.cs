// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// A single failing test as rendered in the job summary's <c>Failures</c> section. Unifies the two shapes the
/// summary reporter renders from: the in-process <see cref="TestRecord"/> of a single-module run and the
/// <see cref="CiRunSummaryTest"/> deserialized from a fragment during multi-module aggregation.
/// </summary>
internal readonly struct GitHubActionsFailureEntry
{
    public GitHubActionsFailureEntry(
        string fullyQualifiedName,
        TimeSpan duration,
        string? message = null,
        string? exceptionType = null,
        string? stackTrace = null,
        string? filePath = null,
        int lineNumber = 0)
    {
        FullyQualifiedName = fullyQualifiedName;
        Duration = duration;
        Message = message;
        ExceptionType = exceptionType;
        StackTrace = stackTrace;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    public string FullyQualifiedName { get; }

    public TimeSpan Duration { get; }

    public string? Message { get; }

    public string? ExceptionType { get; }

    public string? StackTrace { get; }

    public string? FilePath { get; }

    public int LineNumber { get; }

    public bool HasDetails
        => !RoslynString.IsNullOrWhiteSpace(Message)
            || !RoslynString.IsNullOrWhiteSpace(ExceptionType)
            || !RoslynString.IsNullOrWhiteSpace(StackTrace)
            || !RoslynString.IsNullOrWhiteSpace(FilePath);
}

/// <summary>
/// Renders the job summary's <c>Failures</c> section, expanding each failure into a collapsible
/// <c>&lt;details&gt;</c> block that carries its message, exception type, source location and stack trace.
/// </summary>
/// <remarks>
/// A GitHub job summary is capped at 1 MiB per step and is shared by every test host appending to
/// <c>GITHUB_STEP_SUMMARY</c>, so diagnostics are bounded on four axes, and every reduction is stated in
/// the rendered output so a reader never mistakes truncated diagnostics for complete ones:
/// <list type="bullet">
/// <item>each value is clipped by length (<see cref="MaxMessageLength"/> / <see cref="MaxStackTraceLength"/>);</item>
/// <item>each value is clipped by line count (<see cref="MaxMessageRows"/> / <see cref="MaxStackTraceRows"/>),
/// because a 200-line trace of one-word frames is under the character cap yet unreadable;</item>
/// <item>the failure list itself is capped by the caller;</item>
/// <item>expansion stops once the caller's remaining character budget is exhausted.</item>
/// </list>
/// The budget is supplied by the caller rather than being a per-section constant, because the 1 MiB cap
/// applies to the whole file — which several test projects share — not to one project's section.
/// </remarks>
internal static class GitHubActionsFailureDetails
{
    /// <summary>
    /// Maximum characters kept from a single failure message. Long enough for a multi-line assertion diff,
    /// short enough that twenty of them cannot dominate the summary.
    /// </summary>
    internal const int MaxMessageLength = 2000;

    /// <summary>
    /// Maximum characters kept from a single stack trace, which comfortably covers the frames that matter
    /// while excluding pathologically deep recursion traces.
    /// </summary>
    internal const int MaxStackTraceLength = 4000;

    /// <summary>
    /// Maximum lines kept from a single failure message. A character cap alone does not bound readability:
    /// a message of many very short lines stays under it while being far too long to read.
    /// </summary>
    internal const int MaxMessageRows = 30;

    /// <summary>
    /// Maximum stack frames kept from a single stack trace, for the same reason as <see cref="MaxMessageRows"/>.
    /// Deep recursion produces hundreds of near-identical one-line frames that carry no extra information.
    /// </summary>
    internal const int MaxStackTraceRows = 30;

    /// <summary>
    /// GitHub's hard limit on a single job summary file. Exceeding it makes GitHub drop the summary entirely,
    /// so the reporters aim well below it.
    /// </summary>
    internal const int GitHubStepSummaryLimit = 1024 * 1024;

    /// <summary>
    /// The share of <see cref="GitHubStepSummaryLimit"/> this extension will drive the summary file to before
    /// it starts condensing its own output.
    /// </summary>
    /// <remarks>
    /// This is deliberately well below the 1 MiB cap rather than just under it, because this extension is not
    /// the only writer to <c>GITHUB_STEP_SUMMARY</c>. Test frameworks (TUnit, for one) append their own
    /// per-assembly summary block after this reporter runs, measured at roughly 5 KB per test project. This
    /// reporter observes the shared file and so accounts for what earlier projects wrote, but it cannot
    /// prevent the writes that follow it. The headroom left here absorbs that co-writer overhead — at ~5 KB
    /// per project it covers on the order of 80 further test projects — so the file lands under the cap and
    /// GitHub renders it, instead of exceeding the cap and being dropped in its entirety.
    /// </remarks>
    internal const int MaxSummaryLength = (int)(GitHubStepSummaryLimit * 0.4);

    /// <summary>
    /// Characters reserved for one test project's non-detail content — heading, totals table, failure and
    /// slowest-test lines, truncation notes — so the budget bounds the <em>file</em> rather than just the
    /// expanded diagnostics. Without this reserve a job with many projects lands well over
    /// <see cref="MaxSummaryLength"/>: each project writes several kilobytes even when it expands nothing.
    /// </summary>
    internal const int PerProjectOverheadReserve = 8_000;

    /// <summary>
    /// Maximum characters of expanded failure detail available across the whole summary file.
    /// </summary>
    internal const int MaxTotalDetailsLength = MaxSummaryLength - PerProjectOverheadReserve;

    /// <summary>
    /// Clips <paramref name="value"/> to <paramref name="maxLength"/> characters and <paramref name="maxRows"/>
    /// lines, appending a marker whenever either bound trims the value so the reader can tell it was cut.
    /// Returns <see langword="null"/> for null/whitespace input so callers can treat "nothing useful" uniformly.
    /// </summary>
    internal static string? Clip(string? value, int maxLength, int maxRows = int.MaxValue)
    {
        if (RoslynString.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value!.Replace("\r\n", "\n").TrimEnd();
        bool truncated = false;

        if (normalized.Length > maxLength)
        {
            normalized = normalized.Substring(0, maxLength).TrimEnd();
            truncated = true;
        }

        if (maxRows < int.MaxValue)
        {
            string[] rows = normalized.Split('\n');
            if (rows.Length > maxRows)
            {
                normalized = string.Join("\n", rows.Take(maxRows)).TrimEnd();
                truncated = true;
            }
        }

        return truncated
            ? normalized + "\n" + GitHubActionsResources.FailureDetailTruncated
            : normalized;
    }

    /// <summary>
    /// Appends the whole <c>Failures</c> section: the heading, one entry per failure (collapsible when the
    /// failure carries diagnostics and the budget allows) and an explicit note whenever the failure list or
    /// the failure details were truncated.
    /// </summary>
    /// <param name="builder">The summary being built.</param>
    /// <param name="heading">The markdown heading prefix (e.g. <c>###</c>) for the section.</param>
    /// <param name="entries">The failures to render, already capped to the reporter's maximum.</param>
    /// <param name="totalFailedCount">The total number of failing tests, which may exceed <paramref name="entries"/>.</param>
    /// <param name="includeDetails">Whether expanded diagnostics are enabled.</param>
    /// <param name="remainingBudget">
    /// The characters of expanded detail this section may still emit, decremented by what it renders. Passed by
    /// reference so a caller rendering several project sections into one file shares a single budget across them
    /// — the 1 MiB cap applies to the whole file, not to any one section.
    /// </param>
    /// <returns>
    /// The number of listed failures that were rendered without their diagnostics because the budget ran out, so
    /// a caller rendering many sections can also report the shortfall at the file level.
    /// </returns>
    internal static int AppendFailuresSection(
        StringBuilder builder,
        string heading,
        IReadOnlyList<GitHubActionsFailureEntry> entries,
        long totalFailedCount,
        bool includeDetails,
        ref int remainingBudget)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        builder.Append(heading).Append(" ❌ Failures (").Append(totalFailedCount.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");

        int omittedDetails = 0;

        foreach (GitHubActionsFailureEntry entry in entries)
        {
            if (!includeDetails || !entry.HasDetails)
            {
                AppendCompactEntry(builder, entry);
                continue;
            }

            var detailsBuilder = new StringBuilder();
            AppendDetailedEntry(detailsBuilder, entry);

            if (detailsBuilder.Length > remainingBudget)
            {
                // The remaining budget cannot fit this failure's diagnostics. Degrade to the compact line rather
                // than emitting a half-written <details> block, and account for it in the truncation note below.
                omittedDetails++;
                AppendCompactEntry(builder, entry);
                continue;
            }

            remainingBudget -= detailsBuilder.Length;
            builder.Append(detailsBuilder);
        }

        builder.Append('\n');

        if (totalFailedCount > entries.Count)
        {
            builder.Append("> [!NOTE]\n> ")
                .Append(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.FailureListTruncated,
                    entries.Count.ToString(CultureInfo.InvariantCulture),
                    totalFailedCount.ToString(CultureInfo.InvariantCulture)))
                .Append("\n\n");
        }

        if (omittedDetails > 0)
        {
            builder.Append("> [!NOTE]\n> ")
                .Append(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.FailureDetailsOmitted,
                    omittedDetails.ToString(CultureInfo.InvariantCulture)))
                .Append("\n\n");
        }

        return omittedDetails;
    }

    private static void AppendCompactEntry(StringBuilder builder, GitHubActionsFailureEntry entry)
        => builder.Append("- `").Append(EscapeInlineCode(entry.FullyQualifiedName)).Append("` — ")
            .Append(FormatDuration(entry.Duration)).Append('\n');

    private static void AppendDetailedEntry(StringBuilder builder, GitHubActionsFailureEntry entry)
    {
        // The name and duration are HTML-encoded: <summary> is raw HTML, so a generic test name such as
        // `Map<string,int>` would otherwise be parsed as a tag and swallow the rest of the line.
        builder.Append("<details>\n<summary><code>")
            .Append(HtmlEncode(entry.FullyQualifiedName))
            .Append("</code> — ")
            .Append(HtmlEncode(FormatDuration(entry.Duration)))
            .Append("</summary>\n\n");

        if (!RoslynString.IsNullOrWhiteSpace(entry.ExceptionType))
        {
            builder.Append("**").Append(GitHubActionsResources.FailureDetailExceptionLabel).Append(":** `")
                .Append(EscapeInlineCode(entry.ExceptionType!)).Append("`\n\n");
        }

        if (!RoslynString.IsNullOrWhiteSpace(entry.FilePath))
        {
            builder.Append("**").Append(GitHubActionsResources.FailureDetailLocationLabel).Append(":** `")
                .Append(EscapeInlineCode(entry.FilePath!));
            if (entry.LineNumber > 0)
            {
                builder.Append(':').Append(entry.LineNumber.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append("`\n\n");
        }

        string body = BuildBody(entry);
        if (body.Length > 0)
        {
            string fence = GetCodeFence(body);
            builder.Append(fence).Append("text\n").Append(body).Append('\n').Append(fence).Append('\n');
        }

        builder.Append("\n</details>\n\n");
    }

    private static string BuildBody(GitHubActionsFailureEntry entry)
    {
        bool hasMessage = !RoslynString.IsNullOrWhiteSpace(entry.Message);
        bool hasStackTrace = !RoslynString.IsNullOrWhiteSpace(entry.StackTrace);

        return (hasMessage, hasStackTrace) switch
        {
            (true, true) => entry.Message + "\n\n" + entry.StackTrace,
            (true, false) => entry.Message!,
            (false, true) => entry.StackTrace!,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Picks a fence longer than the longest backtick run inside <paramref name="body"/> so a failure message that
    /// itself contains a fenced code block cannot terminate ours and leak raw markdown into the summary.
    /// </summary>
    private static string GetCodeFence(string body)
    {
        int longestRun = 0;
        int currentRun = 0;
        foreach (char c in body)
        {
            if (c == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}

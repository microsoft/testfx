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
/// <item>expansion stops once the caller's remaining byte budget is exhausted.</item>
/// </list>
/// The budget is supplied by the caller rather than being a per-section constant, because the 1 MiB cap
/// applies to the whole file — which several test projects share — not to one project's section. It is
/// denominated in UTF-8 bytes, because that is what GitHub counts: a character budget would under-bill
/// non-ASCII diagnostics by up to threefold.
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
    /// GitHub's hard limit on a single job summary file. An oversized summary is discarded in full — the
    /// runner logs <c>$GITHUB_STEP_SUMMARY upload aborted</c> and nothing is rendered — rather than being
    /// truncated, so the reporters aim well below it.
    /// </summary>
    /// <remarks>
    /// Verified empirically: a step writing 1,148,551 bytes produced
    /// <c>##[error]$GITHUB_STEP_SUMMARY upload aborted, supports content up to a size of 1024k, got 1121k</c>
    /// and rendered no summary at all, confirming that exceeding the limit costs the whole file rather than
    /// its tail. The documented limit is 1 MiB, but the runner has been reported to reject just below it
    /// (<see href="https://github.com/actions/runner/issues/4337"/>), which is why
    /// <see cref="EffectiveStepSummaryLimit"/> rather than this constant is what the reporters compare against.
    /// </remarks>
    internal const int GitHubStepSummaryLimit = 1024 * 1024;

    /// <summary>
    /// The size the reporters treat as the point of no return: the documented limit less a small safety margin.
    /// </summary>
    /// <remarks>
    /// Measured behaviour on ubuntu-latest with byte-exact writes: a summary of exactly
    /// <see cref="GitHubStepSummaryLimit"/> bytes is accepted, and the limit is therefore inclusive. The margin
    /// exists because <see href="https://github.com/actions/runner/issues/4337"/> reports summaries being
    /// rejected just below the documented figure — that did not reproduce here, but the report is recent and the
    /// cost of the margin is two bytes out of a megabyte, against the cost of being wrong, which is the entire
    /// summary.
    /// </remarks>
    internal const int EffectiveStepSummaryLimit = GitHubStepSummaryLimit - 2;

    /// <summary>
    /// The three shares of <see cref="GitHubStepSummaryLimit"/> at which the report gives something up, named by
    /// what they cost the reader rather than by the mechanism that enforces them.
    /// </summary>
    /// <remarks>
    /// The gap between the last share and the cap is not spare capacity. This extension is not the only writer to
    /// <c>GITHUB_STEP_SUMMARY</c>: test frameworks (TUnit, for one) append their own per-assembly block after this
    /// reporter has run, measured at roughly 9 KB per test project in a failing run. This reporter can observe
    /// what earlier projects wrote but cannot prevent what is written after it, so the remaining share absorbs
    /// that co-writer output and keeps the file under the cap. Exceeding the cap costs the entire summary, not
    /// its tail.
    /// </remarks>
    internal const double DetailBudgetShare = 0.4;

    /// <inheritdoc cref="DetailBudgetShare"/>
    internal const double CondenseShare = 0.6;

    /// <inheritdoc cref="DetailBudgetShare"/>
    internal const double StopListingShare = 0.9;

    /// <summary>
    /// Below this many bytes a failure is rendered as a collapsible section carrying its message, exception type,
    /// source location and stack trace; at or above it every failure is still listed, but only as its name and
    /// duration. The listing is what tells a reader which tests failed, and it costs a line rather than a
    /// kilobyte, so it survives long after the diagnostics stop.
    /// </summary>
    internal const int DetailBudgetLength = (int)(GitHubStepSummaryLimit * DetailBudgetShare);

    /// <summary>
    /// At or above this many bytes a test project is reduced to a one-line verdict. Between it and
    /// <see cref="DetailBudgetLength"/> a project still gets its heading, totals table and the names of its
    /// failing tests; past it the file is close enough to the cap that even that costs too much.
    /// </summary>
    internal const int CondenseLength = (int)(GitHubStepSummaryLimit * CondenseShare);

    /// <summary>
    /// At or above this many bytes a combined summary stops listing test projects at all, replacing the remainder
    /// with a single line counting them.
    /// </summary>
    /// <remarks>
    /// This is the only threshold that bounds the report by project count alone. Condensing a project to a
    /// one-line verdict shrinks what each one costs, but it does not bound the total: a run with thousands of
    /// test projects still overruns the cap on those lines.
    /// </remarks>
    internal const int StopListingLength = (int)(GitHubStepSummaryLimit * StopListingShare);

    /// <summary>
    /// Bytes reserved for <em>one</em> test project's non-detail content — heading, totals table, failure and
    /// slowest-test lines, truncation notes — so the budget bounds the <em>file</em> rather than just the
    /// expanded diagnostics. Without this reserve a job with many projects lands well over
    /// <see cref="DetailBudgetLength"/>: each project writes several kilobytes even when it expands nothing.
    /// </summary>
    /// <remarks>
    /// The per-project path subtracts this once, for itself. The aggregate path multiplies it by the module count,
    /// because it renders every project into one file. <see cref="SummaryBudget"/> is what keeps those two uses
    /// apart, so the constant is a single project's overhead and never a total.
    /// </remarks>
    internal const int ProjectOverheadReserve = 8_000;

    /// <summary>
    /// Maximum UTF-8 bytes of expanded failure detail one test project may contribute.
    /// </summary>
    internal const int MaxTotalDetailsLength = DetailBudgetLength - ProjectOverheadReserve;

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

        // Normalize every line ending, not just CRLF. A lone carriage return still renders as a line break, so
        // leaving it unnormalized lets a \r-separated message defeat the row cap entirely: Split('\n') sees one
        // row where the reader sees hundreds.
        string normalized = value!.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
        bool truncated = false;

        if (normalized.Length > maxLength)
        {
            int clipLength = maxLength;
            if (clipLength > 0
                && char.IsHighSurrogate(normalized[clipLength - 1])
                && char.IsLowSurrogate(normalized[clipLength]))
            {
                clipLength--;
            }

            normalized = normalized.Substring(0, clipLength).TrimEnd();
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
    /// <param name="budget">
    /// The shared budget this section draws expanded diagnostics from. Passed rather than a raw count so a
    /// caller rendering several project sections into one file shares one allowance across them — the 1 MiB cap
    /// applies to the whole file, not to any one section.
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
        SummaryBudget budget)
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

            // Reserved in UTF-8 bytes, because that is what the budget is denominated in and what GitHub counts.
            // Charging UTF-16 chars would under-bill a failure carrying Japanese text or emoji by up to
            // threefold, so a project would overshoot its share and force the whole rendering to be refused —
            // trading a section that degrades gracefully for one that is dropped.
            if (!budget.TryReserveDetails(Encoding.UTF8.GetByteCount(detailsBuilder.ToString())))
            {
                // The remaining budget cannot fit this failure's diagnostics. Degrade to the compact line rather
                // than emitting a half-written <details> block, and account for it in the truncation note below.
                omittedDetails++;
                AppendCompactEntry(builder, entry);
                continue;
            }

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

        // Say which failures lost their diagnostics, so a reader who sees a bare list knows the difference between
        // "this failure had nothing more to show" and "the summary ran out of room for it".
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

    /// <summary>
    /// Formats a duration the way this reporter renders them, so the failure list and the slowest-test list
    /// agree.
    /// </summary>
    internal static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");

    /// <summary>
    /// Makes <paramref name="value"/> safe to place inside a single-backtick markdown span: a backtick would
    /// close the span early, and a newline would end it entirely.
    /// </summary>
    internal static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    /// <summary>
    /// Encodes <paramref name="value"/> for a context where markdown allows raw HTML — notably
    /// <c>&lt;summary&gt;</c>, where a generic test name like <c>T.Map&lt;string,int&gt;</c> would otherwise
    /// parse as a tag and swallow the rest of the line.
    /// </summary>
    internal static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}

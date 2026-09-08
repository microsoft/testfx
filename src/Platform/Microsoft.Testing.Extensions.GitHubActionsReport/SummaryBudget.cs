// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// How much of a test project's results the summary still has room to render.
/// </summary>
/// <remarks>
/// The stages are ordered by what they give up, cheapest first. Shedding the diagnostics before the failure list
/// is the whole design: the list of which tests failed is a line per failure, while the diagnostics behind it are
/// kilobytes, so the list survives long after the diagnostics stop.
/// </remarks>
internal enum SummaryStage
{
    /// <summary>A full section, with failures expanded into collapsible diagnostics.</summary>
    Full,

    /// <summary>A full section, but with failures listed as name and duration only.</summary>
    NoDetails,

    /// <summary>A one-line verdict for the whole test project.</summary>
    Condensed,

    /// <summary>Not rendered at all; reported only as part of a count of what was left out.</summary>
    Unlisted,
}

/// <summary>
/// The single place that decides how much of a summary may still be written, and in what shape.
/// </summary>
/// <remarks>
/// GitHub caps a job summary at 1 MiB and discards an oversized one <em>in full</em> rather than truncating it,
/// so the report has to degrade on its own before it reaches the cap. That policy is the same whether one test
/// project is appending to a file its siblings share, or a post-processor is rendering every module of a
/// <c>dotnet test</c> run into one file — so both drive it from here rather than each implementing it again.
/// <para>
/// Everything is denominated in UTF-8 bytes, because that is what GitHub counts and what a file length reports.
/// A UTF-16 char count understates a summary carrying assertion diffs, exception messages, non-Latin test names
/// or the per-project status emoji by up to threefold, which is the difference between a summary that renders
/// and one that is thrown away.
/// </para>
/// </remarks>
internal sealed class SummaryBudget
{
    private long _consumedBytes;
    private long _detailBytesAvailable;
    private long _detailBytesUngranted;

    private SummaryBudget(long consumedBytes, long detailBytesAvailable, long detailBytesUngranted)
    {
        _consumedBytes = consumedBytes;
        _detailBytesAvailable = Math.Max(0, detailBytesAvailable);
        _detailBytesUngranted = Math.Max(0, detailBytesUngranted);
    }

    /// <summary>
    /// Gets the shape the next test project may be rendered in, given everything consumed so far.
    /// </summary>
    /// <remarks>
    /// <see cref="SummaryStage.NoDetails"/> reports that the detail allowance is spent while the file still has
    /// room for a full section. Whether any <em>individual</em> failure keeps its diagnostics is still decided one
    /// failure at a time by <see cref="TryReserveDetails(int)"/>, so a project whose allowance runs out midway
    /// expands what it could afford rather than dropping the lot.
    /// </remarks>
    internal SummaryStage Stage
        => _consumedBytes >= GitHubActionsFailureDetails.StopListingLength ? SummaryStage.Unlisted
            : _consumedBytes >= GitHubActionsFailureDetails.CondenseLength ? SummaryStage.Condensed
            : _detailBytesAvailable <= 0 && _detailBytesUngranted <= 0 ? SummaryStage.NoDetails
            : SummaryStage.Full;

    /// <summary>
    /// Gets the UTF-8 bytes of expanded failure detail available to spend right now.
    /// </summary>
    internal int DetailBytesAvailable => (int)Math.Min(_detailBytesAvailable, int.MaxValue);

    /// <summary>
    /// Creates the budget for a single test project appending to the shared <c>GITHUB_STEP_SUMMARY</c> file.
    /// </summary>
    /// <param name="alreadyWrittenBytes">
    /// The size of the shared file, measured while holding the writer's lock so it cannot change before the
    /// section this budget sizes is written.
    /// </param>
    /// <remarks>
    /// A test project runs in its own process and cannot know how many siblings will run, or whether it is first
    /// or last. It can, however, observe the shared file: whatever is already there is a lower bound on the space
    /// consumed. Claiming only the remainder — minus a reserve for this project's own headings, tables and
    /// failure lines — keeps the whole file near the detail share regardless of project count. There are no
    /// shares to hand out here, because this budget covers exactly one project.
    /// </remarks>
    internal static SummaryBudget ForProject(long alreadyWrittenBytes)
        => new(
            alreadyWrittenBytes,
            Math.Min(
                GitHubActionsFailureDetails.DetailBudgetLength - alreadyWrittenBytes - GitHubActionsFailureDetails.ProjectOverheadReserve,
                GitHubActionsFailureDetails.MaxTotalDetailsLength),
            detailBytesUngranted: 0);

    /// <summary>
    /// Creates the budget for a combined <c>dotnet test</c> rendering covering <paramref name="moduleCount"/>
    /// test projects, with <paramref name="consumedBytes"/> already accounted for — whatever the shared file
    /// holds plus this rendering's own run-level preamble.
    /// </summary>
    /// <remarks>
    /// The detail allowance is divided across the modules rather than granted to each, because the cap applies to
    /// the whole file. Each module's non-detail overhead — heading, totals table, failure lines — is reserved
    /// before dividing, so the rendered file lands near the detail share rather than that much detail
    /// <em>plus</em> the overhead. Nothing is available until <see cref="GrantModuleShare(int)"/> hands out a
    /// share, which is what stops an early module with many large failures starving the rest.
    /// </remarks>
    internal static SummaryBudget ForAggregate(long consumedBytes, int moduleCount)
        => new(
            consumedBytes,
            detailBytesAvailable: 0,
            GitHubActionsFailureDetails.DetailBudgetLength - consumedBytes - ((long)Math.Max(1, moduleCount) * GitHubActionsFailureDetails.ProjectOverheadReserve));

    /// <summary>
    /// Makes one module's share of the detail allowance available to spend, keeping whatever earlier modules
    /// left unspent.
    /// </summary>
    internal void GrantModuleShare(int remainingModuleCount)
    {
        long share = Math.Min(_detailBytesUngranted / Math.Max(1, remainingModuleCount), _detailBytesUngranted);
        _detailBytesUngranted -= share;
        _detailBytesAvailable += share;
    }

    /// <summary>
    /// Accounts for rendered content that was not reserved — headings, tables, verdict lines.
    /// </summary>
    internal void Consume(int bytes)
        => _consumedBytes += bytes;

    /// <summary>
    /// Reserves <paramref name="bytes"/> of expanded failure detail, returning <see langword="false"/> — and
    /// reserving nothing — when the allowance cannot cover it.
    /// </summary>
    /// <remarks>
    /// All-or-nothing on purpose: a partially reserved detail block would be a half-written
    /// <c>&lt;details&gt;</c> element, which renders as broken markup rather than as a shortened report.
    /// <para>
    /// Only the allowance is drawn down here. The reserved bytes end up in the same builder the caller measures,
    /// so charging them to the consumed total as well would count them twice and push the report past its
    /// degradation thresholds at roughly half the real file size.
    /// </para>
    /// </remarks>
    internal bool TryReserveDetails(int bytes)
    {
        if (bytes > _detailBytesAvailable)
        {
            return false;
        }

        _detailBytesAvailable -= bytes;
        return true;
    }
}

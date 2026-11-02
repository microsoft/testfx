// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class StepSummaryWriter
{
    /// <summary>
    /// Appends <paramref name="content"/> to the shared <c>GITHUB_STEP_SUMMARY</c> file in a way that is safe
    /// when multiple test-host processes (one per assembly / target framework in a <c>dotnet test</c> run) write
    /// concurrently.
    /// </summary>
    /// <remarks>
    /// <see cref="FileMode.Append"/> only seeks to the end of the file once, at open time, and performs no
    /// atomic OS-level append. Opening with <see cref="FileShare.ReadWrite"/> would therefore let two processes
    /// position at the same offset and interleave or overwrite each other's section. We instead open with
    /// <see cref="FileShare.Read"/> — which denies other writers — so at most one process appends at a time, and
    /// retry on the resulting sharing violation (an <see cref="IOException"/>) until the holder releases the file.
    /// Each write is a single small section, so contention clears almost immediately; the bounded attempt count
    /// still lets a genuinely unlockable file surface as the caller's best-effort warning rather than looping
    /// forever.
    /// <para>
    /// Retries are scoped to <em>acquiring</em> the exclusive append handle only. Once the handle is acquired the
    /// process appends alone, so contention can no longer occur; a failure that happens <em>during</em> the write
    /// (e.g. disk full) may already have appended a partial section, and retrying would re-append the full section
    /// on top of it and corrupt the summary. Such a mid-write failure is therefore propagated straight to the
    /// caller's best-effort warning path instead of being retried.
    /// </para>
    /// <para>
    /// <paramref name="maxTotalBytes"/> bounds the size the file may reach. It is checked here, under the lock and
    /// against the handle this process holds, rather than by the caller before the call: two sibling projects that
    /// each measured the file before acquiring the lock would both see the same length, both conclude they fit, and
    /// both append — landing over GitHub's cap, which discards the whole summary. The caller's pre-check remains a
    /// cheap fast path; this one is the decision.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the content was appended; <see langword="false"/> if appending it would have taken
    /// the file past <paramref name="maxTotalBytes"/>, in which case nothing was written.
    /// </returns>
    internal async Task<bool> AppendStepSummaryWithRetryAsync(
        string content,
        CancellationToken cancellationToken,
        long maxTotalBytes = long.MaxValue)
    {
        // Taken even for a plain append: the notice-hoisting path replaces the whole file, and an append that
        // slipped between its read and its swap would be silently overwritten.
        using IFileStream lockStream = await AcquireSummaryLockAsync(cancellationToken).ConfigureAwait(false);
        return await AppendCoreAsync(content, cancellationToken, maxTotalBytes).ConfigureAwait(false);
    }

    /// <summary>
    /// The body of <see cref="AppendStepSummaryWithRetryAsync"/>, minus the lock, so a caller that already holds
    /// it can render and write in one transaction.
    /// </summary>
    private async Task<bool> AppendCoreAsync(
        string content,
        CancellationToken cancellationToken,
        long maxTotalBytes)
    {
        var encoding = new UTF8Encoding(false);
        int contentByteCount = encoding.GetByteCount(content);

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream stream;
            try
            {
                stream = _fileSystem.NewFileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (IOException) when (attempt < _maxAttempts)
            {
                // Another test-host process currently holds the summary file open for writing. Back off briefly
                // and retry so this assembly's section is appended intact once the holder releases the file.
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // The exclusive append handle is acquired: from here on we append alone, so any failure is a genuine
            // write error (not contention) and must not be retried — a partial append followed by a full re-append
            // would corrupt the summary. Let it propagate to the caller's best-effort warning path.
            using (stream)
            {
                // A stream that cannot report its length cannot be gated; that only happens in tests, and writing
                // is the behaviour that matters there.
                if (stream.Stream.CanSeek && stream.Stream.Length + contentByteCount > maxTotalBytes)
                {
                    return false;
                }

                using var writer = new StreamWriter(stream.Stream, encoding);
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            return true;
        }
    }

    /// <summary>
    /// Renders this project's section and writes it in a single locked transaction, so the size of the shared
    /// file that the rendering decisions are based on cannot change between the decision and the write.
    /// </summary>
    /// <remarks>
    /// The <c>render</c> callback produces the markdown for a given observed file length and says whether the
    /// top-of-file notice is warranted; it is invoked once, while this process holds the lock.
    /// <para>
    /// Gating the already-rendered payload is not enough. The reporter degrades in stages as the shared file
    /// fills up — full diagnostics, then a bare failure list, then a one-line verdict — and those thresholds are
    /// what reserve headroom for the test framework's own block, which is appended after this reporter has run
    /// and which this reporter cannot bound. Deciding before taking the lock lets every project that finishes at
    /// the same moment observe the same empty file and each render a full section: the absolute cap then admits
    /// the first few and refuses the rest outright, turning a report where every project degrades gracefully
    /// into one where the first projects get everything and the others get nothing.
    /// </para>
    /// </remarks>
    internal async Task<bool> AppendRenderedStepSummarySectionAsync(
        Func<long, (string Markdown, bool IncludeNotice)> render,
        Func<int, string> noticeFactory,
        CancellationToken cancellationToken,
        long maxTotalBytes = long.MaxValue)
    {
        using IFileStream lockStream = await AcquireSummaryLockAsync(cancellationToken).ConfigureAwait(false);

        // Measured under the lock, so this is the length the write will actually land on.
        long currentLength = GetSummaryLength() ?? 0;
        (string markdown, bool includeNotice) = render(currentLength);

        return includeNotice
            ? await AppendWithLeadingNoticeCoreAsync(markdown, noticeFactory, cancellationToken, maxTotalBytes).ConfigureAwait(false)
            : await AppendCoreAsync(markdown, cancellationToken, maxTotalBytes).ConfigureAwait(false);
    }
}

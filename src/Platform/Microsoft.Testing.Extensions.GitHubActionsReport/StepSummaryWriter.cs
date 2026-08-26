// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Every write this extension makes to the shared <c>GITHUB_STEP_SUMMARY</c> file.
/// </summary>
/// <remarks>
/// The file is shared: one test-host process per assembly and target framework appends to it, the aggregated
/// <c>dotnet test</c> post-processor rewrites its own section in it, and other steps and test frameworks append
/// their own content. Every access therefore needs the same file system, path, retry policy and lock, which is
/// what this type holds so its methods can take only what actually varies between calls.
/// </remarks>
internal sealed class StepSummaryWriter
{
    /// <summary>
    /// Ceiling on how much of the shared summary file this writer will read into memory in one go.
    /// </summary>
    /// <remarks>
    /// Callers that pass no explicit bound would otherwise size a buffer from a file other producers control.
    /// Sixty-four megabytes is far above any summary GitHub would accept — it discards anything over 1 MB — and
    /// far below the point where the allocation itself is the failure.
    /// </remarks>
    private const long MaxReadableSummaryBytes = 64L * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;

    internal StepSummaryWriter(IFileSystem fileSystem, string path, ILogger logger, int maxAttempts, TimeSpan retryDelay)
    {
        _fileSystem = fileSystem;
        Path = path;
        _logger = logger;
        _maxAttempts = maxAttempts;
        _retryDelay = retryDelay;
    }

    /// <summary>
    /// Gets the path of the shared summary file, for diagnostics that name it.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// The lock every writer to the shared summary file takes for the duration of its update.
    /// </summary>
    /// <remarks>
    /// Opening the summary itself exclusively is enough to serialize plain appends, but not an update that has to
    /// replace the file: a file cannot be replaced while it is open, so the handle must be released before the
    /// swap, and a sibling appending in that gap would have its section overwritten by content captured before it.
    /// A separate lock file closes that window because it is held across the whole read-modify-replace, and it is
    /// the same lock the aggregated path uses, so the two writing modes serialize against each other too.
    /// </remarks>
    private string GetSummaryLockPath()
        => Path + ".microsoft-testing-platform.lock";

    /// <summary>
    /// Acquires <see cref="GetSummaryLockPath"/>, retrying while another writer holds it.
    /// </summary>
    private async Task<IFileStream> AcquireSummaryLockAsync(
        CancellationToken cancellationToken)
    {
        string lockPath = GetSummaryLockPath();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return _fileSystem.NewFileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < _maxAttempts)
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

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
    /// Appends <paramref name="content"/> to the shared <c>GITHUB_STEP_SUMMARY</c> file, having first placed the
    /// note built by <c>noticeFactory</c> at the very top of that file if it is not there already. The factory
    /// receives the number of full test project sections already in the file, and is invoked while this process
    /// holds the file exclusively so the count cannot change under it.
    /// </summary>
    /// <remarks>
    /// The note belongs at the top for two reasons. It is the first thing the reader sees, which is what a warning
    /// that the report is incomplete deserves; and nothing can displace it, because every writer to this file —
    /// this reporter, sibling test projects, and the test framework's own summary block — only ever appends. A note
    /// placed at the end is only last until the next append, so it cannot be kept there.
    /// <para>
    /// Hoisting the note rewrites the file, so it is done once: every later project finds the marker already
    /// present and simply appends. The count it quotes stays correct without being rewritten, because a project is
    /// only shortened once the file is past the condense threshold, and from that point on no further full
    /// sections are added — so the number of them can no longer change.
    /// </para>
    /// <para>
    /// <paramref name="maxTotalBytes"/> is checked here rather than by the caller, for the same reason the plain
    /// append path checks it here: the file is measured while this process holds it, so a sibling cannot have
    /// grown it since.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the file was updated; <see langword="false"/> if the result would have exceeded
    /// <paramref name="maxTotalBytes"/>, in which case the file is left untouched.
    /// </returns>
    internal async Task<bool> AppendStepSummaryWithLeadingNoticeAsync(
        string content,
        Func<int, string> noticeFactory,
        CancellationToken cancellationToken,
        long maxTotalBytes = long.MaxValue)
    {
        // Held across the whole read-modify-replace. The summary handle alone cannot cover it, because the file
        // has to be closed before it can be replaced, and a sibling appending in that gap would be overwritten.
        using IFileStream lockStream = await AcquireSummaryLockAsync(cancellationToken).ConfigureAwait(false);
        return await AppendWithLeadingNoticeCoreAsync(content, noticeFactory, cancellationToken, maxTotalBytes).ConfigureAwait(false);
    }

    /// <summary>
    /// The body of <see cref="AppendStepSummaryWithLeadingNoticeAsync"/>, minus the lock, so a caller that
    /// already holds it can render and write in one transaction.
    /// </summary>
    private async Task<bool> AppendWithLeadingNoticeCoreAsync(
        string content,
        Func<int, string> noticeFactory,
        CancellationToken cancellationToken,
        long maxTotalBytes)
    {
        var encoding = new UTF8Encoding(false);

        bool appendOnly = false;
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] pendingPayload;
            long lengthAtCapture;

            IFileStream stream;
            try
            {
                stream = _fileSystem.NewFileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < _maxAttempts)
            {
                // Another test-host process currently holds the summary file. Back off briefly and retry, exactly
                // as the plain append path does, so this project's section is written intact once it is released.
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (stream)
            {
                Stream inner = stream.Stream;

                // The file is shared with producers this extension does not control, so its size is not ours to
                // trust. Reading it before checking would let one of them decide how much memory this process
                // allocates — up to the ~2 GiB the old int.MaxValue clamp permitted, which takes the test host
                // down with an OutOfMemoryException. Nothing this method can write fits once the existing content
                // alone is over the bound, so refuse before allocating for it.
                long existingLength = inner.Length;
                if (existingLength > maxTotalBytes || existingLength > MaxReadableSummaryBytes)
                {
                    return false;
                }

                byte[] existingBytes = new byte[(int)existingLength];
                inner.Seek(0, SeekOrigin.Begin);
                int totalRead = 0;
                while (totalRead < existingBytes.Length)
                {
                    int read = await inner.ReadAsync(existingBytes, totalRead, existingBytes.Length - totalRead, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                string existing = encoding.GetString(existingBytes, 0, totalRead);

                // The note already there only wins if it describes a loss at least as bad as this one. Two
                // different losses share one marker so the summary never carries contradictory warnings, but a
                // weaker "diagnostics were dropped" note must not suppress a later "whole sections were removed"
                // note — that would leave results missing with nothing saying so.
                string pendingNotice = noticeFactory(CountProjectSections(existing));
                if (appendOnly || GetLeadingNoticeStrength(existing) >= GetLeadingNoticeStrength(pendingNotice))
                {
                    // Nothing to hoist or upgrade, so this is a plain append and nothing existing is at risk.
                    byte[] appended = encoding.GetBytes(content);
                    if (totalRead + appended.Length > maxTotalBytes)
                    {
                        return false;
                    }

                    inner.Seek(0, SeekOrigin.End);
                    if (appended.Length > 0)
                    {
                        await inner.WriteAsync(appended, 0, appended.Length, cancellationToken).ConfigureAwait(false);
                    }

                    await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                }

                // Hoisting or upgrading the notice means replacing the file, which is the one operation here that
                // can destroy content: everything earlier projects wrote only survives if the replacement
                // completes. Rewriting in place would leave the summary garbled — or, if it were truncated first,
                // empty — when the write is abandoned midway on a full disk or during session teardown. Build the
                // new content in a temporary file and swap it in instead, so the summary is only ever replaced by
                // a complete file.
                pendingPayload = encoding.GetBytes(pendingNotice + StripLeadingTruncationNotice(existing) + content);
                if (pendingPayload.Length > maxTotalBytes)
                {
                    return false;
                }

                // Measured while this process still holds the file exclusively, so it describes exactly the
                // content the payload was built from.
                lengthAtCapture = inner.Length;
            }

            string tempPath = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (IFileStream tempStream = _fileSystem.NewFileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    await tempStream.Stream.WriteAsync(pendingPayload, 0, pendingPayload.Length, cancellationToken).ConfigureAwait(false);
                    await tempStream.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                // The handle has to be released before the swap, because a file that is open cannot be replaced,
                // and this extension's lock only covers its own writers — a test framework appending its own block
                // in that gap is not excluded by it. Re-measure and start over if the file moved, so an append
                // that landed during staging is carried into the next attempt instead of being overwritten by a
                // snapshot taken before it.
                if (GetSummaryLength() is long lengthBeforeSwap && lengthBeforeSwap != lengthAtCapture)
                {
                    if (attempt < _maxAttempts)
                    {
                        continue;
                    }

                    // Out of attempts and the file is still moving under us. Swapping now would delete whatever
                    // landed while this payload was staged, so stop trying to hoist the notice and just append this
                    // project's section on one more pass. An understated leading notice is recoverable — the next
                    // writer re-evaluates it — whereas another writer's deleted block is not.
                    appendOnly = true;
                    continue;
                }

                // Past this point the replacement is complete on disk, so the swap either happens or it does not;
                // the summary is never left half-written.
                _fileSystem.ReplaceFile(tempPath, Path);
            }
            finally
            {
                try
                {
                    _fileSystem.DeleteFile(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup must not hide a successful write or its primary failure.
                }
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

    /// <summary>
    /// Measures the shared summary file, or returns <see langword="null"/> when it does not exist or cannot be
    /// read. Best-effort by design: the length sizes a budget and explains a refusal, so failing to read it must
    /// degrade the report rather than fail the run.
    /// </summary>
    internal long? GetSummaryLength()
    {
        try
        {
            if (!_fileSystem.ExistFile(Path))
            {
                return null;
            }

            using IFileStream stream = _fileSystem.NewFileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Stream.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Could not measure '{Path}': {ex.Message}");
            }

            return null;
        }
    }

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

    /// <summary>
    /// Measures the shared summary file, discounting the section this run is about to replace.
    /// </summary>
    /// <remarks>
    /// Reprocessing writes over its own previous output rather than adding to it, so counting that output as
    /// occupied space would make a re-run condense or drop modules that fit perfectly well once the old block
    /// is gone.
    /// </remarks>
    internal long GetSummaryLengthExcludingSection(string aggregationId)
    {
        try
        {
            if (!_fileSystem.ExistFile(Path))
            {
                return 0;
            }

            string existing;
            using (IFileStream stream = _fileSystem.NewFileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                // Discounting our own section means reading the file, and its size is set by producers this
                // extension does not control. Past the ceiling, report the raw length rather than reading: it
                // over-states the occupied space only by the size of this run's own previous block, and it makes
                // the caller degrade or refuse instead of allocating whatever another producer happened to write.
                long length = stream.Stream.Length;
                if (length > MaxReadableSummaryBytes)
                {
                    return length;
                }

                using var reader = new StreamReader(stream.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                existing = reader.ReadToEnd();
            }

            int start = IndexOfMarkerLine(existing, BuildSectionStartMarker(aggregationId));
            if (start < 0)
            {
                return Encoding.UTF8.GetByteCount(existing);
            }

            string endMarker = BuildSectionEndMarker(aggregationId);
            int end = IndexOfMarkerLine(existing, endMarker, start);
            return end < 0
                ? Encoding.UTF8.GetByteCount(existing)
                : Encoding.UTF8.GetByteCount(existing.Remove(start, end + endMarker.Length - start));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Could not measure '{Path}': {ex.Message}");
            }

            return 0;
        }
    }

    private static string BuildSectionStartMarker(string aggregationId)
        => $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:start -->";

    private static string BuildSectionEndMarker(string aggregationId)
        => $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:end -->";

    /// <summary>
    /// Enumerates the lines of <paramref name="content"/> that are <em>not</em> inside a fenced code block,
    /// yielding each line's start offset and its text.
    /// </summary>
    /// <remarks>
    /// Rendered failure messages and stack traces are copied verbatim into fenced blocks, so anything this
    /// extension uses as a structural marker can also appear there as ordinary user-controlled text. Every
    /// structural scan of the summary therefore has to skip fenced content, or a test could forge a marker.
    /// Fences are chosen longer than the longest backtick run in the body they wrap, so a fence only closes on a
    /// run at least as long as the one that opened it.
    /// </remarks>
    private static IEnumerable<(int Start, string Line)> EnumerateUnfencedLines(string content)
    {
        int fenceLength = 0;
        int start = 0;
        while (start <= content.Length)
        {
            int newline = content.IndexOf('\n', start);
            int end = newline < 0 ? content.Length : newline;
            string line = content.Substring(start, end - start).TrimEnd('\r');

            int backticks = 0;
            while (backticks < line.Length && line[backticks] == '`')
            {
                backticks++;
            }

            if (fenceLength == 0)
            {
                if (backticks >= 3)
                {
                    fenceLength = backticks;
                }
                else
                {
                    yield return (start, line);
                }
            }
            else if (backticks >= fenceLength && line.Length == backticks)
            {
                fenceLength = 0;
            }

            if (newline < 0)
            {
                yield break;
            }

            start = newline + 1;
        }
    }

    /// <summary>
    /// Returns the offset of the first line equal to <paramref name="marker"/> that is not inside a fenced code
    /// block, or <c>-1</c>.
    /// </summary>
    private static int IndexOfMarkerLine(string content, string marker, int searchFrom = 0)
    {
        foreach ((int start, string line) in EnumerateUnfencedLines(content))
        {
            if (start >= searchFrom && string.Equals(line, marker, StringComparison.Ordinal))
            {
                return start;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts the full test project sections this extension has written to the shared summary file.
    /// </summary>
    /// <remarks>
    /// Only a marker occupying a whole line outside a fenced block counts, otherwise a test could inflate the
    /// project count the truncation note reports simply by printing the marker in its failure output.
    /// </remarks>
    internal static int CountProjectSections(string summary)
    {
        int count = 0;
        foreach ((_, string line) in EnumerateUnfencedLines(summary))
        {
            if (string.Equals(line, GitHubActionsSummaryReporter.ProjectSectionMarker, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Replaces (or inserts) this run's section in the shared <c>GITHUB_STEP_SUMMARY</c> file.
    /// </summary>
    /// <remarks>
    /// The leading-notice factory is given the number of per-project sections already in the file and is evaluated
    /// under the writer lock, against the file as it actually stands. The count belongs in the note, and a job can
    /// mix this aggregated writer with the direct per-project one across steps, so a note counting only this run's
    /// modules would understate how much of the summary is fully reported.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the file was updated; <see langword="false"/> if the result would have exceeded
    /// <paramref name="maxTotalBytes"/>, in which case the file is left untouched so the caller can retry with a
    /// smaller rendering.
    /// </returns>
    internal async Task<bool> UpsertStepSummaryWithRetryAsync(
        string aggregationId,
        string content,
        CancellationToken cancellationToken,
        Func<int, string>? leadingNoticeFactory = null,
        long maxTotalBytes = long.MaxValue)
    {
        string startMarker = BuildSectionStartMarker(aggregationId);
        string endMarker = BuildSectionEndMarker(aggregationId);
        string section = $"{startMarker}\n{content.TrimEnd()}\n{endMarker}\n";
        // Keep one stable lock entry for the lifetime of the GitHub step. Deleting it after releasing the handle
        // would let a third writer create a new inode while a second writer still holds the unlinked old lock.
        // This is the same lock the per-project path takes, so the two writing modes serialize against each other.
        string lockPath = GetSummaryLockPath();

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream lockStream;
            try
            {
                lockStream = _fileSystem.NewFileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (attempt < _maxAttempts)
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string tempPath = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (lockStream)
                {
                    string existing;
                    long lengthAtCapture;
                    using (IFileStream summaryStream = _fileSystem.NewFileStream(
                        Path,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(summaryStream.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        // Same reason as the append path: another producer's output decides this size, so refuse
                        // before reading rather than allocating whatever it happens to be.
                        lengthAtCapture = summaryStream.Stream.Length;
                        if (lengthAtCapture > maxTotalBytes || lengthAtCapture > MaxReadableSummaryBytes)
                        {
                            return false;
                        }
#if NET8_0_OR_GREATER
                        existing = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016 // The target framework has no cancellation-aware StreamReader overload.
                        existing = await reader.ReadToEndAsync().ConfigureAwait(false);
#pragma warning restore CA2016
#endif
                    }

                    // Both markers are written on lines of their own, and are matched that way — outside fenced
                    // blocks. The section now carries rendered failure messages, so a test can print the exact
                    // end-marker text inside its diagnostics; matching it anywhere would end the block early and
                    // leave its stale tail behind on the next upsert.
                    //
                    // The project-section count is taken with this run's own section excised, so it counts only
                    // what other writers contributed. The caller adds back whatever this run reports in full, and
                    // that stays right on a re-run, where the file still holds the previous copy of this section.
                    int otherProjectSections;
                    int start = IndexOfMarkerLine(existing, startMarker);
                    if (start >= 0)
                    {
                        int end = IndexOfMarkerLine(existing, endMarker, start);
                        if (end < 0)
                        {
                            throw new FormatException("The existing GitHub step summary contains an incomplete Microsoft Testing Platform summary section.");
                        }

                        string withoutOwnSection = existing.Remove(start, end + endMarker.Length - start);
                        otherProjectSections = CountProjectSections(withoutOwnSection);
                        existing = withoutOwnSection.Insert(start, section.TrimEnd());
                    }
                    else
                    {
                        otherProjectSections = CountProjectSections(existing);
                        existing = existing.Length == 0
                            ? section
                            : existing.TrimEnd() + "\n\n" + section;
                    }

                    // Put the warning at the very top, where the reader meets it before the results it qualifies.
                    // The two writing modes share one marker so a summary can never carry two of them, but a note
                    // already there only wins if it describes a loss at least as bad as this one — otherwise a
                    // weaker note would suppress a stronger one and the summary would never say results are
                    // missing.
                    string? leadingNotice = leadingNoticeFactory?.Invoke(otherProjectSections);
                    if (!RoslynString.IsNullOrWhiteSpace(leadingNotice)
                        && GetLeadingNoticeStrength(existing) < GetLeadingNoticeStrength(leadingNotice!))
                    {
                        existing = leadingNotice + StripLeadingTruncationNotice(existing);
                    }

                    // Measured in bytes, under the lock, against the content that is actually about to be written.
                    // GitHub discards an oversized summary in full — including every section other steps wrote — so
                    // leaving the file as it stands and letting the caller render something smaller is strictly
                    // better than replacing it with a file that will be thrown away.
                    if (Encoding.UTF8.GetByteCount(existing) > maxTotalBytes)
                    {
                        return false;
                    }

                    using (IFileStream tempStream = _fileSystem.NewFileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(tempStream.Stream, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(existing).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016 // The target framework has no cancellation-aware StreamWriter overload.
                        await writer.FlushAsync().ConfigureAwait(false);
#pragma warning restore CA2016
#endif
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // Hold an exclusive handle across validation and replacement, closing the final check/swap
                    // window where another .NET writer could append content that the replacement would lose.
                    // Windows needs delete sharing so File.Replace can swap the open path; Unix permits renaming
                    // an open file, and FileShare.None is what asks the runtime for an exclusive advisory lock.
                    FileShare validationShare = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? FileShare.Delete
                        : FileShare.None;
                    IFileStream validationStream;
                    try
                    {
                        validationStream = _fileSystem.NewFileStream(
                            Path,
                            FileMode.OpenOrCreate,
                            FileAccess.Read,
                            validationShare);
                    }
                    catch (IOException) when (attempt < _maxAttempts)
                    {
                        await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    using (validationStream)
                    {
                        if (validationStream.Stream.Length != lengthAtCapture)
                        {
                            if (attempt < _maxAttempts)
                            {
                                continue;
                            }

                            // There is no append-only fallback for an upsert: it would duplicate this run's section.
                            // Preserve the foreign output and surface the persistent contention as a write failure
                            // instead of misreporting it as a size-limit refusal.
                            throw new IOException($"The GitHub step summary '{Path}' kept changing while its replacement was staged.");
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        _fileSystem.ReplaceFile(tempPath, Path);
                    }
                }
            }
            finally
            {
                try
                {
                    _fileSystem.DeleteFile(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup must not hide a successful write or its primary failure.
                }
            }

            return true;
        }
    }
}

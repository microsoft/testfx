// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
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
    /// </remarks>
    internal static /* for testing */ async Task AppendStepSummaryWithRetryAsync(
        IFileSystem fileSystem,
        string path,
        string content,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream stream;
            try
            {
                stream = fileSystem.NewFileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // Another test-host process currently holds the summary file open for writing. Back off briefly
                // and retry so this assembly's section is appended intact once the holder releases the file.
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // The exclusive append handle is acquired: from here on we append alone, so any failure is a genuine
            // write error (not contention) and must not be retried — a partial append followed by a full re-append
            // would corrupt the summary. Let it propagate to the caller's best-effort warning path.
            using (stream)
            using (var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            return;
        }
    }

    /// <summary>
    /// Appends <paramref name="content"/> and then leaves <paramref name="notice"/> as the closing note of the
    /// shared <c>GITHUB_STEP_SUMMARY</c> file, keeping exactly one copy of that note however many test projects
    /// write while the file is over budget.
    /// </summary>
    /// <remarks>
    /// Every test project that runs after the summary budget is exhausted wants to say the same thing — that the
    /// report stops short on purpose — so appending the note unconditionally would repeat it once per project and
    /// spend the little headroom that is left on saying it. Instead the note is moved: whatever this reporter (or
    /// a sibling project) left behind is lifted, together with anything a co-writer appended after it, and put back
    /// after the new content. The summary therefore carries exactly one note, and it is the last thing the report
    /// says rather than a marker stranded wherever the budget first ran out.
    /// <para>
    /// Because the note is moved on every write it never drifts far from the tail, so the span rewritten here stays
    /// small — a few kilobytes of co-writer output — rather than growing with the summary.
    /// </para>
    /// </remarks>
    internal static /* for testing */ async Task AppendStepSummaryWithTrailingNoticeAsync(
        IFileSystem fileSystem,
        string path,
        string content,
        string notice,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var encoding = new UTF8Encoding(false);
        byte[] noticeBytes = encoding.GetBytes(notice);

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream stream;
            try
            {
                stream = fileSystem.NewFileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // Another test-host process currently holds the summary file. Back off briefly and retry, exactly
                // as the plain append path does, so this project's section is written intact once it is released.
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (stream)
            {
                Stream inner = stream.Stream;

                string existing;
                int existingLength = (int)Math.Min(inner.Length, int.MaxValue);
                byte[] existingBytes = new byte[existingLength];
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

                existing = encoding.GetString(existingBytes, 0, totalRead);

                // Lift the note this reporter left behind earlier, along with whatever was appended after it, so it
                // can be put back last. Because the note is moved on every write it stays near the tail, so the span
                // rewritten here is a few kilobytes rather than the whole summary.
                string displacedTail = string.Empty;
                int noticeIndex = existing.IndexOf(TruncationNoticeMarker, StringComparison.Ordinal);
                if (noticeIndex >= 0
                    && noticeIndex + notice.Length <= existing.Length
                    && string.CompareOrdinal(existing, noticeIndex, notice, 0, notice.Length) == 0)
                {
                    displacedTail = existing.Substring(noticeIndex + notice.Length);
                    inner.SetLength(encoding.GetByteCount(existing.Substring(0, noticeIndex)));
                }

                inner.Seek(0, SeekOrigin.End);
                byte[] pending = encoding.GetBytes(displacedTail + content);
                if (pending.Length > 0)
                {
                    await inner.WriteAsync(pending, 0, pending.Length, cancellationToken).ConfigureAwait(false);
                }

                await inner.WriteAsync(noticeBytes, 0, noticeBytes.Length, cancellationToken).ConfigureAwait(false);
                await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }
    }

    internal static async Task UpsertStepSummaryWithRetryAsync(
        IFileSystem fileSystem,
        string path,
        string aggregationId,
        string content,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        string startMarker = $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:start -->";
        string endMarker = $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:end -->";
        string section = $"{startMarker}\n{content.TrimEnd()}\n{endMarker}\n";
        // Keep one stable lock entry for the lifetime of the GitHub step. Deleting it after releasing the handle
        // would let a third writer create a new inode while a second writer still holds the unlinked old lock.
        string lockPath = path + ".microsoft-testing-platform.lock";

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream lockStream;
            try
            {
                lockStream = fileSystem.NewFileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (lockStream)
                {
                    string existing;
                    using (IFileStream summaryStream = fileSystem.NewFileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(summaryStream.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
#if NET8_0_OR_GREATER
                        existing = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016 // The target framework has no cancellation-aware StreamReader overload.
                        existing = await reader.ReadToEndAsync().ConfigureAwait(false);
#pragma warning restore CA2016
#endif
                    }

                    int start = existing.IndexOf(startMarker, StringComparison.Ordinal);
                    if (start >= 0)
                    {
                        int end = existing.IndexOf(endMarker, start, StringComparison.Ordinal);
                        if (end < 0)
                        {
                            throw new FormatException("The existing GitHub step summary contains an incomplete Microsoft Testing Platform summary section.");
                        }

                        existing = existing.Remove(start, end + endMarker.Length - start).Insert(start, section.TrimEnd());
                    }
                    else
                    {
                        existing = existing.Length == 0
                            ? section
                            : existing.TrimEnd() + "\n\n" + section;
                    }

                    using (IFileStream tempStream = fileSystem.NewFileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
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
                    fileSystem.ReplaceFile(tempPath, path);
                }
            }
            finally
            {
                try
                {
                    fileSystem.DeleteFile(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup must not hide a successful write or its primary failure.
                }
            }

            return;
        }
    }
}

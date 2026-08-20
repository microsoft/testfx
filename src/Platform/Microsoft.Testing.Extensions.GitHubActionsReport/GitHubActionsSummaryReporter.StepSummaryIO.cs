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
    /// </remarks>
    internal static /* for testing */ async Task AppendStepSummaryWithLeadingNoticeAsync(
        IFileSystem fileSystem,
        string path,
        string content,
        Func<int, string> noticeFactory,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var encoding = new UTF8Encoding(false);

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

                byte[] existingBytes = new byte[(int)Math.Min(inner.Length, int.MaxValue)];
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
                bool noticeAlreadyPresent = existing.IndexOf(TruncationNoticeMarker, StringComparison.Ordinal) >= 0;

                byte[] payload = noticeAlreadyPresent
                    ? encoding.GetBytes(content)
                    : encoding.GetBytes(noticeFactory(CountProjectSections(existing)) + existing + content);

                // Rewriting from the start is only correct because the note is hoisted at most once per job; from
                // then on this is a plain append.
                if (noticeAlreadyPresent)
                {
                    inner.Seek(0, SeekOrigin.End);
                }
                else
                {
                    inner.Seek(0, SeekOrigin.Begin);
                    inner.SetLength(0);
                }

                if (payload.Length > 0)
                {
                    await inner.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                }

                await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }
    }

    /// <summary>
    /// Counts the full test project sections this extension has written to the shared summary file.
    /// </summary>
    internal static /* for testing */ int CountProjectSections(string summary)
    {
        int count = 0;
        int index = summary.IndexOf(ProjectSectionMarker, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = summary.IndexOf(ProjectSectionMarker, index + ProjectSectionMarker.Length, StringComparison.Ordinal);
        }

        return count;
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

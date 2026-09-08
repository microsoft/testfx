// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class StepSummaryWriter
{
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
}

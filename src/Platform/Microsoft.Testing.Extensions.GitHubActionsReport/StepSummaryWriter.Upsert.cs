// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class StepSummaryWriter
{
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

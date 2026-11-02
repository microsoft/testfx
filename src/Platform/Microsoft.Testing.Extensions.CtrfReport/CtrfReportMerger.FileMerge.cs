// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// File-based entry points: read the input reports from disk, delegate to the in-memory
/// <see cref="Merge(IReadOnlyList{string}, CtrfMergeMode)"/>, and write the result out.
/// </summary>
internal static partial class CtrfReportMerger
{
    internal static Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken)
        => MergeToFileAsync(inputPaths, outputPath, CtrfMergeMode.Concatenate, cancellationToken);

    internal static Task MergeAllToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken)
        => MergeToFileAsync(inputPaths, outputPath, CtrfMergeMode.Concatenate, requireAllReports: true, cancellationToken);

    internal static Task MergeAllToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CtrfMergeMode mode,
        CancellationToken cancellationToken)
        => MergeToFileAsync(inputPaths, outputPath, mode, requireAllReports: true, cancellationToken);

    internal static Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CtrfMergeMode mode,
        CancellationToken cancellationToken)
        => MergeToFileAsync(inputPaths, outputPath, mode, requireAllReports: false, cancellationToken);

    private static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CtrfMergeMode mode,
        bool requireAllReports,
        CancellationToken cancellationToken)
    {
        if (inputPaths is null)
        {
            throw new ArgumentNullException(nameof(inputPaths));
        }

        if (outputPath is null)
        {
            throw new ArgumentNullException(nameof(outputPath));
        }

        // Reject an empty input list before any filesystem work (Merge throws for empty input, but only
        // after the output directory would already have been created).
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("At least one CTRF report is required to merge.", nameof(inputPaths));
        }

        // RFC 018 treats per-module inputs as read-only and requires them to remain on disk; reject an
        // output that aliases an input so a merge can never overwrite one of its own sources.
        MergeOutputFileHelper.EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

        var reports = new List<string>(inputPaths.Count);
        foreach (string inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NETCOREAPP
            reports.Add(await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false));
#else
            reports.Add(File.ReadAllText(inputPath));
#endif
        }

        string merged = Merge(reports, mode, requireAllReports, nameof(inputPaths));

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Write to a temporary sibling, then replace the destination ENTRY, so a symlink/hardlink output
        // alias of an input has only its link removed rather than the read-only source truncated in place.
        await MergeOutputFileHelper.WriteViaTemporarySiblingAsync(outputPath, async tempPath =>
        {
#if NETCOREAPP
            await File.WriteAllTextAsync(tempPath, merged, cancellationToken).ConfigureAwait(false);
#else
            File.WriteAllText(tempPath, merged);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }).ConfigureAwait(false);
    }
}

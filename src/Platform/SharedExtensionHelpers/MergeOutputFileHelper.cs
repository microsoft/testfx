// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared, security- and data-loss-sensitive filesystem helpers for report mergers (TRX/JUnit/CTRF).
/// Extracted so the read-only-input alias check and the atomic temporary-sibling write cannot diverge
/// across formats: RFC 018 treats per-module inputs as read-only, so a merge must never overwrite one of
/// its own sources.
/// </summary>
internal static class MergeOutputFileHelper
{
    /// <summary>
    /// Rejects an output path that resolves to one of the input report paths. Paths are canonicalized
    /// (symlinks resolved where the runtime supports it) and compared using the case sensitivity probed at
    /// the output's own location, so a differently-cased or symlinked-parent output that aliases an input
    /// is detected while a legitimately distinct output on a case-sensitive volume is not falsely rejected.
    /// On runtimes that cannot resolve links, it fails closed when an output ancestor is a reparse point.
    /// </summary>
    internal static void EnsureOutputDoesNotAliasInput(IReadOnlyList<string> inputPaths, string outputPath)
    {
#if !NETCOREAPP
        // This runtime cannot resolve symlinks/junctions, so a symlinked output parent directory that
        // aliases an input directory would slip past the textual comparison below and let the write
        // replace the input. Fail closed when any existing ancestor of the output is a reparse point.
        if (HasReparsePointAncestor(outputPath))
        {
            throw new ArgumentException($"The output path '{outputPath}' has a symbolic-link parent directory that cannot be safely resolved on this runtime; refusing to write to avoid overwriting a read-only input.", nameof(outputPath));
        }
#endif
        string outputCanonical = GetCanonicalPath(outputPath);
        StringComparison comparison = GetOutputPathComparison(outputPath);
        foreach (string inputPath in inputPaths)
        {
            if (string.Equals(GetCanonicalPath(inputPath), outputCanonical, comparison))
            {
                throw new ArgumentException($"The output path '{outputPath}' cannot be one of the input report paths; inputs are treated as read-only.", nameof(outputPath));
            }
        }
    }

    /// <summary>
    /// Writes the merged report by invoking <paramref name="writeToTempAsync"/> against a temporary sibling
    /// of <paramref name="outputPath"/> and then replacing the destination entry. If the output path is a
    /// symlink/hardlink alias of an input (which the textual alias check cannot detect because
    /// <see cref="Path.GetFullPath(string)"/> does not resolve links), replacing the entry removes only the
    /// link and leaves the read-only source intact, rather than truncating it in place.
    /// </summary>
    internal static async Task WriteViaTemporarySiblingAsync(string outputPath, Func<string, Task> writeToTempAsync)
    {
        string tempPath = GetTempSiblingPath(outputPath);
        try
        {
            await writeToTempAsync(tempPath).ConfigureAwait(false);
            ReplaceFile(tempPath, outputPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    /// <summary>
    /// Canonicalizes <paramref name="path"/> to a full path with symlinks/junctions resolved in every
    /// existing component (so a symlinked parent directory that aliases another location is detected). On
    /// runtimes without link resolution (netstandard/.NET Framework) it falls back to the lexical full
    /// path.
    /// </summary>
    private static string GetCanonicalPath(string path)
    {
        string full = Path.GetFullPath(path);
#if NETCOREAPP
        try
        {
            return ResolveSymlinks(full, remainingHops: 40);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return full;
        }
#else
        return full;
#endif
    }

#if NETCOREAPP
    // Resolves every symlink in 'full' to a stable canonical path — including symlinks in the ANCESTOR
    // directories of a link's target — so two paths that name the same file compare equal regardless of
    // which symlinked route reaches it. A single per-component pass is not enough: when a link's target
    // string itself embeds another symlink (e.g. macOS '/var' -> '/private/var', so a link stored as
    // '/var/.../real' resolves to a path whose '/var' prefix is still a link), the canonical form would
    // otherwise depend on the route taken and the read-only-input alias check would miss an aliased output.
    // Each time a link is followed we therefore recurse from the (absolute) target so its own ancestors are
    // canonicalized too. 'remainingHops' bounds recursion so a symlink cycle degrades to the lexical path
    // instead of looping forever.
    private static string ResolveSymlinks(string full, int remainingHops)
    {
        string? root = Path.GetPathRoot(full);
        if (remainingHops <= 0 || RoslynString.IsNullOrEmpty(root))
        {
            return full;
        }

        string[] parts = full.Substring(root.Length).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        string resolved = root;
        for (int i = 0; i < parts.Length; i++)
        {
            string next = Path.Combine(resolved, parts[i]);
            FileSystemInfo? entry = Directory.Exists(next)
                ? new DirectoryInfo(next)
                : File.Exists(next)
                    ? new FileInfo(next)
                    : null;

            // Follow a single hop only; recursion re-canonicalizes the target's ancestors and any further
            // links in the chain uniformly.
            if (entry?.ResolveLinkTarget(returnFinalTarget: false)?.FullName is { } linkTarget)
            {
                string remainder = string.Join(Path.DirectorySeparatorChar.ToString(), parts, i + 1, parts.Length - (i + 1));
                string combined = remainder.Length == 0 ? linkTarget : Path.Combine(linkTarget, remainder);
                return ResolveSymlinks(Path.GetFullPath(combined), remainingHops - 1);
            }

            resolved = next;
        }

        return resolved;
    }
#endif

    // Whether two paths name the SAME file depends on the case sensitivity of the specific filesystem/
    // directory that will hold the output — which can differ by volume and, on Windows, per-directory — so
    // this is probed at the output's OWN location (nearest existing ancestor) rather than a fixed temp dir.
    // On a case-insensitive location 'a.trx' and 'A.trx' are the same file and must collide; on a
    // case-sensitive one they are distinct, so a case-insensitive check would wrongly reject a legitimate
    // separate output. An alias can only occur when the two paths share a directory, so that shared
    // location's sensitivity is the correct one (a different-directory input never compares equal).
    private static StringComparison GetOutputPathComparison(string outputPath)
    {
        string? probeDirectory = FindNearestExistingDirectory(outputPath);
        return probeDirectory is not null && IsDirectoryCaseSensitive(probeDirectory)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
    }

    private static string? FindNearestExistingDirectory(string path)
    {
        string? current = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!RoslynString.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            string? parent = Path.GetDirectoryName(current);
            if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    private static bool IsDirectoryCaseSensitive(string directory)
    {
        try
        {
            string probeName = "CASESENSITIVEPROBE" + Guid.NewGuid().ToString("N");
            string probePath = Path.Combine(directory, probeName);
            using (new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
            {
                return !File.Exists(BuildCaseFoldedProbePath(directory, probeName));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // If the probe fails, assume case-insensitive-but-preserving (the safer, more conservative
            // choice for the alias check: it rejects more, never less).
            return false;
        }
    }

    // Builds the case-folded candidate used to detect case sensitivity: ONLY the generated probe file name
    // is lower-cased, while the real 'directory' path is preserved verbatim. Lower-casing the whole combined
    // path (the previous bug) corrupts the directory portion, so a case-insensitive child directory sitting
    // beneath a case-sensitive, differently-cased ancestor (e.g. an ext4 casefold dir named with uppercase
    // chars) would be probed at a non-existent lowercased ancestor: File.Exists returns false and the
    // location is misreported as case-sensitive. Kept as its own seam so this behaviour can be unit-tested
    // without needing to materialize a mixed-sensitivity filesystem.
    internal static string BuildCaseFoldedProbePath(string directory, string probeFileName)
        => Path.Combine(directory, probeFileName.ToLowerInvariant());

    private static string GetTempSiblingPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) is { Length: > 0 } dir
            ? dir
            : Directory.GetCurrentDirectory();
        return Path.Combine(directory, Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    private static void ReplaceFile(string tempPath, string outputPath)
    {
#if NETCOREAPP
        // Atomic replace. File.Move(overwrite: true) maps to rename(2) / MoveFileEx(REPLACE_EXISTING),
        // which swaps the destination in a single step, so an interruption or competing writer can never
        // leave the previous report permanently missing (the failure mode of a delete-then-move sequence).
        // It also replaces a symlink or dangling-link ENTRY rather than following it — only the link is
        // swapped, never its target's content — and an exact alias of an input has already been rejected,
        // so this cannot truncate an input in place.
        File.Move(tempPath, outputPath, overwrite: true);
#else
        // .NET Framework's File.Move has no atomic-overwrite overload. Fall back to delete-then-move. On
        // this runtime the alias check has already fail-closed on any reparse-point ancestor of the output,
        // and File.Delete removes only a link entry (never a link target's content), so this cannot delete
        // an input in place; deleting a dangling link (for which File.Exists is false yet the entry exists)
        // also lets the subsequent File.Move succeed.
        File.Delete(outputPath);
        File.Move(tempPath, outputPath);
#endif
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort temp cleanup: leaking a .tmp sibling is preferable to masking the primary
            // exception (or the successful result) with a cleanup failure.
        }
    }

#if !NETCOREAPP
    private static bool HasReparsePointAncestor(string path)
    {
        string? current = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!RoslynString.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return false;
    }
#endif
}

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
            string? root = Path.GetPathRoot(full);
            if (RoslynString.IsNullOrEmpty(root))
            {
                return full;
            }

            string resolved = root;
            foreach (string part in full.Substring(root.Length).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                string next = Path.Combine(resolved, part);
                resolved = Directory.Exists(next)
                    ? new DirectoryInfo(next).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next
                    : File.Exists(next)
                        ? new FileInfo(next).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next
                        : next;
            }

            return resolved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return full;
        }
#else
        return full;
#endif
    }

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
            string upper = Path.Combine(directory, "CASESENSITIVEPROBE" + Guid.NewGuid().ToString("N"));
            using (new FileStream(upper, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
            {
                return !File.Exists(upper.ToLowerInvariant());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // If the probe fails, assume case-insensitive-but-preserving (the safer, more conservative
            // choice for the alias check: it rejects more, never less).
            return false;
        }
    }

    private static string GetTempSiblingPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) is { Length: > 0 } dir
            ? dir
            : Directory.GetCurrentDirectory();
        return Path.Combine(directory, Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    private static void ReplaceFile(string tempPath, string outputPath)
    {
        // Delete any destination entry unconditionally — a regular file, or a symlink/hardlink alias
        // (including a DANGLING symlink, for which File.Exists is false yet the entry still exists and
        // would make File.Move fail). File.Delete is a no-op when nothing exists, and deleting a link
        // removes only the link (never its target's content); an exact alias of an input has already been
        // rejected, so this cannot delete an input in place.
        File.Delete(outputPath);

        File.Move(tempPath, outputPath);
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    /// <summary>
    /// Returns <see langword="true"/> if the relative <paramref name="relativePath"/> resolves above its
    /// own root (i.e. a <c>..</c> segment pops past the start), meaning it would escape the confined
    /// deployment directory once resolved.
    /// </summary>
    private static bool EscapesRoot(string relativePath)
    {
        int depth = 0;
        foreach (string segment in relativePath.Split('/', '\\'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == "..")
            {
                depth--;
                if (depth < 0)
                {
                    return true;
                }
            }
            else
            {
                depth++;
            }
        }

        return false;
    }

    private static bool IsUnderDirectory(string candidateFullPath, string rootDirectory)
    {
        string rootFull = Path.GetFullPath(rootDirectory);
        string rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        StringComparison comparison = PathComparison;
        return string.Equals(candidateFullPath, rootFull, comparison)
            || candidateFullPath.StartsWith(rootWithSeparator, comparison);
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

    /// <summary>
    /// Returns <see langword="true"/> if any existing directory component strictly below
    /// <paramref name="baseDirectory"/> up to and including <paramref name="candidateFullPath"/> is a
    /// reparse point (symlink/junction). Both paths are expected to be normalized full paths with
    /// <paramref name="candidateFullPath"/> under <paramref name="baseDirectory"/>. A symlinked ancestor
    /// can redirect reads/writes outside a lexically-confined path, so callers reject such trees.
    /// </summary>
    private static bool HasReparsePointComponent(string candidateFullPath, string baseDirectory)
    {
        string baseFull = Path.GetFullPath(baseDirectory);
        string current = candidateFullPath;

        while (!string.Equals(current, baseFull, PathComparison) && IsUnderDirectory(current, baseFull))
        {
            if (Directory.Exists(current) && IsReparsePoint(current))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (RoslynString.IsNullOrEmpty(parent) || string.Equals(parent, current, PathComparison))
            {
                break;
            }

            current = parent;
        }

        return false;
    }

    // PathComparison gates *containment* decisions (IsUnderDirectory / HasReparsePointComponent confining
    // a source or destination under a root, and the equal-roots skip). Containment MUST be ordinal
    // (case-sensitive): on a case-sensitive filesystem a hostile deployment root such as '../foo' resolves
    // to the case-distinct sibling '/tmp/foo' of an input under '/tmp/Foo', and a case-insensitive check
    // would wrongly accept it as confined and read the sibling's attachments. Treating case-distinct paths
    // as different only makes containment MORE restrictive (skip/drop) — never an escape.
    //
    // The output-alias EQUALITY check is a different concern (two names for the SAME file) and is compared
    // case-insensitively on canonicalized paths in EnsureOutputDoesNotAliasInput, independently of this.
    private static StringComparison PathComparison => StringComparison.Ordinal;
}

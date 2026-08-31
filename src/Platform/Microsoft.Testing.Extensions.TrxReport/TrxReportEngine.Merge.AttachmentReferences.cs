// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    /// <summary>
    /// Relocates (or, when <paramref name="relocate"/> is false, validates in place) the per-test
    /// <c>ResultFile</c> references of a single <c>UnitTestResult</c>. Consumers resolve them under
    /// <c>In/&lt;relativeResultsDirectory&gt;/&lt;path&gt;</c>; when relocating, each referenced file is
    /// copied to <c>In/&lt;prefix&gt;/&lt;relativeResultsDirectory&gt;/&lt;path&gt;</c> and the directory
    /// is prefixed once. A reference that is rooted, escapes the root, or whose source file was not
    /// materialized (missing or symlinked) is dropped in either mode.
    /// </summary>
    private static void RelocateResultFilesForUnitTestResult(XElement unitTestResult, string prefix, string sourceInRoot, string mergedInRoot, bool relocate, CancellationToken cancellationToken)
    {
        List<XElement> resultFiles = [.. unitTestResult.Descendants().Where(e => e.Name.LocalName == "ResultFile")];
        if (resultFiles.Count == 0)
        {
            return;
        }

        XAttribute? relativeDirectory = unitTestResult.Attribute("relativeResultsDirectory");
        string relativeDirectoryValue = relativeDirectory?.Value ?? string.Empty;

        if (relativeDirectory is null || RoslynString.IsNullOrEmpty(relativeDirectoryValue))
        {
            // No owning directory: each path resolves at In/<path>, like a collector href.
            foreach (XElement resultFile in resultFiles)
            {
                RelocateReference(resultFile, "path", relativeDirectory: null, prefix, sourceInRoot, mergedInRoot, relocate, cancellationToken);
            }

            return;
        }

        // A rooted owning directory makes every ResultFile resolve to an absolute path; RFC 018 keeps
        // absolute attachment paths resolvable, so preserve them unchanged (not relocated, not dropped).
        if (Path.IsPathRooted(relativeDirectoryValue))
        {
            return;
        }

        // Copy (or validate) each referenced file. A rooted per-test path is absolute and preserved
        // unchanged; a relative one that escapes the root or is not materialized is dropped; the rest are
        // relocated. Prefix the directory once if any relative reference survived (only when relocating).
        bool anyKept = false;
        foreach (XElement resultFile in resultFiles)
        {
            XAttribute? pathAttribute = resultFile.Attribute("path");
            string path = pathAttribute?.Value ?? string.Empty;
            if (Path.IsPathRooted(path))
            {
                // Absolute path: preserved as-is, independent of the (relative) owning directory.
                continue;
            }

            string combined = relativeDirectoryValue + "/" + path;
            if (EscapesRoot(combined) || !TryMaterializeOrValidateReference(sourceInRoot, mergedInRoot, prefix, combined, relocate, cancellationToken))
            {
                resultFile.Remove();
            }
            else
            {
                anyKept = true;
                if (relocate && pathAttribute is not null)
                {
                    // Normalize the kept path's separators to '/' so the reference resolves cross-platform,
                    // matching the separator-normalized copy destination.
                    pathAttribute.Value = path.Replace('\\', '/');
                }
            }
        }

        if (anyKept && relocate)
        {
            relativeDirectory.Value = prefix + "/" + relativeDirectoryValue.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Relocates (or validates in place) the file referenced by <paramref name="attributeName"/>. A rooted
    /// (absolute) value is preserved unchanged — RFC 018 keeps absolute attachment paths resolvable — while
    /// a value that escapes the deployment root, or a source file that was not materialized (missing, or a
    /// skipped symlink), causes the reference to be dropped. When <paramref name="relocate"/> is true a
    /// kept relative file is copied into the per-input folder and the reference is prefixed (separators
    /// normalized to '/'); otherwise it is left unchanged.
    /// </summary>
    private static void RelocateReference(XElement element, string attributeName, string? relativeDirectory, string prefix, string sourceInRoot, string mergedInRoot, bool relocate, CancellationToken cancellationToken)
    {
        XAttribute? attribute = element.Attribute(attributeName);
        if (attribute is null || RoslynString.IsNullOrEmpty(attribute.Value))
        {
            return;
        }

        // RFC 018 requires attachment URIs to survive merging and treats current absolute TRX paths as
        // already resolvable, so a rooted value is preserved as-is (not relocated, not dropped).
        if (Path.IsPathRooted(attribute.Value))
        {
            return;
        }

        string relative = relativeDirectory is null ? attribute.Value : relativeDirectory + "/" + attribute.Value;
        if (EscapesRoot(relative) || !TryMaterializeOrValidateReference(sourceInRoot, mergedInRoot, prefix, relative, relocate, cancellationToken))
        {
            RemoveReferenceElement(element);
            return;
        }

        if (relocate)
        {
            // Emit with forward slashes so a reference relocated from a Windows-produced TRX resolves on
            // Unix (and vice versa), matching the separator-normalized copy destination.
            attribute.Value = prefix + "/" + attribute.Value.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Validates a single referenced attachment file under <paramref name="sourceInRoot"/> and, when
    /// <paramref name="relocate"/> is true, copies it to the per-input folder under
    /// <paramref name="mergedInRoot"/>. Returns <see langword="false"/> (so the caller drops the reference)
    /// when the source file is missing, is a symlink, sits behind a symlinked component, or when either
    /// resolved path escapes its root. <paramref name="relative"/> is already known to be non-rooted and
    /// non-escaping.
    /// </summary>
    private static bool TryMaterializeOrValidateReference(string sourceInRoot, string mergedInRoot, string prefix, string relative, bool relocate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalized = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string sourceFile = Path.GetFullPath(Path.Combine(sourceInRoot, normalized));

        // Refuse a source that escapes its root, is missing, is a symlink, or sits behind a symlinked
        // component, so a link cannot redirect the read outside the confined tree.
        if (!IsUnderDirectory(sourceFile, sourceInRoot)
            || !File.Exists(sourceFile)
            || IsReparsePoint(sourceFile)
            || HasReparsePointComponent(Path.GetDirectoryName(sourceFile)!, sourceInRoot))
        {
            return false;
        }

        if (!relocate)
        {
            // Equal-roots: the file already lives at the merged deployment root; validated, nothing to copy.
            return true;
        }

        string destinationFile = Path.GetFullPath(Path.Combine(mergedInRoot, prefix, normalized));
        if (!IsUnderDirectory(destinationFile, mergedInRoot)
            || HasReparsePointComponent(Path.GetDirectoryName(destinationFile)!, mergedInRoot))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

        // Remove ANY pre-existing destination entry — a regular file, a symlink (including a DANGLING one,
        // which File.Exists reports as missing because it follows the link), or a hardlink — then copy
        // WITHOUT overwrite. This guarantees a fresh file is written rather than a link being followed, or a
        // hardlink's shared target (which lives outside the merged root) being overwritten in place.
        DeleteDestinationEntry(destinationFile);

        File.Copy(sourceFile, destinationFile, overwrite: false);
        return true;
    }

    /// <summary>
    /// Deletes any existing directory entry at <paramref name="path"/> — a regular file, a symlink/junction
    /// (including a dangling one, which <see cref="File.Exists(string)"/> reports as missing because it
    /// follows the link), or a hardlink — so a subsequent copy writes a fresh file instead of following a
    /// link out of, or overwriting a hardlink's shared target outside, the confined root. A missing entry
    /// is left untouched.
    /// </summary>
    private static void DeleteDestinationEntry(string path)
    {
#if NETCOREAPP
        // FileInfo.Exists is target-based (a dangling link reads as not-existing) while LinkTarget is
        // non-null for any link entry, so together they cover regular files, hardlinks, and (dangling) links.
        var info = new FileInfo(path);
        if (info.Exists || info.LinkTarget is not null)
        {
            info.Delete();
        }
#else
        if (File.Exists(path))
        {
            File.Delete(path);
        }
#endif
    }
}

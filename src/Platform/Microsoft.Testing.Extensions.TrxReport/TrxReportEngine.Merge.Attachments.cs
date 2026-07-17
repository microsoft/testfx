// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    /// <summary>
    /// Copies each input report's <em>referenced</em> attachment files into a per-input isolated folder
    /// under the merged report's deployment root and rewrites that input's references to match, so
    /// attachments carried into the merged TRX keep resolving without one module's files shadowing
    /// another's. Only the files actually referenced by the report are copied (never a whole directory
    /// tree), which keeps the operation non-destructive (inputs stay read-only, RFC 018), bounded (no
    /// recursion or unbounded accumulation when the output is nested inside a source), and confined
    /// (<c>runName</c> and each input's <c>runDeploymentRoot</c> are attacker-influenced). Any copy
    /// failure is swallowed so it never fails the merge (never-fail-the-run invariant); a reference that
    /// cannot be relocated is dropped rather than left dangling or pointing outside the deployment root.
    /// </summary>
    private static void RelocateAttachments(IReadOnlyList<string> inputPaths, IReadOnlyList<XDocument> reports, string outputDirectory, Guid runId, string runName, CancellationToken cancellationToken)
    {
        string outputFull = Path.GetFullPath(outputDirectory);
        string mergedDeploymentRoot = GetConfinedDeploymentRootLeaf(runId, runName);
        string mergedRootFull = Path.GetFullPath(Path.Combine(outputFull, mergedDeploymentRoot));

        // The confined leaf can no longer be '.' or '..', but keep the defensive lexical/reparse checks:
        // reject if the deployment root escapes the output directory or any component below it is a
        // reparse point (a symlink/junction there would resolve writes outside the confined root). We
        // cannot set up a safe merged root, so drop every input's references before bailing — otherwise
        // they would be emitted unchanged and resolve through the unsafe root.
        if (!IsUnderDirectory(mergedRootFull, outputFull) || HasReparsePointComponent(mergedRootFull, outputFull))
        {
            DropAllReferences(reports);
            return;
        }

        string mergedInRoot = Path.Combine(mergedRootFull, "In");

        // On a reused output directory the merged 'In' root could pre-exist as a junction/symlink that
        // would redirect every copy below it outside the confined merged root. Remove such a link so a
        // fresh, real directory is created instead.
        if (Directory.Exists(mergedInRoot) && IsReparsePoint(mergedInRoot))
        {
            try
            {
                Directory.Delete(mergedInRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The unsafe link remains; drop every input's references so none resolve through it.
                DropAllReferences(reports);
                return;
            }
        }

        for (int i = 0; i < inputPaths.Count && i < reports.Count; i++)
        {
            // Cancellation must interrupt an otherwise long sequence of file copies; check before each
            // input (and inside TryCopyReferencedFile) and let OperationCanceledException propagate rather
            // than being swallowed as a best-effort failure below.
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? inputDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPaths[i]));
                string? inputDeploymentRoot = reports[i].Root is { } root
                    ? FindChild(root, "TestSettings")?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Deployment", StringComparison.Ordinal))?.Attribute("runDeploymentRoot")?.Value
                    : null;

                if (RoslynString.IsNullOrEmpty(inputDirectory) || RoslynString.IsNullOrEmpty(inputDeploymentRoot))
                {
                    // No resolvable source for this input's attachments. Its references are relative to the
                    // input's own deployment root, so carrying them unchanged into the merged report (which
                    // has a different deployment root) would dangle — drop them.
                    DropAllAttachmentReferences(reports[i]);
                    continue;
                }

                // runDeploymentRoot comes straight from the input TRX; a rooted value or '..' segments
                // could make the source tree escape the input report directory. Reject those, and also
                // reject when any component from the input directory down to the source 'In' root is a
                // reparse point (a symlink/junction there could redirect the read outside the input tree
                // even though the lexical confinement check passes).
                string sourceInRoot = Path.GetFullPath(Path.Combine(inputDirectory, inputDeploymentRoot, "In"));
                if (!IsUnderDirectory(sourceInRoot, inputDirectory)
                    || !Directory.Exists(sourceInRoot)
                    || HasReparsePointComponent(sourceInRoot, inputDirectory))
                {
                    // The source cannot be safely relocated; drop this input's references so none dangle
                    // against the merged deployment root.
                    DropAllAttachmentReferences(reports[i]);
                    continue;
                }

                // Equal roots: the source files already live at the merged deployment root under their
                // original relative paths, so references are kept un-prefixed. Still validate them
                // (dropping rooted/traversal references, and any pointing at a missing or symlinked file)
                // so the path-confined contract holds even when nothing is copied.
                if (string.Equals(sourceInRoot, mergedInRoot, PathComparison))
                {
                    RelocateReferencedAttachments(reports[i], prefix: string.Empty, sourceInRoot, mergedInRoot, relocate: false, cancellationToken);
                    continue;
                }

                // Copy only the files the report references (never the whole tree), so an output nested
                // inside a source neither recurses into its own destination nor accumulates across retries.
                RelocateReferencedAttachments(reports[i], i.ToString(CultureInfo.InvariantCulture), sourceInRoot, mergedInRoot, relocate: true, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Best-effort: a failed attachment copy (including a malformed path from a hostile
                // runDeploymentRoot, which Path.GetFullPath surfaces as ArgumentException/
                // NotSupportedException) must not fail the merge. Cancellation is not caught here.
                // Relocation may have stopped part-way, leaving some references un-rewritten; drop all of
                // this input's references so none dangle against the merged deployment root.
                DropAllAttachmentReferences(reports[i]);
            }
        }
    }

    /// <summary>
    /// Removes every attachment reference from all reports. Used when the merged deployment root cannot be
    /// set up safely and the whole relocation is abandoned, so no report keeps references that would
    /// resolve through the unsafe root.
    /// </summary>
    private static void DropAllReferences(IReadOnlyList<XDocument> reports)
    {
        foreach (XDocument report in reports)
        {
            DropAllAttachmentReferences(report);
        }
    }

    /// <summary>
    /// Removes the relative attachment references (<c>&lt;A&gt;</c> and <c>&lt;ResultFile&gt;</c>) from a
    /// report. Used when an input's attachments cannot be relocated (missing/invalid source, or a failed
    /// copy), so the merged report never carries a reference that would resolve against the merged
    /// deployment root to a file that was never placed there. Rooted (absolute) references are preserved —
    /// they resolve independently of the deployment root (RFC 018 keeps absolute paths resolvable).
    /// </summary>
    private static void DropAllAttachmentReferences(XDocument report)
    {
        if (report.Root is not { } root)
        {
            return;
        }

        foreach (XElement element in root.Descendants().Where(e => e.Name.LocalName is "A" or "ResultFile").ToList())
        {
            if (!IsRootedReference(element))
            {
                RemoveReferenceElement(element);
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if an attachment reference resolves to an absolute path (and so is
    /// preserved even when relocation is abandoned): a rooted <c>href</c>/<c>path</c>, or a
    /// <c>&lt;ResultFile&gt;</c> whose owning <c>UnitTestResult</c> has a rooted
    /// <c>relativeResultsDirectory</c>.
    /// </summary>
    private static bool IsRootedReference(XElement element)
    {
        if (element.Name.LocalName == "A")
        {
            return element.Attribute("href")?.Value is { } href && Path.IsPathRooted(href);
        }

        // ResultFile: rooted if its own path is rooted, or its owning UnitTestResult's directory is rooted.
        if (element.Attribute("path")?.Value is { } path && Path.IsPathRooted(path))
        {
            return true;
        }

        XElement? owningResult = element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "UnitTestResult");
        return owningResult?.Attribute("relativeResultsDirectory")?.Value is { } directory && Path.IsPathRooted(directory);
    }

    /// <summary>
    /// Removes a dropped attachment reference. A collector <c>&lt;A&gt;</c> takes its owning
    /// <c>&lt;UriAttachment&gt;</c> with it (the TRX schema requires exactly one <c>&lt;A&gt;</c> child, so
    /// leaving an empty <c>&lt;UriAttachment&gt;</c> would be invalid); a <c>&lt;ResultFile&gt;</c> is
    /// removed alone (an empty <c>&lt;ResultFiles&gt;</c> is schema-valid).
    /// </summary>
    private static void RemoveReferenceElement(XElement element)
    {
        if (element.Name.LocalName == "A" && element.Parent is { } parent && string.Equals(parent.Name.LocalName, "UriAttachment", StringComparison.Ordinal))
        {
            parent.Remove();
        }
        else
        {
            element.Remove();
        }
    }

    private static void RelocateReferencedAttachments(XDocument report, string prefix, string sourceInRoot, string mergedInRoot, bool relocate, CancellationToken cancellationToken)
    {
        if (report.Root is not { } root)
        {
            return;
        }

        // Attachment references come in two shapes with different resolution rules:
        //   * Collector attachments '<A href="...">' (and a standalone '<ResultFile path="...">' from a
        //     foreign producer) are relative to the deployment 'In' root, so the file is copied to
        //     In/<prefix>/<value> and the value is prefixed.
        //   * Per-test '<ResultFile path="...">' under a UnitTestResult are resolved by consumers under
        //     In/<owning @relativeResultsDirectory>/<path>, so the file is copied to
        //     In/<prefix>/<relativeResultsDirectory>/<path> and the *directory* is prefixed once.
        // Only referenced files are copied (never a whole tree), and a reference that is rooted, escapes
        // the deployment root, or whose source file was not materialized is dropped. When
        // <paramref name="relocate"/> is false (equal-roots case: the source already lives at the merged
        // deployment root) references are validated and dropped the same way, but kept un-prefixed and
        // nothing is copied.
        // Materialize each pass first: a dropped reference is removed, and removing while enumerating the
        // lazy Descendants() sequence would be unsafe.
        foreach (XElement collectorAttachment in root.Descendants().Where(e => e.Name.LocalName == "A").ToList())
        {
            RelocateReference(collectorAttachment, "href", relativeDirectory: null, prefix, sourceInRoot, mergedInRoot, relocate, cancellationToken);
        }

        foreach (XElement unitTestResult in root.Descendants().Where(e => e.Name.LocalName == "UnitTestResult").ToList())
        {
            RelocateResultFilesForUnitTestResult(unitTestResult, prefix, sourceInRoot, mergedInRoot, relocate, cancellationToken);
        }

        foreach (XElement standaloneResultFile in root.Descendants()
            .Where(e => e.Name.LocalName == "ResultFile" && e.Ancestors().All(a => a.Name.LocalName != "UnitTestResult"))
            .ToList())
        {
            RelocateReference(standaloneResultFile, "path", relativeDirectory: null, prefix, sourceInRoot, mergedInRoot, relocate, cancellationToken);
        }
    }
}

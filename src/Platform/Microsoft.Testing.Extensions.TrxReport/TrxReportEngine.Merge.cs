// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    /// <summary>
    /// Merges several already-produced TRX reports into a single TRX document.
    /// </summary>
    /// <remarks>
    /// This is a pure, invocation-agnostic XML-level merge: it does no I/O and reads no clock, so a
    /// user-facing merge tool and an SDK-orchestrated post-processor can share it and, given the same
    /// <paramref name="runId"/>/<paramref name="runName"/> and inputs, produce equivalent output. The
    /// only non-deterministic element is the freshly generated <c>TestSettings</c> id.
    /// <para>
    /// Merge rules:
    /// <list type="bullet">
    ///   <item><description><c>Results</c> and <c>TestEntries</c> are unioned as-is; <c>TestDefinitions</c> are deduplicated by <c>id</c> (ids are derived deterministically from each test's UID and the schema forbids duplicates).</description></item>
    ///   <item><description><c>TestLists</c> are deduplicated by <c>id</c> (the well-known lists are shared across files).</description></item>
    ///   <item><description><c>Counters</c> attributes are summed; <c>Times</c> use the earliest start and latest finish.</description></item>
    ///   <item><description><c>RunInfos</c> (crash/exit diagnostics) and <c>CollectorDataEntries</c> (attachment references) are carried across from every input's <c>ResultSummary</c>.</description></item>
    ///   <item><description>The result summary outcome is <c>Failed</c> if any input failed, otherwise <c>Completed</c>.</description></item>
    ///   <item><description>Attachment hrefs inside <c>CollectorDataEntries</c> are carried as-is; because they are relative to each input's deployment root, the physical attachment files are only relocated to the merged deployment root by <see cref="MergeToFileAsync"/> (which has the source paths). Callers of the in-memory <see cref="Merge"/> that need resolvable attachments should relocate them separately.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal static XDocument Merge(IReadOnlyList<XDocument> inputReports, Guid runId, string runName)
    {
        if (runName is null)
        {
            throw new ArgumentNullException(nameof(runName));
        }

        if (inputReports is null)
        {
            throw new ArgumentNullException(nameof(inputReports));
        }

        if (inputReports.Count == 0)
        {
            throw new ArgumentException("At least one TRX report is required to merge.", nameof(inputReports));
        }

        var mergedResults = new XElement(NamespaceUri + "Results");
        var mergedTestDefinitions = new XElement(NamespaceUri + "TestDefinitions");
        var mergedTestEntries = new XElement(NamespaceUri + "TestEntries");
        var mergedTestLists = new XElement(NamespaceUri + "TestLists");
        var seenTestListIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // TestDefinition ids are derived deterministically from each test's UID, so the same test
        // discovered in more than one input yields the same id. The TRX schema (and the producer,
        // see TrxReportEngine.Results.cs) does not allow duplicate <UnitTest id="...">, so we keep
        // only the first definition seen per id.
        var seenTestDefinitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Run-level diagnostics (<RunInfos>) and collector attachments (<CollectorDataEntries>) live
        // under <ResultSummary>; carry them across so merged reports don't silently lose crash/exit
        // messages and attachment references.
        var mergedRunInfos = new XElement(NamespaceUri + "RunInfos");
        var mergedCollectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");

        // Preserve the order in which counter attributes are first seen so the merged
        // <Counters> element keeps the well-known TRX attribute ordering.
        var counterAttributeOrder = new List<string>();
        var counterSums = new Dictionary<string, long>(StringComparer.Ordinal);

        bool anyFailure = false;
        DateTimeOffset? earliestStart = null;
        DateTimeOffset? latestFinish = null;

        foreach (XDocument report in inputReports)
        {
            XElement? testRun = report.Root;
            if (testRun is null)
            {
                continue;
            }

            CloneChildrenInto(FindChild(testRun, "Results"), mergedResults);
            CloneChildrenIntoDeduplicatedById(FindChild(testRun, "TestDefinitions"), mergedTestDefinitions, seenTestDefinitionIds);
            CloneChildrenInto(FindChild(testRun, "TestEntries"), mergedTestEntries);

            XElement? testLists = FindChild(testRun, "TestLists");
            if (testLists is not null)
            {
                foreach (XElement testList in testLists.Elements())
                {
                    string? id = testList.Attribute("id")?.Value;
                    if (id is null || seenTestListIds.Add(id))
                    {
                        mergedTestLists.Add(new XElement(testList));
                    }
                }
            }

            XElement? resultSummary = FindChild(testRun, "ResultSummary");
            if (resultSummary is not null)
            {
                if (string.Equals(resultSummary.Attribute("outcome")?.Value, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    anyFailure = true;
                }

                AccumulateCounters(FindChild(resultSummary, "Counters"), counterAttributeOrder, counterSums);
                CloneChildrenInto(FindChild(resultSummary, "RunInfos"), mergedRunInfos);
                CloneChildrenInto(FindChild(resultSummary, "CollectorDataEntries"), mergedCollectorDataEntries);
            }

            XElement? times = FindChild(testRun, "Times");
            if (times is not null)
            {
                if (TryParseDateTimeOffset(times.Attribute("start")?.Value, out DateTimeOffset start)
                    && (earliestStart is null || start < earliestStart))
                {
                    earliestStart = start;
                }

                if (TryParseDateTimeOffset(times.Attribute("finish")?.Value, out DateTimeOffset finish)
                    && (latestFinish is null || finish > latestFinish))
                {
                    latestFinish = finish;
                }
            }
        }

        if (counterSums.TryGetValue("failed", out long failedCount) && failedCount > 0)
        {
            anyFailure = true;
        }

        if (counterSums.TryGetValue("timeout", out long timeoutCount) && timeoutCount > 0)
        {
            anyFailure = true;
        }

        var mergedTestRun = new XElement(
            NamespaceUri + "TestRun",
            new XAttribute("id", runId),
            new XAttribute("name", runName));

        mergedTestRun.Add(BuildTimes(earliestStart, latestFinish));
        mergedTestRun.Add(BuildTestSettings(runName));
        mergedTestRun.Add(mergedResults);
        mergedTestRun.Add(mergedTestDefinitions);
        mergedTestRun.Add(mergedTestEntries);
        mergedTestRun.Add(mergedTestLists);
        mergedTestRun.Add(BuildResultSummary(anyFailure ? "Failed" : "Completed", counterAttributeOrder, counterSums, mergedRunInfos, mergedCollectorDataEntries));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), mergedTestRun);
    }

    /// <summary>
    /// Loads the given TRX files, merges them (see <see cref="Merge"/>) and writes the result to <paramref name="outputPath"/>.
    /// </summary>
    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        Guid runId,
        string runName,
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

        var reports = new List<XDocument>(inputPaths.Count);
        foreach (string inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(XDocument.Load(inputPath));
        }

        string outputDirectory = Path.GetDirectoryName(outputPath) is { Length: > 0 } dir
            ? dir
            : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);

        // Attachment hrefs inside CollectorDataEntries / ResultFiles are relative to each input's
        // deployment root (they look like "<machine>/<file>" and physically live under
        // "<inputDir>/<inputDeploymentRoot>/In/..."). Relocate those trees into the merged report's
        // deployment root — isolated per input so identical file names from different modules do not
        // collide — and rewrite the input's hrefs to match, before the merge clones them.
        // Best-effort and path-confined: failures never fail the merge.
        RelocateAttachments(inputPaths, reports, outputDirectory, runName, cancellationToken);

        XDocument merged = Merge(reports, runId, runName);

        using FileStream stream = File.Create(outputPath);
#if NETCOREAPP
        await merged.SaveAsync(stream, SaveOptions.None, cancellationToken).ConfigureAwait(false);
#else
        merged.Save(stream, SaveOptions.None);
        await Task.CompletedTask.ConfigureAwait(false);
#endif
    }

    private static XElement? FindChild(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));

    /// <summary>
    /// Relocates each input report's attachment deployment tree (<c>&lt;deploymentRoot&gt;/In</c>) into a
    /// per-input isolated folder under the merged report's deployment root and rewrites that input's
    /// attachment hrefs to match, so references carried into the merged TRX keep resolving without one
    /// module's files shadowing another's. All destination and source paths are confined
    /// (<c>runName</c> and each input's <c>runDeploymentRoot</c> are attacker-influenced), and any
    /// copy failure is swallowed so it never fails the merge (never-fail-the-run invariant).
    /// </summary>
    private static void RelocateAttachments(IReadOnlyList<string> inputPaths, IReadOnlyList<XDocument> reports, string outputDirectory, string runName, CancellationToken cancellationToken)
    {
        string outputFull = Path.GetFullPath(outputDirectory);
        string mergedDeploymentRoot = GetConfinedDeploymentRootLeaf(runName);
        string mergedRootFull = Path.GetFullPath(Path.Combine(outputFull, mergedDeploymentRoot));

        // The confined leaf can no longer be '.' or '..', but keep the defensive lexical/reparse checks:
        // reject if the deployment root escapes the output directory or any component below it is a
        // reparse point (a symlink/junction there would resolve writes outside the confined root).
        if (!IsUnderDirectory(mergedRootFull, outputFull) || HasReparsePointComponent(mergedRootFull, outputFull))
        {
            return;
        }

        string mergedInRoot = Path.Combine(mergedRootFull, "In");

        // Absolute directories that must never be wholesale-deleted by relocation: each input report's
        // parent directory AND each input's resolved source 'In' root. Deleting a destination that
        // contains one of these would remove the originals (which RFC 018 requires to stay on disk) or a
        // sibling/later input's report and attachments.
        var protectedDirectories = new List<string>(inputPaths.Count * 2);
        for (int i = 0; i < inputPaths.Count && i < reports.Count; i++)
        {
            try
            {
                if (Path.GetDirectoryName(Path.GetFullPath(inputPaths[i])) is not { Length: > 0 } inputDir)
                {
                    continue;
                }

                protectedDirectories.Add(inputDir);

                string? deploymentRoot = reports[i].Root is { } r
                    ? FindChild(r, "TestSettings")?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Deployment", StringComparison.Ordinal))?.Attribute("runDeploymentRoot")?.Value
                    : null;

                if (!RoslynString.IsNullOrEmpty(deploymentRoot))
                {
                    string source = Path.GetFullPath(Path.Combine(inputDir, deploymentRoot, "In"));
                    if (IsUnderDirectory(source, inputDir))
                    {
                        protectedDirectories.Add(source);
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                // Ignore malformed input paths here; the per-input loop handles them.
            }
        }

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
                return;
            }
        }

        for (int i = 0; i < inputPaths.Count && i < reports.Count; i++)
        {
            // Cancellation must interrupt an otherwise unbounded copy of a large deployment tree; check
            // before each input (and inside CopyDirectoryRecursive) and let OperationCanceledException
            // propagate rather than being swallowed as a best-effort failure below.
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? inputDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPaths[i]));
                string? inputDeploymentRoot = reports[i].Root is { } root
                    ? FindChild(root, "TestSettings")?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Deployment", StringComparison.Ordinal))?.Attribute("runDeploymentRoot")?.Value
                    : null;

                if (RoslynString.IsNullOrEmpty(inputDirectory) || RoslynString.IsNullOrEmpty(inputDeploymentRoot))
                {
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
                    continue;
                }

                // Equal roots: the source files already live at the merged deployment root under their
                // original relative paths, so the original hrefs resolve as-is. Skip relocation and
                // leave the references un-prefixed.
                if (string.Equals(sourceInRoot, mergedInRoot, PathComparison))
                {
                    continue;
                }

                string prefix = i.ToString(CultureInfo.InvariantCulture);
                string destForInput = Path.Combine(mergedInRoot, prefix);

                // Strict overlap (one 'In' root is nested inside the other): copying source directly
                // into a subfolder of the merged root would either recurse into its own destination
                // (merged nested under source) or, if we simply skipped, leave the prefixed hrefs
                // dangling. Stage the copy through a temporary directory outside both trees, then move
                // it into place, so the per-input prefix rewrite below is always correct.
                bool strictOverlap = IsUnderDirectory(mergedInRoot, sourceInRoot) || IsUnderDirectory(sourceInRoot, mergedInRoot);

                // Isolate each input under its own subfolder so identical relative attachment paths
                // from different inputs cannot shadow each other.
                //
                // Only clear the destination wholesale when it does not contain an input report tree.
                // If it does (e.g. an input lives under the merged 'In' root), a blanket delete would
                // remove originals or sibling/later inputs, so we overlay the copy instead (per-file
                // overwrite; CopyDirectoryRecursive still removes stale destination reparse points).
                bool destinationContainsInput = ContainsAnyDirectory(destForInput, protectedDirectories);

                if (strictOverlap)
                {
                    // Stage the source out first (before clearing the destination, which could otherwise
                    // be inside the source tree). Only when the merged root is nested INSIDE the source
                    // does the source snapshot also contain the previous merged 'In' tree; exclude it in
                    // that direction so repeated merges don't accumulate recursively-nested stale trees.
                    string? excludeSubtree = IsUnderDirectory(mergedInRoot, sourceInRoot) && !string.Equals(mergedInRoot, sourceInRoot, PathComparison)
                        ? mergedInRoot
                        : null;
                    CopyViaStaging(sourceInRoot, destForInput, excludeSubtree, clearDestination: !destinationContainsInput, cancellationToken);
                }
                else
                {
                    // Recreate the destination as a fresh, non-link tree first (unless it holds an input)
                    // so a pre-existing junction/symlink (or stale bytes) from a reused output directory
                    // can't redirect the copy or linger.
                    if (!destinationContainsInput)
                    {
                        DeleteDirectoryTreeOrLink(destForInput);
                    }

                    CopyDirectoryRecursive(sourceInRoot, destForInput, cancellationToken);
                }

                RewriteAttachmentHrefs(reports[i], prefix);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Best-effort: a failed attachment copy (including a malformed path from a hostile
                // runDeploymentRoot, which Path.GetFullPath surfaces as ArgumentException/
                // NotSupportedException) must not fail the merge. Cancellation is not caught here.
            }
        }
    }

    private static void RewriteAttachmentHrefs(XDocument report, string prefix)
    {
        if (report.Root is not { } root)
        {
            return;
        }

        // Attachment references are relative to the deployment root: UriAttachment '<A href="...">'
        // and per-result '<ResultFile path="...">'. Prefix them so they point at the isolated subfolder.
        foreach (XElement element in root.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "A":
                    PrefixRelativeAttribute(element, "href", prefix);
                    break;

                case "ResultFile":
                    PrefixRelativeAttribute(element, "path", prefix);
                    break;
            }
        }
    }

    private static void PrefixRelativeAttribute(XElement element, string attributeName, string prefix)
    {
        XAttribute? attribute = element.Attribute(attributeName);
        if (attribute is null || RoslynString.IsNullOrEmpty(attribute.Value) || Path.IsPathRooted(attribute.Value))
        {
            return;
        }

        attribute.Value = prefix + "/" + attribute.Value;
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

    /// <summary>
    /// Copies <paramref name="sourceDirectory"/> to <paramref name="destinationDirectory"/> via a
    /// temporary staging directory outside both trees. Used when the source and the merged destination
    /// strictly overlap, so the copy never recurses into its own destination while still landing the
    /// files at the prefixed destination (keeping the rewritten hrefs valid).
    /// <paramref name="excludeSubtree"/> (the merged 'In' root) is skipped while staging so a source that
    /// contains the previous merged output does not snapshot it into the new destination.
    /// </summary>
    private static void CopyViaStaging(string sourceDirectory, string destinationDirectory, string? excludeSubtree, bool clearDestination, CancellationToken cancellationToken)
    {
        string staging = Path.Combine(Path.GetTempPath(), "mtp-trx-merge-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Copy the source out to a temp location OUTSIDE both trees first, so clearing the
            // destination (which may be nested inside the source) cannot corrupt the source, and the
            // final copy never recurses into its own destination.
            CopyDirectoryRecursive(sourceDirectory, staging, excludeSubtree, cancellationToken);

            // Only clear the destination when it does not contain an input tree; otherwise overlay so
            // originals are never removed (RFC 018 requires them to remain on disk).
            if (clearDestination)
            {
                DeleteDirectoryTreeOrLink(destinationDirectory);
            }

            CopyDirectoryRecursive(staging, destinationDirectory, excludeSubtree: null, cancellationToken);
        }
        finally
        {
            // Best-effort cleanup: a failure here must not replace an in-flight OperationCanceledException
            // (or the real merge exception) with an I/O/authorization error.
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool ContainsAnyDirectory(string candidateDirectory, IReadOnlyList<string> directories)
    {
        string candidateFull = Path.GetFullPath(candidateDirectory);
        foreach (string directory in directories)
        {
            if (IsUnderDirectory(Path.GetFullPath(directory), candidateFull))
            {
                return true;
            }
        }

        return false;
    }

    private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
        => CopyDirectoryRecursive(sourceDirectory, destinationDirectory, excludeSubtree: null, cancellationToken);

    private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory, string? excludeSubtree, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Skip the excluded subtree (e.g. a previous merged 'In' root nested under the source) so we
        // don't snapshot the merger's own prior output into the new destination.
        if (excludeSubtree is not null && IsUnderDirectory(Path.GetFullPath(sourceDirectory), excludeSubtree))
        {
            return;
        }

        // Do not descend into reparse points (symlinks/junctions): a link inside the (confined) source
        // tree could otherwise redirect the copy to an arbitrary location.
        if (IsReparsePoint(sourceDirectory))
        {
            return;
        }

        // The destination is recreated fresh by the caller, but guard defensively for nested levels:
        // never write through a destination directory that is itself a link.
        if (Directory.Exists(destinationDirectory) && IsReparsePoint(destinationDirectory))
        {
            Directory.Delete(destinationDirectory);
        }

        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.GetFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A source file reparse point (symlink) would make File.Copy follow the link and pull in
            // content from outside the confined tree; skip it.
            if (IsReparsePoint(file))
            {
                continue;
            }

            // Guard the destination too: a pre-existing destination symlink would make the overwrite
            // follow the link and write outside the merged root. Remove it so a real file is written.
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            if (File.Exists(destination) && IsReparsePoint(destination))
            {
                File.Delete(destination);
            }

            // Overwrite so a reused output directory can't leave stale bytes behind while the merged
            // XML is rewritten to reference the (per-input isolated) destination, mirroring the
            // File.Create overwrite used for the merged TRX itself.
            File.Copy(file, destination, overwrite: true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDirectory))
        {
            CopyDirectoryRecursive(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)), excludeSubtree, cancellationToken);
        }
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

    private static StringComparison PathComparison
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void DeleteDirectoryTreeOrLink(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // Deleting a directory reparse point non-recursively removes only the link, never its target's
        // contents; a real directory is deleted recursively (the runtime does not follow nested links).
        if (IsReparsePoint(path))
        {
            Directory.Delete(path);
        }
        else
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void CloneChildrenInto(XElement? source, XElement destination)
    {
        if (source is null)
        {
            return;
        }

        foreach (XElement child in source.Elements())
        {
            destination.Add(new XElement(child));
        }
    }

    private static void CloneChildrenIntoDeduplicatedById(XElement? source, XElement destination, HashSet<string> seenIds)
    {
        if (source is null)
        {
            return;
        }

        foreach (XElement child in source.Elements())
        {
            string? id = child.Attribute("id")?.Value;

            // Keep the first definition seen for a given id; a null/absent id is always kept.
            if (id is null || seenIds.Add(id))
            {
                destination.Add(new XElement(child));
            }
        }
    }

    private static void AccumulateCounters(XElement? counters, List<string> attributeOrder, Dictionary<string, long> sums)
    {
        if (counters is null)
        {
            return;
        }

        foreach (XAttribute attribute in counters.Attributes())
        {
            string name = attribute.Name.LocalName;
            if (!sums.ContainsKey(name))
            {
                attributeOrder.Add(name);
                sums[name] = 0;
            }

            if (long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                sums[name] += value;
            }
        }
    }

    private static XElement BuildTimes(DateTimeOffset? earliestStart, DateTimeOffset? latestFinish)
    {
        var times = new XElement(NamespaceUri + "Times");
        if (earliestStart is { } start)
        {
            times.SetAttributeValue("creation", start);
            times.SetAttributeValue("queuing", start);
            times.SetAttributeValue("start", start);
        }

        if (latestFinish is { } finish)
        {
            times.SetAttributeValue("finish", finish);
        }

        return times;
    }

    private static XElement BuildTestSettings(string runName)
    {
        var testSettings = new XElement(
            NamespaceUri + "TestSettings",
            new XAttribute("name", "default"),
            new XAttribute("id", Guid.NewGuid()));
        testSettings.Add(new XElement(NamespaceUri + "Deployment", new XAttribute("runDeploymentRoot", GetConfinedDeploymentRootLeaf(runName))));
        return testSettings;
    }

    /// <summary>
    /// Produces a single, confined deployment-root leaf from <paramref name="runName"/> for use both in
    /// the emitted <c>TestSettings/Deployment/@runDeploymentRoot</c> and in attachment relocation, so the
    /// two always agree and neither can escape the output directory. The file-name sanitizer already
    /// replaces path separators and reserved names, so the only residual escape values are <c>.</c> and
    /// <c>..</c>, which are prefixed to keep the leaf confined.
    /// </summary>
    private static string GetConfinedDeploymentRootLeaf(string runName)
    {
        string leaf = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(runName);
        return leaf is "." or ".." ? "_" + leaf : leaf;
    }

    private static XElement BuildResultSummary(string outcome, List<string> counterAttributeOrder, Dictionary<string, long> counterSums, XElement runInfos, XElement collectorDataEntries)
    {
        var counters = new XElement(NamespaceUri + "Counters");
        foreach (string name in counterAttributeOrder)
        {
            counters.SetAttributeValue(name, counterSums[name].ToString(CultureInfo.InvariantCulture));
        }

        var resultSummary = new XElement(
            NamespaceUri + "ResultSummary",
            new XAttribute("outcome", outcome),
            counters);

        // Emit the diagnostics/attachment children only when they carry content, matching the shape
        // the single-run producer writes (which omits empty <RunInfos>/<CollectorDataEntries>).
        if (runInfos.HasElements)
        {
            resultSummary.Add(runInfos);
        }

        if (collectorDataEntries.HasElements)
        {
            resultSummary.Add(collectorDataEntries);
        }

        return resultSummary;
    }

    private static bool TryParseDateTimeOffset(string? value, out DateTimeOffset result)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            result = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }
}

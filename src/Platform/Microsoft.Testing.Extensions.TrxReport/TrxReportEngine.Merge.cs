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
    /// emitted <c>TestSettings</c> id is derived deterministically from <paramref name="runId"/>, so
    /// identical inputs reproduce byte-for-byte identical XML (RFC 018 idempotency; the orchestrator may
    /// retry).
    /// <para>
    /// Merge rules:
    /// <list type="bullet">
    ///   <item><description><c>Results</c> and <c>TestEntries</c> are unioned as-is; <c>TestDefinitions</c> are deduplicated by <c>id</c> (ids are derived deterministically from each test's UID and the schema forbids duplicates).</description></item>
    ///   <item><description><c>TestLists</c> are deduplicated by <c>id</c> (the well-known lists are shared across files).</description></item>
    ///   <item><description><c>Counters</c> attributes are summed; <c>Times</c> use the earliest <c>creation</c>/<c>queuing</c>/<c>start</c> and latest <c>finish</c> derived from the inputs (attributes no input supplies are omitted).</description></item>
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

        // TestDefinition ids are derived deterministically from each test's UID (assembly file name plus
        // test identity), so the same test discovered in more than one input yields the same id — but a
        // multi-TFM merge can produce definitions that share an id yet differ (e.g. different storage /
        // codeBase). We keep identical definitions deduplicated (the TRX schema forbids duplicate
        // <UnitTest id="...">), but remap a materially-different same-id definition to a fresh deterministic
        // id and rewrite that input's testId references, so module-specific definitions are not lost.
        var definitionsById = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        // Run-level diagnostics (<RunInfos>), collector attachments (<CollectorDataEntries>) and run-level
        // result files (<ResultFiles>, produced by VSTest) live under <ResultSummary>; carry them across so
        // merged reports don't silently lose crash/exit messages or attachment references.
        var mergedRunInfos = new XElement(NamespaceUri + "RunInfos");
        var mergedCollectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");
        var mergedResultFiles = new XElement(NamespaceUri + "ResultFiles");

        // VSTest records run-level skipped/informational messages under <ResultSummary>/<Output> (see
        // TrxReportEngine.Metadata.cs). Collect each input's Output element so those messages are merged
        // rather than silently dropped.
        var resultSummaryOutputs = new List<XElement>();

        // Preserve the order in which counter attributes are first seen so the merged
        // <Counters> element keeps the well-known TRX attribute ordering.
        var counterAttributeOrder = new List<string>();
        var counterSums = new Dictionary<string, long>(StringComparer.Ordinal);

        bool anyFailure = false;
        DateTimeOffset? earliestCreation = null;
        DateTimeOffset? earliestQueuing = null;
        DateTimeOffset? earliestStart = null;
        DateTimeOffset? latestFinish = null;
        int inputIndex = 0;

        foreach (XDocument report in inputReports)
        {
            XElement? testRun = report.Root;
            if (testRun is null)
            {
                inputIndex++;
                continue;
            }

            // Merge TestDefinitions first to build this input's id remap, then clone Results/TestEntries
            // with that remap applied so their testId references resolve to the right definition.
            Dictionary<string, string> idRemap = MergeTestDefinitions(FindChild(testRun, "TestDefinitions"), mergedTestDefinitions, definitionsById, inputIndex);
            CloneWithRemappedTestIds(FindChild(testRun, "Results"), mergedResults, idRemap);
            CloneWithRemappedTestIds(FindChild(testRun, "TestEntries"), mergedTestEntries, idRemap);

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
                // A successful run's summary outcome is "Completed" (or "Passed"); anything else
                // (Failed, Error, Aborted, Timeout, Inconclusive, …) is an unsuccessful run and must not
                // be flattened to "Completed" in the merged report.
                string? outcome = resultSummary.Attribute("outcome")?.Value;
                if (!RoslynString.IsNullOrEmpty(outcome)
                    && !string.Equals(outcome, "Completed", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Passed", StringComparison.OrdinalIgnoreCase))
                {
                    anyFailure = true;
                }

                AccumulateCounters(FindChild(resultSummary, "Counters"), counterAttributeOrder, counterSums);
                CloneChildrenInto(FindChild(resultSummary, "RunInfos"), mergedRunInfos);
                CloneChildrenInto(FindChild(resultSummary, "CollectorDataEntries"), mergedCollectorDataEntries);
                CloneChildrenInto(FindChild(resultSummary, "ResultFiles"), mergedResultFiles);
                if (FindChild(resultSummary, "Output") is { } output)
                {
                    resultSummaryOutputs.Add(output);
                }
            }

            XElement? times = FindChild(testRun, "Times");
            if (times is not null)
            {
                // Each Times attribute is tracked independently from its own inputs rather than fabricated
                // from the start: creation and queuing predate execution and cannot be derived from start,
                // so any that no input supplies is omitted from the merged report instead of invented.
                if (TryParseDateTimeOffset(times.Attribute("creation")?.Value, out DateTimeOffset creation)
                    && (earliestCreation is null || creation < earliestCreation))
                {
                    earliestCreation = creation;
                }

                if (TryParseDateTimeOffset(times.Attribute("queuing")?.Value, out DateTimeOffset queuing)
                    && (earliestQueuing is null || queuing < earliestQueuing))
                {
                    earliestQueuing = queuing;
                }

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

            inputIndex++;
        }

        if (counterSums.TryGetValue("failed", out long failedCount) && failedCount > 0)
        {
            anyFailure = true;
        }

        if (counterSums.TryGetValue("error", out long errorCount) && errorCount > 0)
        {
            anyFailure = true;
        }

        if (counterSums.TryGetValue("timeout", out long timeoutCount) && timeoutCount > 0)
        {
            anyFailure = true;
        }

        if (counterSums.TryGetValue("aborted", out long abortedCount) && abortedCount > 0)
        {
            anyFailure = true;
        }

        var mergedTestRun = new XElement(
            NamespaceUri + "TestRun",
            new XAttribute("id", runId),
            new XAttribute("name", runName));

        mergedTestRun.Add(BuildTimes(earliestCreation, earliestQueuing, earliestStart, latestFinish));
        mergedTestRun.Add(BuildTestSettings(runId, runName));
        mergedTestRun.Add(mergedResults);
        mergedTestRun.Add(mergedTestDefinitions);
        mergedTestRun.Add(mergedTestEntries);
        mergedTestRun.Add(mergedTestLists);
        mergedTestRun.Add(BuildResultSummary(anyFailure ? "Failed" : "Completed", counterAttributeOrder, counterSums, MergeResultSummaryOutputs(resultSummaryOutputs), mergedRunInfos, mergedCollectorDataEntries, mergedResultFiles));

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

        // Reject an empty input list before any filesystem work: Merge throws for empty input, but only
        // after output-directory creation and attachment relocation would already have touched the disk.
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("At least one TRX report is required to merge.", nameof(inputPaths));
        }

        // RFC 018 treats per-module inputs as read-only and requires them to remain on disk. The merged
        // TRX is written with File.Create (truncating); reject an output that aliases an input so a merge
        // can never overwrite one of its own sources.
        MergeOutputFileHelper.EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

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
        // The merged deployment root is made unique per run (see GetConfinedDeploymentRootLeaf) so this
        // relocation always writes into a fresh tree and can never mutate the attachments referenced by a
        // previously committed merged TRX at the same output path; a failed serialization therefore leaves
        // that prior report and its files consistent, and only orphaned files under the new root remain.
        // Best-effort and path-confined: failures never fail the merge.
        RelocateAttachments(inputPaths, reports, outputDirectory, runId, runName, cancellationToken);

        XDocument merged = Merge(reports, runId, runName);

        // Write to a temporary sibling, then replace the destination ENTRY, so a symlink/hardlink output
        // alias of an input has only its link removed rather than the read-only source truncated in place.
        await MergeOutputFileHelper.WriteViaTemporarySiblingAsync(outputPath, async tempPath =>
        {
            using FileStream stream = File.Create(tempPath);
#if NETCOREAPP
            await merged.SaveAsync(stream, SaveOptions.None, cancellationToken).ConfigureAwait(false);
#else
            merged.Save(stream, SaveOptions.None);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }).ConfigureAwait(false);
    }

    private static XElement? FindChild(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));

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

    /// <summary>
    /// Merges one input's <c>TestDefinitions</c> into the accumulator and returns the id remap to apply to
    /// that input's <c>testId</c> references. An id not seen before is kept; an id whose definition is
    /// identical to the one already kept is deduplicated (the schema forbids duplicate ids); an id whose
    /// definition differs (e.g. the same test from another TFM with different storage) is remapped to a
    /// fresh deterministic id and kept, so module-specific definitions are preserved.
    /// </summary>
    private static Dictionary<string, string> MergeTestDefinitions(XElement? testDefinitions, XElement mergedTestDefinitions, Dictionary<string, XElement> definitionsById, int inputIndex)
    {
        var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (testDefinitions is null)
        {
            return remap;
        }

        foreach (XElement definition in testDefinitions.Elements())
        {
            string? id = definition.Attribute("id")?.Value;
            if (id is null)
            {
                mergedTestDefinitions.Add(new XElement(definition));
                continue;
            }

            if (!definitionsById.TryGetValue(id, out XElement? existing))
            {
                definitionsById[id] = definition;
                mergedTestDefinitions.Add(new XElement(definition));
            }
            else if (XNode.DeepEquals(existing, definition))
            {
                // Identical definition already kept — deduplicate.
            }
            else
            {
                string newId = RemapDefinitionId(id, inputIndex);
                while (definitionsById.ContainsKey(newId))
                {
                    newId = RemapDefinitionId(newId, inputIndex);
                }

                var clone = new XElement(definition);
                clone.SetAttributeValue("id", newId);
                definitionsById[newId] = clone;
                mergedTestDefinitions.Add(clone);
                remap[id] = newId;
            }
        }

        return remap;
    }

    /// <summary>
    /// Clones the children of <paramref name="source"/> into <paramref name="destination"/>, rewriting any
    /// <c>testId</c> attribute (on the child or its descendants) through <paramref name="remap"/> so a
    /// remapped TestDefinition's results/entries reference the right definition.
    /// </summary>
    private static void CloneWithRemappedTestIds(XElement? source, XElement destination, Dictionary<string, string> remap)
    {
        if (source is null)
        {
            return;
        }

        foreach (XElement child in source.Elements())
        {
            var clone = new XElement(child);
            if (remap.Count > 0)
            {
                foreach (XElement element in clone.DescendantsAndSelf())
                {
                    if (element.Attribute("testId") is { } testId && remap.TryGetValue(testId.Value, out string? newId))
                    {
                        testId.Value = newId;
                    }
                }
            }

            destination.Add(clone);
        }
    }

    /// <summary>
    /// Derives a stable, distinct id for a TestDefinition that collides with a materially-different one,
    /// from the original id and the input index, so the remap is deterministic (RFC 018 idempotency).
    /// </summary>
    private static string RemapDefinitionId(string originalId, int inputIndex)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong low = 14695981039346656037UL;
        ulong high = 0x9E3779B97F4A7C15UL;
        foreach (char c in originalId + "|" + inputIndex.ToString(CultureInfo.InvariantCulture))
        {
            low = (low ^ c) * fnvPrime;
            high = (high ^ c) * fnvPrime;
        }

        byte[] bytes = new byte[16];
        BitConverter.GetBytes(low).CopyTo(bytes, 0);
        BitConverter.GetBytes(high).CopyTo(bytes, 8);
        return new Guid(bytes).ToString();
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

    private static XElement BuildTimes(DateTimeOffset? earliestCreation, DateTimeOffset? earliestQueuing, DateTimeOffset? earliestStart, DateTimeOffset? latestFinish)
    {
        var times = new XElement(NamespaceUri + "Times");
        if (earliestCreation is { } creation)
        {
            times.SetAttributeValue("creation", creation);
        }

        if (earliestQueuing is { } queuing)
        {
            times.SetAttributeValue("queuing", queuing);
        }

        if (earliestStart is { } start)
        {
            times.SetAttributeValue("start", start);
        }

        if (latestFinish is { } finish)
        {
            times.SetAttributeValue("finish", finish);
        }

        return times;
    }

    private static XElement BuildTestSettings(Guid runId, string runName)
    {
        var testSettings = new XElement(
            NamespaceUri + "TestSettings",
            new XAttribute("name", "default"),
            new XAttribute("id", CreateDeterministicSettingsId(runId)));
        testSettings.Add(new XElement(NamespaceUri + "Deployment", new XAttribute("runDeploymentRoot", GetConfinedDeploymentRootLeaf(runId, runName))));
        return testSettings;
    }

    // Fixed namespace used to derive the (deterministic) TestSettings id from the run id, so the settings
    // id is stable across retries yet distinct from the run id itself.
    private static readonly Guid TestSettingsIdNamespace = new("b3f8f9d1-2e4a-4c6b-9f0d-7a1c2e5b8d40");

    /// <summary>
    /// Derives a stable <c>TestSettings</c> id from <paramref name="runId"/> by XoR-ing it with a fixed
    /// namespace, so identical inputs produce identical merged XML (RFC 018 idempotency) without emitting
    /// the run id verbatim as the settings id.
    /// </summary>
    private static Guid CreateDeterministicSettingsId(Guid runId)
    {
        byte[] runBytes = runId.ToByteArray();
        byte[] namespaceBytes = TestSettingsIdNamespace.ToByteArray();
        byte[] result = new byte[16];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)(runBytes[i] ^ namespaceBytes[i]);
        }

        return new Guid(result);
    }

    /// <summary>
    /// Produces a single, confined, per-run-unique deployment-root leaf from <paramref name="runName"/> and
    /// <paramref name="runId"/> for use both in the emitted <c>TestSettings/Deployment/@runDeploymentRoot</c>
    /// and in attachment relocation, so the two always agree and neither can escape the output directory. The
    /// file-name sanitizer already replaces path separators and reserved names, so the only residual escape
    /// values are <c>.</c> and <c>..</c>, which are prefixed to keep the leaf confined. The run id (which is
    /// deterministic for a given logical run, preserving RFC 018 idempotency) is appended so a merge never
    /// reuses the deployment tree of a previously committed report at the same output path — relocation then
    /// cannot mutate that prior report's referenced files, and a failed merge leaves it consistent.
    /// </summary>
    private static string GetConfinedDeploymentRootLeaf(Guid runId, string runName)
    {
        string leaf = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(runName);
        leaf = leaf is "." or ".." ? "_" + leaf : leaf;
        return leaf + "." + runId.ToString("N", CultureInfo.InvariantCulture);
    }

    private static XElement BuildResultSummary(string outcome, List<string> counterAttributeOrder, Dictionary<string, long> counterSums, XElement? output, XElement runInfos, XElement collectorDataEntries, XElement resultFiles)
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

        // Emit the optional children in TRX schema order (Counters, Output, RunInfos, CollectorDataEntries,
        // ResultFiles), and only when they carry content, matching the single-run producer's shape.
        if (output is not null)
        {
            resultSummary.Add(output);
        }

        if (runInfos.HasElements)
        {
            resultSummary.Add(runInfos);
        }

        if (collectorDataEntries.HasElements)
        {
            resultSummary.Add(collectorDataEntries);
        }

        if (resultFiles.HasElements)
        {
            resultSummary.Add(resultFiles);
        }

        return resultSummary;
    }

    /// <summary>
    /// Merges the run-level <c>&lt;Output&gt;</c> elements of every input <c>ResultSummary</c> into a single
    /// one so run-level std streams and informational/skipped <c>TextMessages</c> survive the merge. The
    /// scalar text children (<c>StdOut</c>/<c>StdErr</c>/<c>DebugTrace</c>) are concatenated and the
    /// <c>Message</c> entries under <c>TextMessages</c> are unioned. Returns <see langword="null"/> when
    /// nothing is present.
    /// </summary>
    private static XElement? MergeResultSummaryOutputs(List<XElement> outputs)
    {
        if (outputs.Count == 0)
        {
            return null;
        }

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var debugTrace = new StringBuilder();
        var messages = new List<XElement>();

        foreach (XElement output in outputs)
        {
            AppendOutputText(stdOut, FindChild(output, "StdOut"));
            AppendOutputText(stdErr, FindChild(output, "StdErr"));
            AppendOutputText(debugTrace, FindChild(output, "DebugTrace"));

            if (FindChild(output, "TextMessages") is { } textMessages)
            {
                foreach (XElement message in textMessages.Elements().Where(e => string.Equals(e.Name.LocalName, "Message", StringComparison.Ordinal)))
                {
                    messages.Add(new XElement(message));
                }
            }
        }

        var merged = new XElement(NamespaceUri + "Output");
        if (stdOut.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "StdOut", stdOut.ToString()));
        }

        if (stdErr.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "StdErr", stdErr.ToString()));
        }

        if (debugTrace.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "DebugTrace", debugTrace.ToString()));
        }

        if (messages.Count > 0)
        {
            var textMessages = new XElement(NamespaceUri + "TextMessages");
            textMessages.Add(messages);
            merged.Add(textMessages);
        }

        return merged.HasElements ? merged : null;
    }

    private static void AppendOutputText(StringBuilder builder, XElement? element)
    {
        if (element is null || RoslynString.IsNullOrEmpty(element.Value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(element.Value);
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

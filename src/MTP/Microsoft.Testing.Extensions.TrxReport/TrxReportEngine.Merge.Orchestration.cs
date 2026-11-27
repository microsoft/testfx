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
    ///   <item><description>Attachment hrefs inside <c>CollectorDataEntries</c> are carried as-is; because they are relative to each input's deployment root, the physical attachment files are only relocated to the merged deployment root by <see cref="MergeToFileAsync(IReadOnlyList{string}, string, Guid, string, CancellationToken)"/> (which has the source paths). Callers of the in-memory <see cref="Merge"/> that need resolvable attachments should relocate them separately.</description></item>
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
    internal static Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        Guid runId,
        string runName,
        CancellationToken cancellationToken)
        => MergeToFileAsync(inputPaths, outputPath, runId, runName, cancellationToken, retryAttempts: false);

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        Guid runId,
        string runName,
        CancellationToken cancellationToken,
        bool retryAttempts)
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

        XDocument merged = retryAttempts
            ? MergeRetryAttempts(reports, runId, runName)
            : Merge(reports, runId, runName);

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

    private static XDocument MergeRetryAttempts(IReadOnlyList<XDocument> inputReports, Guid runId, string runName)
    {
        var latestAttemptByTestId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int attempt = 0; attempt < inputReports.Count; attempt++)
        {
            if (inputReports[attempt].Root is not { } testRun || FindChild(testRun, "Results") is not { } results)
            {
                continue;
            }

            foreach (XElement result in results.Elements())
            {
                string testId = result.Attribute("testId")?.Value
                    ?? throw new InvalidDataException("A TRX retry result is missing its testId.");
                _ = result.Attribute("executionId")?.Value
                    ?? throw new InvalidDataException("A TRX retry result is missing its executionId.");
                latestAttemptByTestId[testId] = attempt;
            }
        }

        var selectedExecutionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int attempt = 0; attempt < inputReports.Count; attempt++)
        {
            if (inputReports[attempt].Root is not { } testRun || FindChild(testRun, "Results") is not { } results)
            {
                continue;
            }

            foreach (XElement result in results.Elements())
            {
                string testId = result.Attribute("testId")!.Value;
                if (latestAttemptByTestId[testId] == attempt)
                {
                    selectedExecutionIds.Add(result.Attribute("executionId")!.Value);
                }
            }
        }

        // Definitions for the same test differ across attempts because their Execution IDs are new.
        // Keep only the selected definition before module merge can remap the stable test ID.
        var reportsForMerge = new List<XDocument>(inputReports.Count);
        for (int attempt = 0; attempt < inputReports.Count; attempt++)
        {
            var report = new XDocument(inputReports[attempt]);
            if (report.Root is { } testRun && FindChild(testRun, "TestDefinitions") is { } definitions)
            {
                int currentAttempt = attempt;
                definitions.Elements()
                    .Where(definition => definition.Attribute("id") is { } id
                        && latestAttemptByTestId.TryGetValue(id.Value, out int latestAttempt)
                        && latestAttempt != currentAttempt)
                    .Remove();
            }

            reportsForMerge.Add(report);
        }

        XDocument merged = Merge(reportsForMerge, runId, runName);
        XElement mergedTestRun = merged.Root!;
        XElement mergedResults = FindChild(mergedTestRun, "Results")!;
        XElement[] selectedResults =
        [
            .. mergedResults.Elements()
                .Where(result => selectedExecutionIds.Contains(result.Attribute("executionId")?.Value ?? string.Empty)),
        ];
        mergedResults.ReplaceNodes(selectedResults);

        var selectedTestIds = new HashSet<string>(
            selectedResults.Select(result => result.Attribute("testId")!.Value),
            StringComparer.OrdinalIgnoreCase);
        if (FindChild(mergedTestRun, "TestEntries") is { } testEntries)
        {
            testEntries.Elements()
                .Where(entry => !selectedExecutionIds.Contains(entry.Attribute("executionId")?.Value ?? string.Empty))
                .Remove();
        }

        if (FindChild(mergedTestRun, "TestDefinitions") is { } testDefinitions)
        {
            testDefinitions.Elements()
                .Where(definition => !selectedTestIds.Contains(definition.Attribute("id")?.Value ?? string.Empty))
                .Remove();
        }

        int passed = 0;
        int failedResultCount = 0;
        int explicitTimeoutCount = 0;
        int notExecuted = 0;
        foreach (XElement result in selectedResults)
        {
            switch (result.Attribute("outcome")?.Value)
            {
                case "Passed":
                    passed++;
                    break;
                case "Failed":
                    failedResultCount++;
                    break;
                case "Timeout":
                    explicitTimeoutCount++;
                    break;
                case "NotExecuted":
                case "Skipped":
                    notExecuted++;
                    break;
                default:
                    throw new InvalidDataException("A TRX retry result has an unsupported outcome.");
            }
        }

        XElement mergedSummary = FindChild(mergedTestRun, "ResultSummary")!;
        XElement latestSummary = FindChild(inputReports[^1].Root!, "ResultSummary")!;
        XElement latestCounters = FindChild(latestSummary, "Counters") ?? new XElement(NamespaceUri + "Counters");
        int latestExplicitTimeoutCount = inputReports[^1].Root is { } latestRun
            && FindChild(latestRun, "Results") is { } latestResults
                ? latestResults.Elements().Count(result => result.Attribute("outcome")?.Value == "Timeout")
                : 0;
        int latestTimeoutCount = int.TryParse(latestCounters.Attribute("timeout")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTimeoutCount)
            ? parsedTimeoutCount
            : 0;
        int failedTimeoutCount = Math.Min(
            failedResultCount,
            Math.Max(0, latestTimeoutCount - latestExplicitTimeoutCount));
        int failed = failedResultCount - failedTimeoutCount;
        int timedOut = explicitTimeoutCount + failedTimeoutCount;
        XElement counters = new(latestCounters);
        counters.SetAttributeValue("total", selectedResults.Length.ToString(CultureInfo.InvariantCulture));
        counters.SetAttributeValue("executed", (passed + failed).ToString(CultureInfo.InvariantCulture));
        counters.SetAttributeValue("passed", passed.ToString(CultureInfo.InvariantCulture));
        counters.SetAttributeValue("failed", failed.ToString(CultureInfo.InvariantCulture));
        counters.SetAttributeValue("timeout", timedOut.ToString(CultureInfo.InvariantCulture));
        counters.SetAttributeValue("notExecuted", notExecuted.ToString(CultureInfo.InvariantCulture));
        FindChild(mergedSummary, "Counters")?.ReplaceWith(counters);
        if (FindChild(mergedSummary, "Counters") is null)
        {
            mergedSummary.AddFirst(counters);
        }

        XElement? latestRunInfos = FindChild(latestSummary, "RunInfos");
        FindChild(mergedSummary, "RunInfos")?.Remove();
        if (latestRunInfos is not null)
        {
            XElement? output = FindChild(mergedSummary, "Output");
            (output ?? counters).AddAfterSelf(new XElement(latestRunInfos));
        }

        bool latestRunFailed = latestSummary.Attribute("outcome")?.Value is { } outcome
            && !string.Equals(outcome, "Completed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(outcome, "Passed", StringComparison.OrdinalIgnoreCase);
        mergedSummary.SetAttributeValue("outcome", latestRunFailed || failed > 0 || timedOut > 0 ? "Failed" : "Completed");

        return merged;
    }
}

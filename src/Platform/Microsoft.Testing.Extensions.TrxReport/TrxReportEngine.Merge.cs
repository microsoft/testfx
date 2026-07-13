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

        XDocument merged = Merge(reports, runId, runName);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Attachment hrefs inside CollectorDataEntries are relative to each input's deployment root
        // (they look like "<machine>/<file>" and physically live under
        // "<inputDir>/<inputDeploymentRoot>/In/..."). Copy those trees into the merged report's
        // deployment root so the carried-over hrefs resolve. Best-effort: failures to copy an
        // attachment must not fail the merge.
        RelocateAttachments(inputPaths, reports, outputDirectory ?? Directory.GetCurrentDirectory(), runName);

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
    /// Copies each input report's attachment deployment tree (<c>&lt;deploymentRoot&gt;/In</c>) into the
    /// merged report's deployment root, so the attachment hrefs carried over from
    /// <c>CollectorDataEntries</c> keep resolving. Best-effort: any copy failure is swallowed so it
    /// never fails the merge (matching the never-fail-the-run post-processing invariant).
    /// </summary>
    private static void RelocateAttachments(IReadOnlyList<string> inputPaths, IReadOnlyList<XDocument> reports, string outputDirectory, string runName)
    {
        string mergedDeploymentRoot = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(runName);
        string mergedInRoot = Path.Combine(outputDirectory, mergedDeploymentRoot, "In");

        for (int i = 0; i < inputPaths.Count && i < reports.Count; i++)
        {
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

                string sourceInRoot = Path.Combine(inputDirectory, inputDeploymentRoot, "In");
                if (Directory.Exists(sourceInRoot))
                {
                    CopyDirectoryRecursive(sourceInRoot, mergedInRoot);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort: a failed attachment copy must not fail the merge.
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.GetFiles(sourceDirectory))
        {
            // Do not overwrite: if two inputs contributed the same relative attachment path, keep the
            // first one rather than silently clobbering it.
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            if (!File.Exists(destination))
            {
                File.Copy(file, destination);
            }
        }

        foreach (string directory in Directory.GetDirectories(sourceDirectory))
        {
            CopyDirectoryRecursive(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
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
        string runDeploymentRoot = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(runName);
        testSettings.Add(new XElement(NamespaceUri + "Deployment", new XAttribute("runDeploymentRoot", runDeploymentRoot)));
        return testSettings;
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

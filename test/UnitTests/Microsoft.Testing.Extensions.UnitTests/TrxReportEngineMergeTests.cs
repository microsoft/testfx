// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxReportEngineMergeTests
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    [TestMethod]
    public void Merge_WithNullReports_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => TrxReportEngine.Merge(null!, Guid.NewGuid(), "run"));

    [TestMethod]
    public void Merge_WithNoReports_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => TrxReportEngine.Merge([], Guid.NewGuid(), "run"));

    [TestMethod]
    public void Merge_SetsProvidedRunIdAndName()
    {
        var runId = Guid.NewGuid();

        XDocument merged = TrxReportEngine.Merge([BuildReport(), BuildReport()], runId, "my-merged-run");

        XElement testRun = merged.Root!;
        Assert.AreEqual("TestRun", testRun.Name.LocalName);
        Assert.AreEqual(runId.ToString(), testRun.Attribute("id")!.Value);
        Assert.AreEqual("my-merged-run", testRun.Attribute("name")!.Value);
    }

    [TestMethod]
    public void Merge_SumsCounters()
    {
        XDocument a = BuildReport(total: 3, passed: 2, failed: 1, notExecuted: 0, timeout: 0);
        XDocument b = BuildReport(total: 4, passed: 1, failed: 0, notExecuted: 3, timeout: 0);

        XElement counters = Counters(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run"));

        Assert.AreEqual("7", counters.Attribute("total")!.Value);
        Assert.AreEqual("3", counters.Attribute("passed")!.Value);
        Assert.AreEqual("1", counters.Attribute("failed")!.Value);
        Assert.AreEqual("3", counters.Attribute("notExecuted")!.Value);
    }

    [TestMethod]
    public void Merge_WithAllCompletedReports_OutcomeIsCompleted()
    {
        XDocument merged = TrxReportEngine.Merge(
            [BuildReport(outcome: "Completed"), BuildReport(outcome: "Completed")],
            Guid.NewGuid(),
            "run");

        Assert.AreEqual("Completed", ResultSummary(merged).Attribute("outcome")!.Value);
    }

    [TestMethod]
    public void Merge_WhenAnyReportFailed_OutcomeIsFailed()
    {
        XDocument merged = TrxReportEngine.Merge(
            [BuildReport(outcome: "Completed"), BuildReport(outcome: "Failed")],
            Guid.NewGuid(),
            "run");

        Assert.AreEqual("Failed", ResultSummary(merged).Attribute("outcome")!.Value);
    }

    [TestMethod]
    public void Merge_WhenFailedCounterIsPositive_OutcomeIsFailed()
    {
        // Both reports claim "Completed" in the summary, but one has a positive failed counter.
        XDocument merged = TrxReportEngine.Merge(
            [BuildReport(outcome: "Completed", total: 1, passed: 1), BuildReport(outcome: "Completed", total: 1, passed: 0, failed: 1)],
            Guid.NewGuid(),
            "run");

        Assert.AreEqual("Failed", ResultSummary(merged).Attribute("outcome")!.Value);
    }

    [TestMethod]
    public void Merge_UnionsResults()
    {
        XDocument a = BuildReport(results: [Result("e1", "t1", "TestA")]);
        XDocument b = BuildReport(results: [Result("e2", "t2", "TestB"), Result("e3", "t3", "TestC")]);

        XElement results = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "Results");

        List<XElement> merged = [.. results.Elements()];
        Assert.HasCount(3, merged);
        List<string> testNames = [.. merged.Select(e => e.Attribute("testName")!.Value)];
        Assert.Contains("TestA", testNames);
        Assert.Contains("TestC", testNames);
    }

    [TestMethod]
    public void Merge_UnionsTestDefinitionsAndEntries()
    {
        XElement def = new(Ns + "UnitTest", new XAttribute("id", "t1"), new XAttribute("name", "TestA"));
        XElement entry = new(Ns + "TestEntry", new XAttribute("testId", "t1"), new XAttribute("executionId", "e1"));
        XDocument a = BuildReport(testDefinitions: [def], testEntries: [entry]);
        XDocument b = BuildReport(
            testDefinitions: [new XElement(Ns + "UnitTest", new XAttribute("id", "t2"), new XAttribute("name", "TestB"))],
            testEntries: [new XElement(Ns + "TestEntry", new XAttribute("testId", "t2"), new XAttribute("executionId", "e2"))]);

        XElement root = TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!;

        Assert.HasCount(2, Child(root, "TestDefinitions").Elements());
        Assert.HasCount(2, Child(root, "TestEntries").Elements());
    }

    [TestMethod]
    public void Merge_WhenSameIdDefinitionsDiffer_RemapsAndRewritesReferences()
    {
        // A multi-TFM merge can produce two TestDefinitions that share an id but differ (e.g. different
        // storage). The merge must keep both — remapping the later one to a fresh id and rewriting its
        // results/entries — rather than dropping the module-specific definition.
        XElement defA = new(Ns + "UnitTest", new XAttribute("id", "t1"), new XAttribute("storage", "a.dll"), new XAttribute("name", "SharedTest"));
        XElement defB = new(Ns + "UnitTest", new XAttribute("id", "t1"), new XAttribute("storage", "b.dll"), new XAttribute("name", "SharedTest"));
        XDocument a = BuildReport(
            testDefinitions: [defA],
            results: [new XElement(Ns + "UnitTestResult", new XAttribute("testId", "t1"), new XAttribute("executionId", "e1"))],
            testEntries: [new XElement(Ns + "TestEntry", new XAttribute("testId", "t1"), new XAttribute("executionId", "e1"))]);
        XDocument b = BuildReport(
            testDefinitions: [defB],
            results: [new XElement(Ns + "UnitTestResult", new XAttribute("testId", "t1"), new XAttribute("executionId", "e2"))],
            testEntries: [new XElement(Ns + "TestEntry", new XAttribute("testId", "t1"), new XAttribute("executionId", "e2"))]);

        XElement root = TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!;

        // Both definitions survive with distinct ids and both storages are present.
        List<XElement> definitions = [.. Child(root, "TestDefinitions").Elements()];
        Assert.HasCount(2, definitions);
        List<string> ids = [.. definitions.Select(e => e.Attribute("id")!.Value)];
        Assert.HasCount(2, ids.Distinct());
        List<string> storages = [.. definitions.Select(e => e.Attribute("storage")!.Value)];
        Assert.Contains("a.dll", storages);
        Assert.Contains("b.dll", storages);

        // Every result/entry testId must reference a real definition id (the second input's was remapped).
        var definitionIds = new HashSet<string>(ids);
        foreach (XElement result in Child(root, "Results").Elements())
        {
            Assert.Contains(result.Attribute("testId")!.Value, definitionIds);
        }

        foreach (XElement entry in Child(root, "TestEntries").Elements())
        {
            Assert.Contains(entry.Attribute("testId")!.Value, definitionIds);
        }
    }

    [TestMethod]
    public void Merge_WhenAnInputHasUnsuccessfulOutcome_MergedOutcomeIsFailed()
    {
        // A TRX summary outcome of "Error" (not just "Failed") is an unsuccessful run and must not be
        // flattened to "Completed" in the merged report.
        XDocument a = BuildReport(outcome: "Completed");
        XDocument b = BuildReport(outcome: "Error");

        XElement summary = ResultSummary(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run"));

        Assert.AreEqual("Failed", summary.Attribute("outcome")!.Value);
    }

    [TestMethod]
    public void Merge_DeduplicatesTestDefinitionsById()
    {
        // The same test discovered in two inputs yields the same deterministic UnitTest id; the
        // merged report must not emit duplicate <UnitTest id="...">.
        XElement sharedDef = new(Ns + "UnitTest", new XAttribute("id", "t1"), new XAttribute("name", "SharedTest"));
        XDocument a = BuildReport(testDefinitions: [new XElement(sharedDef)]);
        XDocument b = BuildReport(
            testDefinitions:
            [
                new XElement(sharedDef),
                new XElement(Ns + "UnitTest", new XAttribute("id", "t2"), new XAttribute("name", "OtherTest")),
            ]);

        XElement definitions = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "TestDefinitions");

        List<string> ids = [.. definitions.Elements().Select(e => e.Attribute("id")!.Value)];
        Assert.HasCount(2, ids);
        Assert.HasCount(2, ids.Distinct());
    }

    [TestMethod]
    public void Merge_PreservesRunInfosFromInputs()
    {
        XElement runInfos = new(
            Ns + "RunInfos",
            new XElement(Ns + "RunInfo", new XAttribute("outcome", "Error"), new XElement(Ns + "Text", "host crashed")));
        XDocument a = BuildReport(resultSummaryChildren: [runInfos]);
        XDocument b = BuildReport();

        XElement summary = ResultSummary(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run"));

        XElement? mergedRunInfos = summary.Elements().FirstOrDefault(e => e.Name.LocalName == "RunInfos");
        Assert.IsNotNull(mergedRunInfos);
        Assert.Contains("host crashed", mergedRunInfos!.Value);
    }

    [TestMethod]
    public void Merge_PreservesCollectorDataEntriesFromInputs()
    {
        XElement entries = new(
            Ns + "CollectorDataEntries",
            new XElement(Ns + "Collector", new XAttribute("collectorDisplayName", "Code Coverage")));
        XDocument a = BuildReport(resultSummaryChildren: [entries]);
        XDocument b = BuildReport();

        XElement summary = ResultSummary(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run"));

        XElement? mergedEntries = summary.Elements().FirstOrDefault(e => e.Name.LocalName == "CollectorDataEntries");
        Assert.IsNotNull(mergedEntries);
        Assert.HasCount(1, mergedEntries.Elements().Where(e => e.Name.LocalName == "Collector"));
    }

    [TestMethod]
    public void Merge_WhenNoDiagnostics_OmitsEmptyRunInfosAndCollectorDataEntries()
    {
        XElement summary = ResultSummary(TrxReportEngine.Merge([BuildReport(), BuildReport()], Guid.NewGuid(), "run"));

        List<string> childNames = [.. summary.Elements().Select(e => e.Name.LocalName)];
        Assert.DoesNotContain("RunInfos", childNames);
        Assert.DoesNotContain("CollectorDataEntries", childNames);
    }

    [TestMethod]
    public void Merge_DeduplicatesTestListsById()
    {
        // Both reports carry the two well-known shared test lists; the merged output keeps each id once.
        XDocument a = BuildReport(testLists: DefaultTestLists());
        XDocument b = BuildReport(testLists: [.. DefaultTestLists(), TestList("11111111-1111-1111-1111-111111111111", "Extra")]);

        XElement testLists = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "TestLists");

        List<string> ids = [.. testLists.Elements().Select(e => e.Attribute("id")!.Value)];
        Assert.HasCount(3, ids);
        Assert.HasCount(3, ids.Distinct());
    }

    [TestMethod]
    public void Merge_TimesUseEarliestStartAndLatestFinish()
    {
        var earlyStart = new DateTimeOffset(2020, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var lateFinish = new DateTimeOffset(2020, 1, 1, 13, 0, 0, TimeSpan.Zero);
        XDocument a = BuildReport(start: new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero), finish: new DateTimeOffset(2020, 1, 1, 11, 0, 0, TimeSpan.Zero));
        XDocument b = BuildReport(start: earlyStart, finish: lateFinish);

        XElement times = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "Times");

        Assert.AreEqual(earlyStart, DateTimeOffset.Parse(times.Attribute("start")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.AreEqual(lateFinish, DateTimeOffset.Parse(times.Attribute("finish")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    [TestMethod]
    public void Merge_PreservesResultFileAttachmentPaths()
    {
        string attachmentPath = Path.Combine(Path.GetTempPath(), "results-a", "log.txt");
        XElement resultWithAttachment = new(
            Ns + "UnitTestResult",
            new XAttribute("executionId", "e1"),
            new XElement(Ns + "ResultFiles", new XElement(Ns + "ResultFile", new XAttribute("path", attachmentPath))));

        XElement results = Child(TrxReportEngine.Merge([BuildReport(results: [resultWithAttachment]), BuildReport()], Guid.NewGuid(), "run").Root!, "Results");

        XElement? resultFile = results.Descendants(Ns + "ResultFile").FirstOrDefault();
        Assert.IsNotNull(resultFile);
        Assert.AreEqual(attachmentPath, resultFile.Attribute("path")!.Value);
    }

    [TestMethod]
    public void Merge_WithSingleReport_KeepsAllResults()
    {
        XDocument single = BuildReport(total: 2, passed: 2, results: [Result("e1", "t1", "TestA"), Result("e2", "t2", "TestB")]);

        XElement root = TrxReportEngine.Merge([single], Guid.NewGuid(), "run").Root!;

        Assert.HasCount(2, Child(root, "Results").Elements());
        Assert.AreEqual("2", Counters(root.Document!).Attribute("total")!.Value);
    }

    [TestMethod]
    public async Task MergeToFileAsync_WritesMergedFileToDisk()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string first = Path.Combine(tempDirectory, "a.trx");
            string second = Path.Combine(tempDirectory, "b.trx");
            string output = Path.Combine(tempDirectory, "nested", "merged.trx");
            BuildReport(total: 2, passed: 2).Save(first);
            BuildReport(total: 3, passed: 3).Save(second);

            await TrxReportEngine.MergeToFileAsync([first, second], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            var merged = XDocument.Load(output);
            Assert.AreEqual("5", Counters(merged).Attribute("total")!.Value);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_IsolatesCollidingAttachmentsPerInputAndRewritesHrefs()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Two inputs, each with an attachment at the SAME relative href ("machine/log.txt") but
            // different bytes. Without per-input isolation the second would resolve to the first's bytes.
            string firstInputDir = Path.Combine(tempDirectory, "inA");
            string secondInputDir = Path.Combine(tempDirectory, "inB");
            string first = WriteReportWithAttachment(firstInputDir, "a.trx", deploymentRoot: "depA", attachmentContent: "AAA");
            string second = WriteReportWithAttachment(secondInputDir, "b.trx", deploymentRoot: "depB", attachmentContent: "BBB");
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([first, second], output, Guid.NewGuid(), "run", CancellationToken.None);

            string mergedInRoot = Path.Combine(tempDirectory, "out", "run", "In");
            string firstCopied = Path.Combine(mergedInRoot, "0", "machine", "log.txt");
            string secondCopied = Path.Combine(mergedInRoot, "1", "machine", "log.txt");
            Assert.IsTrue(File.Exists(firstCopied));
            Assert.IsTrue(File.Exists(secondCopied));
            Assert.AreEqual("AAA", File.ReadAllText(firstCopied));
            Assert.AreEqual("BBB", File.ReadAllText(secondCopied));

            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.Contains("0/machine/log.txt", hrefs);
            Assert.Contains("1/machine/log.txt", hrefs);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Merge_WithIdenticalInputs_ProducesByteIdenticalXml()
    {
        var runId = Guid.NewGuid();

        // RFC 018 requires the merge to be idempotent: identical inputs, runId and runName must reproduce
        // identical output (the emitted TestSettings id is derived deterministically from runId).
        string first = TrxReportEngine.Merge([BuildReport(), BuildReport()], runId, "run").ToString();
        string second = TrxReportEngine.Merge([BuildReport(), BuildReport()], runId, "run").ToString();

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Merge_DerivesDeterministicTestSettingsIdDistinctFromRunId()
    {
        var runId = Guid.NewGuid();

        XDocument merged = TrxReportEngine.Merge([BuildReport()], runId, "run");
        string? settingsId = merged.Descendants().FirstOrDefault(e => e.Name.LocalName == "TestSettings")?.Attribute("id")?.Value;

        Assert.IsNotNull(settingsId);
        // Deterministic yet not the run id verbatim.
        Assert.AreNotEqual(runId.ToString(), settingsId);
        Assert.IsTrue(Guid.TryParse(settingsId, out _));
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputAliasesAnInput_ThrowsArgumentException()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string input = Path.Combine(tempDirectory, "a.trx");
            BuildReport().Save(input);

            // Writing the merged output over an input would destroy a read-only source; it must be rejected.
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => TrxReportEngine.MergeToFileAsync([input], input, Guid.NewGuid(), "run", CancellationToken.None));

            // The input must be left untouched.
            Assert.IsTrue(File.Exists(input));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenAttachmentHrefIsRooted_DropsTheReference()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // A rooted (absolute) href points outside the confined deployment tree; the path-confined
            // merge must drop it rather than preserve an absolute path to some file on disk.
            string inputDir = Path.Combine(tempDirectory, "in");
            Directory.CreateDirectory(Path.Combine(inputDir, "dep", "In", "machine"));

            string rootedHref = Path.Combine(tempDirectory, "outside", "secret.txt");
            var collectorDataEntries = new XElement(
                Ns + "CollectorDataEntries",
                new XElement(
                    Ns + "Collector",
                    new XAttribute("collectorDisplayName", "Code Coverage"),
                    new XElement(
                        Ns + "UriAttachments",
                        new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", rootedHref))))));

            XDocument report = BuildReport(resultSummaryChildren: [collectorDataEntries]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            var mergedDoc = XDocument.Load(output);
            List<string> hrefs = [.. mergedDoc.Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.IsEmpty(hrefs);
            // Dropping the <A> must also remove its owning <UriAttachment> so no schema-invalid empty
            // element is left behind.
            Assert.IsEmpty(mergedDoc.Descendants().Where(e => e.Name.LocalName == "UriAttachment"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenAttachmentHrefEscapesRoot_DropsTheReference()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string inputDir = Path.Combine(tempDirectory, "in");
            Directory.CreateDirectory(inputDir);

            // A hostile relative href that climbs above the deployment root. Even though it is not rooted,
            // resolving it from the merged deployment directory would escape the output tree, so the merge
            // must drop the reference rather than emit a traversal href.
            string attachmentDirectory = Path.Combine(inputDir, "dep", "In", "machine");
            Directory.CreateDirectory(attachmentDirectory);
            File.WriteAllText(Path.Combine(attachmentDirectory, "log.txt"), "AAA");

            var collectorDataEntries = new XElement(
                Ns + "CollectorDataEntries",
                new XElement(
                    Ns + "Collector",
                    new XAttribute("collectorDisplayName", "Code Coverage"),
                    new XElement(
                        Ns + "UriAttachments",
                        new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", "../../../secret"))))));

            XDocument report = BuildReport(resultSummaryChildren: [collectorDataEntries]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.IsEmpty(hrefs);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_RelocatesPerTestResultFilesByPrefixingResultDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Per-test ResultFiles resolve under In/<relativeResultsDirectory>/<path>. The physical file
            // lives at <deploymentRoot>/In/<relativeResultsDirectory>/<machine>/<file>, and the ResultFile
            // path is '<machine>/<file>'. Relocation must prefix the directory (not the path) so the
            // merged href resolves to the copied bytes.
            string inputDir = Path.Combine(tempDirectory, "in");
            const string executionId = "exec-1111";
            string physical = Path.Combine(inputDir, "dep", "In", executionId, "machine");
            Directory.CreateDirectory(physical);
            File.WriteAllText(Path.Combine(physical, "log.txt"), "AAA");

            var unitTestResult = new XElement(
                Ns + "UnitTestResult",
                new XAttribute("executionId", executionId),
                new XAttribute("relativeResultsDirectory", executionId),
                new XElement(Ns + "ResultFiles", new XElement(Ns + "ResultFile", new XAttribute("path", "machine/log.txt"))));

            XDocument report = BuildReport(results: [unitTestResult]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            var mergedDoc = XDocument.Load(output);
            XElement mergedResult = mergedDoc.Descendants().First(e => e.Name.LocalName == "UnitTestResult");
            string relativeDirectory = mergedResult.Attribute("relativeResultsDirectory")!.Value;
            string resultFilePath = mergedResult.Descendants().First(e => e.Name.LocalName == "ResultFile").Attribute("path")!.Value;

            // The directory is prefixed with the per-input isolation folder; the path is left intact.
            Assert.AreEqual($"0/{executionId}", relativeDirectory);
            Assert.AreEqual("machine/log.txt", resultFilePath);

            // The consumer-resolved path (In/<relativeResultsDirectory>/<path>) must point at real bytes.
            string resolved = Path.Combine(tempDirectory, "out", "run", "In", relativeDirectory.Replace('/', Path.DirectorySeparatorChar), resultFilePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(resolved));
            Assert.AreEqual("AAA", File.ReadAllText(resolved));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenResultDirectoryEscapesRoot_DropsResultFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // A hostile relativeResultsDirectory with a benign path: consumers resolve
            // In/<relativeResultsDirectory>/<path>, which escapes. The reference must be dropped.
            string inputDir = Path.Combine(tempDirectory, "in");
            string physical = Path.Combine(inputDir, "dep", "In", "machine");
            Directory.CreateDirectory(physical);
            File.WriteAllText(Path.Combine(physical, "log.txt"), "AAA");

            var unitTestResult = new XElement(
                Ns + "UnitTestResult",
                new XAttribute("executionId", "exec-1"),
                new XAttribute("relativeResultsDirectory", "../../.."),
                new XElement(Ns + "ResultFiles", new XElement(Ns + "ResultFile", new XAttribute("path", "machine/log.txt"))));

            XDocument report = BuildReport(results: [unitTestResult]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            List<string> resultFilePaths = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "ResultFile").Select(e => e.Attribute("path")!.Value)];
            Assert.IsEmpty(resultFilePaths);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenReferencedAttachmentIsNotMaterialized_DropsTheReference()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // The report references an attachment that has no physical backing (e.g. it was a skipped
            // source symlink, or a partial copy). The relocation must not emit a dangling href.
            string inputDir = Path.Combine(tempDirectory, "in");
            Directory.CreateDirectory(Path.Combine(inputDir, "dep", "In", "machine"));

            var collectorDataEntries = new XElement(
                Ns + "CollectorDataEntries",
                new XElement(
                    Ns + "Collector",
                    new XAttribute("collectorDisplayName", "Code Coverage"),
                    new XElement(
                        Ns + "UriAttachments",
                        new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", "machine/missing.txt"))))));

            XDocument report = BuildReport(resultSummaryChildren: [collectorDataEntries]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.IsEmpty(hrefs);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenInputHasNoMaterializedSource_DropsAllReferences()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // The report references an attachment but its deployment 'In' root does not exist at all, so
            // the source cannot be relocated. Its references are relative to the input's own deployment
            // root and would dangle against the (different) merged deployment root, so they must all be
            // dropped rather than carried through unchanged.
            string inputDir = Path.Combine(tempDirectory, "in");
            Directory.CreateDirectory(inputDir);

            var collectorDataEntries = new XElement(
                Ns + "CollectorDataEntries",
                new XElement(
                    Ns + "Collector",
                    new XAttribute("collectorDisplayName", "Code Coverage"),
                    new XElement(
                        Ns + "UriAttachments",
                        new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", "machine/log.txt"))))));

            XDocument report = BuildReport(resultSummaryChildren: [collectorDataEntries]);
            report.Root!.Add(new XElement(
                Ns + "TestSettings",
                new XAttribute("name", "default"),
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", "dep"))));

            string input = Path.Combine(inputDir, "a.trx");
            report.Save(input);
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.IsEmpty(hrefs);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

#if NETCOREAPP
    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputAliasesInputViaSymlinkedParent_ThrowsAndPreservesInput()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string realDir = Path.Combine(tempDirectory, "real");
            Directory.CreateDirectory(realDir);
            string input = Path.Combine(realDir, "a.trx");
            BuildReport().Save(input);

            string linkDir = Path.Combine(tempDirectory, "link");
            if (!TryCreateDirectorySymlink(linkDir, realDir))
            {
                // Directory symlinks require privileges on some platforms (e.g. non-elevated Windows);
                // skip when unavailable rather than fail.
                return;
            }

            // Output goes through the symlinked parent, so it is the SAME physical file as the input even
            // though the textual paths differ. Canonicalization must detect this and reject it, leaving
            // the input untouched.
            string aliasedOutput = Path.Combine(linkDir, "a.trx");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => TrxReportEngine.MergeToFileAsync([input], aliasedOutput, Guid.NewGuid(), "run", CancellationToken.None));

            Assert.IsTrue(File.Exists(input));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TryCreateDirectorySymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return Directory.Exists(linkPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
#endif

    [TestMethod]
    public async Task MergeToFileAsync_WhenRunNameEscapesOutputDirectory_UsesConfinedDeploymentRoot()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string inputDir = Path.Combine(tempDirectory, "in");
            string input = WriteReportWithAttachment(inputDir, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            // A hostile runName of ".." must be confined to a safe leaf used consistently for both the
            // emitted deployment root and attachment relocation, so nothing escapes the output directory.
            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "..", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));

            // The merged TRX must declare a confined deployment root (never "..").
            string? deploymentRoot = XDocument.Load(output).Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Deployment")?.Attribute("runDeploymentRoot")?.Value;
            Assert.AreEqual("_..", deploymentRoot);

            // Every physical attachment must remain under the output directory (never in its parent).
            List<string> attachmentCopies = [.. Directory.GetFiles(tempDirectory, "log.txt", SearchOption.AllDirectories)];
            foreach (string copy in attachmentCopies)
            {
                Assert.StartsWith(tempDirectory, copy);
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenMergedRootOverlapsInputTree_SkipsRelocationWithoutRecursing()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Output written beside the input, with runName matching the input's runDeploymentRoot, so
            // the merged 'In' root equals the source 'In' root. Relocation must skip (not recurse into
            // its own destination) and leave the already-resolvable href untouched.
            string input = WriteReportWithAttachment(tempDirectory, "a.trx", deploymentRoot: "run", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            Assert.IsTrue(File.Exists(Path.Combine(tempDirectory, "run", "In", "machine", "log.txt")));
            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.Contains("machine/log.txt", hrefs);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenSourceNestedUnderMergedRoot_RelocatesAndKeepsHrefsValid()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Output written into a subfolder whose deployment root ('run') sits ABOVE the input's own
            // deployment tree, so the source 'In' root is strictly nested under the merged 'In' root.
            // Relocation must stage the copy so the merged TRX's rewritten href points at real bytes.
            string inputDir = Path.Combine(tempDirectory, "run", "In", "child");
            string input = WriteReportWithAttachment(inputDir, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            // The rewritten href must resolve to a real file under the merged deployment root.
            string resolved = Path.Combine(tempDirectory, "run", "In", hrefs[0].Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(resolved));
            Assert.AreEqual("AAA", File.ReadAllText(resolved));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_RepeatedIntoSameNestedLayout_IsBoundedAndNonDestructive()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Layout where the merged deployment root is nested strictly INSIDE the input's source 'In'
            // tree. Because only referenced attachment files are copied (never the whole tree), repeated
            // merges into the same output neither recurse into their own destination nor accumulate
            // deeper copies: the original stays untouched and there is exactly one relocated copy.
            string input = WriteReportWithAttachment(tempDirectory, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "dep", "In", "out", "merged.trx");

            for (int run = 0; run < 4; run++)
            {
                await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);
            }

            Assert.IsTrue(File.Exists(output));

            // The original source report and attachment must survive every repeated merge untouched.
            Assert.IsTrue(File.Exists(input));
            string originalAttachment = Path.Combine(tempDirectory, "dep", "In", "machine", "log.txt");
            Assert.IsTrue(File.Exists(originalAttachment));
            Assert.AreEqual("AAA", File.ReadAllText(originalAttachment));

            // Original attachment + exactly one relocated copy — bounded, not one-per-run.
            List<string> copies = [.. Directory.GetFiles(tempDirectory, "log.txt", SearchOption.AllDirectories)];
            Assert.HasCount(2, copies);

            // The merged report's rewritten href must resolve to the relocated copy under the merged
            // deployment root ('<outputDir>/run/In').
            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            string resolved = Path.Combine(tempDirectory, "dep", "In", "out", "run", "In", hrefs[0].Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(resolved));
            Assert.AreEqual("AAA", File.ReadAllText(resolved));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenInputLivesUnderMergedRoot_PreservesOriginalReportAndAttachment()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // The input report and its attachment live under the merged 'In' root (output beside them,
            // runName 'run'). Relocation must never delete the originals (RFC 018 requires they remain).
            string inputDir = Path.Combine(tempDirectory, "run", "In", "0");
            string input = WriteReportWithAttachment(inputDir, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            // Original report and original attachment must still exist.
            Assert.IsTrue(File.Exists(input));
            Assert.IsTrue(File.Exists(Path.Combine(inputDir, "dep", "In", "machine", "log.txt")));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string WriteReportWithAttachment(string inputDirectory, string fileName, string deploymentRoot, string attachmentContent)
    {
        Directory.CreateDirectory(inputDirectory);

        // Physical attachment under "<deploymentRoot>/In/machine/log.txt", referenced by a
        // machine-relative href of "machine/log.txt".
        string attachmentDirectory = Path.Combine(inputDirectory, deploymentRoot, "In", "machine");
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "log.txt"), attachmentContent);

        var collectorDataEntries = new XElement(
            Ns + "CollectorDataEntries",
            new XElement(
                Ns + "Collector",
                new XAttribute("collectorDisplayName", "Code Coverage"),
                new XElement(
                    Ns + "UriAttachments",
                    new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", "machine/log.txt"))))));

        XDocument report = BuildReport(resultSummaryChildren: [collectorDataEntries]);
        report.Root!.Add(new XElement(
            Ns + "TestSettings",
            new XAttribute("name", "default"),
            new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", deploymentRoot))));

        string path = Path.Combine(inputDirectory, fileName);
        report.Save(path);
        return path;
    }

    private static XElement Child(XElement parent, string localName)
        => parent.Elements().First(e => e.Name.LocalName == localName);

    private static XElement ResultSummary(XDocument document)
        => Child(document.Root!, "ResultSummary");

    private static XElement Counters(XDocument document)
        => Child(ResultSummary(document), "Counters");

    private static XElement Result(string executionId, string testId, string testName)
        => new(
            Ns + "UnitTestResult",
            new XAttribute("executionId", executionId),
            new XAttribute("testId", testId),
            new XAttribute("testName", testName));

    private static XElement TestList(string id, string name)
        => new(Ns + "TestList", new XAttribute("id", id), new XAttribute("name", name));

    private static XElement[] DefaultTestLists()
        =>
        [
            TestList("8C84FA94-04C1-424b-9868-57A2D4851A1D", "Results Not in a List"),
            TestList("19431567-8539-422a-85D7-44EE4E166BDA", "All Loaded Results"),
        ];

    private static XDocument BuildReport(
        string outcome = "Completed",
        int total = 1,
        int passed = 1,
        int failed = 0,
        int notExecuted = 0,
        int timeout = 0,
        DateTimeOffset? start = null,
        DateTimeOffset? finish = null,
        IEnumerable<XElement>? results = null,
        IEnumerable<XElement>? testDefinitions = null,
        IEnumerable<XElement>? testEntries = null,
        IEnumerable<XElement>? testLists = null,
        IEnumerable<XElement>? resultSummaryChildren = null)
    {
        DateTimeOffset startTime = start ?? new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset finishTime = finish ?? new DateTimeOffset(2020, 1, 1, 11, 0, 0, TimeSpan.Zero);

        var resultSummary = new XElement(
            Ns + "ResultSummary",
            new XAttribute("outcome", outcome),
            new XElement(
                Ns + "Counters",
                new XAttribute("total", total),
                new XAttribute("executed", passed + failed),
                new XAttribute("passed", passed),
                new XAttribute("failed", failed),
                new XAttribute("timeout", timeout),
                new XAttribute("notExecuted", notExecuted)));

        if (resultSummaryChildren is not null)
        {
            resultSummary.Add(resultSummaryChildren);
        }

        var testRun = new XElement(
            Ns + "TestRun",
            new XAttribute("id", Guid.NewGuid()),
            new XAttribute("name", "run"),
            new XElement(
                Ns + "Times",
                new XAttribute("creation", startTime),
                new XAttribute("queuing", startTime),
                new XAttribute("start", startTime),
                new XAttribute("finish", finishTime)),
            new XElement(Ns + "Results", results ?? []),
            new XElement(Ns + "TestDefinitions", testDefinitions ?? []),
            new XElement(Ns + "TestEntries", testEntries ?? []),
            new XElement(Ns + "TestLists", testLists ?? DefaultTestLists()),
            resultSummary);

        return new XDocument(testRun);
    }
}

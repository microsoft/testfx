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
    public async Task MergeToFileAsync_WithNoInputs_ThrowsWithoutTouchingFilesystem()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        try
        {
            // An empty input list must be rejected before any filesystem work — the output directory must
            // not be created for an invalid call.
            string output = Path.Combine(tempDirectory, "out", "merged.trx");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => TrxReportEngine.MergeToFileAsync([], output, Guid.NewGuid(), "run", CancellationToken.None));

            Assert.IsFalse(Directory.Exists(tempDirectory));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Merge_CarriesRunLevelOutputMessages()
    {
        // VSTest records run-level skipped/informational messages under ResultSummary/Output/TextMessages;
        // the merge must carry them across (in schema order, right after Counters) rather than dropping them.
        var outputA = new XElement(
            Ns + "Output",
            new XElement(Ns + "StdOut", "hello from a"),
            new XElement(Ns + "TextMessages", new XElement(Ns + "Message", "skipped test X")));
        var outputB = new XElement(
            Ns + "Output",
            new XElement(Ns + "TextMessages", new XElement(Ns + "Message", "informational Y")));
        XDocument a = BuildReport(resultSummaryChildren: [outputA]);
        XDocument b = BuildReport(resultSummaryChildren: [outputB]);

        XElement summary = ResultSummary(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run"));

        // Output must appear immediately after Counters (schema order).
        List<string> childOrder = [.. summary.Elements().Select(e => e.Name.LocalName)];
        Assert.AreEqual("Counters", childOrder[0]);
        Assert.AreEqual("Output", childOrder[1]);

        XElement output = summary.Elements().First(e => e.Name.LocalName == "Output");
        Assert.AreEqual("hello from a", output.Elements().First(e => e.Name.LocalName == "StdOut").Value);
        List<string> messages = [.. output.Descendants().Where(e => e.Name.LocalName == "Message").Select(e => e.Value)];
        Assert.Contains("skipped test X", messages);
        Assert.Contains("informational Y", messages);
    }

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

        // Every result/entry testId must reference a real definition id, and — critically — each
        // execution must map to the definition with ITS OWN storage: e1 -> a.dll, e2 -> b.dll. This fails
        // if the remap did not rewrite the second input's references (both would resolve to a.dll).
        var storageByDefinitionId = definitions.ToDictionary(e => e.Attribute("id")!.Value, e => e.Attribute("storage")!.Value);

        string StorageForExecution(string containerName, string executionId)
        {
            XElement element = Child(root, containerName).Elements()
                .First(e => e.Attribute("executionId")!.Value == executionId);
            return storageByDefinitionId[element.Attribute("testId")!.Value];
        }

        Assert.AreEqual("a.dll", StorageForExecution("Results", "e1"));
        Assert.AreEqual("b.dll", StorageForExecution("Results", "e2"));
        Assert.AreEqual("a.dll", StorageForExecution("TestEntries", "e1"));
        Assert.AreEqual("b.dll", StorageForExecution("TestEntries", "e2"));
    }

    [TestMethod]
    public void Merge_CarriesRunLevelResultFiles()
    {
        // VSTest-produced TRX stores run-level attachments under ResultSummary/ResultFiles; the merge must
        // carry them across rather than silently losing them (only RunInfos/CollectorDataEntries were kept).
        var resultFiles = new XElement(Ns + "ResultFiles", new XElement(Ns + "ResultFile", new XAttribute("path", "run/summary.txt")));
        XDocument a = BuildReport(resultSummaryChildren: [resultFiles]);

        XElement summary = ResultSummary(TrxReportEngine.Merge([a, BuildReport()], Guid.NewGuid(), "run"));

        XElement? mergedResultFiles = summary.Elements().FirstOrDefault(e => e.Name.LocalName == "ResultFiles");
        Assert.IsNotNull(mergedResultFiles);
        Assert.HasCount(1, mergedResultFiles.Elements());
        Assert.AreEqual("run/summary.txt", mergedResultFiles.Elements().First().Attribute("path")!.Value);
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenSourceMissing_DropsRelativeButPreservesRootedReferences()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // The deployment 'In' root does not exist, so relocation is abandoned for this input. Its
            // RELATIVE references must be dropped (they would dangle against the merged root), but a ROOTED
            // reference resolves independently of the deployment root and must be preserved (RFC 018).
            string inputDir = Path.Combine(tempDirectory, "in");
            Directory.CreateDirectory(inputDir);
            string rootedHref = Path.Combine(tempDirectory, "abs", "kept.txt");

            var collectorDataEntries = new XElement(
                Ns + "CollectorDataEntries",
                new XElement(
                    Ns + "Collector",
                    new XAttribute("collectorDisplayName", "Code Coverage"),
                    new XElement(
                        Ns + "UriAttachments",
                        new XElement(Ns + "UriAttachment", new XElement(Ns + "A", new XAttribute("href", rootedHref))),
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
            Assert.HasCount(1, hrefs);
            Assert.AreEqual(rootedHref, hrefs[0]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
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
    public void Merge_TimesTrackEarliestCreationQueuingStartIndependently()
    {
        // Each Times attribute is tracked from its own inputs, not fabricated from the start: the merged
        // report keeps the earliest creation, earliest queuing and earliest start even when they come from
        // different inputs (creation and queuing legitimately predate execution).
        XDocument a = BuildTimesReport(creation: "2020-01-01T08:00:00.0000000+00:00", queuing: "2020-01-01T08:30:00.0000000+00:00", start: "2020-01-01T10:00:00.0000000+00:00", finish: "2020-01-01T11:00:00.0000000+00:00");
        XDocument b = BuildTimesReport(creation: "2020-01-01T09:00:00.0000000+00:00", queuing: "2020-01-01T08:15:00.0000000+00:00", start: "2020-01-01T09:30:00.0000000+00:00", finish: "2020-01-01T12:00:00.0000000+00:00");

        XElement times = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "Times");

        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T08:00:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("creation")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T08:15:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("queuing")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T09:30:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("start")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T12:00:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("finish")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    [TestMethod]
    public void Merge_TimesOmitAttributesNoInputSupplies()
    {
        // When no input carries creation/queuing, the merged report must omit them rather than invent them
        // from the start time.
        XDocument a = BuildTimesReport(creation: null, queuing: null, start: "2020-01-01T10:00:00.0000000+00:00", finish: "2020-01-01T11:00:00.0000000+00:00");
        XDocument b = BuildTimesReport(creation: null, queuing: null, start: "2020-01-01T09:00:00.0000000+00:00", finish: "2020-01-01T12:00:00.0000000+00:00");

        XElement times = Child(TrxReportEngine.Merge([a, b], Guid.NewGuid(), "run").Root!, "Times");

        Assert.IsNull(times.Attribute("creation"));
        Assert.IsNull(times.Attribute("queuing"));
        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T09:00:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("start")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.AreEqual(DateTimeOffset.Parse("2020-01-01T12:00:00+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(times.Attribute("finish")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    private static XDocument BuildTimesReport(string? creation, string? queuing, string? start, string? finish)
    {
        var times = new XElement(Ns + "Times");
        if (creation is not null)
        {
            times.SetAttributeValue("creation", creation);
        }

        if (queuing is not null)
        {
            times.SetAttributeValue("queuing", queuing);
        }

        if (start is not null)
        {
            times.SetAttributeValue("start", start);
        }

        if (finish is not null)
        {
            times.SetAttributeValue("finish", finish);
        }

        var testRun = new XElement(
            Ns + "TestRun",
            new XAttribute("id", Guid.NewGuid()),
            new XAttribute("name", "run"),
            times,
            new XElement(Ns + "Results"),
            new XElement(Ns + "TestDefinitions"),
            new XElement(Ns + "TestEntries"),
            new XElement(Ns + "TestLists", DefaultTestLists()),
            new XElement(
                Ns + "ResultSummary",
                new XAttribute("outcome", "Completed"),
                new XElement(Ns + "Counters", new XAttribute("total", 0), new XAttribute("passed", 0))));

        return new XDocument(testRun);
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

            string mergedRoot = XDocument.Load(output).Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            string mergedInRoot = Path.Combine(tempDirectory, "out", mergedRoot, "In");
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
    public async Task MergeToFileAsync_WhenAttachmentHrefIsRooted_PreservesTheReference()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // RFC 018 keeps absolute (rooted) attachment paths resolvable, so a rooted href is preserved
            // unchanged rather than relocated or dropped.
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

            List<string> hrefs = [.. XDocument.Load(output).Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            Assert.AreEqual(rootedHref, hrefs[0]);
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

            var mergedDoc = XDocument.Load(output);
            List<string> hrefs = [.. mergedDoc.Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.IsEmpty(hrefs);
            // Dropping the escaping <A> must also remove its owning <UriAttachment> so no schema-invalid
            // empty element is left behind.
            Assert.IsEmpty(mergedDoc.Descendants().Where(e => e.Name.LocalName == "UriAttachment"));
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
            string mergedRoot = mergedDoc.Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            string resolved = Path.Combine(tempDirectory, "out", mergedRoot, "In", relativeDirectory.Replace('/', Path.DirectorySeparatorChar), resultFilePath.Replace('/', Path.DirectorySeparatorChar));
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
            var mergedDoc = XDocument.Load(output);
            string? deploymentRoot = mergedDoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Deployment")?.Attribute("runDeploymentRoot")?.Value;
            Assert.IsNotNull(deploymentRoot);

            // The escape value ".." is confined to a leaf beginning "_.." and carrying no path separator,
            // so it can never traverse out of the output directory (the run-id suffix keeps it unique).
            Assert.StartsWith("_..", deploymentRoot);
            Assert.IsFalse(deploymentRoot.Contains('/') || deploymentRoot.Contains('\\'));
            Assert.HasCount(1, mergedDoc.Descendants().Where(e => e.Name.LocalName == "A"));

            // The relocated attachment must live under the OUTPUT directory (confined by the "_.." leaf),
            // never escaping it — asserting confinement to the output dir, not merely the temp root.
            string outputDirectory = Path.GetFullPath(Path.Combine(tempDirectory, "out"));
            List<string> relocatedCopies = [.. Directory.GetFiles(outputDirectory, "log.txt", SearchOption.AllDirectories)];
            Assert.HasCount(1, relocatedCopies);
            Assert.StartsWith(outputDirectory + Path.DirectorySeparatorChar, Path.GetFullPath(relocatedCopies[0]));
            Assert.AreEqual("AAA", File.ReadAllText(relocatedCopies[0]));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenRunNameMatchesInputDeploymentRoot_UsesUniqueRootAndRelocates()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // Output written beside the input, with runName matching the input's runDeploymentRoot. Even so,
            // the merged deployment root is made unique per run, so it can never coincide with (or recurse
            // into) the input's own tree: the attachment is relocated into the unique root, the input's
            // original stays untouched, and the rewritten href resolves under the emitted root.
            string input = WriteReportWithAttachment(tempDirectory, "a.trx", deploymentRoot: "run", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));

            // The input's original attachment must remain (RFC 018 inputs are read-only).
            Assert.IsTrue(File.Exists(Path.Combine(tempDirectory, "run", "In", "machine", "log.txt")));

            var mergedDoc = XDocument.Load(output);
            string mergedRoot = mergedDoc.Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            Assert.AreNotEqual("run", mergedRoot);

            List<string> hrefs = [.. mergedDoc.Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            string resolved = Path.Combine(tempDirectory, mergedRoot, "In", hrefs[0].Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(resolved));
            Assert.AreEqual("AAA", File.ReadAllText(resolved));
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
            // Output written beside a deeply-nested input. The merged deployment root is unique, so the
            // rewritten href resolves to the relocated copy under the emitted root (read from the report,
            // never assumed to be the plain runName).
            string inputDir = Path.Combine(tempDirectory, "run", "In", "child");
            string input = WriteReportWithAttachment(inputDir, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "merged.trx");

            await TrxReportEngine.MergeToFileAsync([input], output, Guid.NewGuid(), "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            var mergedDoc = XDocument.Load(output);
            string mergedRoot = mergedDoc.Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            List<string> hrefs = [.. mergedDoc.Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            // The rewritten href must resolve to a real file under the merged deployment root.
            string resolved = Path.Combine(tempDirectory, mergedRoot, "In", hrefs[0].Replace('/', Path.DirectorySeparatorChar));
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
            // Layout where the input attachment lives under a 'dep' tree and the merged output is written
            // deep inside it. Repeated merges of the SAME logical run (a stable run id, hence a stable
            // unique deployment root) must stay bounded and non-destructive: only referenced files are
            // copied (never the whole tree), so the original stays untouched and there is exactly one
            // relocated copy no matter how many times the merge runs.
            string input = WriteReportWithAttachment(tempDirectory, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "dep", "In", "out", "merged.trx");

            var runId = Guid.NewGuid();
            for (int run = 0; run < 4; run++)
            {
                await TrxReportEngine.MergeToFileAsync([input], output, runId, "run", CancellationToken.None);
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
            // deployment root (read from the report; unique per run).
            var mergedDoc = XDocument.Load(output);
            string mergedRoot = mergedDoc.Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            List<string> hrefs = [.. mergedDoc.Descendants().Where(e => e.Name.LocalName == "A").Select(e => e.Attribute("href")!.Value)];
            Assert.HasCount(1, hrefs);
            string resolved = Path.Combine(tempDirectory, "dep", "In", "out", mergedRoot, "In", hrefs[0].Replace('/', Path.DirectorySeparatorChar));
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
            // The input report and its attachment live under a 'run/In' tree while the output is written
            // beside them with runName 'run'. Relocation must never delete the originals (RFC 018 requires
            // they remain), and the unique merged root keeps relocation clear of the input's own tree.
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

    [TestMethod]
    public async Task MergeToFileAsync_SecondMergeIntoSameOutput_DoesNotCorruptPriorReportsAttachments()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            // A first merge commits a report plus its relocated attachment tree. A second, DIFFERENT merge
            // (distinct run id) into the same output path must write into its own unique deployment root and
            // leave the first report's referenced files byte-for-byte intact — so a failure of the second
            // merge could never corrupt an already-committed report (RFC 018 data-integrity).
            string firstInput = WriteReportWithAttachment(Path.Combine(tempDirectory, "in1"), "a.trx", deploymentRoot: "dep", attachmentContent: "FIRST");
            string output = Path.Combine(tempDirectory, "out", "merged.trx");
            await TrxReportEngine.MergeToFileAsync([firstInput], output, Guid.NewGuid(), "run", CancellationToken.None);

            var firstDoc = XDocument.Load(output);
            string firstRoot = firstDoc.Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            string firstHref = firstDoc.Descendants().First(e => e.Name.LocalName == "A").Attribute("href")!.Value;
            string firstCopy = Path.Combine(tempDirectory, "out", firstRoot, "In", firstHref.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(firstCopy));

            string secondInput = WriteReportWithAttachment(Path.Combine(tempDirectory, "in2"), "b.trx", deploymentRoot: "dep", attachmentContent: "SECOND");
            await TrxReportEngine.MergeToFileAsync([secondInput], output, Guid.NewGuid(), "run", CancellationToken.None);

            // The second merge used a distinct unique root, so the first report's attachment is untouched.
            string secondRoot = XDocument.Load(output).Descendants().First(e => e.Name.LocalName == "Deployment").Attribute("runDeploymentRoot")!.Value;
            Assert.AreNotEqual(firstRoot, secondRoot);
            Assert.IsTrue(File.Exists(firstCopy));
            Assert.AreEqual("FIRST", File.ReadAllText(firstCopy));
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

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
    public async Task MergeToFileAsync_WhenRunNameEscapesOutputDirectory_SkipsRelocation()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"trx-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string inputDir = Path.Combine(tempDirectory, "in");
            string input = WriteReportWithAttachment(inputDir, "a.trx", deploymentRoot: "dep", attachmentContent: "AAA");
            string output = Path.Combine(tempDirectory, "out", "merged.trx");

            // A hostile runName of ".." would place the merged deployment root outside the output
            // directory; relocation must refuse to write there but the merge itself must still succeed.
            await TrxReportEngine.MergeToFileAsync([input, input], output, Guid.NewGuid(), "..", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));

            // The only physical attachment must remain the single source copy under the input tree —
            // relocation must not have copied it anywhere (it was refused for escaping the output dir).
            List<string> attachmentCopies = [.. Directory.GetFiles(tempDirectory, "log.txt", SearchOption.AllDirectories)];
            Assert.HasCount(1, attachmentCopies);
            Assert.StartsWith(inputDir, attachmentCopies[0]);
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

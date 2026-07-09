// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportMergerTests
{
    [TestMethod]
    public void Merge_WithNullReports_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => JUnitReportMerger.Merge(null!, "run"));

    [TestMethod]
    public void Merge_WithNoReports_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => JUnitReportMerger.Merge([], "run"));

    [TestMethod]
    public void Merge_SumsRootCounters()
    {
        XDocument a = BuildReport(tests: 3, failures: 1, errors: 0, skipped: 1, time: 1.5);
        XDocument b = BuildReport(tests: 4, failures: 0, errors: 2, skipped: 0, time: 2.25);

        XElement root = JUnitReportMerger.Merge([a, b], "run").Root!;

        Assert.AreEqual("7", root.Attribute("tests")!.Value);
        Assert.AreEqual("1", root.Attribute("failures")!.Value);
        Assert.AreEqual("2", root.Attribute("errors")!.Value);
        Assert.AreEqual("1", root.Attribute("skipped")!.Value);
        Assert.AreEqual("3.750", root.Attribute("time")!.Value);
    }

    [TestMethod]
    public void Merge_UnionsSuitesAndReassignsSequentialIds()
    {
        XDocument a = BuildReport(suites: [Suite("SuiteA"), Suite("SuiteB")]);
        XDocument b = BuildReport(suites: [Suite("SuiteC")]);

        XElement root = JUnitReportMerger.Merge([a, b], "run").Root!;

        List<XElement> suites = [.. root.Elements().Where(e => e.Name.LocalName == "testsuite")];
        Assert.HasCount(3, suites);
        List<string> ids = [.. suites.Select(s => s.Attribute("id")!.Value)];
        Assert.AreSequenceEqual(new[] { "0", "1", "2" }, ids);
        List<string> names = [.. suites.Select(s => s.Attribute("name")!.Value)];
        Assert.Contains("SuiteA", names);
        Assert.Contains("SuiteC", names);
    }

    [TestMethod]
    public void Merge_SetsProvidedReportName()
    {
        XElement root = JUnitReportMerger.Merge([BuildReport(), BuildReport()], "my-merged-run").Root!;

        Assert.AreEqual("testsuites", root.Name.LocalName);
        Assert.AreEqual("my-merged-run", root.Attribute("name")!.Value);
    }

    [TestMethod]
    public void Merge_TimestampIsEarliestAcrossInputs()
    {
        XDocument a = BuildReport(timestamp: new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero));
        XDocument b = BuildReport(timestamp: new DateTimeOffset(2020, 1, 1, 9, 0, 0, TimeSpan.Zero));

        XElement root = JUnitReportMerger.Merge([a, b], "run").Root!;

        Assert.AreEqual("2020-01-01T09:00:00.000", root.Attribute("timestamp")!.Value);
    }

    [TestMethod]
    public void Merge_SupportsBareTestSuiteRootedInput()
    {
        // A document rooted at a bare <testsuite> (rather than <testsuites>) is a valid JUnit shape
        // and must not be silently dropped.
        var bareSuite = new XDocument(Suite("BareSuite", tests: 2, failures: 1));
        XDocument normal = BuildReport(tests: 3);

        XElement root = JUnitReportMerger.Merge([bareSuite, normal], "run").Root!;

        Assert.HasCount(2, root.Elements().Where(e => e.Name.LocalName == "testsuite"));
        Assert.AreEqual("5", root.Attribute("tests")!.Value);
        Assert.AreEqual("1", root.Attribute("failures")!.Value);
    }

    [TestMethod]
    public void Merge_DerivesCountersFromSuitesWhenRootAggregatesMissing()
    {
        // Root carries no aggregate attributes; totals must still come from the child suites.
        var rootWithoutAggregates = new XDocument(new XElement("testsuites", new XAttribute("name", "asm"), Suite("S1", tests: 4, skipped: 2)));
        XDocument normal = BuildReport(tests: 1);

        XElement root = JUnitReportMerger.Merge([rootWithoutAggregates, normal], "run").Root!;

        Assert.AreEqual("5", root.Attribute("tests")!.Value);
        Assert.AreEqual("2", root.Attribute("skipped")!.Value);
    }

    [TestMethod]
    public async Task MergeToFileAsync_WritesMergedFileToDisk()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"junit-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string first = Path.Combine(tempDirectory, "a.xml");
            string second = Path.Combine(tempDirectory, "b.xml");
            string output = Path.Combine(tempDirectory, "nested", "merged.xml");
            BuildReport(tests: 2).Save(first);
            BuildReport(tests: 3).Save(second);

            await JUnitReportMerger.MergeToFileAsync([first, second], output, "run", CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            var merged = XDocument.Load(output);
            Assert.AreEqual("5", merged.Root!.Attribute("tests")!.Value);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static XElement Suite(
        string name,
        long tests = 1,
        long failures = 0,
        long errors = 0,
        long skipped = 0,
        double time = 0,
        DateTimeOffset? timestamp = null)
        => new(
            "testsuite",
            new XAttribute("name", name),
            new XAttribute("tests", tests),
            new XAttribute("failures", failures),
            new XAttribute("errors", errors),
            new XAttribute("skipped", skipped),
            new XAttribute("time", time.ToString("0.000", CultureInfo.InvariantCulture)),
            new XAttribute("timestamp", (timestamp ?? new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)),
            new XElement("testcase", new XAttribute("name", $"{name}.Test1"), new XAttribute("classname", name)));

    private static XDocument BuildReport(
        long tests = 1,
        long failures = 0,
        long errors = 0,
        long skipped = 0,
        double time = 0,
        DateTimeOffset? timestamp = null,
        IEnumerable<XElement>? suites = null)
    {
        // The merged aggregates are derived from the per-suite counters, so the default suite carries
        // the report's counters/timestamp (the root aggregates are illustrative only).
        var root = new XElement(
            "testsuites",
            new XAttribute("name", "assembly"),
            new XAttribute("tests", tests),
            new XAttribute("failures", failures),
            new XAttribute("errors", errors),
            new XAttribute("skipped", skipped),
            new XAttribute("time", time.ToString("0.000", CultureInfo.InvariantCulture)),
            new XAttribute("timestamp", (timestamp ?? new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)),
            suites ?? [Suite("DefaultSuite", tests, failures, errors, skipped, time, timestamp)]);

        return new XDocument(root);
    }
}

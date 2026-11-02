// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxCompareToolTests
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_MatchingTestsInDifferentOrder_ReportsTotalsAndIgnoresStorageDifferences()
    {
        string baseline = WriteReport("baseline.trx", CreateReport(("Alpha", "Passed", "tests.dll"), ("Bravo", "Failed", "tests.dll")));
        string compared = WriteReport("compared.trx", CreateReport(("Bravo", "Failed", "other.dll"), ("Alpha", "Passed", "third.dll")));
        var context = new CompareContext(baseline, compared);

        int exitCode = await context.Tool.RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exitCode);
        string expected = $"""
            --- Baseline ---
            File '{baseline}'
            Issues:
              None

            Test containers (assemblies):
              - tests.dll

            Test results:
              - Passed: 1
              - Failed: 1

            --- Compared TRX ---
            File '{compared}'
            Issues:
              None

            Test containers (assemblies):
              - other.dll
              - third.dll

            Test results:
              - Failed: 1
              - Passed: 1

            --- Comparing TRX files ---

            Comparison check succeeded!

            """;
        Assert.AreEqual(Normalize(expected), Normalize(context.GetOutput()));
        Assert.AreSame(context.Tool, context.Producer);
        Assert.AreEqual(TestContext.CancellationToken, context.DisplayToken);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("outcome")]
    public async Task RunAsync_DifferentResults_ReturnsFailureWithCompleteComparisonDiagnostics(string difference)
    {
        XDocument comparison = difference switch
        {
            "missing" => CreateReport(),
            "duplicate" => CreateReport(("Alpha", "Passed", "tests.dll"), ("Alpha", "Passed", "tests.dll")),
            "outcome" => CreateReport(("Alpha", "Failed", "tests.dll")),
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };
        string baseline = WriteReport("baseline.trx", CreateReport(("Alpha", "Passed", "tests.dll")));
        var context = new CompareContext(baseline, WriteReport("compared.trx", comparison));

        int exitCode = await context.Tool.RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, exitCode);
        string expected = difference switch
        {
            "missing" => """
                --- Comparing TRX files ---
                  - Test 'Suite.Alpha' is missing inside the trx 'other'

                Comparison check failed!

                """,
            "duplicate" => """
                --- Comparing TRX files ---
                  - Test 'Suite.Alpha' is found multiple times inside the trx 'other'

                Comparison check failed!

                """,
            _ => """
                --- Comparing TRX files ---
                  - Test 'Suite.Alpha' has a different outcome. Got 'Failed', expected 'Passed'
                  - Test 'Suite.Alpha' has a different outcome. Got 'Passed', expected 'Failed'

                Comparison check failed!

                """,
        };
        Assert.EndsWith(Normalize(expected), Normalize(context.GetOutput()));
    }

    [TestMethod]
    [DataRow("testId", "UnitTestResult at index '1' is missing 'testId' attribute.")]
    [DataRow("testName", "UnitTestResult at index '1' is missing 'testName' attribute.")]
    [DataRow("outcome", "UnitTestResult at index '1' is missing 'outcome' attribute.")]
    [DataRow("duplicate-definition", "Found more than one entry in 'TestDefinitions.UnitTest' matching the test ID 'test-0'.")]
    [DataRow("missing-definition", "Cannot find any 'TestDefinitions.UnitTest' matching the test ID 'test-0'.")]
    [DataRow("storage", "Cannot find attribute 'storage' for 'TestDefinitions.UnitTest' with ID 'test-0'.")]
    [DataRow("className", "Cannot find attribute 'className' on sub node 'TestMethod' for 'TestDefinitions.UnitTest' with ID 'test-0'.")]
    [DataRow("TestMethod", "Cannot find attribute 'className' on sub node 'TestMethod' for 'TestDefinitions.UnitTest' with ID 'test-0'.")]
    public async Task RunAsync_MalformedResult_ReportsIssueAndStillComparesRemainingValidTests(string missingPart, string issue)
    {
        XDocument report = CreateReport(("Invalid", "Passed", "tests.dll"), ("Valid", "Passed", "tests.dll"));
        XElement invalidResult = report.Root!.Element(Ns + "Results")!.Elements().First();
        XElement invalidDefinition = report.Root.Element(Ns + "TestDefinitions")!.Elements().First();
        switch (missingPart)
        {
            case "testId":
            case "testName":
            case "outcome":
                invalidResult.Attribute(missingPart)!.Remove();
                break;
            case "duplicate-definition":
                invalidDefinition.AddAfterSelf(new XElement(invalidDefinition));
                break;
            case "missing-definition":
                invalidDefinition.Remove();
                break;
            case "storage":
                invalidDefinition.Attribute("storage")!.Remove();
                break;
            case "className":
                invalidDefinition.Element(Ns + "TestMethod")!.Attribute("className")!.Remove();
                break;
            default:
                invalidDefinition.Element(Ns + "TestMethod")!.Remove();
                break;
        }

        string baseline = WriteReport("baseline.trx", report);
        string compared = WriteReport("compared.trx", CreateReport(("Valid", "Passed", "tests.dll")));
        var context = new CompareContext(baseline, compared);

        int exitCode = await context.Tool.RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exitCode, "Reported metadata issues do not fail comparison when the valid entries match.");
        string expected = $"""
            --- Baseline ---
            File '{baseline}'
            Issues:
              - {issue}

            Test containers (assemblies):
              - tests.dll

            Test results:
              - Passed: 1

            --- Compared TRX ---
            File '{compared}'
            Issues:
              None

            Test containers (assemblies):
              - tests.dll

            Test results:
              - Passed: 1

            --- Comparing TRX files ---

            Comparison check succeeded!

            """;
        Assert.AreEqual(Normalize(expected), Normalize(context.GetOutput()));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_UnreadableReport_PropagatesFailureWithoutDisplayingSuccess(bool malformedXml)
    {
        string baseline = Path.Combine(TestContext.TestTempDirectory!, "invalid.trx");
        if (malformedXml)
        {
            File.WriteAllText(baseline, "<TestRun");
        }

        var context = new CompareContext(baseline, WriteReport("compared.trx", CreateReport()));

        if (malformedXml)
        {
            await Assert.ThrowsExactlyAsync<XmlException>(() => context.Tool.RunAsync(TestContext.CancellationToken));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => context.Tool.RunAsync(TestContext.CancellationToken));
        }

        Assert.IsEmpty(context.Output);
    }

    [TestMethod]
    public async Task RunAsync_OutputCanceled_PropagatesCancellationAfterComparingReports()
    {
        var context = new CompareContext(
            WriteReport("baseline.trx", CreateReport()), WriteReport("compared.trx", CreateReport()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.OutputDevice.Setup(output => output.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), cancellation.Token))
            .Returns(Task.FromCanceled(cancellation.Token));

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => context.Tool.RunAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        context.OutputDevice.Verify(
            output => output.DisplayAsync(context.Tool, It.IsAny<TextOutputDeviceData>(), cancellation.Token), Times.Once);
    }

    private string WriteReport(string filename, XDocument report)
    {
        string path = Path.Combine(TestContext.TestTempDirectory!, filename);
        report.Save(path);
        return path;
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static XDocument CreateReport(params (string Name, string Outcome, string Storage)[] tests)
    {
        var results = new XElement(Ns + "Results");
        var definitions = new XElement(Ns + "TestDefinitions");
        for (int i = 0; i < tests.Length; i++)
        {
            string id = $"test-{i}";
            results.Add(new XElement(
                Ns + "UnitTestResult",
                new XAttribute("testId", id), new XAttribute("testName", tests[i].Name), new XAttribute("outcome", tests[i].Outcome)));
            definitions.Add(new XElement(
                Ns + "UnitTest",
                new XAttribute("id", id), new XAttribute("storage", tests[i].Storage),
                new XElement(Ns + "TestMethod", new XAttribute("className", "Suite"))));
        }

        return new XDocument(new XElement(Ns + "TestRun", results, definitions));
    }

    private sealed class CompareContext
    {
        public CompareContext(string baseline, string compared)
        {
            OutputDevice.Setup(output => output.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((producer, data, token) =>
                {
                    Producer = producer;
                    DisplayToken = token;
                    Output.Add(data);
                })
                .Returns(Task.CompletedTask);
            Tool = new TrxCompareTool(
                new TestCommandLineOptions(new()
                {
                    [TrxCompareToolCommandLine.BaselineTrxOptionName] = [baseline],
                    [TrxCompareToolCommandLine.TrxToCompareOptionName] = [compared],
                }), new TestExtension(), OutputDevice.Object, new SystemTask());
        }

        public TrxCompareTool Tool { get; }

        public Mock<IOutputDevice> OutputDevice { get; } = new(MockBehavior.Strict);

        public List<IOutputDeviceData> Output { get; } = [];

        public IOutputDeviceDataProducer? Producer { get; private set; }

        public CancellationToken DisplayToken { get; private set; }

        public string GetOutput() => Assert.IsInstanceOfType<TextOutputDeviceData>(Assert.ContainsSingle(Output)).Text;
    }
}

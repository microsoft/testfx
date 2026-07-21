// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxCrashConsistencyContractTests
{
    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;

    [TestMethod]
    public async Task CurrentRenderer_DeterministicMixedResults_IsSemanticallyTruthful()
    {
        TrxRenderedScenario rendered = await TrxPrototypeScenarioFactory.RenderAsync(
            TrxPrototypeScenarioFactory.CreateMixedResultsScenario());

        TrxDocumentObservationContext context = new()
        {
            PublishedEventSequenceNumber = 5,
            LatestEventSequenceNumber = 5,
        };
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            rendered.Bytes,
            rendered.Expectation,
            context);

        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
        Assert.AreEqual(TrxSchemaStatus.NotChecked, observation.SchemaStatus);
        Assert.AreEqual(0, observation.Staleness);

        XElement root = rendered.Document.Root!;
        XElement results = root.Element(Ns + "Results")!;
        XElement definitions = root.Element(Ns + "TestDefinitions")!;
        XElement entries = root.Element(Ns + "TestEntries")!;
        XElement counters = root.Element(Ns + "ResultSummary")!.Element(Ns + "Counters")!;

        Assert.AreSequenceEqual(
            ["Scenario.Pass[1]", "Scenario.Failure", "Scenario.Skipped", "Scenario.Timeout", "Scenario.Pass[2]"],
            results.Elements(Ns + "UnitTestResult").Select(element => element.Attribute("testName")!.Value).ToArray());
        Assert.AreSequenceEqual(
            ["Passed", "Failed", "NotExecuted", "Failed", "Passed"],
            results.Elements(Ns + "UnitTestResult").Select(element => element.Attribute("outcome")!.Value).ToArray());
        Assert.HasCount(4, definitions.Elements(Ns + "UnitTest"));
        Assert.HasCount(5, entries.Elements(Ns + "TestEntry"));
        Assert.AreEqual("5", counters.Attribute("total")!.Value);
        Assert.AreEqual("3", counters.Attribute("executed")!.Value);
        Assert.AreEqual("2", counters.Attribute("passed")!.Value);
        Assert.AreEqual("1", counters.Attribute("failed")!.Value);
        Assert.AreEqual("1", counters.Attribute("timeout")!.Value);
        Assert.AreEqual("1", counters.Attribute("notExecuted")!.Value);

        foreach (TrxExpectedResult expected in rendered.Expectation.CompletedResults)
        {
            XElement result = results.Elements(Ns + "UnitTestResult")
                .Single(element => element.Attribute("executionId")!.Value == expected.ExecutionId);
            Assert.AreEqual(expected.TestId, result.Attribute("testId")!.Value);
            Assert.AreEqual(expected.TestName, result.Attribute("testName")!.Value);
            Assert.AreEqual(expected.ComputerName, result.Attribute("computerName")!.Value);
            Assert.AreEqual(expected.Duration, result.Attribute("duration")!.Value);
            Assert.AreEqual(
                expected.StartTime.ToString("O", CultureInfo.InvariantCulture),
                result.Attribute("startTime")!.Value);
            Assert.AreEqual(
                expected.EndTime.ToString("O", CultureInfo.InvariantCulture),
                result.Attribute("endTime")!.Value);
            Assert.AreEqual(expected.TestTypeId, result.Attribute("testType")!.Value);
            Assert.AreEqual(expected.TestListId, result.Attribute("testListId")!.Value);
            Assert.AreEqual(expected.RelativeResultsDirectory, result.Attribute("relativeResultsDirectory")!.Value);
            Assert.HasCount(
                1,
                entries.Elements(Ns + "TestEntry").Where(entry =>
                    entry.Attribute("testId")!.Value == expected.TestId
                    && entry.Attribute("executionId")!.Value == expected.ExecutionId
                    && entry.Attribute("testListId")!.Value == expected.TestListId));

            XElement definition = definitions.Elements(Ns + "UnitTest")
                .Single(element => element.Attribute("id")!.Value == expected.TestId);
            Assert.AreEqual(expected.Definition.Name, definition.Attribute("name")!.Value);
            Assert.AreEqual(expected.Definition.Storage, definition.Attribute("storage")!.Value);
            XElement method = definition.Element(Ns + "TestMethod")!;
            Assert.AreEqual(expected.Definition.ClassName, method.Attribute("className")!.Value);
            Assert.AreEqual(expected.Definition.MethodName, method.Attribute("name")!.Value);
            Assert.AreEqual(expected.Definition.CodeBase, method.Attribute("codeBase")!.Value);
            Assert.AreEqual(expected.Definition.AdapterTypeName, method.Attribute("adapterTypeName")!.Value);
        }
    }

    [TestMethod]
    public async Task CurrentRenderer_CurrentChildOrder_IsCaptured()
    {
        TrxPrototypeScenario scenario = TrxPrototypeScenarioFactory.CreateUnicodeMetadataScenario();
        TrxRenderedScenario rendered = await TrxPrototypeScenarioFactory.RenderAsync(
            scenario,
            new TrxScenarioRenderOptions
            {
                IncludeExtensionArtifact = true,
                FailingArtifactFileNames = [TrxPrototypeScenarioFactory.BadResultArtifactName],
            });

        XElement root = rendered.Document.Root!;
        Assert.AreSequenceEqual(
            ["Times", "TestSettings", "Results", "TestDefinitions", "TestEntries", "TestLists", "ResultSummary"],
            root.Elements().Select(element => element.Name.LocalName).ToArray());

        XElement result = root.Element(Ns + "Results")!.Element(Ns + "UnitTestResult")!;
        Assert.AreSequenceEqual(
            ["Output", "ResultFiles"],
            result.Elements().Select(element => element.Name.LocalName).ToArray());

        XElement definition = root.Element(Ns + "TestDefinitions")!.Element(Ns + "UnitTest")!;
        Assert.AreSequenceEqual(
            ["TestCategory", "Execution", "Owners", "Description", "Properties", "TestMethod"],
            definition.Elements().Select(element => element.Name.LocalName).ToArray());

        XElement summary = root.Element(Ns + "ResultSummary")!;
        Assert.AreSequenceEqual(
            ["Counters", "CollectorDataEntries", "RunInfos"],
            summary.Elements().Select(element => element.Name.LocalName).ToArray());

        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            rendered.Bytes,
            rendered.Expectation,
            new TrxDocumentObservationContext
            {
                PublishedEventSequenceNumber = 1,
                LatestEventSequenceNumber = 1,
            });
        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
    }

    [TestMethod]
    public async Task CurrentRenderer_UnicodeMetadataLargeOutputAndAttachments_RemainsTruthful()
    {
        TrxPrototypeScenario scenario = TrxPrototypeScenarioFactory.CreateUnicodeMetadataScenario();
        TrxRenderedScenario rendered = await TrxPrototypeScenarioFactory.RenderAsync(
            scenario,
            new TrxScenarioRenderOptions
            {
                FailingArtifactFileNames = [TrxPrototypeScenarioFactory.BadResultArtifactName],
            });

        XElement root = rendered.Document.Root!;
        XElement result = root.Element(Ns + "Results")!.Element(Ns + "UnitTestResult")!;
        XElement output = result.Element(Ns + "Output")!;
        XElement definition = root.Element(Ns + "TestDefinitions")!.Element(Ns + "UnitTest")!;
        XElement properties = definition.Element(Ns + "Properties")!;

        Assert.AreEqual("Unicode é漢😀 e\u0301 مرحبا <test>", result.Attribute("testName")!.Value);
        Assert.AreEqual(
            "stdout é漢😀 <&>\n control:\\u0001\nstdout second e\u0301 مرحبا",
            output.Element(Ns + "StdOut")!.Value);
        Assert.AreEqual("stderr 漢", output.Element(Ns + "StdErr")!.Value);
        Assert.AreEqual("debug 😀", output.Element(Ns + "DebugTrace")!.Value);
        Assert.AreEqual("exception é漢😀 <&> control:\\u0001", output.Descendants(Ns + "Message").Single().Value);
        Assert.AreEqual("stack e\u0301 مرحبا\nline", output.Descendants(Ns + "StackTrace").Single().Value);

        Assert.AreEqual("7", definition.Attribute("priority")!.Value);
        Assert.AreEqual("Zoë <owner>", definition.Element(Ns + "Owners")!.Element(Ns + "Owner")!.Attribute("name")!.Value);
        string description = definition.Element(Ns + "Description")!.Value;
        Assert.IsGreaterThan(500, Encoding.UTF8.GetByteCount(description));
        Assert.Contains("é漢😀", description);
        Assert.AreSequenceEqual(
            ["fast", "unicode-漢"],
            definition.Descendants(Ns + "TestCategoryItem").Select(element => element.Attribute("TestCategory")!.Value).ToArray());

        XElement customProperty = properties.Elements(Ns + "Property")
            .Single(element => element.Element(Ns + "Key")!.Value == "Custom<&>");
        Assert.IsGreaterThan(500, Encoding.UTF8.GetByteCount(customProperty.Element(Ns + "Value")!.Value));
        XElement resultFile = result.Element(Ns + "ResultFiles")!.Element(Ns + "ResultFile")!;
        Assert.AreEqual(
            Path.Combine(TrxPrototypeScenarioFactory.MachineName, TrxPrototypeScenarioFactory.GoodResultArtifactName),
            resultFile.Attribute("path")!.Value);

        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            rendered.Bytes,
            rendered.Expectation,
            new TrxDocumentObservationContext
            {
                PublishedEventSequenceNumber = 1,
                LatestEventSequenceNumber = 1,
            });
        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
    }

    [TestMethod]
    public async Task CurrentRenderer_CrashInfoAndAttachmentWarnings_PreserveSummarySemantics()
    {
        TrxRenderedScenario rendered = await TrxPrototypeScenarioFactory.RenderAsync(
            TrxPrototypeScenarioFactory.CreateUnicodeMetadataScenario(),
            new TrxScenarioRenderOptions
            {
                IsTestHostCrashed = true,
                TestHostCrashInfo = "test host crashed after deterministic event 1",
                IncludeExtensionArtifact = true,
                FailingArtifactFileNames = [TrxPrototypeScenarioFactory.BadResultArtifactName],
            });

        XElement summary = rendered.Document.Root!.Element(Ns + "ResultSummary")!;
        XElement counters = summary.Element(Ns + "Counters")!;
        List<XElement> runInfos = [.. summary.Element(Ns + "RunInfos")!.Elements(Ns + "RunInfo")];
        XElement collector = summary.Element(Ns + "CollectorDataEntries")!.Element(Ns + "Collector")!;

        Assert.AreEqual("Failed", summary.Attribute("outcome")!.Value);
        Assert.AreEqual("1", counters.Attribute("total")!.Value);
        Assert.AreEqual("1", counters.Attribute("passed")!.Value);
        Assert.HasCount(2, runInfos);
        Assert.AreSequenceEqual(
            ["Error", "Warning"],
            runInfos.Select(info => info.Attribute("outcome")!.Value).ToArray());
        Assert.AreEqual("test host crashed after deterministic event 1", runInfos[0].Element(Ns + "Text")!.Value);
        Assert.Contains("Unable to copy attachment", runInfos[1].Element(Ns + "Text")!.Value);
        Assert.Contains(TrxPrototypeScenarioFactory.BadResultArtifactName, runInfos[1].Element(Ns + "Text")!.Value);
        Assert.Contains("UnauthorizedAccessException", runInfos[1].Element(Ns + "Text")!.Value);
        Assert.AreEqual("datacollector://Uid/Version", collector.Attribute("uri")!.Value);
        Assert.AreEqual(
            Path.Combine(TrxPrototypeScenarioFactory.MachineName, TrxPrototypeScenarioFactory.ExtensionArtifactName),
            collector.Descendants(Ns + "A").Single().Attribute("href")!.Value);
    }
}

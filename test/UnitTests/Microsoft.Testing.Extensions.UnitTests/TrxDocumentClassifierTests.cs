// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxDocumentClassifierTests
{
    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;

    [TestMethod]
    public async Task Classify_InvalidUtf8OrTornXml_ReturnsMalformed()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();
        byte[] invalidUtf8 = [0x3C, 0x54, 0x65, 0x73, 0x74, 0xFF];
        byte[] tornXml = rendered.Bytes.Take(rendered.Bytes.Length - 17).ToArray();

        TrxDocumentObservation invalidUtf8Observation = TrxDocumentClassifier.Classify(invalidUtf8, rendered.Expectation);
        TrxDocumentObservation tornXmlObservation = TrxDocumentClassifier.Classify(tornXml, rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.Malformed, invalidUtf8Observation.Classification);
        Assert.Contains("Strict UTF-8 decoding failed", invalidUtf8Observation.Errors.Single());
        Assert.AreEqual(TrxDocumentClassification.Malformed, tornXmlObservation.Classification);
        Assert.Contains("XML parsing failed", tornXmlObservation.Errors.Single());
    }

    [TestMethod]
    public async Task Classify_ParseableWrongCountersOutcomeOrTimestamp_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        XDocument wrongCounters = Clone(rendered.Document);
        wrongCounters.Root!.Element(Ns + "ResultSummary")!.Element(Ns + "Counters")!.SetAttributeValue("passed", "99");
        TrxDocumentObservation countersObservation = Classify(wrongCounters, rendered.Expectation);

        XDocument wrongOutcome = Clone(rendered.Document);
        wrongOutcome.Root!.Element(Ns + "ResultSummary")!.SetAttributeValue("outcome", "Completed");
        TrxDocumentObservation outcomeObservation = Classify(wrongOutcome, rendered.Expectation);

        XDocument wrongTimestamp = Clone(rendered.Document);
        wrongTimestamp.Root!.Element(Ns + "Times")!.SetAttributeValue("finish", "not-a-timestamp");
        TrxDocumentObservation timestampObservation = Classify(wrongTimestamp, rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, countersObservation.Classification);
        Assert.Contains(error => error.Contains("Counters/@passed must be 2 but was 99", StringComparison.Ordinal), countersObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, outcomeObservation.Classification);
        Assert.Contains(error => error.Contains("ResultSummary/@outcome must be 'Failed'", StringComparison.Ordinal), outcomeObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, timestampObservation.Classification);
        Assert.Contains(error => error.Contains("Times/@finish must be a round-trip timestamp", StringComparison.Ordinal), timestampObservation.Errors);
    }

    [TestMethod]
    public async Task Classify_SemanticallyWrongCompletedResultAttributes_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();
        (string Name, Action<XElement> Mutate, string Error)[] mutations =
        [
            ("duration", result => result.SetAttributeValue("duration", "00:00:42.0000000"), "Result[0]/@duration"),
            ("startTime", result => result.SetAttributeValue("startTime", "2026-07-20T10:00:09.0000000+00:00"), "Result[0]/@startTime"),
            ("endTime", result => result.SetAttributeValue("endTime", "2026-07-20T10:00:10.0000000+00:00"), "Result[0]/@endTime"),
            ("computerName", result => result.SetAttributeValue("computerName", "other-machine"), "Result[0]/@computerName"),
            ("testType", result => result.SetAttributeValue("testType", Guid.Empty), "Result[0]/@testType"),
            ("testListId", result => result.SetAttributeValue("testListId", Guid.Empty), "Result[0]/@testListId"),
            ("relativeResultsDirectory", result => result.SetAttributeValue("relativeResultsDirectory", "plausible-but-wrong"), "Result[0]/@relativeResultsDirectory"),
        ];

        foreach ((string name, Action<XElement> mutate, string error) in mutations)
        {
            XDocument mutated = Clone(rendered.Document);
            mutate(mutated.Root!.Element(Ns + "Results")!.Elements(Ns + "UnitTestResult").First());

            TrxDocumentObservation observation = Classify(mutated, rendered.Expectation);

            Assert.AreEqual(
                TrxDocumentClassification.ParseableInconsistent,
                observation.Classification,
                $"{name}: {observation.Diagnostic}");
            Assert.Contains(
                candidate => candidate.Contains(error, StringComparison.Ordinal),
                observation.Errors,
                name);
        }
    }

    [TestMethod]
    public async Task Classify_SemanticallyWrongDefinitionIdentity_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();
        (string Name, Action<XElement> Mutate, string Error)[] mutations =
        [
            ("definition name", definition => definition.SetAttributeValue("name", "Wrong.Definition"), "/@name"),
            ("storage", definition => definition.SetAttributeValue("storage", "wrong.tests.dll"), "/@storage"),
            ("class", definition => definition.Element(Ns + "TestMethod")!.SetAttributeValue("className", "Wrong.Class"), "TestMethod/@className"),
            ("method", definition => definition.Element(Ns + "TestMethod")!.SetAttributeValue("name", "WrongMethod"), "TestMethod/@name"),
            ("code base", definition => definition.Element(Ns + "TestMethod")!.SetAttributeValue("codeBase", "Wrong.dll"), "TestMethod/@codeBase"),
            ("adapter", definition => definition.Element(Ns + "TestMethod")!.SetAttributeValue("adapterTypeName", "executor://wrong/0"), "TestMethod/@adapterTypeName"),
        ];

        foreach ((string name, Action<XElement> mutate, string error) in mutations)
        {
            XDocument mutated = Clone(rendered.Document);
            mutate(mutated.Root!.Element(Ns + "TestDefinitions")!.Elements(Ns + "UnitTest").First());

            TrxDocumentObservation observation = Classify(mutated, rendered.Expectation);

            Assert.AreEqual(
                TrxDocumentClassification.ParseableInconsistent,
                observation.Classification,
                $"{name}: {observation.Diagnostic}");
            Assert.Contains(
                candidate => candidate.Contains(error, StringComparison.Ordinal),
                observation.Errors,
                name);
        }
    }

    [TestMethod]
    public async Task Classify_SemanticallyWrongDefinitionMetadata_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await TrxPrototypeScenarioFactory.RenderAsync(
            TrxPrototypeScenarioFactory.CreateUnicodeMetadataScenario());
        (string Name, Action<XElement> Mutate, string Error)[] mutations =
        [
            ("priority", definition => definition.SetAttributeValue("priority", "8"), "/@priority"),
            ("owner", definition => definition.Element(Ns + "Owners")!.Element(Ns + "Owner")!.SetAttributeValue("name", "Wrong Owner"), "Owners/Owner/@name"),
            ("description", definition => definition.Element(Ns + "Description")!.Value = "Wrong description", "/Description"),
            ("category", definition => definition.Descendants(Ns + "TestCategoryItem").First().SetAttributeValue("TestCategory", "wrong-category"), "categories must be"),
            ("property key", definition => definition.Descendants(Ns + "Property").First().Element(Ns + "Key")!.Value = "WrongKey", "properties must be"),
            ("property value", definition => definition.Descendants(Ns + "Property").First().Element(Ns + "Value")!.Value = "WrongValue", "properties must be"),
        ];

        foreach ((string name, Action<XElement> mutate, string error) in mutations)
        {
            XDocument mutated = Clone(rendered.Document);
            mutate(mutated.Root!.Element(Ns + "TestDefinitions")!.Element(Ns + "UnitTest")!);

            TrxDocumentObservation observation = Classify(mutated, rendered.Expectation);

            Assert.AreEqual(
                TrxDocumentClassification.ParseableInconsistent,
                observation.Classification,
                $"{name}: {observation.Diagnostic}");
            Assert.Contains(
                candidate => candidate.Contains(error, StringComparison.Ordinal),
                observation.Errors,
                name);
        }
    }

    [TestMethod]
    public async Task Classify_SemanticallyWrongEntryOrDeclaredTestList_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        XDocument wrongEntryList = Clone(rendered.Document);
        wrongEntryList.Root!.Element(Ns + "TestEntries")!.Elements(Ns + "TestEntry").First()
            .SetAttributeValue("testListId", Guid.Empty);
        TrxDocumentObservation entryObservation = Classify(wrongEntryList, rendered.Expectation);

        XDocument wrongDeclaredList = Clone(rendered.Document);
        wrongDeclaredList.Root!.Element(Ns + "TestLists")!.Elements(Ns + "TestList").First()
            .SetAttributeValue("id", Guid.Empty);
        TrxDocumentObservation listObservation = Classify(wrongDeclaredList, rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, entryObservation.Classification);
        Assert.Contains(
            error => error.Contains("Entry[0]/@testListId", StringComparison.Ordinal),
            entryObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, listObservation.Classification);
        Assert.Contains(
            error => error.Contains("TestList[0]/@id", StringComparison.Ordinal),
            listObservation.Errors);
    }

    [TestMethod]
    public async Task Classify_TornTargetWithCompleteJournalPrefix_ReturnsRepairable()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();
        byte[] tornTarget = rendered.Bytes.Take(rendered.Bytes.Length / 2).ToArray();

        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            tornTarget,
            rendered.Expectation,
            new TrxDocumentObservationContext
            {
                PublishedEventSequenceNumber = 5,
                LatestEventSequenceNumber = 5,
            },
            completeRecoveryBytes: rendered.Bytes);

        Assert.AreEqual(TrxDocumentClassification.Repairable, observation.Classification, observation.Diagnostic);
        Assert.AreEqual(tornTarget.Length, observation.TargetLength);
        Assert.AreEqual(rendered.Bytes.Length, observation.RecoveryLength);
        Assert.Contains("XML parsing failed", observation.Errors.Single());
    }

    [TestMethod]
    public async Task Classify_ConsistentCurrentOrPriorSnapshot_ReturnsTruthfulAndReportsStaleness()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        TrxDocumentObservation current = TrxDocumentClassifier.Classify(
            rendered.Bytes,
            rendered.Expectation,
            new TrxDocumentObservationContext
            {
                PublishedEventSequenceNumber = 5,
                LatestEventSequenceNumber = 5,
            });
        TrxDocumentObservation prior = TrxDocumentClassifier.Classify(
            rendered.Bytes,
            rendered.Expectation,
            new TrxDocumentObservationContext
            {
                PublishedEventSequenceNumber = 5,
                LatestEventSequenceNumber = 8,
            });

        Assert.AreEqual(TrxDocumentClassification.Truthful, current.Classification, current.Diagnostic);
        Assert.AreEqual(0, current.Staleness);
        Assert.AreEqual(TrxDocumentClassification.Truthful, prior.Classification, prior.Diagnostic);
        Assert.AreEqual(3, prior.Staleness);
        Assert.AreEqual(5, prior.PublishedEventSequenceNumber);
    }

    [TestMethod]
    public async Task Classify_MissingDefinitionEntryOrExecutionLink_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        XDocument missingDefinition = Clone(rendered.Document);
        missingDefinition.Root!.Element(Ns + "TestDefinitions")!.Elements(Ns + "UnitTest").First().Remove();
        TrxDocumentObservation definitionObservation = Classify(missingDefinition, rendered.Expectation);

        XDocument missingEntry = Clone(rendered.Document);
        missingEntry.Root!.Element(Ns + "TestEntries")!.Elements(Ns + "TestEntry").First().Remove();
        TrxDocumentObservation entryObservation = Classify(missingEntry, rendered.Expectation);

        XDocument wrongLink = Clone(rendered.Document);
        wrongLink.Root!.Element(Ns + "TestEntries")!.Elements(Ns + "TestEntry").First()
            .SetAttributeValue("executionId", "ffffffff-ffff-ffff-ffff-ffffffffffff");
        TrxDocumentObservation linkObservation = Classify(wrongLink, rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, definitionObservation.Classification);
        Assert.Contains(error => error.Contains("Definition count must be 4 but was 3", StringComparison.Ordinal), definitionObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, entryObservation.Classification);
        Assert.Contains(error => error.Contains("Entry count must be 5 but was 4", StringComparison.Ordinal), entryObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, linkObservation.Classification);
        Assert.Contains(error => error.Contains("matching entries; exactly one is required", StringComparison.Ordinal), linkObservation.Errors);
    }

    [TestMethod]
    public async Task Classify_RunningSetAndOrderingMismatch_ReturnsParseableInconsistent()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        XDocument runningMismatch = Clone(rendered.Document);
        runningMismatch.Root!.Element(Ns + "Results")!.Elements(Ns + "UnitTestResult").First()
            .SetAttributeValue("outcome", "InProgress");
        TrxDocumentObservation runningObservation = Classify(runningMismatch, rendered.Expectation);

        XDocument orderMismatch = Clone(rendered.Document);
        XElement results = orderMismatch.Root!.Element(Ns + "Results")!;
        XElement first = results.Elements(Ns + "UnitTestResult").First();
        first.Remove();
        results.Add(first);
        TrxDocumentObservation orderObservation = Classify(orderMismatch, rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, runningObservation.Classification);
        Assert.Contains(error => error.Contains("Running result count must be 0 but was 1", StringComparison.Ordinal), runningObservation.Errors);
        Assert.AreEqual(TrxDocumentClassification.ParseableInconsistent, orderObservation.Classification);
        Assert.Contains(error => error.Contains("Result[0]/@testId", StringComparison.Ordinal), orderObservation.Errors);
    }

    [TestMethod]
    public async Task Classify_WithoutApprovedSchema_ReportsNotCheckedAndNeverClaimsSchemaValidity()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();

        TrxDocumentObservation truthful = TrxDocumentClassifier.Classify(rendered.Bytes, rendered.Expectation);
        TrxDocumentObservation malformed = TrxDocumentClassifier.Classify([0xFF], rendered.Expectation);

        Assert.AreEqual(TrxDocumentClassification.Truthful, truthful.Classification, truthful.Diagnostic);
        Assert.AreEqual(TrxSchemaStatus.NotChecked, truthful.SchemaStatus);
        Assert.DoesNotContain("schema=Valid", truthful.Diagnostic);
        Assert.AreEqual(TrxSchemaStatus.NotChecked, malformed.SchemaStatus);
    }

    [TestMethod]
    public async Task Diagnostics_IncludeOperationCutLengthsErrorAndBoundedByteWindow()
    {
        TrxRenderedScenario rendered = await RenderMixedAsync();
        byte[] torn = rendered.Bytes.Take(73).ToArray();
        TrxDocumentObservationContext context = new()
        {
            OperationIndex = 7,
            OperationKind = "Write",
            CommittedByteCount = 23,
            PreviousTargetLength = 4096,
            PublishedEventSequenceNumber = 2,
            LatestEventSequenceNumber = 5,
        };

        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            torn,
            rendered.Expectation,
            context,
            completeRecoveryBytes: rendered.Bytes);

        Assert.AreEqual(TrxDocumentClassification.Repairable, observation.Classification);
        Assert.Contains("operation=7:Write", observation.Diagnostic);
        Assert.Contains("cut=23", observation.Diagnostic);
        Assert.Contains("previousLength=4096", observation.Diagnostic);
        Assert.Contains($"targetLength={torn.Length}", observation.Diagnostic);
        Assert.Contains($"recoveryLength={rendered.Bytes.Length}", observation.Diagnostic);
        Assert.Contains("eventSequence=2", observation.Diagnostic);
        Assert.Contains("staleness=3", observation.Diagnostic);
        Assert.Contains("XML parsing failed", observation.Diagnostic);
        Assert.IsLessThanOrEqualTo(32, ParseWindowCount(observation.ByteWindow));
        Assert.IsLessThan(900, observation.Diagnostic.Length);
    }

    private static async Task<TrxRenderedScenario> RenderMixedAsync()
        => await TrxPrototypeScenarioFactory.RenderAsync(TrxPrototypeScenarioFactory.CreateMixedResultsScenario());

    private static XDocument Clone(XDocument document) => new(document);

    private static TrxDocumentObservation Classify(XDocument document, TrxDocumentExpectation expectation)
        => TrxDocumentClassifier.Classify(TrxPrototypeScenarioFactory.SaveUtf8(document), expectation);

    private static int ParseWindowCount(string byteWindow)
    {
        const string marker = "count=";
        int start = byteWindow.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = byteWindow.IndexOf(';', start);
        return int.Parse(byteWindow.Substring(start, end - start), CultureInfo.InvariantCulture);
    }
}

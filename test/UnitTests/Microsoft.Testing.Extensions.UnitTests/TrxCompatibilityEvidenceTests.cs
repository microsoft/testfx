// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxCompatibilityEvidenceTests
{
    private const string JournalPath = "compatibility.trx.journal";

    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;

    [TestMethod]
    public async Task PrototypeOrder_CurrentCanonicalAndIssueLayout_AreReportedSeparately()
    {
        TrxRenderedScenario current = await TrxPrototypeScenarioFactory.RenderAsync(
            TrxPrototypeScenarioFactory.CreateMixedResultsScenario());
        string[] currentOrder = [.. current.Document.Root!.Elements().Select(element => element.Name.LocalName)];

        byte[] issueBytes = TrxPrototypeXmlRenderer.RenderInitial(
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes: 256,
            entryPadBytes: 128,
            summaryPadBytes: 128,
            counterWidth: 3,
            runningSlotCount: 1,
            runningSlotByteCapacity: 320);
        string[] issueOrder =
        [
            .. TrxPrototypeScenarioFactory.LoadStrict(issueBytes).Root!
                .Elements()
                .Select(element => element.Name.LocalName),
        ];

        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            601,
            "streaming canonical",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(601);
        byte[] journalBytes = PublishJournal([result], [executionId], []);
        string[] journalOrder =
        [
            .. TrxPrototypeScenarioFactory.LoadStrict(journalBytes).Root!
                .Elements()
                .Select(element => element.Name.LocalName),
        ];

        Assert.AreSequenceEqual(
            ["Times", "TestSettings", "Results", "TestDefinitions", "TestEntries", "TestLists", "ResultSummary"],
            currentOrder);
        Assert.AreSequenceEqual(currentOrder, journalOrder);
        Assert.AreSequenceEqual(TrxPhase3EvidenceMatrix.PrototypeRootOrder, issueOrder);
        Assert.IsFalse(currentOrder.SequenceEqual(issueOrder, StringComparer.Ordinal));
    }

    [TestMethod]
    public void InProgress_WellFormednessSemanticMeaningAndSchemaStatus_AreSeparate()
    {
        TrxPrototypeRunningTest running = new()
        {
            Uid = TrxPhase3EvidenceMatrix.TestId(602),
            DisplayName = "running compatibility probe",
            ExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(602),
            StartTime = TrxPhase3EvidenceMatrix.StartTime,
        };
        byte[] bytes = PublishJournal([], [], [running]);
        TrxDocumentExpectation expectation = CreateExpectation(
            [],
            [],
            [
                new TrxExpectedRunningTest
                {
                    TestId = Guid.Parse(running.Uid).ToString(),
                    ExecutionId = running.ExecutionId.ToString(),
                    TestName = running.DisplayName,
                    ComputerName = TrxPhase3EvidenceMatrix.MachineName,
                    StartTime = running.StartTime,
                },
            ]);

        XDocument parsed = TrxPrototypeScenarioFactory.LoadStrict(bytes);
        TrxDocumentObservation semantic = TrxDocumentClassifier.Classify(bytes, expectation);
        var evidence = CompatibilityEvidence.CreateWithoutExternalProbes(semantic);

        Assert.IsNotNull(parsed.Root);
        Assert.AreEqual(TrxDocumentClassification.Truthful, evidence.SemanticClassification);
        Assert.AreEqual(TrxSchemaStatus.NotChecked, evidence.SchemaStatus);
        Assert.AreEqual(ExternalCompatibilityStatus.NotChecked, evidence.VisualStudio);
        Assert.AreEqual(ExternalCompatibilityStatus.NotChecked, evidence.AzureDevOps);
        Assert.AreEqual(ExternalCompatibilityStatus.NotChecked, evidence.ThirdPartyReaders);
    }

    [TestMethod]
    public async Task SummaryAndDefinitionChildOrder_MatchesCurrentCanonicalRenderer()
    {
        TrxPrototypeScenario scenario = TrxPrototypeScenarioFactory.CreateUnicodeMetadataScenario();
        TrxRenderedScenario current = await TrxPrototypeScenarioFactory.RenderAsync(
            scenario,
            new TrxScenarioRenderOptions
            {
                IsTestHostCrashed = true,
                TestHostCrashInfo = "compatibility crash",
                IncludeExtensionArtifact = true,
                FailingArtifactFileNames = [TrxPrototypeScenarioFactory.BadResultArtifactName],
            });
        TrxTestResult result = scenario.Results[0];
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(603);
        byte[] journalBytes = PublishJournal(
            [result],
            [executionId],
            [],
            new TrxPrototypeCompletion
            {
                FinishTime = TrxPrototypeScenarioFactory.FinishTime,
                IsTestHostCrashed = true,
                CrashText = "compatibility crash",
                AttachmentWarnings = ["compatibility warning"],
                CollectorAttachmentHrefs = [TrxPrototypeScenarioFactory.ExtensionArtifactName],
            });
        XDocument journal = TrxPrototypeScenarioFactory.LoadStrict(journalBytes);

        AssertRelativeOrder(
            current.Document.Root!.Element(Ns + "ResultSummary")!,
            ["Counters", "RunInfos", "CollectorDataEntries"]);
        AssertRelativeOrder(
            journal.Root!.Element(Ns + "ResultSummary")!,
            ["Counters", "RunInfos", "CollectorDataEntries"]);
        AssertRelativeOrder(
            current.Document.Root!.Element(Ns + "TestDefinitions")!.Element(Ns + "UnitTest")!,
            ["TestCategory", "Execution", "Owners", "Description", "Properties", "TestMethod"]);
        AssertRelativeOrder(
            journal.Root!.Element(Ns + "TestDefinitions")!.Element(Ns + "UnitTest")!,
            ["TestCategory", "Execution", "Owners", "Description", "Properties", "TestMethod"]);
    }

    [TestMethod]
    public void NoApprovedSchema_DoesNotProduceAFalseSchemaSuccess()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            604,
            "no schema claim",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(604);
        byte[] bytes = PublishJournal([result], [executionId], []);
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(
            bytes,
            CreateExpectation([result], [executionId]));
        var evidence = CompatibilityEvidence.CreateWithoutExternalProbes(observation);

        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification);
        Assert.AreEqual(TrxSchemaStatus.NotChecked, observation.SchemaStatus);
        Assert.DoesNotContain("schema=Valid", observation.Diagnostic);
        Assert.AreEqual("No approved TRX XSD is checked in.", evidence.SchemaBlocker);
        Assert.AreEqual(
            "Requires versioned execution in Visual Studio, Azure DevOps, and named third-party readers.",
            evidence.ExternalConsumerBlocker);
    }

    private static byte[] PublishJournal(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        IReadOnlyList<TrxPrototypeRunningTest> running,
        TrxPrototypeCompletion? completion = null)
    {
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        var journal = new TrxJournalSnapshotPrototype(
            operations,
            JournalPath,
            TrxPhase3EvidenceMatrix.TargetPath,
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion,
            TrxPhase3EvidenceMatrix.StartTime);
        for (int i = 0; i < results.Count; i++)
        {
            journal.Append(results[i], executionIds[i]);
        }

        journal.PublishSnapshot(
            completion
                ?? new TrxPrototypeCompletion
                {
                    FinishTime = TrxPhase3EvidenceMatrix.StartTime,
                    ExitCode = 1,
                },
            running);
        return operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
    }

    private static TrxDocumentExpectation CreateExpectation(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        IReadOnlyList<TrxExpectedRunningTest>? running = null)
    {
        TrxDocumentExpectation padded = TrxPhase3EvidenceMatrix.CreateExpectation(
            results,
            executionIds,
            running,
            finishTime: TrxPhase3EvidenceMatrix.StartTime);
        return new TrxDocumentExpectation
        {
            RunId = padded.RunId,
            RunName = padded.RunName,
            StartTime = padded.StartTime,
            FinishTime = padded.FinishTime,
            SummaryOutcome = padded.SummaryOutcome,
            CompletedResults = padded.CompletedResults,
            RunningTests = padded.RunningTests,
            Counters = padded.Counters,
        };
    }

    private static void AssertRelativeOrder(XElement parent, IReadOnlyList<string> allowedOrder)
    {
        int previous = -1;
        foreach (XElement child in parent.Elements())
        {
            int current = -1;
            for (int i = 0; i < allowedOrder.Count; i++)
            {
                if (string.Equals(allowedOrder[i], child.Name.LocalName, StringComparison.Ordinal))
                {
                    current = i;
                    break;
                }
            }

            Assert.IsGreaterThanOrEqualTo(0, current, child.Name.LocalName);
            Assert.IsGreaterThanOrEqualTo(previous, current, child.Name.LocalName);
            previous = current;
        }
    }

    private enum ExternalCompatibilityStatus
    {
        NotChecked,
        Compatible,
        Incompatible,
    }

    private sealed class CompatibilityEvidence
    {
        public required TrxDocumentClassification SemanticClassification { get; init; }

        public required TrxSchemaStatus SchemaStatus { get; init; }

        public required ExternalCompatibilityStatus VisualStudio { get; init; }

        public required ExternalCompatibilityStatus AzureDevOps { get; init; }

        public required ExternalCompatibilityStatus ThirdPartyReaders { get; init; }

        public required string SchemaBlocker { get; init; }

        public required string ExternalConsumerBlocker { get; init; }

        public static CompatibilityEvidence CreateWithoutExternalProbes(TrxDocumentObservation observation)
            => new()
            {
                SemanticClassification = observation.Classification,
                SchemaStatus = observation.SchemaStatus,
                VisualStudio = ExternalCompatibilityStatus.NotChecked,
                AzureDevOps = ExternalCompatibilityStatus.NotChecked,
                ThirdPartyReaders = ExternalCompatibilityStatus.NotChecked,
                SchemaBlocker = "No approved TRX XSD is checked in.",
                ExternalConsumerBlocker =
                    "Requires versioned execution in Visual Studio, Azure DevOps, and named third-party readers.",
            };
    }
}

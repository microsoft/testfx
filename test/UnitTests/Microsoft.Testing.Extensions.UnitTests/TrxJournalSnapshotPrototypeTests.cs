// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxJournalSnapshotPrototypeTests
{
    private const string JournalPath = "results.trx.journal";

    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;
    private static readonly Lazy<JournalFaultEvidence> SnapshotEvidence = new(CreateSnapshotFaultEvidence);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void JournalAppend_EveryOperationAndByteCut_ReplaysCompletePrefixOnly()
    {
        TrxTestResult result = CreateResult(501, "journal append é漢😀", TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(501);
        TrxFaultInjectingFileOperations baseline = AppendOne(result, executionId, terminationPlan: null);
        TrxFileOperationRecord[] trace = [.. baseline.Operations];
        Assert.AreSequenceEqual(
            [TrxFileOperationKind.Open, TrxFileOperationKind.Write, TrxFileOperationKind.Flush],
            trace.Select(operation => operation.Kind).ToArray());

        int emptyPrefixes = 0;
        int completePrefixes = 0;
        foreach (TrxFileOperationRecord operation in trace)
        {
            TrxFaultInjectingFileOperations faulted = AppendOne(
                result,
                executionId,
                new TrxTerminationPlan(operation.OperationIndex),
                expectTermination: true);
            int recovered = RecoverPublishedRecordCount(faulted.GetFileBytes(JournalPath));
            int expected = operation.Kind == TrxFileOperationKind.Open ? 0 : 1;
            Assert.AreEqual(expected, recovered, operation.ToString());
            emptyPrefixes += expected == 0 ? 1 : 0;
            completePrefixes += expected;
        }

        TrxFileOperationRecord write = trace.Single(operation => operation.Kind == TrxFileOperationKind.Write);
        for (int cut = 0; cut <= write.RequestedByteCount; cut++)
        {
            TrxFaultInjectingFileOperations faulted = AppendOne(
                result,
                executionId,
                new TrxTerminationPlan(write.OperationIndex, cut),
                expectTermination: true);
            int expected = cut == write.RequestedByteCount ? 1 : 0;
            Assert.AreEqual(expected, RecoverPublishedRecordCount(faulted.GetFileBytes(JournalPath)), $"cut={cut}");
            emptyPrefixes += expected == 0 ? 1 : 0;
            completePrefixes += expected;
        }

        TestContext.WriteLine(
            $"journal-append operations={trace.Length}; byteCuts={write.RequestedByteCount + 1}; emptyPrefixes={emptyPrefixes}; completePrefixes={completePrefixes}; encodedBytes={write.RequestedByteCount}");
    }

    [TestMethod]
    public void JournalAppend_TornLengthOrPayload_IgnoresOnlyFinalIncompleteRecord()
    {
        TrxTestResult first = CreateResult(502, "first complete", TrxTestOutcome.Passed);
        TrxTestResult second = CreateResult(503, "second torn", TrxTestOutcome.Failed);
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        journal.Append(first, TrxPhase3EvidenceMatrix.ExecutionId(502));
        int firstLength = operations.GetFileBytes(JournalPath).Length;
        journal.Append(second, TrxPhase3EvidenceMatrix.ExecutionId(503));
        byte[] complete = operations.GetFileBytes(JournalPath);
        int[] cuts =
        [
            firstLength,
            firstLength + 1,
            firstLength + 3,
            firstLength + 4,
            firstLength + ((complete.Length - firstLength) / 2),
            complete.Length - 1,
            complete.Length,
        ];

        foreach (int cut in cuts)
        {
            int expected = cut == complete.Length ? 2 : 1;
            Assert.AreEqual(expected, RecoverPublishedRecordCount(complete.Take(cut).ToArray()), $"cut={cut}");
        }
    }

    [TestMethod]
    public void JournalAppend_AfterTornRecord_EveryRecoveryOperationAndPartialAppend_RemainsAppendable()
    {
        TrxTestResult first = CreateResult(522, "first complete", TrxTestOutcome.Passed);
        TrxTestResult torn = CreateResult(523, "torn record", TrxTestOutcome.Failed);
        TrxTestResult resumed = CreateResult(524, "resumed record", TrxTestOutcome.Skipped);
        TrxTestResult final = CreateResult(525, "final record", TrxTestOutcome.Timeout);
        var sourceOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype source = CreateJournal(sourceOperations);
        source.Append(first, TrxPhase3EvidenceMatrix.ExecutionId(522));
        int firstLength = sourceOperations.GetFileBytes(JournalPath).Length;
        source.Append(torn, TrxPhase3EvidenceMatrix.ExecutionId(523));
        byte[] complete = sourceOperations.GetFileBytes(JournalPath);
        byte[] tornJournal = complete.Take(firstLength + ((complete.Length - firstLength) / 2)).ToArray();

        var baselineOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        baselineOperations.SeedFile(JournalPath, tornJournal);
        CreateJournal(baselineOperations).Append(resumed, TrxPhase3EvidenceMatrix.ExecutionId(524));
        TrxFileOperationRecord[] trace = [.. baselineOperations.Operations];
        Assert.Contains(TrxFileOperationKind.SetLength, trace.Select(operation => operation.Kind).ToArray());

        foreach (TrxFileOperationRecord operation in trace)
        {
            AssertJournalCanResumeAfterFault(
                tornJournal,
                resumed,
                TrxPhase3EvidenceMatrix.ExecutionId(524),
                final,
                TrxPhase3EvidenceMatrix.ExecutionId(525),
                new TrxTerminationPlan(operation.OperationIndex),
                operation.ToString());
        }

        TrxFileOperationRecord appendWrite = trace.Single(operation => operation.Kind == TrxFileOperationKind.Write);
        int[] byteCuts =
        [
            0,
            1,
            appendWrite.RequestedByteCount / 2,
            appendWrite.RequestedByteCount - 1,
            appendWrite.RequestedByteCount,
        ];
        foreach (int byteCut in byteCuts.Distinct())
        {
            AssertJournalCanResumeAfterFault(
                tornJournal,
                resumed,
                TrxPhase3EvidenceMatrix.ExecutionId(524),
                final,
                TrxPhase3EvidenceMatrix.ExecutionId(525),
                new TrxTerminationPlan(appendWrite.OperationIndex, byteCut),
                $"appendCut={byteCut}");
        }
    }

    [TestMethod]
    public void SnapshotPublish_EveryTempWriteFlushAndReplaceCut_PreservesPriorSnapshot()
    {
        JournalFaultEvidence evidence = SnapshotEvidence.Value;

        Assert.IsGreaterThan(0, evidence.OperationCaseCount);
        Assert.IsGreaterThan(0, evidence.ByteCutCaseCount);
        Assert.AreEqual(evidence.ObservationCount, evidence.OldTruthfulCount + evidence.NewTruthfulCount);
        Assert.AreEqual(0, evidence.MalformedCount);
        Assert.AreEqual(0, evidence.ParseableInconsistentCount);
        Assert.AreEqual(1, evidence.NewTruthfulCount);
        Assert.AreEqual(evidence.ObservationCount - 1, evidence.OldTruthfulCount);
        TestContext.WriteLine(evidence.Summary);
    }

    [TestMethod]
    public void SnapshotPublish_AfterReplace_EqualsCanonicalCompleteJournalPrefix()
    {
        TrxTestResult[] results =
        [
            CreateResult(504, "canonical pass", TrxTestOutcome.Passed),
            CreateResult(505, "canonical skip", TrxTestOutcome.Skipped),
        ];
        Guid[] executionIds =
        [
            TrxPhase3EvidenceMatrix.ExecutionId(504),
            TrxPhase3EvidenceMatrix.ExecutionId(505),
        ];
        TrxPrototypeCompletion completion = CreateCompletion();
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        for (int i = 0; i < results.Length; i++)
        {
            journal.Append(results[i], executionIds[i]);
        }

        journal.PublishSnapshot(completion);

        byte[] expected = CreateRenderer().RenderCompact(
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.StartTime,
            results,
            executionIds,
            completion);
        Assert.AreSequenceEqual(expected, operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
    }

    [TestMethod]
    public void SnapshotPublish_RepeatedAndUniqueIds_PreservesOrderDefinitionsEntriesAndCounters()
    {
        TrxTestResult[] results =
        [
            CreateResult(506, "repeat[1]", TrxTestOutcome.Passed, "Repeated.Definition"),
            CreateResult(507, "unique", TrxTestOutcome.Failed, "Unique.Definition"),
            CreateResult(506, "repeat[2]", TrxTestOutcome.Skipped, "Repeated.Definition"),
            CreateResult(508, "timeout", TrxTestOutcome.Timeout, "Timeout.Definition"),
        ];
        Guid[] executionIds =
        [
            TrxPhase3EvidenceMatrix.ExecutionId(506),
            TrxPhase3EvidenceMatrix.ExecutionId(507),
            TrxPhase3EvidenceMatrix.ExecutionId(509),
            TrxPhase3EvidenceMatrix.ExecutionId(508),
        ];
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        for (int i = 0; i < results.Length; i++)
        {
            journal.Append(results[i], executionIds[i]);
        }

        journal.PublishSnapshot(CreateCompletion());

        byte[] bytes = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        AssertTruthful(bytes, CreateExpectation(results, executionIds));
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(bytes);
        XElement root = document.Root!;
        Assert.AreSequenceEqual(
            executionIds.Select(value => value.ToString()).ToArray(),
            root.Element(Ns + "Results")!.Elements(Ns + "UnitTestResult")
                .Select(result => result.Attribute("executionId")!.Value).ToArray());
        Assert.AreSequenceEqual(
            [
                Guid.Parse(results[0].Uid).ToString(),
                Guid.Parse(results[1].Uid).ToString(),
                Guid.Parse(results[3].Uid).ToString(),
            ],
            root.Element(Ns + "TestDefinitions")!.Elements(Ns + "UnitTest")
                .Select(definition => definition.Attribute("id")!.Value).ToArray());
        Assert.HasCount(4, root.Element(Ns + "TestEntries")!.Elements(Ns + "TestEntry"));
        Assert.AreEqual(3, journal.Diagnostics.PublishedDefinitionCount);
        Assert.AreEqual(4, journal.Diagnostics.PublishedRecordCount);
    }

    [TestMethod]
    public void SnapshotPublish_RunningState_CarriesExplicitInProgressBreadcrumbsWithoutChangingJournalOutcomeEnum()
    {
        TrxTestResult completed = CreateResult(510, "completed", TrxTestOutcome.Passed);
        Guid completedExecution = TrxPhase3EvidenceMatrix.ExecutionId(510);
        TrxPrototypeRunningTest running = new()
        {
            Uid = TrxPhase3EvidenceMatrix.TestId(511),
            DisplayName = "running é漢😀 <&>",
            ExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(511),
            StartTime = TrxPhase3EvidenceMatrix.StartTime.AddMinutes(1),
        };
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        journal.Append(completed, completedExecution);

        journal.PublishSnapshot(CreateCompletion(), [running]);

        TrxDocumentExpectation expectation = CreateExpectation(
            [completed],
            [completedExecution],
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
        byte[] bytes = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        AssertTruthful(bytes, expectation);
        XElement runningElement = TrxPrototypeScenarioFactory.LoadStrict(bytes).Root!
            .Element(Ns + "Results")!
            .Elements(Ns + "UnitTestResult")
            .Single(element => element.Attribute("outcome")!.Value == "InProgress");
        Assert.AreEqual("InProgress", runningElement.Attribute("outcome")!.Value);
        Assert.DoesNotContain("InProgress", Enum.GetNames(typeof(TrxTestOutcome)));
    }

    [TestMethod]
    public void SnapshotPublish_UnicodeMetadataLargeOutputSummaryAndAttachmentReferences_MatchesCanonicalSemantics()
    {
        string longMetadata = "é漢😀 <&> e\u0301 مرحبا " + new string('m', 700);
        string largeOutput = "stdout é漢😀 <&> " + new string('o', 8_192);
        TrxTestResult result = CreateResult(
            512,
            "unicode é漢😀 <test>&",
            TrxTestOutcome.Failed,
            metadata:
            [
                new TrxTestMetadata { Key = "Owner", Value = "Zoë <owner>" },
                new TrxTestMetadata { Key = "Description", Value = longMetadata },
                new TrxTestMetadata { Key = "Custom<&>", Value = longMetadata },
            ],
            messages:
            [
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = largeOutput },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardError, Message = "stderr مرحبا" },
            ]);
        result = CloneWithArtifacts(
            result,
            [
                new TrxTestFileArtifact { FullPath = "result<&>.txt" },
                new TrxTestFileArtifact { FullPath = "résultat-漢😀.log" },
            ]);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(512);
        TrxPrototypeCompletion completion = new()
        {
            FinishTime = TrxPhase3EvidenceMatrix.FinishTime,
            IsTestHostCrashed = true,
            CrashText = "crash é漢😀 <&>",
            AttachmentWarnings = ["warning é漢😀 <&>"],
            CollectorAttachmentHrefs = ["collector/résultat-漢😀<&>.bin"],
        };
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        journal.Append(result, executionId);

        journal.PublishSnapshot(completion);

        byte[] actual = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        byte[] canonical = CreateRenderer().RenderCompact(
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.StartTime,
            [result],
            [executionId],
            completion);
        Assert.AreSequenceEqual(canonical, actual);
        AssertTruthful(
            actual,
            CreateExpectation([result], [executionId], finishTime: completion.FinishTime));
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(actual);
        XElement unitResult = document.Root!.Element(Ns + "Results")!.Element(Ns + "UnitTestResult")!;
        Assert.AreEqual(largeOutput, unitResult.Element(Ns + "Output")!.Element(Ns + "StdOut")!.Value);
        Assert.HasCount(2, unitResult.Element(Ns + "ResultFiles")!.Elements(Ns + "ResultFile"));
        Assert.AreEqual(
            completion.CollectorAttachmentHrefs[0],
            document.Descendants(Ns + "A").Single().Attribute("href")!.Value);
        Assert.IsGreaterThan(500, document.Descendants(Ns + "Description").Single().Value.Length);
        Assert.AreSequenceEqual(
            ["Counters", "RunInfos", "CollectorDataEntries"],
            document.Root!.Element(Ns + "ResultSummary")!.Elements().Select(element => element.Name.LocalName).ToArray());
    }

    [TestMethod]
    public void JournalAndPaddedWriter_SameEvents_ReportDeterministicClassificationDifferences()
    {
        TrxTestResult result = CreateResult(513, "differential", TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(513);
        var paddedBaselineOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxIncrementalWriterPrototype paddedBaseline = TrxPhase3EvidenceMatrix.CreateWriter(paddedBaselineOperations);
        paddedBaseline.Initialize();
        byte[] paddedPrior = paddedBaselineOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        paddedBaselineOperations.BeginFaultWindow(terminationPlan: null);
        paddedBaseline.AppendCompleted(result, executionId);
        TrxFileOperationRecord paddedWrite = paddedBaselineOperations.Operations.First(
            operation => operation.Kind == TrxFileOperationKind.Write
                && operation.RequestedByteCount > TrxPhase3EvidenceMatrix.CounterWidth);

        var paddedFaultOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxIncrementalWriterPrototype paddedFault = TrxPhase3EvidenceMatrix.CreateWriter(paddedFaultOperations);
        paddedFault.Initialize();
        Assert.AreSequenceEqual(paddedPrior, paddedFaultOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        paddedFaultOperations.BeginFaultWindow(
            new TrxTerminationPlan(paddedWrite.OperationIndex, committedByteCount: 1));
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => paddedFault.AppendCompleted(result, executionId));
        TrxDocumentObservation paddedObservation = TrxDocumentClassifier.Classify(
            paddedFaultOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
            CreateExpectation([], []));

        JournalFaultEvidence journalEvidence = SnapshotEvidence.Value;
        Assert.AreEqual(
            TrxDocumentClassification.Malformed,
            paddedObservation.Classification,
            paddedObservation.Diagnostic);
        Assert.AreEqual(0, journalEvidence.MalformedCount);
        Assert.AreEqual(0, journalEvidence.ParseableInconsistentCount);
    }

    [TestMethod]
    public void Replay_UsesBoundedRecordBufferAndDoesNotRetainAllResultsOrXDocument()
    {
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype journal = CreateJournal(operations);
        for (int i = 0; i < 50; i++)
        {
            journal.Append(
                CreateResult(520 + (i % 5), $"bounded-{i}", TrxTestOutcome.Passed, $"Definition.{i % 5}"),
                TrxPhase3EvidenceMatrix.ExecutionId(520 + i));
        }

        journal.PublishSnapshot(CreateCompletion());

        TrxJournalSnapshotDiagnostics diagnostics = journal.Diagnostics;
        Assert.AreEqual(0, diagnostics.CurrentReplayRecordCount);
        Assert.AreEqual(1, diagnostics.PeakReplayRecordCount);
        Assert.AreEqual(0, diagnostics.CurrentRecordBufferBytes);
        Assert.IsGreaterThan(diagnostics.MaxEncodedRecordBytes, diagnostics.PeakRecordBufferBytes);
        Assert.AreEqual(0, diagnostics.CurrentDefinitionIdCount);
        Assert.AreEqual(5, diagnostics.PeakDefinitionIdCount);
        Assert.IsFalse(diagnostics.RetainsResultCollection);
        Assert.IsFalse(diagnostics.RetainsXDocument);
        Assert.AreEqual(50, diagnostics.PublishedRecordCount);
        Assert.AreEqual(5, diagnostics.PublishedDefinitionCount);
        Assert.IsGreaterThan(0, diagnostics.MaxEncodedRecordBytes);
        Assert.IsGreaterThan(0, diagnostics.MaxRenderedFragmentBytes);
        Assert.IsLessThan(
            operations.GetFileBytes(JournalPath).Length,
            diagnostics.PeakLogicalBufferBytes);
        AssertNoRetainedResultCollectionOrXDocument(journal);
    }

    [TestMethod]
    public void JournalSnapshot_FullFaultMatrix_HasNoMalformedOrParseableInconsistentPublishedTarget()
    {
        JournalFaultEvidence evidence = SnapshotEvidence.Value;

        Assert.AreEqual(0, evidence.MalformedCount);
        Assert.AreEqual(0, evidence.ParseableInconsistentCount);
        Assert.AreEqual(evidence.ObservationCount, evidence.OldTruthfulCount + evidence.NewTruthfulCount);
        Assert.Contains(TrxFileOperationKind.Open, evidence.AllPrimitiveKinds);
        Assert.Contains(TrxFileOperationKind.Read, evidence.AllPrimitiveKinds);
        Assert.Contains(TrxFileOperationKind.Write, evidence.AllPrimitiveKinds);
        Assert.Contains(TrxFileOperationKind.Flush, evidence.AllPrimitiveKinds);
        Assert.Contains(TrxFileOperationKind.Replace, evidence.AllPrimitiveKinds);
    }

    private static JournalFaultEvidence CreateSnapshotFaultEvidence()
    {
        TrxTestResult oldResult = CreateResult(530, "prior", TrxTestOutcome.Passed);
        TrxTestResult newResult = CreateResult(531, "next é漢😀", TrxTestOutcome.Failed);
        Guid oldExecution = TrxPhase3EvidenceMatrix.ExecutionId(530);
        Guid newExecution = TrxPhase3EvidenceMatrix.ExecutionId(531);
        TrxDocumentExpectation oldExpectation = CreateExpectation([oldResult], [oldExecution]);
        TrxDocumentExpectation newExpectation = CreateExpectation(
            [oldResult, newResult],
            [oldExecution, newExecution]);

        var stateOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxJournalSnapshotPrototype state = CreateJournal(stateOperations);
        state.Append(oldResult, oldExecution);
        state.PublishSnapshot(CreateCompletion());
        byte[] oldSnapshot = stateOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        state.Append(newResult, newExecution);
        byte[] journalBytes = stateOperations.GetFileBytes(JournalPath);

        TrxFaultInjectingFileOperations baseline = PublishFromState(
            journalBytes,
            oldSnapshot,
            terminationPlan: null);
        TrxFileOperationRecord[] baselineTrace = [.. baseline.Operations];
        int oldTruthful = 0;
        int newTruthful = 0;
        int malformed = 0;
        int inconsistent = 0;
        int operationCases = 0;
        int byteCases = 0;

        foreach (TrxFileOperationRecord operation in baselineTrace)
        {
            TrxFaultInjectingFileOperations faulted = PublishFromState(
                journalBytes,
                oldSnapshot,
                new TrxTerminationPlan(operation.OperationIndex),
                expectTermination: true);
            ClassifyPublished(
                faulted.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
                oldSnapshot,
                oldExpectation,
                newExpectation,
                operation.Kind == TrxFileOperationKind.Replace,
                ref oldTruthful,
                ref newTruthful,
                ref malformed,
                ref inconsistent);
            operationCases++;

            if (operation.Kind != TrxFileOperationKind.Write)
            {
                continue;
            }

            for (int cut = 0; cut <= operation.RequestedByteCount; cut++)
            {
                faulted = PublishFromState(
                    journalBytes,
                    oldSnapshot,
                    new TrxTerminationPlan(operation.OperationIndex, cut),
                    expectTermination: true);
                ClassifyPublished(
                    faulted.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
                    oldSnapshot,
                    oldExpectation,
                    newExpectation,
                    expectNew: false,
                    ref oldTruthful,
                    ref newTruthful,
                    ref malformed,
                    ref inconsistent);
                byteCases++;
            }
        }

        return new JournalFaultEvidence
        {
            OperationCaseCount = operationCases,
            ByteCutCaseCount = byteCases,
            ObservationCount = operationCases + byteCases,
            OldTruthfulCount = oldTruthful,
            NewTruthfulCount = newTruthful,
            MalformedCount = malformed,
            ParseableInconsistentCount = inconsistent,
            AllPrimitiveKinds = baselineTrace.Select(operation => operation.Kind).Distinct().ToArray(),
            Summary = string.Format(
                CultureInfo.InvariantCulture,
                "journal-snapshot operations={0}; byteCuts={1}; observations={2}; oldTruthful={3}; newTruthful={4}; malformed={5}; inconsistent={6}",
                operationCases,
                byteCases,
                operationCases + byteCases,
                oldTruthful,
                newTruthful,
                malformed,
                inconsistent),
        };
    }

    private static void AssertNoRetainedResultCollectionOrXDocument(TrxJournalSnapshotPrototype journal)
    {
        foreach (System.Reflection.FieldInfo field in typeof(TrxJournalSnapshotPrototype).GetFields(
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
        {
            object? value = field.GetValue(journal);
            Assert.IsFalse(
                value is XDocument || typeof(XDocument).IsAssignableFrom(field.FieldType),
                $"Prototype field '{field.Name}' retains an XDocument.");
            Assert.IsFalse(
                ContainsResultType(field.FieldType, []),
                $"Prototype field '{field.Name}' retains a result collection.");
        }
    }

    private static bool ContainsResultType(Type type, HashSet<Type> visited)
    {
        if (type == typeof(TrxTestResult))
        {
            return true;
        }

        if (!visited.Add(type))
        {
            return false;
        }

        Type? elementType = type.HasElementType ? type.GetElementType() : null;
        bool genericContainsResult = type.IsGenericType
            && type.GetGenericArguments().Any(argument => ContainsResultType(argument, visited));
        bool fieldContainsResult = type.Assembly == typeof(TrxTestResult).Assembly
            && type.GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
                .Any(field => ContainsResultType(field.FieldType, visited));
        return elementType is not null
            ? ContainsResultType(elementType, visited)
            : genericContainsResult || fieldContainsResult;
    }

    private static void ClassifyPublished(
        byte[] actual,
        byte[] oldSnapshot,
        TrxDocumentExpectation oldExpectation,
        TrxDocumentExpectation newExpectation,
        bool expectNew,
        ref int oldTruthful,
        ref int newTruthful,
        ref int malformed,
        ref int inconsistent)
    {
        TrxDocumentExpectation expectation = expectNew ? newExpectation : oldExpectation;
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(actual, expectation);
        switch (observation.Classification)
        {
            case TrxDocumentClassification.Truthful:
                if (expectNew)
                {
                    newTruthful++;
                }
                else
                {
                    oldTruthful++;
                    Assert.AreSequenceEqual(oldSnapshot, actual);
                }

                break;
            case TrxDocumentClassification.Malformed:
                malformed++;
                break;
            case TrxDocumentClassification.ParseableInconsistent:
                inconsistent++;
                break;
            default:
                Assert.Fail(observation.Diagnostic);
                break;
        }
    }

    private static TrxFaultInjectingFileOperations PublishFromState(
        byte[] journalBytes,
        byte[] oldSnapshot,
        TrxTerminationPlan? terminationPlan,
        bool expectTermination = false)
    {
        var operations = new TrxFaultInjectingFileOperations(
            terminationPlan: terminationPlan,
            captureSnapshots: false);
        operations.SeedFile(JournalPath, journalBytes);
        operations.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, oldSnapshot);
        void Publish() => CreateJournal(operations).PublishSnapshot(CreateCompletion());
        if (expectTermination)
        {
            _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(Publish);
        }
        else
        {
            Publish();
        }

        return operations;
    }

    private static TrxFaultInjectingFileOperations AppendOne(
        TrxTestResult result,
        Guid executionId,
        TrxTerminationPlan? terminationPlan,
        bool expectTermination = false)
    {
        var operations = new TrxFaultInjectingFileOperations(
            terminationPlan: terminationPlan,
            captureSnapshots: false);
        void Append() => CreateJournal(operations).Append(result, executionId);
        if (expectTermination)
        {
            _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(Append);
        }
        else
        {
            Append();
        }

        return operations;
    }

    private static int RecoverPublishedRecordCount(byte[] journalBytes)
    {
        var recovery = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        recovery.SeedFile(JournalPath, journalBytes);
        TrxJournalSnapshotPrototype journal = CreateJournal(recovery);
        journal.PublishSnapshot(CreateCompletion());
        return journal.Diagnostics.PublishedRecordCount;
    }

    private static void AssertJournalCanResumeAfterFault(
        byte[] initialJournal,
        TrxTestResult interruptedResult,
        Guid interruptedExecutionId,
        TrxTestResult resumedResult,
        Guid resumedExecutionId,
        TrxTerminationPlan terminationPlan,
        string context)
    {
        var faulted = new TrxFaultInjectingFileOperations(
            terminationPlan: terminationPlan,
            captureSnapshots: false);
        faulted.SeedFile(JournalPath, initialJournal);
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => CreateJournal(faulted).Append(interruptedResult, interruptedExecutionId),
            context);
        byte[] persisted = faulted.GetFileBytes(JournalPath);
        int completePrefixCount = RecoverPublishedRecordCount(persisted);

        var restarted = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        restarted.SeedFile(JournalPath, persisted);
        TrxJournalSnapshotPrototype journal = CreateJournal(restarted);
        journal.Append(resumedResult, resumedExecutionId);
        journal.PublishSnapshot(CreateCompletion());

        Assert.AreEqual(completePrefixCount + 1, journal.Diagnostics.PublishedRecordCount, context);
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(
            restarted.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        Assert.Contains(
            resumedResult.DisplayName,
            document.Descendants(Ns + "UnitTestResult")
                .Select(element => element.Attribute("testName")!.Value)
                .ToArray(),
            context);
    }

    private static TrxJournalSnapshotPrototype CreateJournal(ITrxPrototypeFileOperations operations)
        => new(
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

    private static TrxPrototypeXmlRenderer CreateRenderer()
        => new(
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion);

    private static TrxTestResult CreateResult(
        int number,
        string displayName,
        TrxTestOutcome outcome,
        string? definitionName = null,
        IReadOnlyList<TrxTestMetadata>? metadata = null,
        IReadOnlyList<TrxStreamMessage>? messages = null)
        => TrxPhase3EvidenceMatrix.CreateResult(
            number,
            displayName,
            outcome,
            definitionName,
            metadata,
            categories: ["phase5", "journal"],
            messages);

    private static TrxTestResult CloneWithArtifacts(
        TrxTestResult source,
        IReadOnlyList<TrxTestFileArtifact> artifacts)
        => new()
        {
            Uid = source.Uid,
            DisplayName = source.DisplayName,
            Outcome = source.Outcome,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Duration = source.Duration,
            TrxTestDefinitionName = source.TrxTestDefinitionName,
            TrxFullyQualifiedTypeName = source.TrxFullyQualifiedTypeName,
            TestMethodIdentifier = source.TestMethodIdentifier,
            ExceptionMessage = "exception é漢😀 <&>",
            ExceptionStackTrace = "stack e\u0301 مرحبا",
            Messages = source.Messages,
            Categories = source.Categories,
            Metadata = source.Metadata,
            FileArtifacts = artifacts,
        };

    private static TrxPrototypeCompletion CreateCompletion()
        => new()
        {
            FinishTime = TrxPhase3EvidenceMatrix.StartTime,
            ExitCode = 1,
        };

    private static TrxDocumentExpectation CreateExpectation(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        IReadOnlyList<TrxExpectedRunningTest>? running = null,
        DateTimeOffset? finishTime = null)
    {
        TrxDocumentExpectation padded = TrxPhase3EvidenceMatrix.CreateExpectation(
            results,
            executionIds,
            running,
            finishTime: finishTime ?? TrxPhase3EvidenceMatrix.StartTime);
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

    private static void AssertTruthful(byte[] bytes, TrxDocumentExpectation expectation)
    {
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(bytes, expectation);
        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
    }

    private sealed class JournalFaultEvidence
    {
        public required int OperationCaseCount { get; init; }

        public required int ByteCutCaseCount { get; init; }

        public required int ObservationCount { get; init; }

        public required int OldTruthfulCount { get; init; }

        public required int NewTruthfulCount { get; init; }

        public required int MalformedCount { get; init; }

        public required int ParseableInconsistentCount { get; init; }

        public required IReadOnlyList<TrxFileOperationKind> AllPrimitiveKinds { get; init; }

        public required string Summary { get; init; }
    }
}

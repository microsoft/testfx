// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxConcurrentReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void VirtualReader_AfterEveryPrimitive_ReopensAndClassifiesPublishedTarget()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            61,
            "barrier-reader",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(61);
        TrxDocumentExpectation before = TrxPhase3EvidenceMatrix.CreateExpectation([], []);
        TrxDocumentExpectation after = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);
        bool observe = false;
        List<TrxDocumentObservation> observations = [];
        var operations = new TrxFaultInjectingFileOperations(
            afterOperation: (_, snapshot) =>
            {
                if (observe)
                {
                    byte[] reopened = snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
                    observations.Add(ClassifyAgainstEither(reopened, before, after));
                }
            });
        TrxIncrementalWriterPrototype writer = TrxPhase3EvidenceMatrix.CreateWriter(operations);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);
        observe = true;

        writer.AppendCompleted(result, executionId);

        Assert.HasCount(operations.Operations.Count, observations);
        Assert.IsTrue(observations.All(
            observation => observation.Classification is TrxDocumentClassification.Truthful
                or TrxDocumentClassification.ParseableInconsistent));
        Assert.Contains(
            observation => observation.Classification == TrxDocumentClassification.Truthful,
            observations);
        Assert.Contains(
            observation => observation.Classification == TrxDocumentClassification.ParseableInconsistent,
            observations);
        Assert.AreEqual(
            TrxDocumentClassification.Truthful,
            observations[observations.Count - 1].Classification,
            observations[observations.Count - 1].Diagnostic);
    }

    [TestMethod]
    public void MultipleVirtualReaders_AtSameBarrier_ObserveIdenticalBytes()
    {
        const int readerCount = 4;
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            62,
            "multiple-readers",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(62);
        bool observe = false;
        int barrierCount = 0;
        var operations = new TrxFaultInjectingFileOperations(
            afterOperation: (_, snapshot) =>
            {
                if (!observe)
                {
                    return;
                }

                barrierCount++;
                string?[] copies = new string?[readerCount];
                using var ready = new CountdownEvent(readerCount);
                using var start = new ManualResetEventSlim(initialState: false);
                var readers = new Thread[readerCount];
                for (int i = 0; i < readers.Length; i++)
                {
                    int readerIndex = i;
                    readers[i] = new Thread(
                        () =>
                        {
                            ready.Signal();
                            start.Wait(TestContext.CancellationToken);
                            copies[readerIndex] = Convert.ToBase64String(
                                snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
                        });
                    readers[i].Start();
                }

                ready.Wait(TestContext.CancellationToken);
                start.Set();
                foreach (Thread reader in readers)
                {
                    reader.Join();
                }

                Assert.IsNotNull(copies[0]);
                for (int i = 1; i < copies.Length; i++)
                {
                    Assert.AreEqual(copies[0], copies[i], $"Barrier {barrierCount}, reader {i}");
                }
            });
        TrxIncrementalWriterPrototype writer = TrxPhase3EvidenceMatrix.CreateWriter(operations);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);
        observe = true;

        writer.AppendCompleted(result, executionId);

        Assert.AreEqual(operations.Operations.Count, barrierCount);
        Assert.IsGreaterThan(0, barrierCount);
    }

    [TestMethod]
    public void Reader_BeforeAndAfterReplace_SeesOnlyWholeOldOrWholeNewSnapshot()
    {
        TrxDocumentExpectation oldExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([], []);
        byte[] oldBytes = RenderInitial();

        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            63,
            "replacement-new",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(63);
        TrxDocumentExpectation newExpectation =
            TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);
        var newOperations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype newWriter = TrxPhase3EvidenceMatrix.CreateWriter(newOperations);
        newWriter.Initialize();
        newWriter.AppendCompleted(result, executionId);
        byte[] newBytes = newOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);

        const string temporaryPath = "results.trx.prototype-0001.tmp";
        byte[]? atReplacement = null;
        var replaceOperations = new TrxFaultInjectingFileOperations(
            afterOperation: (operation, snapshot) =>
            {
                if (operation.Kind == TrxFileOperationKind.Replace)
                {
                    atReplacement = snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
                }
            });
        replaceOperations.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, oldBytes);
        replaceOperations.SeedFile(temporaryPath, newBytes);
        byte[] beforeReplacement = replaceOperations.CaptureSnapshot()
            .GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);

        replaceOperations.ReplaceTemporarySibling(temporaryPath, TrxPhase3EvidenceMatrix.TargetPath);

        Assert.IsNotNull(atReplacement);
        Assert.AreSequenceEqual(oldBytes, beforeReplacement);
        Assert.AreSequenceEqual(newBytes, atReplacement);
        Assert.AreEqual(
            TrxDocumentClassification.Truthful,
            TrxDocumentClassifier.Classify(beforeReplacement, oldExpectation).Classification);
        Assert.AreEqual(
            TrxDocumentClassification.Truthful,
            TrxDocumentClassifier.Classify(atReplacement, newExpectation).Classification);
        Assert.IsFalse(replaceOperations.CaptureSnapshot().Contains(temporaryPath));
    }

    [TestMethod]
    public void Reader_DuringPaddedMutation_CapturesExpectedIntermediateCounterexample()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            64,
            "partial-reader <&> é漢😀",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(64);
        TrxDocumentExpectation before = TrxPhase3EvidenceMatrix.CreateExpectation([], []);
        TrxDocumentExpectation after = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);

        var traceOperations = new TrxFaultInjectingFileOperations(captureSnapshots: false, recordOperations: false);
        TrxIncrementalWriterPrototype traceWriter = TrxPhase3EvidenceMatrix.CreateWriter(traceOperations);
        traceWriter.Initialize();
        traceOperations.BeginFaultWindow(terminationPlan: null);
        traceWriter.AppendCompleted(result, executionId);
        TrxFileOperationRecord definitionWrite = traceOperations.Operations.First(
            operation => operation.Kind == TrxFileOperationKind.Write
                && operation.RequestedByteCount > 100);

        TrxDocumentObservation? observed = null;
        bool observe = false;
        var faultOperations = new TrxFaultInjectingFileOperations(
            afterOperation: (operation, snapshot) =>
            {
                if (observe && operation.OperationIndex == definitionWrite.OperationIndex)
                {
                    observed = ClassifyAgainstEither(
                        snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
                        before,
                        after);
                }
            },
            captureSnapshots: false,
            recordOperations: false);
        TrxIncrementalWriterPrototype faultWriter = TrxPhase3EvidenceMatrix.CreateWriter(faultOperations);
        faultWriter.Initialize();
        faultOperations.BeginFaultWindow(
            new TrxTerminationPlan(definitionWrite.OperationIndex, committedByteCount: 1));
        observe = true;

        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => faultWriter.AppendCompleted(result, executionId));

        Assert.IsNotNull(observed);
        Assert.AreEqual(TrxDocumentClassification.Malformed, observed.Classification, observed.Diagnostic);
        Assert.IsTrue(faultOperations.IsProcessDead);
        Assert.HasCount(definitionWrite.OperationIndex + 1, faultOperations.Operations);
    }

    private static TrxDocumentObservation ClassifyAgainstEither(
        byte[] bytes,
        TrxDocumentExpectation before,
        TrxDocumentExpectation after)
    {
        TrxDocumentObservation prior = TrxDocumentClassifier.Classify(bytes, before);
        if (prior.Classification == TrxDocumentClassification.Truthful)
        {
            return prior;
        }

        TrxDocumentObservation current = TrxDocumentClassifier.Classify(bytes, after);
        return current.Classification == TrxDocumentClassification.Truthful
            ? current
            : prior.Classification == TrxDocumentClassification.ParseableInconsistent
                ? prior
                : current.Classification == TrxDocumentClassification.ParseableInconsistent
                    ? current
                    : prior;
    }

    private static byte[] RenderInitial()
        => TrxPrototypeXmlRenderer.RenderInitial(
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes: 4_096,
            entryPadBytes: 2_048,
            summaryPadBytes: 2_048,
            counterWidth: TrxPhase3EvidenceMatrix.CounterWidth,
            runningSlotCount: 2,
            runningSlotByteCapacity: 320);
}

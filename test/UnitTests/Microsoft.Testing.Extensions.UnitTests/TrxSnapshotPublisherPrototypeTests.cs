// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxSnapshotPublisherPrototypeTests
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DefinitionPadOverflow_ReflowsToLargerCapacityWithoutDataLoss()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            401,
            "definition overflow é漢😀",
            TrxTestOutcome.Passed,
            metadata:
            [
                new TrxTestMetadata
                {
                    Key = "Description",
                    Value = new string('d', 700),
                },
            ]);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(401);
        int definitionBytes = CreateRenderer().RenderDefinition(result, executionId).Length;
        int entryBytes = TrxPrototypeXmlRenderer.RenderEntry(result.Uid, executionId).Length;
        var operations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(
            operations,
            definitionPadBytes: definitionBytes - 1,
            entryPadBytes: entryBytes + 64);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);

        writer.AppendCompleted(result, executionId);

        _ = Assert.ContainsSingle(
            operation => operation.Kind == TrxFileOperationKind.Replace,
            operations.Operations);
        AssertTruthful(
            operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
            TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]));
        Assert.IsGreaterThan(
            0,
            GetWhitespaceBytes(
                TrxPrototypeScenarioFactory.LoadStrict(
                    operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath)),
                "TestDefinitions"));
    }

    [TestMethod]
    public void EntryPadOverflow_ReflowsToLargerCapacityWithoutDataLoss()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            402,
            "entry overflow",
            TrxTestOutcome.Skipped);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(402);
        int definitionBytes = CreateRenderer().RenderDefinition(result, executionId).Length;
        int entryBytes = TrxPrototypeXmlRenderer.RenderEntry(result.Uid, executionId).Length;
        var operations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(
            operations,
            definitionPadBytes: definitionBytes + 64,
            entryPadBytes: entryBytes - 1);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);

        writer.AppendCompleted(result, executionId);

        _ = Assert.ContainsSingle(
            operation => operation.Kind == TrxFileOperationKind.Replace,
            operations.Operations);
        AssertTruthful(
            operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
            TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]));
        Assert.IsGreaterThan(
            0,
            GetWhitespaceBytes(
                TrxPrototypeScenarioFactory.LoadStrict(
                    operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath)),
                "TestEntries"));
    }

    [TestMethod]
    public void SimultaneousPadOverflow_ReflowsOnceWithBothCapacitiesIncreased()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            403,
            "both pads overflow",
            TrxTestOutcome.Failed,
            metadata:
            [
                new TrxTestMetadata
                {
                    Key = "Custom",
                    Value = new string('x', 600),
                },
            ]);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(403);
        int definitionBytes = CreateRenderer().RenderDefinition(result, executionId).Length;
        int entryBytes = TrxPrototypeXmlRenderer.RenderEntry(result.Uid, executionId).Length;
        var operations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(
            operations,
            definitionPadBytes: definitionBytes - 1,
            entryPadBytes: entryBytes - 1);
        writer.Initialize();
        long oldLength = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath).LongLength;
        operations.BeginFaultWindow(terminationPlan: null);

        writer.AppendCompleted(result, executionId);

        byte[] published = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        _ = Assert.ContainsSingle(
            operation => operation.Kind == TrxFileOperationKind.Replace,
            operations.Operations);
        Assert.IsGreaterThan(oldLength, published.LongLength);
        AssertTruthful(
            published,
            TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]));
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(published);
        Assert.IsGreaterThan(0, GetWhitespaceBytes(document, "TestDefinitions"));
        Assert.IsGreaterThan(0, GetWhitespaceBytes(document, "TestEntries"));
    }

    [TestMethod]
    public void RunningSlotOverflow_IsDiagnosedOrReflowedWithoutLosingCompletedResults()
    {
        TrxTestResult completed = TrxPhase3EvidenceMatrix.CreateResult(
            404,
            "completed before slot overflow",
            TrxTestOutcome.Passed);
        Guid completedExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(404);
        Guid runningExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(405);
        var operations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(
            operations,
            definitionPadBytes: 4_096,
            entryPadBytes: 2_048,
            runningSlotCount: 1);
        writer.Initialize();
        writer.AppendCompleted(completed, completedExecutionId);
        _ = writer.ClaimRunning(
            TrxPhase3EvidenceMatrix.TestId(405),
            "occupied running slot",
            runningExecutionId,
            TrxPhase3EvidenceMatrix.StartTime.AddMinutes(1));
        byte[] before = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        operations.BeginFaultWindow(terminationPlan: null);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => writer.ClaimRunning(
                TrxPhase3EvidenceMatrix.TestId(406),
                "overflow",
                TrxPhase3EvidenceMatrix.ExecutionId(406),
                TrxPhase3EvidenceMatrix.StartTime));

        Assert.Contains("All 1 running-test slots are occupied", error.Message);
        Assert.IsEmpty(operations.Operations);
        Assert.AreSequenceEqual(before, operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        AssertTruthful(
            before,
            TrxPhase3EvidenceMatrix.CreateExpectation(
                [completed],
                [completedExecutionId],
                [
                    new TrxExpectedRunningTest
                    {
                        TestId = Guid.Parse(TrxPhase3EvidenceMatrix.TestId(405)).ToString(),
                        ExecutionId = runningExecutionId.ToString(),
                        TestName = "occupied running slot",
                        ComputerName = TrxPhase3EvidenceMatrix.MachineName,
                        StartTime = TrxPhase3EvidenceMatrix.StartTime.AddMinutes(1),
                    },
                ]));
    }

    [TestMethod]
    public void Reflow_EveryOpenReadSeekWriteFlushReplaceCleanupAndTempByteCut_PreservesTarget()
    {
        PublishedPair pair = CreatePublishedPair();
        TrxFaultInjectingFileOperations baselineOperations = PublishVerified(
            pair.OldBytes,
            pair.NewBytes,
            terminationPlan: null);
        TrxFileOperationRecord[] baseline = [.. baselineOperations.Operations];
        Assert.AreSequenceEqual(
            [
                TrxFileOperationKind.Open,
                TrxFileOperationKind.Write,
                TrxFileOperationKind.Seek,
                TrxFileOperationKind.Read,
                TrxFileOperationKind.Flush,
                TrxFileOperationKind.Replace,
            ],
            baseline.Select(operation => operation.Kind).ToArray());

        int oldClassifications = 0;
        int newClassifications = 0;
        foreach (TrxFileOperationRecord operation in baseline)
        {
            TrxFaultInjectingFileOperations faulted = PublishVerified(
                pair.OldBytes,
                pair.NewBytes,
                new TrxTerminationPlan(operation.OperationIndex),
                expectTermination: true);
            byte[] target = faulted.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
            if (operation.Kind == TrxFileOperationKind.Replace)
            {
                Assert.AreSequenceEqual(pair.NewBytes, target);
                AssertTruthful(target, pair.NewExpectation);
                newClassifications++;
            }
            else
            {
                Assert.AreSequenceEqual(pair.OldBytes, target);
                AssertTruthful(target, pair.OldExpectation);
                oldClassifications++;
            }
        }

        TrxFileOperationRecord write = baseline.Single(operation => operation.Kind == TrxFileOperationKind.Write);
        for (int cut = 0; cut <= write.RequestedByteCount; cut++)
        {
            TrxFaultInjectingFileOperations faulted = PublishVerified(
                pair.OldBytes,
                pair.NewBytes,
                new TrxTerminationPlan(write.OperationIndex, cut),
                expectTermination: true);
            Assert.AreSequenceEqual(pair.OldBytes, faulted.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
            string temporaryPath = faulted.CaptureSnapshot().Paths.Single(
                path => !string.Equals(path, TrxPhase3EvidenceMatrix.TargetPath, StringComparison.Ordinal));
            byte[] partial = faulted.GetFileBytes(temporaryPath);
            Assert.HasCount(cut, partial);
            Assert.AreSequenceEqual(pair.NewBytes.Take(cut).ToArray(), partial);
            oldClassifications++;
        }

        var cleanupBaselineInner = new TrxFaultInjectingFileOperations();
        cleanupBaselineInner.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, pair.OldBytes);
        var cleanupBaseline = new ThrowingReplaceOperations(cleanupBaselineInner);
        _ = Assert.ThrowsExactly<IOException>(
            () => new TrxSnapshotPublisherPrototype(cleanupBaseline).Publish(
                TrxPhase3EvidenceMatrix.TargetPath,
                file => WriteAndReadBack(file, pair.NewBytes)));
        TrxFileOperationRecord cleanup = cleanupBaselineInner.Operations.Single(
            operation => operation.Kind == TrxFileOperationKind.Delete);
        Assert.AreSequenceEqual(pair.OldBytes, cleanupBaselineInner.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));

        var cleanupFaultInner = new TrxFaultInjectingFileOperations(
            terminationPlan: new TrxTerminationPlan(cleanup.OperationIndex));
        cleanupFaultInner.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, pair.OldBytes);
        var cleanupFault = new ThrowingReplaceOperations(cleanupFaultInner);
        _ = Assert.ThrowsExactly<IOException>(
            () => new TrxSnapshotPublisherPrototype(cleanupFault).Publish(
                TrxPhase3EvidenceMatrix.TargetPath,
                file => WriteAndReadBack(file, pair.NewBytes)));
        Assert.IsTrue(cleanupFaultInner.IsProcessDead);
        Assert.AreSequenceEqual(pair.OldBytes, cleanupFaultInner.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));

        TestContext.WriteLine(
            "phase4 publication matrix: operations={0}; tempByteCuts={1}; oldTruthful={2}; newTruthful={3}; cleanup={4}",
            baseline.Length,
            write.RequestedByteCount + 1,
            oldClassifications,
            newClassifications,
            cleanup.Kind);
    }

    [TestMethod]
    public void Reflow_BeforeReplace_TargetIsPriorTruthfulSnapshot()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            407,
            "before replace",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(407);
        TrxDocumentExpectation oldExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([], []);
        TrxDocumentExpectation newExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);
        bool observe = false;
        int oldBarriers = 0;
        var operations = new TrxFaultInjectingFileOperations(
            afterOperation: (operation, snapshot) =>
            {
                if (!observe || operation.Kind == TrxFileOperationKind.Replace)
                {
                    return;
                }

                AssertTruthful(
                    snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
                    oldExpectation);
                oldBarriers++;
            });
        TrxIncrementalWriterPrototype writer = CreateOverflowingSafeWriter(operations, result, executionId);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);
        observe = true;

        writer.AppendCompleted(result, executionId);

        Assert.IsGreaterThan(0, oldBarriers);
        AssertTruthful(operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath), newExpectation);
    }

    [TestMethod]
    public void Reflow_AfterReplace_TargetIsNewTruthfulSnapshot()
    {
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            408,
            "after replace",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(408);
        TrxDocumentExpectation expectation = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);
        bool observe = false;
        int replaceBarriers = 0;
        var operations = new TrxFaultInjectingFileOperations(
            afterOperation: (operation, snapshot) =>
            {
                if (observe && operation.Kind == TrxFileOperationKind.Replace)
                {
                    AssertTruthful(
                        snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
                        expectation);
                    Assert.DoesNotContain(
                        path => path.EndsWith(".tmp", StringComparison.Ordinal),
                        snapshot.Paths);
                    replaceBarriers++;
                }
            });
        TrxIncrementalWriterPrototype writer = CreateOverflowingSafeWriter(operations, result, executionId);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);
        observe = true;

        writer.AppendCompleted(result, executionId);

        Assert.AreEqual(1, replaceBarriers);
    }

    [TestMethod]
    public void FailedReplace_LeavesPriorTargetAndCompleteOrPartialTempOnly()
    {
        PublishedPair pair = CreatePublishedPair();
        var completeInner = new TrxFaultInjectingFileOperations();
        completeInner.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, pair.OldBytes);
        var completeFailure = new ThrowingReplaceOperations(completeInner, failDelete: true);

        _ = Assert.ThrowsExactly<IOException>(
            () => new TrxSnapshotPublisherPrototype(completeFailure).Publish(
                TrxPhase3EvidenceMatrix.TargetPath,
                file => file.Write(pair.NewBytes, 0, pair.NewBytes.Length)));

        Assert.AreSequenceEqual(pair.OldBytes, completeInner.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        string completeTemporaryPath = completeInner.CaptureSnapshot().Paths.Single(
            path => !string.Equals(path, TrxPhase3EvidenceMatrix.TargetPath, StringComparison.Ordinal));
        Assert.AreSequenceEqual(pair.NewBytes, completeInner.GetFileBytes(completeTemporaryPath));

        var trace = new TrxFaultInjectingFileOperations();
        trace.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, pair.OldBytes);
        new TrxSnapshotPublisherPrototype(trace).Publish(
            TrxPhase3EvidenceMatrix.TargetPath,
            file => file.Write(pair.NewBytes, 0, pair.NewBytes.Length));
        int writeIndex = trace.Operations.Single(operation => operation.Kind == TrxFileOperationKind.Write).OperationIndex;
        var partial = new TrxFaultInjectingFileOperations(
            terminationPlan: new TrxTerminationPlan(writeIndex, committedByteCount: 7));
        partial.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, pair.OldBytes);

        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => new TrxSnapshotPublisherPrototype(partial).Publish(
                TrxPhase3EvidenceMatrix.TargetPath,
                file => file.Write(pair.NewBytes, 0, pair.NewBytes.Length)));

        Assert.AreSequenceEqual(pair.OldBytes, partial.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        string partialTemporaryPath = partial.CaptureSnapshot().Paths.Single(
            path => !string.Equals(path, TrxPhase3EvidenceMatrix.TargetPath, StringComparison.Ordinal));
        Assert.AreSequenceEqual(pair.NewBytes.Take(7).ToArray(), partial.GetFileBytes(partialTemporaryPath));
    }

    [TestMethod]
    public void Compact_EveryOperationAndByteCut_LeavesPaddedOldOrCompactNewDocument()
    {
        CompactRun baseline = CreateCompactRun(terminationPlan: null);
        baseline.Writer.Compact(baseline.Completion);
        byte[] compact = baseline.Operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        TrxFileOperationRecord[] operations = [.. baseline.Operations.Operations];
        Assert.AreSequenceEqual(
            [
                TrxFileOperationKind.Open,
                TrxFileOperationKind.Write,
                TrxFileOperationKind.Flush,
                TrxFileOperationKind.Replace,
            ],
            operations.Select(operation => operation.Kind).ToArray());
        AssertTruthful(compact, baseline.CompactExpectation);

        foreach (TrxFileOperationRecord operation in operations)
        {
            CompactRun fault = CreateCompactRun(new TrxTerminationPlan(operation.OperationIndex));
            _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
                () => fault.Writer.Compact(fault.Completion));
            byte[] target = fault.Operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
            if (operation.Kind == TrxFileOperationKind.Replace)
            {
                Assert.AreSequenceEqual(compact, target);
                AssertTruthful(target, fault.CompactExpectation);
            }
            else
            {
                Assert.AreSequenceEqual(fault.PaddedBytes, target);
                AssertTruthful(target, fault.PaddedExpectation);
            }
        }

        TrxFileOperationRecord write = operations.Single(operation => operation.Kind == TrxFileOperationKind.Write);
        for (int cut = 0; cut <= write.RequestedByteCount; cut++)
        {
            CompactRun fault = CreateCompactRun(
                new TrxTerminationPlan(write.OperationIndex, cut));
            _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
                () => fault.Writer.Compact(fault.Completion));
            Assert.AreSequenceEqual(
                fault.PaddedBytes,
                fault.Operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
            AssertTruthful(fault.PaddedBytes, fault.PaddedExpectation);
        }

        TestContext.WriteLine(
            "phase4 compaction matrix: operations={0}; compactByteCuts={1}; paddedBytes={2}; compactBytes={3}",
            operations.Length,
            write.RequestedByteCount + 1,
            baseline.PaddedBytes.Length,
            compact.Length);
    }

    [TestMethod]
    public void Compact_OnlyAfterSuccessfulReplace_HasCanonicalSemanticParity()
    {
        CompactRun run = CreateCompactRun(terminationPlan: null);
        XDocument padded = TrxPrototypeScenarioFactory.LoadStrict(run.PaddedBytes);

        run.Writer.Compact(run.Completion);

        byte[] compactBytes = run.Operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        XDocument compact = TrxPrototypeScenarioFactory.LoadStrict(compactBytes);
        AssertTruthful(run.PaddedBytes, run.PaddedExpectation);
        AssertTruthful(compactBytes, run.CompactExpectation);
        Assert.AreEqual(CreateSemanticProjection(padded), CreateSemanticProjection(compact));
        Assert.IsLessThan(run.PaddedBytes.Length, compactBytes.Length);
    }

    [TestMethod]
    public void TemporaryPath_IsSiblingAndDifferentVolumeIsRejectedBeforeMutation()
    {
        string destination = Path.Combine("phase4", "results.trx");
        var inner = new TrxFaultInjectingFileOperations();
        byte[] prior = StrictUtf8.GetBytes("prior");
        inner.SeedFile(destination, prior);
        var outOfDirectory = new OutOfDirectoryOperations(inner);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => new TrxSnapshotPublisherPrototype(outOfDirectory).Publish(
                destination,
                file => file.Write([1, 2, 3], 0, 3)));

        Assert.Contains("distinct sibling", error.Message);
        Assert.IsEmpty(inner.Operations);
        Assert.AreSequenceEqual(prior, inner.GetFileBytes(destination));

        string realDirectory = Path.Combine(Path.GetTempPath(), $"trx-phase4-sibling-{Guid.NewGuid():N}");
        string realDestination = Path.Combine(realDirectory, "results.trx");
        string realTemporary = new TrxPrototypeFileOperations().CreateTemporarySiblingPath(realDestination);
        Assert.AreEqual(Path.GetFullPath(realDirectory), Path.GetDirectoryName(realTemporary));
        Assert.AreNotEqual(Path.GetFullPath(realDestination), Path.GetFullPath(realTemporary));
    }

    [TestMethod]
    public void AtomicAndDeleteThenMoveModels_ProduceExplicitlyDifferentGuarantees()
    {
        byte[] oldBytes = StrictUtf8.GetBytes("old");
        byte[] newBytes = StrictUtf8.GetBytes("new");
        var atomic = new TrxFaultInjectingFileOperations();
        atomic.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, oldBytes);
        new TrxSnapshotPublisherPrototype(atomic).Publish(
            TrxPhase3EvidenceMatrix.TargetPath,
            file => file.Write(newBytes, 0, newBytes.Length));
        Assert.AreSequenceEqual(newBytes, atomic.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        _ = Assert.ContainsSingle(
            operation => operation.Kind == TrxFileOperationKind.Replace,
            atomic.Operations);

        var targetPresence = new List<bool>();
        var deleteThenMove = new TrxFaultInjectingFileOperations(
            TrxReplacementModel.DeleteThenMove,
            afterOperation: (_, snapshot) =>
                targetPresence.Add(snapshot.Contains(TrxPhase3EvidenceMatrix.TargetPath)));
        deleteThenMove.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, oldBytes);
        deleteThenMove.SeedFile("results.trx.tmp", newBytes);
        _ = Assert.ThrowsExactly<PlatformNotSupportedException>(
            () => new TrxSnapshotPublisherPrototype(deleteThenMove).Publish(
                TrxPhase3EvidenceMatrix.TargetPath,
                file => file.Write(newBytes, 0, newBytes.Length)));
        Assert.IsEmpty(deleteThenMove.Operations);
        Assert.AreSequenceEqual(oldBytes, deleteThenMove.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));

        deleteThenMove.ReplaceTemporarySibling("results.trx.tmp", TrxPhase3EvidenceMatrix.TargetPath);
        Assert.AreSequenceEqual([false, true], targetPresence);
        Assert.AreSequenceEqual(
            [TrxFileOperationKind.Delete, TrxFileOperationKind.Replace],
            deleteThenMove.Operations.Select(operation => operation.Kind).ToArray());
    }

    [TestMethod]
    public void Startup_PreExistingTargetOrOrphanTemp_NeverTruncatesTargetBeforeReplace()
    {
        byte[] prior = TrxPrototypeXmlRenderer.RenderInitial(
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes: 512,
            entryPadBytes: 256,
            summaryPadBytes: 256,
            counterWidth: TrxPhase3EvidenceMatrix.CounterWidth,
            runningSlotCount: 1,
            runningSlotByteCapacity: 256);
        const string orphan = "results.trx.prototype-0001.tmp";
        byte[] orphanBytes = StrictUtf8.GetBytes("orphan partial bytes");
        bool observe = false;
        int oldBarriers = 0;
        int newBarriers = 0;
        var operations = new TrxFaultInjectingFileOperations(
            afterOperation: (operation, snapshot) =>
            {
                if (!observe)
                {
                    return;
                }

                byte[] target = snapshot.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
                if (operation.Kind == TrxFileOperationKind.Replace)
                {
                    AssertTruthful(
                        target,
                        TrxPhase3EvidenceMatrix.CreateExpectation([], []));
                    newBarriers++;
                }
                else
                {
                    Assert.AreSequenceEqual(prior, target);
                    oldBarriers++;
                }
            });
        operations.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, prior);
        operations.SeedFile(orphan, orphanBytes);
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(
            operations,
            definitionPadBytes: 1_024,
            entryPadBytes: 512);
        observe = true;

        writer.Initialize();

        Assert.IsGreaterThan(0, oldBarriers);
        Assert.AreEqual(1, newBarriers);
        Assert.AreSequenceEqual(orphanBytes, operations.GetFileBytes(orphan));
        AssertTruthful(
            operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
            TrxPhase3EvidenceMatrix.CreateExpectation([], []));
    }

    [TestMethod]
    public void Startup_NoDirectoryPermissionOrLockFailure_LeavesPriorTargetUntouched()
    {
        byte[] prior = StrictUtf8.GetBytes("prior target");
        var permissionInner = new TrxFaultInjectingFileOperations();
        permissionInner.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, prior);
        var denied = new DenyTemporaryOpenOperations(permissionInner);
        TrxIncrementalWriterPrototype deniedWriter = CreateSafeWriter(denied);

        _ = Assert.ThrowsExactly<UnauthorizedAccessException>(() => deniedWriter.Initialize());

        Assert.AreSequenceEqual(prior, permissionInner.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        Assert.IsEmpty(permissionInner.Operations);

        var locked = new TrxFaultInjectingFileOperations();
        locked.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, prior);
        using ITrxPrototypeFile lockHandle = locked.Open(
            TrxPhase3EvidenceMatrix.TargetPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        TrxIncrementalWriterPrototype lockedWriter = CreateSafeWriter(locked);

        _ = Assert.ThrowsExactly<IOException>(() => lockedWriter.Initialize());

        Assert.AreSequenceEqual(prior, locked.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath));
        Assert.DoesNotContain(
            path => path.EndsWith(".tmp", StringComparison.Ordinal),
            locked.CaptureSnapshot().Paths);
    }

    private static TrxIncrementalWriterPrototype CreateSafeWriter(
        ITrxPrototypeFileOperations operations,
        int definitionPadBytes = 4_096,
        int entryPadBytes = 2_048,
        int summaryPadBytes = 2_048,
        int counterWidth = TrxPhase3EvidenceMatrix.CounterWidth,
        int runningSlotCount = 2,
        int runningSlotByteCapacity = 320)
        => new(
            operations,
            TrxPhase3EvidenceMatrix.TargetPath,
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion,
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes,
            entryPadBytes,
            summaryPadBytes,
            counterWidth,
            runningSlotCount,
            runningSlotByteCapacity,
            new TrxSnapshotPublisherPrototype(operations));

    private static TrxIncrementalWriterPrototype CreateOverflowingSafeWriter(
        ITrxPrototypeFileOperations operations,
        TrxTestResult result,
        Guid executionId)
    {
        int definitionBytes = CreateRenderer().RenderDefinition(result, executionId).Length;
        return CreateSafeWriter(
            operations,
            definitionPadBytes: definitionBytes - 1,
            entryPadBytes: 2_048);
    }

    private static TrxPrototypeXmlRenderer CreateRenderer()
        => new(
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion);

    private static void AssertTruthful(byte[] bytes, TrxDocumentExpectation expectation)
    {
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(bytes, expectation);
        Assert.AreEqual(
            TrxDocumentClassification.Truthful,
            observation.Classification,
            observation.Diagnostic);
    }

    private static int GetWhitespaceBytes(XDocument document, string localName)
        => StrictUtf8.GetByteCount(
            string.Concat(
                document.Root!
                    .Element(Ns + localName)!
                    .Nodes()
                    .OfType<XText>()
                    .Select(text => text.Value)));

    private static PublishedPair CreatePublishedPair()
    {
        var oldOperations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype oldWriter = TrxPhase3EvidenceMatrix.CreateWriter(oldOperations);
        oldWriter.Initialize();
        byte[] oldBytes = oldOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);

        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            409,
            "published replacement é漢😀",
            TrxTestOutcome.Passed);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(409);
        var newOperations = new TrxFaultInjectingFileOperations();
        TrxIncrementalWriterPrototype newWriter = TrxPhase3EvidenceMatrix.CreateWriter(newOperations);
        newWriter.Initialize();
        newWriter.AppendCompleted(result, executionId);
        return new PublishedPair
        {
            OldBytes = oldBytes,
            NewBytes = newOperations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath),
            OldExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([], []),
            NewExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]),
        };
    }

    private static TrxFaultInjectingFileOperations PublishVerified(
        byte[] oldBytes,
        byte[] newBytes,
        TrxTerminationPlan? terminationPlan,
        bool expectTermination = false)
    {
        var operations = new TrxFaultInjectingFileOperations(
            terminationPlan: terminationPlan,
            captureSnapshots: false);
        operations.SeedFile(TrxPhase3EvidenceMatrix.TargetPath, oldBytes);
        Action publish = () => new TrxSnapshotPublisherPrototype(operations).Publish(
            TrxPhase3EvidenceMatrix.TargetPath,
            file => WriteAndReadBack(file, newBytes));
        if (expectTermination)
        {
            _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(publish);
        }
        else
        {
            publish();
        }

        return operations;
    }

    private static void WriteAndReadBack(ITrxPrototypeFile file, byte[] bytes)
    {
        file.Write(bytes, 0, bytes.Length);
        file.Seek(0, SeekOrigin.Begin);
        byte[] readBack = new byte[bytes.Length];
        int read = file.Read(readBack, 0, readBack.Length);
        if (read != readBack.Length)
        {
            throw new IOException($"Expected to read {readBack.Length} temporary bytes but read {read}.");
        }

        if (!bytes.SequenceEqual(readBack))
        {
            throw new IOException("Temporary snapshot read-back did not match the complete document.");
        }
    }

    private static CompactRun CreateCompactRun(TrxTerminationPlan? terminationPlan)
    {
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false);
        TrxIncrementalWriterPrototype writer = CreateSafeWriter(operations);
        TrxTestResult[] results =
        [
            TrxPhase3EvidenceMatrix.CreateResult(410, "compact pass", TrxTestOutcome.Passed),
            TrxPhase3EvidenceMatrix.CreateResult(411, "compact skip", TrxTestOutcome.Skipped),
        ];
        Guid[] executionIds =
        [
            TrxPhase3EvidenceMatrix.ExecutionId(410),
            TrxPhase3EvidenceMatrix.ExecutionId(411),
        ];
        var completion = new TrxPrototypeCompletion
        {
            FinishTime = TrxPhase3EvidenceMatrix.FinishTime,
        };
        writer.Initialize();
        writer.AppendCompleted(results[0], executionIds[0]);
        writer.AppendCompleted(results[1], executionIds[1]);
        writer.Complete(completion);
        byte[] padded = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        TrxDocumentExpectation paddedExpectation = TrxPhase3EvidenceMatrix.CreateExpectation(
            results,
            executionIds,
            finishTime: TrxPhase3EvidenceMatrix.FinishTime,
            summaryOutcome: "Completed");
        operations.BeginFaultWindow(terminationPlan);
        return new CompactRun
        {
            Operations = operations,
            Writer = writer,
            Completion = completion,
            PaddedBytes = padded,
            PaddedExpectation = paddedExpectation,
            CompactExpectation = CreateCompactExpectation(paddedExpectation),
        };
    }

    private static TrxDocumentExpectation CreateCompactExpectation(TrxDocumentExpectation padded)
        => new()
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

    private static string CreateSemanticProjection(XDocument document)
    {
        XElement root = document.Root!;
        XElement summary = root.Element(Ns + "ResultSummary")!;
        string counters = string.Join(
            ";",
            summary.Element(Ns + "Counters")!
                .Attributes()
                .Select(attribute => $"{attribute.Name.LocalName}={int.Parse(attribute.Value, CultureInfo.InvariantCulture)}"));
        string results = string.Join(
            ";",
            root.Element(Ns + "Results")!
                .Elements(Ns + "UnitTestResult")
                .Select(
                    result => string.Join(
                        "|",
                        result.Attribute("executionId")!.Value,
                        result.Attribute("testId")!.Value,
                        result.Attribute("testName")!.Value,
                        result.Attribute("outcome")!.Value)));
        string definitions = string.Join(
            ";",
            root.Element(Ns + "TestDefinitions")!
                .Elements(Ns + "UnitTest")
                .Select(definition => definition.Attribute("id")!.Value));
        string entries = string.Join(
            ";",
            root.Element(Ns + "TestEntries")!
                .Elements(Ns + "TestEntry")
                .Select(
                    entry => $"{entry.Attribute("testId")!.Value}|{entry.Attribute("executionId")!.Value}"));
        return string.Join(
            "#",
            root.Attribute("id")!.Value,
            root.Attribute("name")!.Value,
            root.Element(Ns + "Times")!.Attribute("finish")!.Value,
            summary.Attribute("outcome")!.Value,
            counters,
            results,
            definitions,
            entries);
    }

    private sealed class PublishedPair
    {
        public required byte[] OldBytes { get; init; }

        public required byte[] NewBytes { get; init; }

        public required TrxDocumentExpectation OldExpectation { get; init; }

        public required TrxDocumentExpectation NewExpectation { get; init; }
    }

    private sealed class CompactRun
    {
        public required TrxFaultInjectingFileOperations Operations { get; init; }

        public required TrxIncrementalWriterPrototype Writer { get; init; }

        public required TrxPrototypeCompletion Completion { get; init; }

        public required byte[] PaddedBytes { get; init; }

        public required TrxDocumentExpectation PaddedExpectation { get; init; }

        public required TrxDocumentExpectation CompactExpectation { get; init; }
    }

    private abstract class ForwardingOperations(TrxFaultInjectingFileOperations inner)
        : ITrxPrototypeFileOperations
    {
        protected TrxFaultInjectingFileOperations Inner { get; } = inner;

        public virtual bool SupportsAtomicReplace => Inner.SupportsAtomicReplace;

        public virtual ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
            => Inner.Open(path, mode, access, share);

        public virtual string CreateTemporarySiblingPath(string destinationPath)
            => Inner.CreateTemporarySiblingPath(destinationPath);

        public virtual void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
            => Inner.ReplaceTemporarySibling(temporaryPath, destinationPath);

        public virtual bool Exists(string path) => Inner.Exists(path);

        public virtual void Delete(string path) => Inner.Delete(path);
    }

    private sealed class ThrowingReplaceOperations(
        TrxFaultInjectingFileOperations inner,
        bool failDelete = false)
        : ForwardingOperations(inner)
    {
        public override void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
            => throw new IOException("Deterministic replacement failure.");

        public override void Delete(string path)
        {
            if (failDelete)
            {
                throw new UnauthorizedAccessException("Deterministic temp cleanup denial.");
            }

            base.Delete(path);
        }
    }

    private sealed class OutOfDirectoryOperations(TrxFaultInjectingFileOperations inner)
        : ForwardingOperations(inner)
    {
        public override string CreateTemporarySiblingPath(string destinationPath)
            => Path.Combine(Path.GetTempPath(), "different-filesystem-probe", "results.trx.tmp");
    }

    private sealed class DenyTemporaryOpenOperations(TrxFaultInjectingFileOperations inner)
        : ForwardingOperations(inner)
    {
        public override ITrxPrototypeFile Open(
            string path,
            FileMode mode,
            FileAccess access,
            FileShare share)
            => throw new UnauthorizedAccessException("Deterministic directory permission denial.");
    }
}

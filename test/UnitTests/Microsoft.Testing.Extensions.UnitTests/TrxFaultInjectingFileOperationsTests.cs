// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxFaultInjectingFileOperationsTests
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [TestMethod]
    public void Open_Create_TruncatesExistingEntryAndRecordsPrePostLength()
    {
        TrxFaultInjectingFileOperations operations = new();
        operations.SeedFile("results.trx", Utf8.GetBytes("prior"));

        using ITrxPrototypeFile file = operations.Open(
            "results.trx",
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read);

        Assert.AreEqual(0, file.Length);
        Assert.AreEqual(0, file.Position);
        Assert.HasCount(1, operations.Operations);
        TrxFileOperationRecord record = operations.Operations[0];
        Assert.AreEqual(0, record.OperationIndex);
        Assert.AreEqual(TrxFileOperationKind.Open, record.Kind);
        Assert.AreEqual(5, record.PreLength);
        Assert.AreEqual(0, record.PostLength);
        Assert.AreEqual(FileMode.Create, record.Mode);
        Assert.AreEqual(FileAccess.ReadWrite, record.Access);
        Assert.AreEqual(FileShare.Read, record.Share);
        Assert.HasCount(1, operations.Snapshots);
        Assert.IsEmpty(operations.Snapshots[0].GetFileBytes("results.trx"));
    }

    [TestMethod]
    public void SeekReadWriteFlushSetLength_RecordMonotonicOperations()
    {
        TrxFaultInjectingFileOperations operations = new();
        operations.SeedFile("results.trx", Utf8.GetBytes("abcdef"));

        using ITrxPrototypeFile file = operations.Open(
            "results.trx",
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        file.Seek(2, SeekOrigin.Begin);
        byte[] readBuffer = new byte[2];
        int read = file.Read(readBuffer, 0, readBuffer.Length);
        file.Seek(-1, SeekOrigin.Current);
        byte[] writeBuffer = Utf8.GetBytes("_12_");
        file.Write(writeBuffer, 1, 2);
        file.Flush();
        file.SetLength(4);

        Assert.AreEqual(2, read);
        Assert.AreEqual("cd", Utf8.GetString(readBuffer));
        Assert.AreEqual("abc1", Utf8.GetString(operations.GetFileBytes("results.trx")));
        Assert.AreSequenceEqual(
            [
                TrxFileOperationKind.Open,
                TrxFileOperationKind.Seek,
                TrxFileOperationKind.Read,
                TrxFileOperationKind.Seek,
                TrxFileOperationKind.Write,
                TrxFileOperationKind.Flush,
                TrxFileOperationKind.SetLength,
            ],
            operations.Operations.Select(record => record.Kind).ToArray());
        Assert.AreSequenceEqual(
            Enumerable.Range(0, 7).ToArray(),
            operations.Operations.Select(record => record.OperationIndex).ToArray());
        Assert.AreEqual(2, operations.Operations[2].RequestedByteCount);
        Assert.AreEqual(2, operations.Operations[2].CommittedByteCount);
        Assert.AreEqual(3, operations.Operations[4].PrePosition);
        Assert.AreEqual(5, operations.Operations[4].PostPosition);
        Assert.AreEqual(6, operations.Operations[6].PreLength);
        Assert.AreEqual(4, operations.Operations[6].PostLength);
    }

    [TestMethod]
    public void Write_TerminationAtEveryByte_CommitsExactlyRequestedPrefix()
    {
        byte[] original = Utf8.GetBytes("xxxxx");
        byte[] payload = Utf8.GetBytes("ABCDE");

        for (int committedByteCount = 0; committedByteCount <= payload.Length; committedByteCount++)
        {
            TrxFaultInjectingFileOperations operations = new(
                terminationPlan: new TrxTerminationPlan(operationIndex: 1, committedByteCount));
            operations.SeedFile("results.trx", original);
            ITrxPrototypeFile file = operations.Open(
                "results.trx",
                FileMode.Open,
                FileAccess.Write,
                FileShare.Read);

            TrxSimulatedProcessTerminationException exception =
                Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
                    () => file.Write(payload, 0, payload.Length));

            byte[] expected = (byte[])original.Clone();
            Array.Copy(payload, expected, committedByteCount);
            Assert.AreSequenceEqual(expected, operations.GetFileBytes("results.trx"), $"Cut {committedByteCount}");
            Assert.AreEqual(1, exception.OperationIndex);
            Assert.IsTrue(operations.IsProcessDead);
            Assert.HasCount(2, operations.Operations);
            Assert.AreEqual(payload.Length, operations.Operations[1].RequestedByteCount);
            Assert.AreEqual(committedByteCount, operations.Operations[1].CommittedByteCount);
            Assert.IsTrue(operations.Snapshots[1].IsProcessDead);
        }
    }

    [TestMethod]
    public void Termination_RejectsEveryLaterMutationIncludingFinallyCleanup()
    {
        TrxFaultInjectingFileOperations operations = new(
            terminationPlan: new TrxTerminationPlan(operationIndex: 1, committedByteCount: 2));
        operations.SeedFile("results.trx", Utf8.GetBytes("012345"));
        operations.SeedFile("results.trx.tmp", Utf8.GetBytes("replacement"));
        ITrxPrototypeFile file = operations.Open(
            "results.trx",
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => file.Write(Utf8.GetBytes("ABC"), 0, 3));
        TrxVirtualFileSystemSnapshot frozen = operations.CaptureSnapshot();

        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(() => file.Flush());
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(() => file.SetLength(0));
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(() => file.Dispose());
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(() => operations.Delete("results.trx"));
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => operations.ReplaceTemporarySibling("results.trx.tmp", "results.trx"));
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => operations.Open("other.trx", FileMode.Create, FileAccess.Write, FileShare.None));
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(() => operations.Exists("results.trx"));

        Assert.HasCount(2, operations.Operations);
        Assert.AreEqual(frozen.ToString(), operations.CaptureSnapshot().ToString());
        Assert.AreEqual("AB2345", Utf8.GetString(operations.GetFileBytes("results.trx")));
        Assert.AreEqual("replacement", Utf8.GetString(operations.GetFileBytes("results.trx.tmp")));
    }

    [TestMethod]
    public void AtomicReplace_SwapsWholeEntryInOneOperation()
    {
        var barriers = new List<TrxVirtualFileSystemSnapshot>();
        TrxFaultInjectingFileOperations operations = new(
            afterOperation: (_, snapshot) => barriers.Add(snapshot));
        operations.SeedFile("results.trx", Utf8.GetBytes("old"));
        operations.SeedFile("results.trx.tmp", Utf8.GetBytes("complete-new"));

        operations.ReplaceTemporarySibling("results.trx.tmp", "results.trx");

        Assert.IsTrue(operations.SupportsAtomicReplace);
        Assert.HasCount(1, operations.Operations);
        Assert.AreEqual(TrxFileOperationKind.Replace, operations.Operations[0].Kind);
        Assert.AreEqual("results.trx.tmp", operations.Operations[0].ReplacementSource);
        Assert.AreEqual("results.trx", operations.Operations[0].ReplacementTarget);
        Assert.AreEqual("complete-new", Utf8.GetString(operations.GetFileBytes("results.trx")));
        Assert.IsFalse(operations.Exists("results.trx.tmp"));
        Assert.HasCount(1, barriers);
        Assert.IsTrue(barriers[0].Contains("results.trx"));
        Assert.IsFalse(barriers[0].Contains("results.trx.tmp"));
    }

    [TestMethod]
    public void DeleteThenMoveEmulation_ExposesTargetMissingWindow()
    {
        var targetPresenceAtBarriers = new List<bool>();
        TrxFaultInjectingFileOperations operations = new(
            TrxReplacementModel.DeleteThenMove,
            afterOperation: (_, snapshot) => targetPresenceAtBarriers.Add(snapshot.Contains("results.trx")));
        operations.SeedFile("results.trx", Utf8.GetBytes("old"));
        operations.SeedFile("results.trx.tmp", Utf8.GetBytes("complete-new"));

        operations.ReplaceTemporarySibling("results.trx.tmp", "results.trx");

        Assert.IsFalse(operations.SupportsAtomicReplace);
        Assert.AreSequenceEqual(
            [TrxFileOperationKind.Delete, TrxFileOperationKind.Replace],
            operations.Operations.Select(record => record.Kind).ToArray());
        Assert.AreSequenceEqual([false, true], targetPresenceAtBarriers);
        Assert.AreEqual("complete-new", Utf8.GetString(operations.GetFileBytes("results.trx")));
    }

    [TestMethod]
    public void Barrier_AfterEveryPrimitive_ObservesFrozenPostOperationState()
    {
        var records = new List<TrxFileOperationRecord>();
        var snapshots = new List<TrxVirtualFileSystemSnapshot>();
        TrxFaultInjectingFileOperations operations = new(
            afterOperation: (record, snapshot) =>
            {
                records.Add(record);
                snapshots.Add(snapshot);
            });
        operations.SeedFile("results.trx", Utf8.GetBytes("abcd"));

        using ITrxPrototypeFile file = operations.Open(
            "results.trx",
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        file.Seek(1, SeekOrigin.Begin);
        file.Write(Utf8.GetBytes("XY"), 0, 2);
        file.Flush();
        file.SetLength(2);

        Assert.HasCount(5, records);
        Assert.HasCount(5, snapshots);
        for (int i = 0; i < records.Count; i++)
        {
            Assert.AreEqual(i, records[i].OperationIndex);
            Assert.AreEqual(i, snapshots[i].OperationIndex);
            Assert.IsFalse(snapshots[i].IsProcessDead);
        }

        Assert.AreEqual("abcd", Utf8.GetString(snapshots[1].GetFileBytes("results.trx")));
        Assert.AreEqual("aXYd", Utf8.GetString(snapshots[2].GetFileBytes("results.trx")));
        Assert.AreEqual("aX", Utf8.GetString(snapshots[4].GetFileBytes("results.trx")));
        Assert.AreEqual(snapshots[4].ToString(), operations.CaptureSnapshot().ToString());
    }

    [TestMethod]
    public void FreshRun_WithSamePlan_ProducesIdenticalTraceAndSnapshots()
    {
        TrxFaultInjectingFileOperations first = RunDeterministicFaultScenario();
        TrxFaultInjectingFileOperations second = RunDeterministicFaultScenario();

        Assert.IsTrue(first.IsProcessDead);
        Assert.IsTrue(second.IsProcessDead);
        Assert.AreEqual(first.FormatTrace(), second.FormatTrace());
        Assert.AreSequenceEqual(
            first.Snapshots.Select(snapshot => snapshot.ToString()).ToArray(),
            second.Snapshots.Select(snapshot => snapshot.ToString()).ToArray());
        Assert.AreSequenceEqual(first.GetFileBytes("results.trx"), second.GetFileBytes("results.trx"));
    }

    [TestMethod]
    public void MultipleHandles_RespectConfiguredShareAndSnapshotRules()
    {
        TrxFaultInjectingFileOperations operations = new();
        operations.SeedFile("results.trx", Utf8.GetBytes("old"));
        TrxVirtualFileSystemSnapshot initial = operations.CaptureSnapshot();

        using (ITrxPrototypeFile firstReader = operations.Open(
                   "results.trx",
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read | FileShare.Delete))
        using (ITrxPrototypeFile secondReader = operations.Open(
                   "results.trx",
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read | FileShare.Delete))
        {
            _ = Assert.ThrowsExactly<IOException>(
                () => operations.Open(
                    "results.trx",
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete));

            byte[] firstByte = new byte[1];
            byte[] secondByte = new byte[1];
            Assert.AreEqual(1, firstReader.Read(firstByte, 0, 1));
            Assert.AreEqual(1, secondReader.Read(secondByte, 0, 1));
            Assert.AreEqual((byte)'o', firstByte[0]);
            Assert.AreEqual((byte)'o', secondByte[0]);
        }

        using (ITrxPrototypeFile writer = operations.Open(
                   "results.trx",
                   FileMode.Open,
                   FileAccess.Write,
                   FileShare.Read))
        {
            writer.Write(Utf8.GetBytes("N"), 0, 1);
        }

        Assert.AreEqual("old", Utf8.GetString(initial.GetFileBytes("results.trx")));
        Assert.AreEqual("Nld", Utf8.GetString(operations.GetFileBytes("results.trx")));

        TrxFaultInjectingFileOperations relaxed = new(enforceShareSemantics: false);
        relaxed.SeedFile("relaxed.trx", Utf8.GetBytes("data"));
        using ITrxPrototypeFile exclusiveReader = relaxed.Open(
            "relaxed.trx",
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        using ITrxPrototypeFile conflictingWriter = relaxed.Open(
            "relaxed.trx",
            FileMode.Open,
            FileAccess.Write,
            FileShare.None);
        conflictingWriter.Write(Utf8.GetBytes("X"), 0, 1);
        Assert.AreEqual("Xata", Utf8.GetString(relaxed.GetFileBytes("relaxed.trx")));
    }

    [TestMethod]
    public void TraceDiagnostics_AreStableAndBounded()
    {
        TrxFaultInjectingFileOperations operations = new();
        operations.SeedFile("trace.trx", Utf8.GetBytes("AéZ"));
        using ITrxPrototypeFile file = operations.Open(
            "trace.trx",
            FileMode.Open,
            FileAccess.Write,
            FileShare.Read);
        file.Write(Utf8.GetBytes("!"), 0, 1);

        const string expectedWriteDiagnostic =
            "#001 Write path='trace.trx' position=0->1 length=4->4 requested=1 committed=1 mode=- access=- share=- replace='-'->'-' window=[offset=0; count=4; hex=21 C3 A9 5A; utf8=!éZ]";
        Assert.AreEqual(expectedWriteDiagnostic, operations.Operations[1].ToString());
        Assert.Contains("mode=Open access=Write share=Read", operations.Operations[0].ToString());
        Assert.IsLessThan(600, operations.FormatTrace().Length);
        Assert.IsTrue(operations.Operations.All(record => record.ByteWindow.Contains("count=", StringComparison.Ordinal)));
        Assert.IsTrue(operations.Operations.All(record => record.ByteWindow.Contains("hex=", StringComparison.Ordinal)));
        Assert.IsTrue(operations.Operations.All(record => record.ByteWindow.Contains("utf8=", StringComparison.Ordinal)));
    }

    private static TrxFaultInjectingFileOperations RunDeterministicFaultScenario()
    {
        TrxFaultInjectingFileOperations operations = new(
            terminationPlan: new TrxTerminationPlan(operationIndex: 3, committedByteCount: 1));
        operations.SeedFile("results.trx", Utf8.GetBytes("old"));
        ITrxPrototypeFile file = operations.Open(
            "results.trx",
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read);
        file.Write(Utf8.GetBytes("AB"), 0, 2);
        file.Seek(0, SeekOrigin.Begin);
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => file.Write(Utf8.GetBytes("XY"), 0, 2));
        return operations;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxPrototypeRealFileSystemTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RealFile_ReopenAfterEveryControlledBarrier_ParsesBytesFromDisk()
    {
        using var directory = new TempDirectory("trx-phase4-reopen");
        string path = Path.Combine(directory.Path, "results.trx");
        TrxPrototypeFileOperations realOperations = new();
        if (!realOperations.SupportsAtomicReplace)
        {
            Assert.IsFalse(realOperations.SupportsAtomicReplace);
            return;
        }

        using var operations = new BarrierFileOperations(realOperations, TestContext.CancellationToken);
        TrxTestResult result = TrxPhase3EvidenceMatrix.CreateResult(
            420,
            "real barrier reflow é漢😀",
            TrxTestOutcome.Passed,
            metadata:
            [
                new TrxTestMetadata
                {
                    Key = "Description",
                    Value = new string('r', 600),
                },
            ]);
        Guid executionId = TrxPhase3EvidenceMatrix.ExecutionId(420);
        int definitionBytes = CreateRenderer().RenderDefinition(result, executionId).Length;
        TrxIncrementalWriterPrototype writer = CreateRealWriter(
            operations,
            path,
            definitionPadBytes: definitionBytes - 1);
        writer.Initialize();
        byte[] oldBytes = File.ReadAllBytes(path);
        TrxDocumentExpectation oldExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([], []);
        TrxDocumentExpectation newExpectation = TrxPhase3EvidenceMatrix.CreateExpectation([result], [executionId]);
        operations.EnableBarriers();

        var publish = Task.Run(
            () => writer.AppendCompleted(result, executionId),
            TestContext.CancellationToken);
        TrxFileOperationKind[] expected =
        [
            TrxFileOperationKind.Open,
            TrxFileOperationKind.Write,
            TrxFileOperationKind.Flush,
            TrxFileOperationKind.Replace,
        ];
        var observed = new List<TrxFileOperationKind>();
        foreach (TrxFileOperationKind expectedKind in expected)
        {
            TrxFileOperationKind kind = operations.WaitForOperation();
            observed.Add(kind);
            try
            {
                Assert.AreEqual(expectedKind, kind);
                byte[] reopened = File.ReadAllBytes(path);
                _ = TrxPrototypeScenarioFactory.LoadStrict(reopened);
                if (kind == TrxFileOperationKind.Replace)
                {
                    AssertTruthful(reopened, newExpectation);
                }
                else
                {
                    Assert.AreSequenceEqual(oldBytes, reopened);
                    AssertTruthful(reopened, oldExpectation);
                }
            }
            finally
            {
                operations.Continue();
            }
        }

        publish.GetAwaiter().GetResult();
        Assert.AreSequenceEqual(expected, observed);
        AssertTruthful(File.ReadAllBytes(path), newExpectation);
        TestContext.WriteLine(
            "phase4 real barriers: runtime={0}; os={1}; operations={2}; finalBytes={3}",
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            string.Join(",", observed),
            new FileInfo(path).Length);
    }

    [TestMethod]
    public void RealReplace_OpenReaderWithDeleteShare_SeesOldOrNewCompleteDocument()
    {
        using var directory = new TempDirectory("trx-phase4-reader-delete-share");
        string path = Path.Combine(directory.Path, "results.trx");
        TrxPrototypeFileOperations operations = new();
        if (!operations.SupportsAtomicReplace)
        {
            Assert.IsFalse(operations.SupportsAtomicReplace);
            return;
        }

        byte[] oldBytes = CreateInitialBytes(421);
        byte[] newBytes = CreateInitialBytes(422);
        File.WriteAllBytes(path, oldBytes);
        using FileStream reader = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete);

        new TrxSnapshotPublisherPrototype(operations).Publish(
            path,
            file => file.Write(newBytes, 0, newBytes.Length));

        byte[] heldHandleBytes = ReadAll(reader);
        byte[] reopenedBytes = File.ReadAllBytes(path);
        _ = TrxPrototypeScenarioFactory.LoadStrict(heldHandleBytes);
        _ = TrxPrototypeScenarioFactory.LoadStrict(reopenedBytes);
        Assert.AreSequenceEqual(oldBytes, heldHandleBytes);
        Assert.AreSequenceEqual(newBytes, reopenedBytes);
    }

    [TestMethod]
    public void RealReplace_OpenReaderWithoutDeleteShare_OnWindowsFailsWithoutTargetLoss()
    {
        using var directory = new TempDirectory("trx-phase4-reader-no-delete-share");
        string path = Path.Combine(directory.Path, "results.trx");
        TrxPrototypeFileOperations operations = new();
        if (!operations.SupportsAtomicReplace)
        {
            Assert.IsFalse(operations.SupportsAtomicReplace);
            return;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.IsFalse(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            return;
        }

        byte[] oldBytes = CreateInitialBytes(423);
        byte[] newBytes = CreateInitialBytes(424);
        File.WriteAllBytes(path, oldBytes);
        using FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        _ = AssertSharingFailure(
            () => new TrxSnapshotPublisherPrototype(operations).Publish(
                path,
                file => file.Write(newBytes, 0, newBytes.Length)));

        Assert.AreSequenceEqual(oldBytes, File.ReadAllBytes(path));
        Assert.AreSequenceEqual(oldBytes, ReadAll(reader));
        Assert.IsEmpty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [TestMethod]
    public void RealReplace_OpenWriterOrTransientLock_FailsWithoutIndefiniteWait()
    {
        using var directory = new TempDirectory("trx-phase4-writer-lock");
        string path = Path.Combine(directory.Path, "results.trx");
        TrxPrototypeFileOperations operations = new();
        if (!operations.SupportsAtomicReplace)
        {
            Assert.IsFalse(operations.SupportsAtomicReplace);
            return;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.IsFalse(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            return;
        }

        byte[] oldBytes = CreateInitialBytes(425);
        byte[] newBytes = CreateInitialBytes(426);
        File.WriteAllBytes(path, oldBytes);
        using (FileStream writerLock = new(
                   path,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        {
            _ = AssertSharingFailure(
                () => new TrxSnapshotPublisherPrototype(operations).Publish(
                    path,
                    file => file.Write(newBytes, 0, newBytes.Length)));
            Assert.AreSequenceEqual(oldBytes, ReadAll(writerLock));
        }

        new TrxSnapshotPublisherPrototype(operations).Publish(
            path,
            file => file.Write(newBytes, 0, newBytes.Length));
        Assert.AreSequenceEqual(newBytes, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void RealReplace_TargetAbsentAndPresent_UsesSameFilesystemSibling()
    {
        using var directory = new TempDirectory("trx-phase4-absent-present");
        string path = Path.Combine(directory.Path, "results.trx");
        TrxPrototypeFileOperations realOperations = new();
        if (!realOperations.SupportsAtomicReplace)
        {
            Assert.IsFalse(realOperations.SupportsAtomicReplace);
            return;
        }

        var operations = new RecordingTemporaryPathOperations(realOperations);
        byte[] first = CreateInitialBytes(427);
        byte[] second = CreateInitialBytes(428);
        TrxSnapshotPublisherPrototype publisher = new(operations);

        publisher.Publish(path, file => file.Write(first, 0, first.Length));
        Assert.AreSequenceEqual(first, File.ReadAllBytes(path));
        publisher.Publish(path, file => file.Write(second, 0, second.Length));
        Assert.AreSequenceEqual(second, File.ReadAllBytes(path));

        Assert.HasCount(2, operations.TemporaryPaths);
        foreach (string temporaryPath in operations.TemporaryPaths)
        {
            Assert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetDirectoryName(temporaryPath));
            Assert.AreEqual(Path.GetPathRoot(Path.GetFullPath(path)), Path.GetPathRoot(temporaryPath));
            Assert.IsFalse(File.Exists(temporaryPath));
        }
    }

    [TestMethod]
    public void RealReplace_NetFrameworkCapability_IsExplicitlyUnsupportedAndNonDestructive()
    {
        using var directory = new TempDirectory("trx-phase4-netframework");
        string path = Path.Combine(directory.Path, "results.trx");
        byte[] oldBytes = CreateInitialBytes(429);
        byte[] newBytes = CreateInitialBytes(430);
        File.WriteAllBytes(path, oldBytes);
        TrxPrototypeFileOperations operations = new();

#if NETFRAMEWORK
        Assert.IsFalse(operations.SupportsAtomicReplace);
        _ = Assert.ThrowsExactly<PlatformNotSupportedException>(
            () => new TrxSnapshotPublisherPrototype(operations).Publish(
                path,
                file => file.Write(newBytes, 0, newBytes.Length)));
        Assert.AreSequenceEqual(oldBytes, File.ReadAllBytes(path));
        Assert.IsEmpty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
#else
        Assert.IsTrue(operations.SupportsAtomicReplace);
        new TrxSnapshotPublisherPrototype(operations).Publish(
            path,
            file => file.Write(newBytes, 0, newBytes.Length));
        Assert.AreSequenceEqual(newBytes, File.ReadAllBytes(path));
#endif
    }

    [TestMethod]
    public void Startup_DirectCreateTruncatesExistingFile_ButSnapshotInitializeDoesNot()
    {
        using var directory = new TempDirectory("trx-phase4-startup");
        string path = Path.Combine(directory.Path, "results.trx");
        byte[] prior = CreateInitialBytes(431);
        File.WriteAllBytes(path, prior);
        TrxPrototypeFileOperations realOperations = new();

        using (ITrxPrototypeFile direct = realOperations.Open(
                   path,
                   FileMode.Create,
                   FileAccess.ReadWrite,
                   FileShare.Read | FileShare.Delete))
        {
            Assert.AreEqual(0, direct.Length);
        }

        Assert.IsEmpty(File.ReadAllBytes(path));
        File.WriteAllBytes(path, prior);
        if (!realOperations.SupportsAtomicReplace)
        {
            TrxIncrementalWriterPrototype unsupportedWriter = CreateRealWriter(
                realOperations,
                path,
                definitionPadBytes: 1_024);
            _ = Assert.ThrowsExactly<PlatformNotSupportedException>(() => unsupportedWriter.Initialize());
            Assert.AreSequenceEqual(prior, File.ReadAllBytes(path));
            return;
        }

        using var barriers = new BarrierFileOperations(realOperations, TestContext.CancellationToken);
        TrxIncrementalWriterPrototype writer = CreateRealWriter(
            barriers,
            path,
            definitionPadBytes: 1_024);
        barriers.EnableBarriers();
        var initialize = Task.Run(writer.Initialize, TestContext.CancellationToken);
        foreach (TrxFileOperationKind expected in new[]
                 {
                     TrxFileOperationKind.Open,
                     TrxFileOperationKind.Write,
                     TrxFileOperationKind.Flush,
                     TrxFileOperationKind.Replace,
                 })
        {
            TrxFileOperationKind actual = barriers.WaitForOperation();
            try
            {
                Assert.AreEqual(expected, actual);
                byte[] target = File.ReadAllBytes(path);
                if (actual == TrxFileOperationKind.Replace)
                {
                    Assert.IsNotEmpty(target);
                    _ = TrxPrototypeScenarioFactory.LoadStrict(target);
                }
                else
                {
                    Assert.AreSequenceEqual(prior, target);
                }
            }
            finally
            {
                barriers.Continue();
            }
        }

        initialize.GetAwaiter().GetResult();
        AssertTruthful(
            File.ReadAllBytes(path),
            TrxPhase3EvidenceMatrix.CreateExpectation([], []));
    }

    private static TrxIncrementalWriterPrototype CreateRealWriter(
        ITrxPrototypeFileOperations operations,
        string path,
        int definitionPadBytes)
        => new(
            operations,
            path,
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion,
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes,
            entryPadBytes: 2_048,
            summaryPadBytes: 2_048,
            counterWidth: TrxPhase3EvidenceMatrix.CounterWidth,
            runningSlotCount: 2,
            runningSlotByteCapacity: 320,
            snapshotPublisher: new TrxSnapshotPublisherPrototype(operations));

    private static TrxPrototypeXmlRenderer CreateRenderer()
        => new(
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion);

    private static byte[] CreateInitialBytes(int runNumber)
        => TrxPrototypeXmlRenderer.RenderInitial(
            new Guid($"10000000-0000-0000-0000-{runNumber:D12}"),
            $"phase4-real-{runNumber}",
            TrxPhase3EvidenceMatrix.StartTime,
            definitionPadBytes: 512,
            entryPadBytes: 256,
            summaryPadBytes: 256,
            counterWidth: TrxPhase3EvidenceMatrix.CounterWidth,
            runningSlotCount: 1,
            runningSlotByteCapacity: 256);

    private static byte[] ReadAll(FileStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        byte[] bytes = new byte[stream.Length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return bytes;
    }

    private static void AssertTruthful(byte[] bytes, TrxDocumentExpectation expectation)
    {
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(bytes, expectation);
        Assert.AreEqual(
            TrxDocumentClassification.Truthful,
            observation.Classification,
            observation.Diagnostic);
    }

    private static Exception AssertSharingFailure(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception;
        }

        Assert.Fail("Expected the open Windows handle to reject atomic replacement.");
        throw new InvalidOperationException("Unreachable.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory(string? subdirectoryName = null)
        {
            string uniqueName = Guid.NewGuid().ToString("N");
            Path = subdirectoryName is null
                ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), uniqueName)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), subdirectoryName, uniqueName);

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            TryDelete(Path);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    TryDeleteFile(path);
                    return;
                }

                if (!Directory.Exists(path))
                {
                    return;
                }

                foreach (string directory in Directory.EnumerateDirectories(path))
                {
                    TryDelete(directory);
                }

                foreach (string file in Directory.EnumerateFiles(path))
                {
                    TryDeleteFile(file);
                }

                try
                {
                    Directory.Delete(path, recursive: false);
                }
                catch
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch
            {
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    private sealed class RecordingTemporaryPathOperations(ITrxPrototypeFileOperations inner)
        : ITrxPrototypeFileOperations
    {
        public List<string> TemporaryPaths { get; } = [];

        public bool SupportsAtomicReplace => inner.SupportsAtomicReplace;

        public ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
            => inner.Open(path, mode, access, share);

        public string CreateTemporarySiblingPath(string destinationPath)
        {
            string temporaryPath = inner.CreateTemporarySiblingPath(destinationPath);
            TemporaryPaths.Add(temporaryPath);
            return temporaryPath;
        }

        public void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
            => inner.ReplaceTemporarySibling(temporaryPath, destinationPath);

        public bool Exists(string path) => inner.Exists(path);

        public void Delete(string path) => inner.Delete(path);
    }

    private sealed class BarrierFileOperations(
        ITrxPrototypeFileOperations inner,
        CancellationToken cancellationToken)
        : ITrxPrototypeFileOperations, IDisposable
    {
        private readonly ManualResetEventSlim _operationReached = new(initialState: false);
        private readonly ManualResetEventSlim _continue = new(initialState: false);
        private readonly object _sync = new();
        private TrxFileOperationKind _currentOperation;
        private bool _barriersEnabled;

        public bool SupportsAtomicReplace => inner.SupportsAtomicReplace;

        public void EnableBarriers() => _barriersEnabled = true;

        public TrxFileOperationKind WaitForOperation()
        {
            _operationReached.Wait(cancellationToken);
            lock (_sync)
            {
                _operationReached.Reset();
                return _currentOperation;
            }
        }

        public void Continue() => _continue.Set();

        public ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            ITrxPrototypeFile file = inner.Open(path, mode, access, share);
            Pause(TrxFileOperationKind.Open);
            return new BarrierFile(this, file);
        }

        public string CreateTemporarySiblingPath(string destinationPath)
            => inner.CreateTemporarySiblingPath(destinationPath);

        public void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
        {
            inner.ReplaceTemporarySibling(temporaryPath, destinationPath);
            Pause(TrxFileOperationKind.Replace);
        }

        public bool Exists(string path) => inner.Exists(path);

        public void Delete(string path)
        {
            inner.Delete(path);
            Pause(TrxFileOperationKind.Delete);
        }

        public void Dispose()
        {
            _operationReached.Dispose();
            _continue.Dispose();
        }

        private void Pause(TrxFileOperationKind operation)
        {
            if (!_barriersEnabled)
            {
                return;
            }

            lock (_sync)
            {
                _currentOperation = operation;
                _continue.Reset();
                _operationReached.Set();
            }

            _continue.Wait(cancellationToken);
        }

        private sealed class BarrierFile(BarrierFileOperations owner, ITrxPrototypeFile innerFile)
            : ITrxPrototypeFile
        {
            public long Length => innerFile.Length;

            public long Position => innerFile.Position;

            public int Read(byte[] buffer, int offset, int count)
            {
                int read = innerFile.Read(buffer, offset, count);
                owner.Pause(TrxFileOperationKind.Read);
                return read;
            }

            public void Seek(long offset, SeekOrigin origin)
            {
                innerFile.Seek(offset, origin);
                owner.Pause(TrxFileOperationKind.Seek);
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                innerFile.Write(buffer, offset, count);
                owner.Pause(TrxFileOperationKind.Write);
            }

            public void Flush()
            {
                innerFile.Flush();
                owner.Pause(TrxFileOperationKind.Flush);
            }

            public void SetLength(long length)
            {
                innerFile.SetLength(length);
                owner.Pause(TrxFileOperationKind.SetLength);
            }

            public void Dispose() => innerFile.Dispose();
        }
    }
}

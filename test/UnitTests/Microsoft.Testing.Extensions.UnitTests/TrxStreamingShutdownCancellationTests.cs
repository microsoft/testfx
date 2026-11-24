// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxStreamingShutdownCancellationTests
{
    [TestMethod]
    public async Task Dispose_CanceledRecordBoundaryPreservesPrefixWithoutFaultOrDrops()
    {
        const string path = "shutdown-sidecar.bin";
        var startWriter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposalCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ShutdownBoundaryStream();
        var logs = new ConcurrentQueue<(LogLevel Level, string Message, Exception? Error)>();
        CancellationToken disposalToken = default;
        CancellationTokenRegistration disposalRegistration = default;
        Task writerTask = Task.CompletedTask;
        Task? disposing = null;
        MemoryStream? readSnapshot = null;

        var writerFile = new Mock<IFileStream>(MockBehavior.Strict);
        writerFile.SetupGet(value => value.Stream).Returns(stream);
        writerFile.Setup(value => value.Dispose()).Callback(stream.Dispose);
        var readerFile = new Mock<IFileStream>(MockBehavior.Strict);
        readerFile.SetupGet(value => value.Stream).Returns(() => readSnapshot!);
        readerFile.Setup(value => value.Dispose()).Callback(() => readSnapshot!.Dispose());
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(value => value.ExistFile(path)).Returns(true);
        fileSystem.Setup(value => value.NewFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            .Returns(writerFile.Object);
        fileSystem.Setup(value => value.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(() =>
            {
                readSnapshot = new MemoryStream(stream.Bytes, writable: false);
                return readerFile.Object;
            });

        var scheduler = new Mock<ITask>(MockBehavior.Strict);
        scheduler.Setup(value => value.RunLongRunning(
            It.IsAny<Func<Task>>(), "TRX streaming store writer", CancellationToken.None))
            .Returns<Func<Task>, string, CancellationToken>((body, _, _) =>
            {
                writerTask = Task.Run(
                    async () =>
                    {
                        await startWriter.Task;
                        await body();
                    },
                    CancellationToken.None);
                return writerTask;
            });
        scheduler.Setup(value => value.Delay(TimeSpan.FromMilliseconds(50), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, token) =>
            {
                disposalToken = token;
                stream.Cancellation = new OperationCanceledException("record boundary canceled during public disposal", token);
                disposalRegistration = token.Register(() => disposalCanceled.TrySetResult(true));
            })
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger>(MockBehavior.Strict);
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, error, _) => logs.Enqueue((level, message, error)));

        var store = new TrxResultStreamingStore(
            path, fileSystem.Object, scheduler.Object, logger.Object, batchSize: 2, flushIntervalMs: 0, useBackgroundWriter: true);
        try
        {
            store.Enqueue(new TrxTestResult
            {
                Uid = "prefix-passed",
                DisplayName = "préfixe réussi 東京",
                Outcome = TrxTestOutcome.Passed,
                TrxFullyQualifiedTypeName = "Shutdown.Tests",
            });
            store.Enqueue(new TrxTestResult
            {
                Uid = "prefix-timeout",
                DisplayName = "prefix timeout",
                Outcome = TrxTestOutcome.Timeout,
                ExceptionMessage = "retained timeout diagnostic",
            });
            store.Enqueue(PendingResult("active-at-cancellation"));
            store.Enqueue(PendingResult("local-batch-tail"));
            store.Enqueue(PendingResult("still-queued"));
            startWriter.TrySetResult(true);

            Task firstBoundary = await Task.WhenAny(stream.BoundaryEntered, writerTask);
            Assert.AreSame(stream.BoundaryEntered, firstBoundary, "The writer must reach the gated record boundary, not exit early.");
            await stream.BoundaryEntered;
            Assert.AreEqual(2, store.BufferedCount, "The transient write must recover and the first complete batch must be buffered.");
            Assert.AreEqual(0, store.DroppedCount);
            Assert.IsTrue(disposalToken.CanBeCanceled, "Capture the actual internal disposal token through the injected retry delay.");
            Assert.IsFalse(disposalToken.IsCancellationRequested);
            byte[] prefixBeforeDisposal = stream.Bytes;

            disposing = Task.Run(store.Dispose, CancellationToken.None);
            Task firstCancellation = await Task.WhenAny(disposalCanceled.Task, disposing);
            Assert.AreSame(disposalCanceled.Task, firstCancellation, "Public disposal must cancel the token before returning.");
            await disposalCanceled.Task;
            Assert.IsTrue(disposalToken.IsCancellationRequested);
            Assert.IsFalse(disposing.IsCompleted, "Dispose must still be joining the writer while its explicit gate remains closed.");
            writerFile.Verify(value => value.Dispose(), Times.Never);

            // Write failures are caught and counted by WriteBatch. Its pre-record Position read is
            // outside that handler, so cancellation here reaches the loop's benign shutdown catch.
            stream.ReleaseBoundary();
            await Task.WhenAll(writerTask, disposing);

            Assert.AreEqual(TaskStatus.RanToCompletion, writerTask.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, disposing.Status);
            Assert.IsTrue(stream.CancellationRequestedAtThrow);
            Assert.IsFalse(store.IsFaulted);
            Assert.IsFalse(store.CompletionTimedOut);
            Assert.AreEqual(2, store.BufferedCount);
            Assert.AreEqual(0, store.DroppedCount, "Shutdown must not count the active batch or still-queued records as fault drops.");
            Assert.AreEqual(1, stream.InjectedWriteFailures);
            Assert.AreEqual(1, stream.Seeks);
            Assert.AreEqual(1, stream.Truncations, "Shutdown cancellation must not be handled as another failed record write.");
            Assert.AreSequenceEqual(prefixBeforeDisposal, stream.Bytes, "Cancellation must preserve the successfully retried prefix without a torn tail.");

            IReadOnlyList<TrxTestResult> recovered = store.ReadAll();
            Assert.AreSequenceEqual(["prefix-passed", "prefix-timeout"], recovered.Select(value => value.Uid));
            Assert.AreEqual("préfixe réussi 東京", recovered[0].DisplayName);
            Assert.AreEqual(TrxTestOutcome.Passed, recovered[0].Outcome);
            Assert.AreEqual("Shutdown.Tests", recovered[0].TrxFullyQualifiedTypeName);
            Assert.AreEqual("prefix timeout", recovered[1].DisplayName);
            Assert.AreEqual(TrxTestOutcome.Timeout, recovered[1].Outcome);
            Assert.AreEqual("retained timeout diagnostic", recovered[1].ExceptionMessage);
            (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(logs);
            Assert.AreEqual(LogLevel.Debug, level);
            Assert.AreEqual("TRX streaming store writer cancelled during shutdown.", message);
            Assert.IsNull(error);
            writerFile.VerifyGet(value => value.Stream, Times.AtLeastOnce);
            writerFile.Verify(value => value.Dispose(), Times.Once);
            readerFile.VerifyGet(value => value.Stream, Times.Once);
            readerFile.Verify(value => value.Dispose(), Times.Once);
            Assert.AreEqual(1, stream.DisposeCalls);
            scheduler.Verify(
                value => value.RunLongRunning(It.IsAny<Func<Task>>(), "TRX streaming store writer", CancellationToken.None), Times.Once);
            scheduler.Verify(value => value.Delay(TimeSpan.FromMilliseconds(50), disposalToken), Times.Once);
            scheduler.VerifyNoOtherCalls();
            fileSystem.Verify(value => value.NewFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), Times.Once);
            fileSystem.Verify(value => value.ExistFile(path), Times.Once);
            fileSystem.Verify(value => value.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Times.Once);
            fileSystem.VerifyNoOtherCalls();
        }
        finally
        {
            startWriter.TrySetResult(true);
            stream.ReleaseBoundary();
            disposing ??= Task.Run(store.Dispose, CancellationToken.None);
            try
            {
                await Task.WhenAll(writerTask, disposing);
            }
            finally
            {
                disposalRegistration.Dispose();
                readSnapshot?.Dispose();
                if (stream.DisposeCalls == 0)
                {
                    stream.Dispose();
                }
            }
        }
    }

    private static TrxTestResult PendingResult(string uid)
        => new() { Uid = uid, DisplayName = "unobserved " + uid, Outcome = TrxTestOutcome.Failed };

    private sealed class ShutdownBoundaryStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly TaskCompletionSource<bool> _boundaryEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseBoundary = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _positionReads;

        public OperationCanceledException? Cancellation { get; set; }

        public Task BoundaryEntered => _boundaryEntered.Task;

        public bool CancellationRequestedAtThrow { get; private set; }

        public int InjectedWriteFailures { get; private set; }

        public int Seeks { get; private set; }

        public int Truncations { get; private set; }

        public int DisposeCalls { get; private set; }

        public byte[] Bytes => _inner.ToArray();

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get
            {
                if (++_positionReads == 3)
                {
                    _boundaryEntered.TrySetResult(true);
                    _releaseBoundary.Task.GetAwaiter().GetResult();
                    OperationCanceledException cancellation = Cancellation ?? throw new InvalidOperationException("The retry must supply the disposal token first.");
                    CancellationRequestedAtThrow = cancellation.CancellationToken.IsCancellationRequested;
                    throw cancellation;
                }

                return _inner.Position;
            }

            set => _inner.Position = value;
        }

        public void ReleaseBoundary() => _releaseBoundary.TrySetResult(true);

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
        {
            Seeks++;
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Truncations++;
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (InjectedWriteFailures == 0)
            {
                InjectedWriteFailures++;
                _inner.Write(buffer, offset, Math.Min(count, 1));
                throw new IOException("transient torn prefix");
            }

            _inner.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCalls++;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

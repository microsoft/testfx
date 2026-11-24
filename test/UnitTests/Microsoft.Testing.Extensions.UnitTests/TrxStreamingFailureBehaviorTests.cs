// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxStreamingFailureBehaviorTests
{
    [TestMethod]
    [DataRow("directory")]
    [DataRow("open")]
    [DataRow("position")]
    public async Task Background_ProvisionFailureAccountsBatchAndQueuedRecords(string failurePoint)
    {
        using var fixture = new StoreFixture();
        var original = new InvalidOperationException("injected " + failurePoint + " failure");
        switch (failurePoint)
        {
            case "directory":
                fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(false);
                fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Throws(original);
                break;
            case "open":
                fixture.FileSystem.Setup(value => value.NewFileStream(
                    fixture.Path, FileMode.Create, FileAccess.Write, FileShare.Read)).Throws(original);
                break;
            default:
                fixture.Stream.PositionFailure = original;
                break;
        }

        TrxResultStreamingStore store = fixture.CreateStore();
        EnqueueThree(store);
        Task completion = store.CompleteAsync(CancellationToken.None);
        await fixture.Scheduler.DrainAsync();
        await completion;

        Assert.IsTrue(store.IsFaulted);
        Assert.AreEqual(0, store.BufferedCount);
        Assert.AreEqual(3, store.DroppedCount, "Both the two-record local batch and the still-queued record must be accounted.");
        (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(fixture.Logs);
        Assert.AreEqual(LogLevel.Error, level);
        Assert.AreSame(original, error);
        Assert.AreEqual("TRX streaming store writer faulted; intermediate file may be incomplete. 3 record(s) were dropped from the in-memory queue and will not appear in the TRX.", message);
        store.Enqueue(Result("rejected"));
        Assert.AreEqual(4, store.DroppedCount);
        Assert.IsEmpty(store.ReadAll());
        Assert.AreEqual(failurePoint == "position", fixture.FileClosed);
        if (failurePoint == "directory")
        {
            fixture.FileSystem.Verify(
                value => value.NewFileStream(
                It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read), Times.Never);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Background_CompletedWriterDropsSubsequentResults(bool debugEnabled)
    {
        using var fixture = new StoreFixture();
        fixture.Logger.Setup(value => value.IsEnabled(LogLevel.Debug)).Returns(debugEnabled);
        TrxResultStreamingStore store = fixture.CreateStore();
        store.Enqueue(Result("kept"));
        Task completion = store.CompleteAsync(CancellationToken.None);
        await fixture.Scheduler.DrainAsync();
        await completion;

        store.Enqueue(Result("not-written"));

        Assert.AreEqual("kept", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.AreEqual(1, store.BufferedCount);
        Assert.AreEqual(1, store.DroppedCount);
        Assert.IsFalse(store.IsFaulted);
        Assert.HasCount(debugEnabled ? 1 : 0, fixture.Logs);
        if (debugEnabled)
        {
            Assert.AreEqual("TRX streaming store dropped a record (writer is completed or faulted). Total dropped: 1.", fixture.Logs[0].Message);
            Assert.AreEqual(LogLevel.Debug, fixture.Logs[0].Level);
        }
    }

    [TestMethod]
    public async Task Background_TimeoutReportsQueueWithoutInventingFault()
    {
        using var fixture = new StoreFixture();
        fixture.Scheduler.DelayOverride = (_, _) => Task.CompletedTask;
        TrxResultStreamingStore store = fixture.CreateStore();
        EnqueueThree(store);

        await store.CompleteAsync(CancellationToken.None);

        Assert.IsTrue(store.CompletionTimedOut);
        Assert.IsFalse(store.IsFaulted);
        Assert.AreEqual(0, store.DroppedCount);
        Assert.AreEqual(0, store.BufferedCount);
        Assert.IsFalse(fixture.FileOpened);
        (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(fixture.Logs);
        Assert.AreEqual(LogLevel.Warning, level);
        Assert.AreEqual("TRX streaming store writer did not drain within the hang timeout; intermediate file may be incomplete. Approximately 3 record(s) are still queued and will not appear in the TRX.", message);

        await fixture.Scheduler.DrainAsync();

        Assert.AreSequenceEqual(["first", "second", "third"], store.ReadAll().Select(value => value.Uid));
        Assert.AreEqual(3, store.BufferedCount);
        Assert.IsTrue(store.CompletionTimedOut, "A later drain must not turn a previously incomplete read into an authoritative one.");
        Assert.IsTrue(fixture.FileClosed);
    }

    [TestMethod]
    public async Task Background_CancellationDoesNotMasqueradeAsTimeout()
    {
        using var fixture = new StoreFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Scheduler.DelayOverride = async (_, token) =>
        {
            Assert.AreEqual(cancellation.Token, token);
            cancellation.Cancel();
        };
        TrxResultStreamingStore store = fixture.CreateStore();
        store.Enqueue(Result("pending"));

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.CompleteAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsFalse(store.CompletionTimedOut);
        Assert.IsFalse(store.IsFaulted);
        Assert.IsEmpty(fixture.Logs);
        await fixture.Scheduler.DrainAsync();
        Assert.AreEqual("pending", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.AreEqual(0, store.DroppedCount);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxStreaming_IoFailureRetainedWhenBackoffCanceled")]
    [DataRow(true, DisplayName = "TrxStreaming_AccessFailureRetainedWhenBackoffCanceled")]
    public async Task Background_CanceledBackoffRetainsOriginalWriteFailure(bool unauthorized)
    {
        using var fixture = new StoreFixture();
        Exception original = unauthorized ? new UnauthorizedAccessException("record denied") : new IOException("torn record");
        fixture.Stream.WriteFailure = original;
        fixture.Stream.WriteFailuresRemaining = 1;
        var hangTimeout = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Scheduler.DelayOverride = (delay, _) => delay < TimeSpan.FromSeconds(1)
            ? Task.FromException(new OperationCanceledException("backoff canceled"))
            : hangTimeout.Task;
        TrxResultStreamingStore store = fixture.CreateStore();
        EnqueueThree(store);
        Task completion = store.CompleteAsync(CancellationToken.None);

        await fixture.Scheduler.DrainAsync();
        await completion;

        Assert.IsFalse(store.IsFaulted, "A successful truncate leaves the store usable for the following batch.");
        Assert.AreEqual(2, store.DroppedCount);
        Assert.AreEqual(1, store.BufferedCount);
        Assert.AreEqual("third", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.AreEqual(2, fixture.Stream.Truncations);
        Assert.AreSequenceEqual(
            [TimeSpan.FromMilliseconds(50)],
            fixture.Scheduler.Delays.Where(value => value.Delay < TimeSpan.FromSeconds(1)).Select(value => value.Delay));
        (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(fixture.Logs);
        Assert.AreSame(original, error);
        Assert.AreEqual("Failed to write TRX record 1/2 after up to 3 retries; truncating to last good record. 2 record(s) from this batch will not appear in the TRX.", message);
    }

    [TestMethod]
    public void Background_FaultedJoinIsBestEffort()
    {
        using var fixture = new StoreFixture();
        fixture.CreateStore();
        fixture.Scheduler.FaultJoin(new InvalidOperationException("injected scheduler failure"));

        fixture.DisposeStore();

        Assert.IsFalse(fixture.FileOpened);
        Assert.IsEmpty(fixture.Logs);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("fault")]
    [DataRow("drop")]
    [DataRow("timeout")]
    [DataRow("delete")]
    public async Task Logging_FailuresCannotEscapeFaultDropTimeoutOrDelete(string operation)
    {
        using var fixture = new StoreFixture();
        fixture.Logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Throws(new InvalidOperationException("logger unavailable"));
        if (operation == "fault")
        {
            fixture.FileSystem.Setup(value => value.NewFileStream(
                fixture.Path, FileMode.Create, FileAccess.Write, FileShare.Read)).Throws(new UnauthorizedAccessException("sidecar denied"));
        }
        else if (operation == "timeout")
        {
            fixture.Scheduler.DelayOverride = (_, _) => Task.CompletedTask;
        }

        TrxResultStreamingStore store = fixture.CreateStore();
        EnqueueThree(store);
        Task completion = store.CompleteAsync(CancellationToken.None);
        if (operation == "timeout")
        {
            await completion;
            Assert.IsTrue(store.CompletionTimedOut);
        }

        await fixture.Scheduler.DrainAsync();
        await completion;
        if (operation == "drop")
        {
            store.Enqueue(Result("rejected"));
        }
        else if (operation == "delete")
        {
            fixture.FileSystem.Setup(value => value.DeleteFile(fixture.Path)).Throws(new InvalidOperationException("delete failed"));
            store.TryDelete();
        }

        Assert.AreEqual(operation == "fault", store.IsFaulted);
        Assert.AreEqual(operation == "fault" ? 3 : operation == "drop" ? 1 : 0, store.DroppedCount);
        Assert.AreEqual(operation == "fault" ? 0 : 3, store.BufferedCount);
        LogLevel level = operation == "fault" ? LogLevel.Error : operation == "timeout" ? LogLevel.Warning : LogLevel.Debug;
        fixture.Logger.Verify(
            value => value.Log(
            level, It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        if (operation == "delete")
        {
            Assert.AreSequenceEqual(["first", "second", "third"], store.ReadAll().Select(value => value.Uid));
        }
    }

    [TestMethod]
    public async Task CloseFailure_DoesNotEraseBufferedResults()
    {
        using var fixture = new StoreFixture();
        var original = new IOException("flush unavailable");
        fixture.Stream.FlushFailure = original;
        TrxResultStreamingStore store = fixture.CreateStore();
        store.Enqueue(Result("durable-prefix"));
        Task completion = store.CompleteAsync(CancellationToken.None);

        await fixture.Scheduler.DrainAsync();
        await completion;

        Assert.AreEqual(1, store.BufferedCount);
        Assert.AreEqual(0, store.DroppedCount);
        Assert.IsFalse(store.IsFaulted);
        Assert.AreEqual("durable-prefix", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.AreSequenceEqual(
            ["Failed to flush TRX streaming store; records remain in OS buffer.", "Failed to close TRX streaming store file."],
            fixture.Logs.Select(value => value.Message));
        Assert.IsTrue(fixture.Logs.All(value => value.Level == LogLevel.Error && ReferenceEquals(original, value.Error)));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxStreaming_InlineIoFailureCannotTruncate")]
    [DataRow(true, DisplayName = "TrxStreaming_InlineAccessFailureCannotTruncate")]
    public async Task Inline_UntruncatableWriteFaultsOnceAndRejectsLaterRecords(bool unauthorized)
    {
        using var fixture = new StoreFixture();
        Exception original = unauthorized ? new UnauthorizedAccessException("record denied") : new IOException("torn record");
        var truncateFailure = new IOException("truncate denied");
        fixture.Stream.WriteFailure = original;
        fixture.Stream.WriteFailuresRemaining = 1;
        fixture.Stream.TruncateFailure = truncateFailure;
        TrxResultStreamingStore store = fixture.CreateStore(background: false);

        store.Enqueue(Result("torn"));
        store.Enqueue(Result("must-not-follow-corruption"));
        await store.CompleteAsync(CancellationToken.None);

        Assert.IsTrue(store.IsFaulted);
        Assert.AreEqual(0, store.BufferedCount);
        Assert.AreEqual(2, store.DroppedCount);
        Assert.AreEqual(2, fixture.Stream.Truncations);
        Assert.IsEmpty(store.ReadAll(), "A partial length prefix is not a recoverable complete record.");
        (LogLevel Level, string Message, Exception? Error)[] errors = fixture.Logs.Where(value => value.Level == LogLevel.Error).ToArray();
        Assert.HasCount(2, errors);
        Assert.AreSame(original, errors[0].Error);
        Assert.AreSame(truncateFailure, errors[1].Error);
        Assert.AreEqual("Failed to truncate TRX streaming store after write failure; marking store faulted. 0 additional record(s) from the queue were dropped.", errors[1].Message);
        Assert.IsEmpty(fixture.Scheduler.Delays);
        Assert.IsNull(fixture.Scheduler.WriterName);
        Assert.IsTrue(fixture.FileClosed);
    }

    [TestMethod]
    public async Task Background_FaultSealsOpenQueueBeforeProducersCanAppend()
    {
        using var fixture = new StoreFixture();
        var original = new IOException("sidecar provisioning failed");
        fixture.FileSystem.Setup(value => value.NewFileStream(
            fixture.Path, FileMode.Create, FileAccess.Write, FileShare.Read)).Throws(original);
        TrxResultStreamingStore store = fixture.CreateStore();
        EnqueueThree(store);

        // Unlike the normal completion tests, producers have not called CompleteAdding yet.
        // The deterministic provisioning failure occurs before the writer could ever poll idle.
        await fixture.Scheduler.DrainAsync();
        store.Enqueue(Result("rejected-after-writer-sealed-queue"));
        await store.CompleteAsync(CancellationToken.None);

        Assert.IsTrue(store.IsFaulted);
        Assert.AreEqual(4, store.DroppedCount);
        Assert.AreEqual(0, store.BufferedCount);
        Assert.IsFalse(store.CompletionTimedOut);
        (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(fixture.Logs.Where(value => value.Level == LogLevel.Error));
        Assert.AreSame(original, error);
        Assert.Contains("3 record(s) were dropped", message);
        Assert.Contains(
            value => value.Level == LogLevel.Debug &&
            value.Message == "TRX streaming store dropped a record (writer is completed or faulted). Total dropped: 4.", fixture.Logs);
        fixture.FileSystem.Verify(
            value => value.NewFileStream(
            fixture.Path, FileMode.Create, FileAccess.Write, FileShare.Read), Times.Once);
        Assert.IsEmpty(store.ReadAll());
    }

    private static void EnqueueThree(TrxResultStreamingStore store)
    {
        store.Enqueue(Result("first"));
        store.Enqueue(Result("second"));
        store.Enqueue(Result("third"));
    }

    private static TrxTestResult Result(string uid)
        => new() { Uid = uid, DisplayName = "result " + uid, Outcome = TrxTestOutcome.Passed, TrxFullyQualifiedTypeName = "Suite.Tests" };

    private sealed class StoreFixture : IDisposable
    {
        private TrxResultStreamingStore? _store;

        public StoreFixture()
        {
            Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            Logger.Setup(value => value.Log(
                It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
                .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) => Logs.Add((level, message, error)));
            FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(true);
            FileSystem.Setup(value => value.ExistFile(Path)).Returns(() => FileOpened);
            FileSystem.Setup(value => value.NewFileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read))
                .Returns(() =>
                {
                    FileOpened = true;
                    var file = new Mock<IFileStream>();
                    file.SetupGet(value => value.Stream).Returns(Stream);
                    file.Setup(value => value.Dispose()).Callback(() =>
                    {
                        FileClosed = true;
                        Stream.Dispose();
                    });
                    return file.Object;
                });
            FileSystem.Setup(value => value.NewFileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                .Returns(() => new ReadOnlyFile(new MemoryStream(Stream.Bytes, writable: false)));
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "virtual-stream-" + Guid.NewGuid().ToString("N"), "records.bin");

        public Mock<IFileSystem> FileSystem { get; } = new();

        public Mock<ILogger> Logger { get; } = new();

        public TrxBehaviorFixture.DeferredTrxTask Scheduler { get; } = new();

        public FaultingStream Stream { get; } = new();

        public List<(LogLevel Level, string Message, Exception? Error)> Logs { get; } = [];

        public bool FileOpened { get; private set; }

        public bool FileClosed { get; private set; }

        public TrxResultStreamingStore CreateStore(bool background = true)
            => _store = new(Path, FileSystem.Object, Scheduler, Logger.Object, batchSize: 2, flushIntervalMs: 1, useBackgroundWriter: background);

        public void DisposeStore()
        {
            Scheduler.ReleaseJoin();
            _store?.Dispose();
            _store = null;
        }

        public void Dispose()
        {
            DisposeStore();
            Stream.Dispose();
        }
    }

    private sealed class ReadOnlyFile(Stream stream) : IFileStream
    {
        public string Name => "read snapshot";

        public Stream Stream { get; } = stream;

        public void Dispose() => Stream.Dispose();

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }

    private sealed class FaultingStream : Stream
    {
        private readonly MemoryStream _inner = new();

        public Exception? PositionFailure { get; set; }

        public Exception? WriteFailure { get; set; }

        public int WriteFailuresRemaining { get; set; }

        public Exception? FlushFailure { get; set; }

        public Exception? TruncateFailure { get; set; }

        public int Truncations { get; private set; }

        public byte[] Bytes => _inner.ToArray();

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => PositionFailure is { } failure ? throw failure : _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            if (FlushFailure is { } failure)
            {
                throw failure;
            }

            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value)
        {
            Truncations++;
            if (TruncateFailure is { } failure)
            {
                throw failure;
            }

            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (WriteFailuresRemaining > 0)
            {
                WriteFailuresRemaining--;
                _inner.Write(buffer, offset, Math.Min(count, 1));
                throw WriteFailure ?? new IOException("injected write failure");
            }

            _inner.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

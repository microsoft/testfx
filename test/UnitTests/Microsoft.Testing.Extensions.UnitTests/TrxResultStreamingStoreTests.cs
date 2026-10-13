// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers the inline (single-threaded) mode of <c>TrxResultStreamingStore</c> that makes
/// <c>--report-trx</c> usable on single-threaded WebAssembly runtimes (browser-wasm / wasi-wasm),
/// where the dedicated writer thread and the blocking queue are unavailable.
/// See <see href="https://github.com/microsoft/testfx/issues/2196"/>.
/// </summary>
[TestClass]
public sealed class TrxResultStreamingStoreTests
{
    [TestMethod]
    [DataRow(1, false, true)]
    [DataRow(2, true, true)]
    [DataRow(3, false, false)]
    [DataRow(3, true, false)]
    public void InlineWriteRetry_RewindsPartialRecordsAndHonorsThreeAttemptLimit(int failures, bool unauthorized, bool retained)
    {
        using MemoryStreamingFixture fixture = new();
        using TrxResultStreamingStore store = fixture.CreateStore(new ThreadlessTask(), background: false);
        store.Enqueue(Result("kept"));
        fixture.Stream.WriteFailuresRemaining = failures;
        fixture.Stream.UnauthorizedFailure = unauthorized;

        store.Enqueue(Result("candidate"));
        fixture.Stream.WriteFailuresRemaining = 0;
        store.Enqueue(Result("following"));

        Assert.AreSequenceEqual(retained ? ["kept", "candidate", "following"] : ["kept", "following"], store.ReadAll().Select(record => record.Uid));
        Assert.AreEqual(retained ? 3 : 2, store.BufferedCount);
        Assert.AreEqual(retained ? 0 : 1, store.DroppedCount);
        Assert.AreEqual(failures, fixture.Stream.InjectedWriteFailures);
        Assert.AreEqual(failures + (retained ? 0 : 1), fixture.Stream.Truncations);
        Assert.IsFalse(store.IsFaulted, "A successful truncate preserves a usable store after a lost record.");
    }

    [TestMethod]
    public async Task BackgroundWriteRetry_UsesBoundedBackoffAndDrainsControlledWriterWithoutThreads()
    {
        using MemoryStreamingFixture fixture = new();
        ControlledWriterTask task = new();
        using TrxResultStreamingStore store = fixture.CreateStore(task, background: true);
        fixture.Stream.WriteFailuresRemaining = 2;
        store.Enqueue(Result("first"));
        store.Enqueue(Result("second"));
        store.Enqueue(Result("third"));
        Task completion = store.CompleteAsync(CancellationToken.None);

        await task.DrainAsync();
        await completion;

        Assert.AreSequenceEqual([TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)], task.RetryDelays);
        Assert.AreSequenceEqual(["first", "second", "third"], store.ReadAll().Select(record => record.Uid));
        Assert.AreEqual(3, store.BufferedCount);
        Assert.AreEqual(0, store.DroppedCount);
        Assert.IsFalse(store.CompletionTimedOut);
        Assert.IsFalse(fixture.Stream.CanWrite);
    }

    [TestMethod]
    public async Task BackgroundWriteFailure_UntruncatableRecordFaultsAndAccountsBatchAndQueuedDrops()
    {
        using MemoryStreamingFixture fixture = new();
        ControlledWriterTask task = new();
        using TrxResultStreamingStore store = fixture.CreateStore(task, background: true);
        fixture.Stream.WriteFailuresRemaining = 1;
        fixture.Stream.TruncationFails = true;
        store.Enqueue(Result("first"));
        store.Enqueue(Result("second"));
        store.Enqueue(Result("queued"));
        Task completion = store.CompleteAsync(CancellationToken.None);

        await task.DrainAsync();
        await completion;

        Assert.IsTrue(store.IsFaulted);
        Assert.AreEqual(3, store.DroppedCount);
        Assert.AreEqual(0, store.BufferedCount);
        Assert.AreEqual(1, fixture.Stream.InjectedWriteFailures, "A failed rewind must stop retrying the corrupt record.");
        Assert.IsEmpty(task.RetryDelays);
        store.Enqueue(Result("rejected-after-fault"));
        Assert.AreEqual(4, store.DroppedCount);
        Assert.Contains(log => log.Contains("marking store faulted", StringComparison.Ordinal), fixture.Logs);
    }

    [TestMethod]
    public void FlushAndDeleteFailures_DoNotDiscardRecoverableRecordsOrEscapeToTestSession()
    {
        using MemoryStreamingFixture fixture = new();
        using TrxResultStreamingStore store = fixture.CreateStore(new ThreadlessTask(), background: false);
        fixture.Stream.FlushFails = true;
        store.Enqueue(Result("recoverable"));

        Assert.AreEqual(1, store.BufferedCount);
        Assert.AreEqual(0, store.DroppedCount);
        Assert.AreEqual("recoverable", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.Contains(log => log.Contains("Failed to flush TRX streaming store", StringComparison.Ordinal), fixture.Logs);
        fixture.FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>())).Throws(new UnauthorizedAccessException("delete denied"));
        store.TryDelete();
        Assert.AreEqual("recoverable", Assert.ContainsSingle(store.ReadAll()).Uid);
        Assert.Contains(log => log.Contains("delete denied", StringComparison.Ordinal), fixture.Logs);
    }

    private sealed class MemoryStreamingFixture : IDisposable
    {
        private readonly Mock<ILogger> _logger = new();

        public MemoryStreamingFixture()
        {
            FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(() => Exists);
            FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(true);
            FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                .Returns((string path, FileMode mode, FileAccess access, FileShare share) =>
                {
                    Stream stream;
                    if (mode == FileMode.Create)
                    {
                        Exists = true;
                        stream = Stream;
                    }
                    else
                    {
                        stream = new MemoryStream(Stream.Bytes);
                    }

                    var fileStream = new Mock<IFileStream>();
                    fileStream.SetupGet(value => value.Stream).Returns(stream);
                    fileStream.Setup(value => value.Dispose()).Callback(stream.Dispose);
                    return fileStream.Object;
                });
            _logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            _logger.Setup(value => value.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
                .Callback((LogLevel level, string state, Exception? error, Func<string, Exception?, string> formatter) => Logs.Add(formatter(state, error)));
        }

        public FaultingMemoryStream Stream { get; } = new();

        public Mock<IFileSystem> FileSystem { get; } = new();

        public List<string> Logs { get; } = [];

        private bool Exists { get; set; }

        public TrxResultStreamingStore CreateStore(ITask task, bool background)
            => new("memory-results.bin", FileSystem.Object, task, _logger.Object, batchSize: 2, flushIntervalMs: 1, useBackgroundWriter: background);

        public void Dispose() => Stream.Dispose();
    }

    private sealed class FaultingMemoryStream : Stream
    {
        private readonly MemoryStream _inner = new();

        public int WriteFailuresRemaining { get; set; }

        public int InjectedWriteFailures { get; private set; }

        public int Truncations { get; private set; }

        public bool UnauthorizedFailure { get; set; }

        public bool TruncationFails { get; set; }

        public bool FlushFails { get; set; }

        public byte[] Bytes => _inner.ToArray();

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush()
        {
            if (FlushFails)
            {
                throw new IOException("flush denied");
            }

            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value)
        {
            if (TruncationFails)
            {
                throw new IOException("truncate denied");
            }

            Truncations++;
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (WriteFailuresRemaining > 0)
            {
                WriteFailuresRemaining--;
                InjectedWriteFailures++;
                _inner.Write(buffer, offset, Math.Min(1, count));
                throw UnauthorizedFailure ? new UnauthorizedAccessException("write denied") : new IOException("write denied");
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

    private sealed class ControlledWriterTask : ITask
    {
        private readonly TaskCompletionSource<bool> _writerCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _pendingTimeout = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<Task>? _writer;

        public List<TimeSpan> RetryDelays { get; } = [];

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
        {
            _writer = action;
            return _writerCompletion.Task;
        }

        public async Task DrainAsync()
        {
            await _writer!();
            _writerCompletion.TrySetResult(true);
        }

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            if (timeSpan >= TimeSpan.FromSeconds(1))
            {
                return _pendingTimeout.Task;
            }

            RetryDelays.Add(timeSpan);
            return Task.CompletedTask;
        }

        public Task Run(Action action) => throw new NotSupportedException();

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => throw new NotSupportedException();
    }

    [TestMethod]
    public void Ctor_WhenBackgroundWriterUnavailable_DoesNotStartWriterThread()
    {
        using var temp = TempDirectory.Create();
        var task = new ThreadlessTask();

        // A PlatformNotSupportedException escaping here is exactly the browser-wasm crash being fixed.
        using TrxResultStreamingStore store = CreateInlineStore(temp, task);

        Assert.IsTrue(store.IsInline);
        Assert.AreEqual(0, task.RunLongRunningCallCount);
    }

    [TestMethod]
    public void Ctor_WhenBackgroundWriterAvailable_UsesBackgroundWriter()
    {
        // The parameterless-ish public ctor resolves the mode from RuntimeFeatureHelper.IsMultiThreaded.
        // Unit tests never run on browser-wasm/wasi-wasm, so the background writer must be selected here;
        // this fails if the runtime probe is inverted.
        using var temp = TempDirectory.Create();
        var task = new RealTask();
        using var store = new TrxResultStreamingStore(temp.NewFilePath(), new RealFileSystem(), task, Mock.Of<ILogger>());

        Assert.IsFalse(store.IsInline);
        Assert.AreEqual(1, task.RunLongRunningCallCount);
    }

    [TestMethod]
    public void Enqueue_InlineMode_WritesRecordsSynchronouslyAndInOrder()
    {
        using var temp = TempDirectory.Create();
        using TrxResultStreamingStore store = CreateInlineStore(temp, new ThreadlessTask());

        store.Enqueue(Result("u0"));
        store.Enqueue(Result("u1"));
        store.Enqueue(Result("u2"));

        // Inline mode has no hand-off: records are durable as soon as Enqueue returns, without CompleteAsync.
        Assert.AreEqual(3, store.BufferedCount);
        Assert.AreEqual(0, store.DroppedCount);
        Assert.IsFalse(store.IsFaulted);
        Assert.AreSequenceEqual(["u0", "u1", "u2"], store.ReadAll().Select(r => r.Uid).ToArray());
    }

    [TestMethod]
    public async Task CompleteAsync_InlineMode_KeepsRecordsAndDoesNotTimeOut()
    {
        using var temp = TempDirectory.Create();
        using TrxResultStreamingStore store = CreateInlineStore(temp, new ThreadlessTask());
        store.Enqueue(Result("u0"));

        await store.CompleteAsync(CancellationToken.None);

        Assert.IsFalse(store.CompletionTimedOut);
        Assert.AreSequenceEqual(["u0"], store.ReadAll().Select(r => r.Uid).ToArray());
    }

    [TestMethod]
    public async Task Enqueue_InlineModeAfterCompletion_DropsRecordInsteadOfWriting()
    {
        using var temp = TempDirectory.Create();
        using TrxResultStreamingStore store = CreateInlineStore(temp, new ThreadlessTask());
        store.Enqueue(Result("kept"));
        await store.CompleteAsync(CancellationToken.None);

        store.Enqueue(Result("dropped"));

        Assert.AreEqual(1, store.DroppedCount);
        Assert.AreEqual(1, store.BufferedCount);
        Assert.AreSequenceEqual(["kept"], store.ReadAll().Select(r => r.Uid).ToArray());
    }

    [TestMethod]
    public void Dispose_InlineMode_ClosesFileWithoutBlockingOnAWriterTask()
    {
        using var temp = TempDirectory.Create();
        string filePath = temp.NewFilePath();
        var store = new TrxResultStreamingStore(filePath, new RealFileSystem(), new ThreadlessTask(), Mock.Of<ILogger>(), batchSize: 64, flushIntervalMs: 1, useBackgroundWriter: false);
        store.Enqueue(Result("u0"));

        Assert.AreEqual(1, store.BufferedCount, "Record enqueued before Dispose must be persisted, not silently dropped.");
        store.Dispose();

        // The handle must be released: on Windows an open writer would make the delete fail.
        File.Delete(filePath);
        Assert.IsFalse(File.Exists(filePath));
    }

    [TestMethod]
    public void Enqueue_InlineModeWhenFileCannotBeProvisioned_FaultsAndAccountsTheDropWithoutThrowing()
    {
        using var temp = TempDirectory.Create();
        var fileSystem = new RealFileSystem { NewFileStreamError = () => new IOException("disk is on fire") };
        using var store = new TrxResultStreamingStore(temp.NewFilePath(), fileSystem, new ThreadlessTask(), Mock.Of<ILogger>(), batchSize: 64, flushIntervalMs: 1, useBackgroundWriter: false);

        store.Enqueue(Result("u0"));

        Assert.IsTrue(store.IsFaulted);
        Assert.AreEqual(1, store.DroppedCount);
        Assert.AreEqual(0, store.BufferedCount);

        // A faulted store must stop accepting records rather than retrying the broken file for the whole session.
        store.Enqueue(Result("u1"));
        Assert.AreEqual(2, store.DroppedCount);
    }

    [TestMethod]
    public async Task InlineAndBackgroundModes_ProduceIdenticalOnDiskBytes()
    {
        using var temp = TempDirectory.Create();
        TrxTestResult[] records = [Result("u0"), Result("u1"), Result("u2")];

        string inlinePath = temp.NewFilePath();
        using (var inline = new TrxResultStreamingStore(inlinePath, new RealFileSystem(), new ThreadlessTask(), Mock.Of<ILogger>(), batchSize: 64, flushIntervalMs: 1, useBackgroundWriter: false))
        {
            foreach (TrxTestResult record in records)
            {
                inline.Enqueue(record);
            }

            await inline.CompleteAsync(CancellationToken.None);
        }

        string backgroundPath = temp.NewFilePath();
        using (var background = new TrxResultStreamingStore(backgroundPath, new RealFileSystem(), new RealTask(), Mock.Of<ILogger>(), batchSize: 64, flushIntervalMs: 1, useBackgroundWriter: true))
        {
            foreach (TrxTestResult record in records)
            {
                background.Enqueue(record);
            }

            await background.CompleteAsync(CancellationToken.None);
        }

        Assert.AreSequenceEqual(File.ReadAllBytes(backgroundPath), File.ReadAllBytes(inlinePath));
    }

    private static TrxResultStreamingStore CreateInlineStore(TempDirectory temp, ITask task)
        => new(temp.NewFilePath(), new RealFileSystem(), task, Mock.Of<ILogger>(), batchSize: 64, flushIntervalMs: 1, useBackgroundWriter: false);

    private static TrxTestResult Result(string uid)
        => new() { Uid = uid, DisplayName = uid, Outcome = TrxTestOutcome.Passed };

    private sealed class TempDirectory : IDisposable
    {
        private int _counter;

        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trx-stream-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public string NewFilePath()
            => System.IO.Path.Combine(Path, $"store-{Interlocked.Increment(ref _counter)}.bin");

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a leftover temp folder must not fail the test.
            }
        }
    }

    private sealed class RealFileSystem : IFileSystem
    {
        public Func<Exception>? NewFileStreamError { get; set; }

        public bool ExistFile(string path) => File.Exists(path);

        public bool ExistDirectory(string? path) => path is not null && Directory.Exists(path);

        public string CreateDirectory(string path) => Directory.CreateDirectory(path).FullName;

        public IFileStream NewFileStream(string path, FileMode mode)
            => NewFileStream(path, mode, FileAccess.ReadWrite, FileShare.None);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access)
            => NewFileStream(path, mode, access, FileShare.None);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access, FileShare share)
            => NewFileStreamError is not null
                ? throw NewFileStreamError()
                : new RealFileStream(new FileStream(path, mode, access, share));

        public void DeleteFile(string path) => File.Delete(path);

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) => throw new NotSupportedException();

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => throw new NotSupportedException();

        public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) => throw new NotSupportedException();

        public void ReplaceFile(string sourceFileName, string destFileName) => throw new NotSupportedException();

        public string ReadAllText(string path) => File.ReadAllText(path);

        public Task<string> ReadAllTextAsync(string path) => Task.FromResult(File.ReadAllText(path));
    }

    private sealed class RealFileStream(FileStream stream) : IFileStream
    {
        public Stream Stream { get; } = stream;

        public string Name => stream.Name;

        public void Dispose() => Stream.Dispose();

#if NETCOREAPP
        public ValueTask DisposeAsync() => Stream.DisposeAsync();
#endif
    }

    /// <summary>
    /// Emulates a single-threaded WebAssembly runtime: creating a dedicated thread is not supported.
    /// </summary>
    private sealed class ThreadlessTask : ITask
    {
        public int RunLongRunningCallCount { get; private set; }

        public Task Run(Action action) => throw new NotSupportedException();

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
        {
            RunLongRunningCallCount++;
            throw new PlatformNotSupportedException("Threads are not supported on this runtime.");
        }

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => Task.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => Task.Delay(timeSpan, cancellationToken);
    }

    private sealed class RealTask : ITask
    {
        public int RunLongRunningCallCount { get; private set; }

        public Task Run(Action action) => Task.Run(action);

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => Task.Run(function, cancellationToken);

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => Task.Run(function, cancellationToken);

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
        {
            RunLongRunningCallCount++;
            return Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => Task.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => Task.Delay(timeSpan, cancellationToken);
    }
}

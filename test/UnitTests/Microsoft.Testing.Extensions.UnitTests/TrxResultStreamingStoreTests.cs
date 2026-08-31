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

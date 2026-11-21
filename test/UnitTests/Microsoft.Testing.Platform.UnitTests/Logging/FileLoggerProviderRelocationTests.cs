// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileLoggerProviderRelocationTests : IDisposable
{
    public TestContext TestContext { get; set; } = null!;

    private const string NewDirectory = "relocated";
    private const string Category = "Test";

    private static readonly Func<string, Exception?, string> Formatter = (state, _) => state;

    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IConsole> _console = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFileStream> _fileStream = new();
    private readonly Mock<IFileStreamFactory> _fileStreamFactory = new();
    private readonly MemoryStream _stream = new();
    private readonly List<MemoryStream> _laterStreams = [];
    private int _createCount;

    public FileLoggerProviderRelocationTests()
    {
        _fileStream.Setup(x => x.Name).Returns("ccc");
        _fileStream.Setup(x => x.Stream).Returns(_stream);
#if NETCOREAPP
        _fileStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif
        _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(() => ++_createCount == 1 ? _fileStream.Object : CreateLaterFileStream());
    }

    void IDisposable.Dispose()
    {
        _stream.Dispose();
        foreach (MemoryStream stream in _laterStreams)
        {
            stream.Dispose();
        }
    }

    [TestMethod]
    public void CreateLogger_IsEnabled_UsesTheProviderLevel()
    {
        using FileLoggerProvider provider = CreateProvider(new ControlledLoopTask(), syncFlush: true, LogLevel.Warning);

        ILogger logger = provider.CreateLogger(Category);

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Error));
    }

    [TestMethod]
    public void Log_SyncFlush_WritesTheEntryExactlyOnce()
    {
        using FileLoggerProvider provider = CreateProvider(new ControlledLoopTask(), syncFlush: true);

        provider.Log(LogLevel.Information, "hello", null, Formatter, Category);

        Assert.AreEqual("[00:00:00.000 Test - INFORMATION] hello" + Environment.NewLine, ReadStream());
    }

    [TestMethod]
    public void Log_DisabledLevel_DoesNotEvenInvokeTheFormatterAndWritesNothing()
    {
        using FileLoggerProvider provider = CreateProvider(new ControlledLoopTask(), syncFlush: true, LogLevel.Warning);
        int formatterCalls = 0;

        provider.Log(LogLevel.Information, "hidden", null, (state, _) =>
        {
            formatterCalls++;
            return state;
        }, Category);

        Assert.AreEqual(0, formatterCalls);
        Assert.AreEqual(string.Empty, ReadStream());
    }

    [TestMethod]
    [UnsupportedOSPlatform("browser")]
    public async Task Dispose_DisposesTheInnerFileLoggerAndCanBeCalledAgainWithoutBlocking()
    {
        FileLoggerProvider provider = CreateProvider(new ControlledLoopTask(), syncFlush: true);

        provider.Dispose();

        Assert.IsTrue(provider.FileLogger.IsFileHandleReleased);
        _fileStream.Verify(x => x.Dispose(), Times.Once);

        // A relocation semaphore that was not released by the first Dispose would block this call forever.
        var second = Task.Run(provider.Dispose, TestContext.CancellationToken);
        await BoundedTaskWait.CompleteWithinAsync(second, 10);
    }

    [TestMethod]
    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync_CalledTwice_ReleasesTheRelocationSemaphoreEachTime()
    {
        ControlledLoopTask task = new(runRealLoops: true);
        FileLoggerProvider provider = CreateProvider(task, syncFlush: false);
        task.CompleteFirstLoop();

        await BoundedTaskWait.CompleteWithinAsync(provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory), 10);
        await BoundedTaskWait.CompleteWithinAsync(provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory), 10);

        _fileSystem.Verify(x => x.MoveFile("ccc", Path.Combine(NewDirectory, "ccc")), Times.Exactly(2));
    }

    [TestMethod]
    public async Task LogAsync_AsyncFlush_DuringRelocation_DoesNotWaitForTheRelocationAndIsReplayedOnTheReplacementLogger()
    {
        ControlledLoopTask task = new(runRealLoops: true);
        FileLoggerProvider provider = CreateProvider(task, syncFlush: false);

        // Relocation takes the semaphore and then waits for the previous consumer loop to end.
        Task relocation = provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory);
        Assert.IsFalse(relocation.IsCompleted);

        // In async-flush mode the entry is queued without touching the relocation semaphore, so this must not block.
        await BoundedTaskWait.CompleteWithinAsync(provider.LogAsync(LogLevel.Information, "queued", null, Formatter, Category), 10);

        task.CompleteFirstLoop();
        await BoundedTaskWait.CompleteWithinAsync(relocation, 10);
#if NETFRAMEWORK
        provider.Dispose();
#else
        await provider.DisposeAsync();
#endif

        Assert.AreEqual("0001-01-01T00:00:00.0000000+00:00 Test INFORMATION queued" + Environment.NewLine, ReadStream());
        _fileSystem.Verify(x => x.MoveFile("ccc", Path.Combine(NewDirectory, "ccc")), Times.Once);
    }

    [TestMethod]
    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync_WhenTheReplacementCannotBeCreated_StopsQueueingLogs()
    {
        ControlledLoopTask task = new(runRealLoops: true);
        FileLoggerProvider provider = CreateProvider(task, syncFlush: false);
        task.CompleteFirstLoop();
        _fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("cannot create"));

        await Assert.ThrowsAsync<IOException>(() => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory));

        // The previous logger is disposed and no replacement exists: a log must go to it (and be rejected by its completed channel)
        // rather than being silently queued forever in a pending queue that nobody will replay.
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.Log(LogLevel.Information, "lost", null, Formatter, Category));
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_DisposesTheInnerFileLoggerAndCanBeCalledAgainWithoutBlocking()
    {
        ControlledLoopTask task = new(runRealLoops: true);
        FileLoggerProvider provider = CreateProvider(task, syncFlush: false);
        task.CompleteFirstLoop();

#if NETFRAMEWORK
        provider.Dispose();
#else
        await provider.DisposeAsync();
#endif

        Assert.IsTrue(provider.FileLogger.IsFileHandleReleased);
        _fileStream.Verify(x => x.DisposeAsync(), Times.Once);

        // A relocation semaphore that was not released by the first DisposeAsync would block this call forever.
        await BoundedTaskWait.CompleteWithinAsync(provider.DisposeAsync().AsTask(), 10);
    }
#endif

    private FileLoggerProvider CreateProvider(ITask task, bool syncFlush, LogLevel level = LogLevel.Trace)
        => new(
            new FileLoggerOptions("aaa", "bbb", fileName: "ccc", syncFlush: syncFlush),
            level,
            customDirectory: false,
            _clock.Object,
            task,
            _console.Object,
            _fileSystem.Object,
            _fileStreamFactory.Object);

    private IFileStream CreateLaterFileStream()
    {
        MemoryStream stream = new();
        _laterStreams.Add(stream);
        Mock<IFileStream> fileStream = new();
        fileStream.Setup(x => x.Name).Returns("ccc");
        fileStream.Setup(x => x.Stream).Returns(stream);
        return fileStream.Object;
    }

    private string ReadStream()
        => Encoding.UTF8.GetString(_stream.ToArray()) + string.Concat(_laterStreams.Select(s => Encoding.UTF8.GetString(s.ToArray())));

    // The first consumer loop (the one of the logger created by the provider constructor) only ends when the test says so,
    // which freezes a relocation at the point where the previous logger is being disposed. Later loops run for real.
    private sealed class ControlledLoopTask : ITask
    {
        private readonly TaskCompletionSource<bool> _firstLoop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SystemTask _inner = new();
        private readonly bool _runRealLoops;
        private int _runCount;

        public ControlledLoopTask(bool runRealLoops = false) => _runRealLoops = runRealLoops;

        public void CompleteFirstLoop() => _firstLoop.TrySetResult(true);

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
            => Interlocked.Increment(ref _runCount) == 1
                ? _firstLoop.Task
                : _runRealLoops ? _inner.Run(function, cancellationToken) : new TaskCompletionSource<bool>().Task;

        public Task Run(Action action) => _inner.Run(action);

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => _inner.Run(function, cancellationToken);

        [UnsupportedOSPlatform("browser")]
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => _inner.RunLongRunning(action, name, cancellationToken);

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }
}

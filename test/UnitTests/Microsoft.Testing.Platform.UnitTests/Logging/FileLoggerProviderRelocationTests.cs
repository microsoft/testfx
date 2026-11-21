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
        using FileLoggerProvider provider = CreateProvider(syncFlush: true, LogLevel.Warning);

        ILogger logger = provider.CreateLogger(Category);

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Error));
    }

    [TestMethod]
    public void Log_SyncFlush_WritesTheEntryExactlyOnce()
    {
        using FileLoggerProvider provider = CreateProvider(syncFlush: true);

        provider.Log(LogLevel.Information, "hello", null, Formatter, Category);

        Assert.AreEqual("[00:00:00.000 Test - INFORMATION] hello" + Environment.NewLine, ReadStream());
    }

    [TestMethod]
    public void Log_DisabledLevel_DoesNotEvenInvokeTheFormatterAndWritesNothing()
    {
        using FileLoggerProvider provider = CreateProvider(syncFlush: true, LogLevel.Warning);
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
        FileLoggerProvider provider = CreateProvider(syncFlush: true);

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
#if NETFRAMEWORK
        using FileLoggerProvider provider = CreateProvider(syncFlush: false);
#else
        await using FileLoggerProvider provider = CreateProvider(syncFlush: false);
#endif

        await BoundedTaskWait.CompleteWithinAsync(provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory), 10);
        await BoundedTaskWait.CompleteWithinAsync(provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory), 10);

        _fileSystem.Verify(x => x.MoveFile("ccc", Path.Combine(NewDirectory, "ccc")), Times.Exactly(2));
    }

    [TestMethod]
    public async Task LogAsync_AsyncFlush_DuringRelocation_DoesNotWaitForTheRelocationAndIsReplayedOnTheReplacementLogger()
    {
        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The pending queue is open while the provider disposes the old file, before it installs the replacement.
#if NETFRAMEWORK
        _fileStream.Setup(x => x.Dispose()).Callback(() =>
        {
            disposeStarted.SetResult(true);
            releaseDispose.Task.Wait(TestContext.CancellationToken);
        });
#else
        _fileStream.Setup(x => x.DisposeAsync()).Returns(() =>
        {
            disposeStarted.SetResult(true);
            return new ValueTask(releaseDispose.Task);
        });
#endif

        FileLoggerProvider provider = CreateProvider(syncFlush: false);
        try
        {
            var relocation = Task.Run(() => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(NewDirectory), TestContext.CancellationToken);
            Task log = Task.CompletedTask;
            try
            {
                await BoundedTaskWait.CompleteWithinAsync(disposeStarted.Task);
                log = Task.Run(() => provider.LogAsync(LogLevel.Information, "queued", null, Formatter, Category), TestContext.CancellationToken);
                await BoundedTaskWait.CompleteWithinAsync(log);
            }
            finally
            {
                releaseDispose.TrySetResult(true);
                await BoundedTaskWait.CompleteWithinAsync(Task.WhenAll(relocation, log));
            }
        }
        finally
        {
            await DisposeHelper.DisposeAsync(provider);
        }

        Assert.IsEmpty(_stream.ToArray());
        MemoryStream replacement = Assert.ContainsSingle(_laterStreams);
        Assert.AreEqual("0001-01-01T00:00:00.0000000+00:00 Test INFORMATION queued" + Environment.NewLine, Encoding.UTF8.GetString(replacement.ToArray()));
    }

    [TestMethod]
    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync_WhenTheReplacementCannotBeCreated_StopsQueueingLogs()
    {
        FileLoggerProvider provider = CreateProvider(syncFlush: false);
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
        FileLoggerProvider provider = CreateProvider(syncFlush: false);

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

    private FileLoggerProvider CreateProvider(bool syncFlush, LogLevel level = LogLevel.Trace)
        => new(
            new FileLoggerOptions("aaa", "bbb", fileName: "ccc", syncFlush: syncFlush),
            level,
            customDirectory: false,
            _clock.Object,
            new SystemTask(),
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
}

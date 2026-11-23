// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileLoggerDisposalTests : IDisposable
{
    private const string LogFolder = "aaa";
    private const string LogPrefix = "bbb";
    private const string FileName = "ccc";
    private const string Category = "Test";
    private const string Message = "Message";

    private static readonly Func<string, Exception?, string> Formatter = (state, _) => state;

    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IConsole> _console = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFileStream> _fileStream = new();
    private readonly Mock<IFileStreamFactory> _fileStreamFactory = new();
    private readonly DisposeTrackingStream _stream = new();

    public FileLoggerDisposalTests()
    {
        _fileStream.Setup(x => x.Name).Returns(FileName);
        _fileStream.Setup(x => x.Stream).Returns(_stream);
#if NETCOREAPP
        _fileStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif
        _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_fileStream.Object);
    }

    void IDisposable.Dispose() => _stream.Dispose();

    [TestMethod]
    public void Dispose_AsyncFlush_CompletesTheChannelSoTheConsumerEndsAndTheFileIsReleased()
    {
        FileLogger logger = CreateLogger(new SystemTask(), syncFlush: false, flushTimeout: TimeSpan.FromSeconds(30));
        logger.Log(LogLevel.Trace, Message, null, Formatter, Category);

        logger.Dispose();

        Assert.IsTrue(logger.IsFileHandleReleased);
        Assert.AreEqual("0001-01-01T00:00:00.0000000+00:00 Test TRACE Message" + Environment.NewLine, _stream.GetText());
        Assert.IsTrue(_stream.IsDisposed, "Disposing the logger must dispose the writer, which closes the underlying stream.");
        _fileStream.Verify(x => x.Dispose(), Times.Once);
        _console.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void Dispose_WhenFlushTimesOut_WarnsOnceKeepsTheHandleAndIgnoresASecondDispose()
    {
        FileLogger logger = CreateLogger(new NeverCompletingFileLoggerTask(), syncFlush: false, flushTimeout: TimeSpan.FromMilliseconds(50));

        logger.Dispose();
        logger.Dispose();

        Assert.IsFalse(logger.IsFileHandleReleased);
        Assert.IsFalse(_stream.IsDisposed, "The consumer may still own the writer, so it must not be disposed.");
        _fileStream.Verify(x => x.Dispose(), Times.Never);
        _console.Verify(x => x.WriteLine(It.Is<string>(s => s.Contains(FileName) && s.Contains("Failed to flush logs"))), Times.Once);
        _console.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void Dispose_Twice_DisposesTheFileOnlyOnceAndDoesNotThrow()
    {
        FileLogger logger = CreateLogger(new SystemTask(), syncFlush: true);

        logger.Dispose();
        logger.Dispose();

        Assert.IsTrue(logger.IsFileHandleReleased);
        _fileStream.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task LogAsync_AsyncFlush_EnqueuesTheEntryForTheConsumer()
    {
        FileLogger logger = CreateLogger(new SystemTask(), syncFlush: false, flushTimeout: TimeSpan.FromSeconds(30));

        await BoundedTaskWait.CompleteWithinAsync(logger.LogAsync(LogLevel.Information, Message, null, Formatter, Category));
        logger.Dispose();

        Assert.AreEqual("0001-01-01T00:00:00.0000000+00:00 Test INFORMATION Message" + Environment.NewLine, _stream.GetText());
    }

#if NETCOREAPP
    [TestMethod]
    public void Log_AsyncFlush_AfterDispose_ThrowsBecauseTheChannelRejectsTheEntry()
    {
        FileLogger logger = CreateLogger(new SystemTask(), syncFlush: false, flushTimeout: TimeSpan.FromSeconds(30));
        logger.Dispose();

        Assert.ThrowsExactly<InvalidOperationException>(() => logger.Log(LogLevel.Trace, Message, null, Formatter, Category));
    }

    [TestMethod]
    public async Task DisposeAsync_WhenFlushTimesOut_WarnsOnceKeepsTheHandleAndIgnoresASecondDispose()
    {
        FileLogger logger = CreateLogger(new NeverCompletingFileLoggerTask(), syncFlush: false, flushTimeout: TimeSpan.FromMilliseconds(50));

        await logger.DisposeAsync();
        await logger.DisposeAsync();

        Assert.IsFalse(logger.IsFileHandleReleased);
        _fileStream.Verify(x => x.DisposeAsync(), Times.Never);
        _console.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenTheConsumerLoopWasNeverStarted_ThrowsInvalidOperation()
    {
        FileLogger logger = CreateLogger(new NullReturningFileLoggerTask(), syncFlush: false);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await logger.DisposeAsync());
    }
#endif

    [TestMethod]
    public void LogAndDispose_WhenTheConsumerLoopWasNeverStarted_ThrowInvalidOperation()
    {
        FileLogger logger = CreateLogger(new NullReturningFileLoggerTask(), syncFlush: false);

        Assert.ThrowsExactly<InvalidOperationException>(() => logger.Log(LogLevel.Trace, Message, null, Formatter, Category));
        Assert.ThrowsExactly<InvalidOperationException>(logger.Dispose);
    }

    private FileLogger CreateLogger(ITask task, bool syncFlush, TimeSpan? flushTimeout = null)
        => new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: syncFlush),
            LogLevel.Trace,
            _clock.Object,
            task,
            _console.Object,
            _fileSystem.Object,
            _fileStreamFactory.Object,
            flushTimeout);

    private sealed class DisposeTrackingStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        public string GetText() => Encoding.UTF8.GetString(ToArray());

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    // The consumer loop never completes, which deterministically drives the dispose-time flush timeout.
    private sealed class NeverCompletingFileLoggerTask : ITask
    {
        private readonly ITask _inner = new SystemTask();

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => new TaskCompletionSource<bool>().Task;

        public Task Run(Action action) => new TaskCompletionSource<bool>().Task;

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => _inner.Run(function, cancellationToken);

        [UnsupportedOSPlatform("browser")]
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => _inner.RunLongRunning(action, name, cancellationToken);

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }

    // Simulates a task runner that failed to start the consumer loop (no task is handed back).
    private sealed class NullReturningFileLoggerTask : ITask
    {
        private readonly ITask _inner = new SystemTask();

#pragma warning disable VSTHRD114 // Returning null is the point of this fake.
        public Task Run(Func<Task> function, CancellationToken cancellationToken) => null!;

        public Task Run(Action action) => null!;
#pragma warning restore VSTHRD114

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => _inner.Run(function, cancellationToken);

        [UnsupportedOSPlatform("browser")]
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => _inner.RunLongRunning(action, name, cancellationToken);

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }
}

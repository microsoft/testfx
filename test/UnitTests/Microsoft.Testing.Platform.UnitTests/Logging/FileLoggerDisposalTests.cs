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

#if NETFRAMEWORK
    [TestMethod]
    public async Task Dispose_AsyncFlush_WhenThreadPoolIsStarved_FlushesAndReleasesFile()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("file-logger-starvation");

        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = child.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();
        string expectedLog = """
            0001-01-01T00:00:00.0000000+00:00 Test TRACE Message

            """;
        Assert.AreSequenceEqual(
            ["released:True", "log:" + expectedLog.Replace("\r\n", "\n"), "warnings:0"],
            records);
    }

    [TestMethod]
    public async Task Dispose_AsyncFlush_WhenConsoleWriteFails_ReportsFaultWithoutCrashingProcess()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("file-logger-console-failure");

        Assert.AreEqual(0, child.ExitCode, child.Error);
        Assert.AreEqual(
            "PROBE|" + Convert.ToBase64String(Encoding.UTF8.GetBytes("fault:console failure")),
            child.Output.Trim().TrimStart('\uFEFF'));
    }

    internal static void RunConsoleFailureProbe(Action<string> record)
    {
        using var attemptedWarning = new ManualResetEvent(false);
        using var stream = new FailingWriteStream();
        using var fixture = new FileLoggerDisposalTests();
        fixture._fileStream.Setup(x => x.Stream).Returns(stream);
        Thread? warningThread = null;
        fixture._console.Setup(x => x.WriteLine(It.IsAny<string>()))
            .Callback(() =>
            {
                warningThread = Thread.CurrentThread;
                attemptedWarning.Set();
            })
            .Throws(new IOException("console failure"));

        FileLogger logger = fixture.CreateLogger(new SystemTask(), syncFlush: false, flushTimeout: TimeSpan.FromSeconds(30));
        logger.Log(LogLevel.Trace, Message, null, Formatter, Category);
        if (!attemptedWarning.WaitOne(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The child process did not attempt to report the write failure.");
        }

        try
        {
            logger.Dispose();
        }
        catch (AggregateException ex) when (ex.InnerException is IOException { Message: "console failure" })
        {
            if (warningThread is null)
            {
                throw new InvalidOperationException("The warning thread was not recorded.");
            }

            // RunLongRunning can fault its task before an unhandled exception terminates its thread.
            if (!warningThread.IsThreadPoolThread && !warningThread.Join(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("The dedicated consumer thread did not terminate after the fault.");
            }

            record("fault:console failure");
            return;
        }

        throw new InvalidOperationException("The consumer fault was not reported during disposal.");
    }

    internal static void RunThreadPoolStarvationProbe(Action<string> record)
    {
        ThreadPool.GetMinThreads(out int minimumWorkers, out int minimumIoThreads);
        ThreadPool.GetMaxThreads(out _, out int maximumIoThreads);
        int workerCount = Math.Max(minimumWorkers, Environment.ProcessorCount);
        if (!ThreadPool.SetMinThreads(workerCount, minimumIoThreads)
            || !ThreadPool.SetMaxThreads(workerCount, maximumIoThreads))
        {
            throw new InvalidOperationException("Could not constrain the child process thread pool.");
        }

        var started = new CountdownEvent(workerCount);
        var release = new ManualResetEvent(false);
        var blockers = new List<Task>(workerCount);
        FileLoggerDisposalTests? fixture = null;
        Task? consumer = null;
        bool released = false;
        string output = string.Empty;
        int warnings = 0;
        Exception? failure = null;
        Exception? cleanupFailure = null;
        try
        {
            for (int i = 0; i < workerCount; i++)
            {
                blockers.Add(Task.Run(() =>
                {
                    started.Signal();
                    release.WaitOne();
                }));
            }

            if (!started.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("The child process did not occupy every thread-pool worker.");
            }

            ThreadPool.GetAvailableThreads(out int availableWorkers, out _);
            if (availableWorkers != 0)
            {
                throw new InvalidOperationException("The child process thread pool was not saturated.");
            }

            fixture = new FileLoggerDisposalTests();
            fixture._console.Setup(x => x.WriteLine(It.IsAny<string>())).Callback(() => Interlocked.Increment(ref warnings));
            var systemTask = new SystemTask();
            var task = new Mock<ITask>(MockBehavior.Strict);
            task.Setup(x => x.Run(It.IsAny<Action>()))
                .Returns((Action action) => consumer = systemTask.Run(action));
            task.Setup(x => x.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> action, string name, CancellationToken token) => consumer = systemTask.RunLongRunning(action, name, token));

            FileLogger logger = fixture.CreateLogger(task.Object, syncFlush: false, flushTimeout: TimeSpan.FromSeconds(30));
            logger.Log(LogLevel.Trace, Message, null, Formatter, Category);
            logger.Dispose();
            released = logger.IsFileHandleReleased;
            output = fixture._stream.GetText().Replace("\r\n", "\n");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            release.Set();
            if (consumer is not null)
            {
                blockers.Add(consumer);
            }

            var completion = Task.WhenAll(blockers);
            try
            {
                if (!completion.Wait(TimeSpan.FromSeconds(30)))
                {
                    cleanupFailure = new TimeoutException("The child process did not finish its thread-pool work.");
                }
            }
            catch (Exception ex)
            {
                cleanupFailure = ex;
            }

            if (completion.IsCompleted)
            {
                started.Dispose();
                release.Dispose();
                (fixture as IDisposable)?.Dispose();
            }
        }

        if (failure is not null || cleanupFailure is not null)
        {
            throw new AggregateException(new[] { failure, cleanupFailure }.OfType<Exception>());
        }

        record("released:" + released);
        record("log:" + output);
        record("warnings:" + warnings.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("log write failure");
    }
#endif

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
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => new TaskCompletionSource<bool>().Task;

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

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => _inner.Run(function, cancellationToken);

        [UnsupportedOSPlatform("browser")]
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => null!;
#pragma warning restore VSTHRD114

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }
}

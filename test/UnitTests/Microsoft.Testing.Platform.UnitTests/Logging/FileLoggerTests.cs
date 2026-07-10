// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileLoggerTests : IDisposable
{
    // https://github.com/microsoft/testfx/issues/6136
    public TestContext TestContext { get; set; } = null!;

    private const string LogFolder = "aaa";
    private const string LogPrefix = "bbb";
    private const string FileName = "ccc";
    private const string Result1 = "Result1";
    private const string Result2 = "Result2";
    private const string Category = "Test";
    private const string Message = "Message";

    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1}", state, exception is not null ? $" -- {exception}" : string.Empty);

    private readonly Mock<IClock> _mockClock = new();
    private readonly Mock<IConsole> _mockConsole = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<IFileStream> _mockStream = new();
    private readonly Mock<IFileStreamFactory> _mockFileStreamFactory = new();
    private readonly CustomMemoryStream _memoryStream;

    public FileLoggerTests()
    {
        _mockStream.Setup(x => x.Dispose());
#if NETCOREAPP
        _mockStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif

        _mockStream.Setup(x => x.Name).Returns(FileName);
        _memoryStream = new CustomMemoryStream();
        _mockStream.Setup(x => x.Stream).Returns(_memoryStream);
    }

    [TestMethod]
    public void Write_IfMalformedUTF8_ShouldNotCrash()
    {
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var fileStreamFactory = new Mock<IFileStreamFactory>(MockBehavior.Strict);
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        var memoryStream = new MemoryStream();
        fileStream.Setup(f => f.Stream).Returns(memoryStream);
        fileStream.Setup(f => f.Dispose()).Callback(() => { });

        fileStreamFactory
            .Setup(f => f.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object)
            .Callback((string fileName, FileMode _1, FileAccess _2, FileShare _3) => fileStream.Setup(f => f.Name).Returns(fileName));

        using FileLogger fileLogger = new(
            new FileLoggerOptions(nameof(Write_IfMalformedUTF8_ShouldNotCrash), "Test", fileName: null),
            LogLevel.Trace,
            new SystemClock(),
            new SystemTask(),
            new SystemConsole(),
            fileSystem.Object,
            fileStreamFactory.Object);

        fileLogger.Log(LogLevel.Trace, "\uD886", null, LoggingExtensions.Formatter, "Category");

        memoryStream.Seek(0, SeekOrigin.Begin);
        string logWritten = new StreamReader(memoryStream).ReadToEnd();

        // logWritten looks like this: "[15:01:57.130 Category - Trace] �\r\n"
        Assert.StartsWith("[", logWritten);
        Assert.EndsWith($" Category - Trace] \uFFFD{Environment.NewLine}", logWritten);
    }

    [TestMethod]
    public void FileLogger_NullFileSyncFlush_FileStreamCreated()
    {
        // First return is to compute the expected file name. It's ok that first time is greater
        // than all following ones.
        _mockClock.SetupSequence(x => x.UtcNow)
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 14)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 16)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));

        string expectedFileName = $"{LogPrefix}_{_mockClock.Object.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture)}.diag";
        _mockStream.Setup(x => x.Name).Returns(expectedFileName);
        _mockFileStreamFactory
            .SetupSequence(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Throws<IOException>()
            .Returns(_mockStream.Object);

        string fileLoggerName;
        using (FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object))
        {
            fileLoggerName = fileLogger.FileName;
        }

        _mockFileStreamFactory.Verify(
            x => x.Create(Path.Combine(LogFolder, expectedFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Times.Once);
        Assert.AreEqual(expectedFileName, fileLoggerName);
    }

    [TestMethod]
    public void FileLogger_NullFileSyncFlush_FileStreamCreationThrows()
    {
        // First return is to compute the expected file name. It's ok that first time is greater
        // than all following ones.
        _mockClock
            .SetupSequence(x => x.UtcNow)
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));
        _mockFileStreamFactory
            .SetupSequence(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Throws<IOException>()
            .Returns(_mockStream.Object);

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = new FileLogger(
            new(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object));
    }

    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [TestMethod]
    public void FileLogger_ValidFileName_FileStreamCreatedSuccessfully(bool syncFlush, bool fileExists)
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockFileSystem.Setup(x => x.ExistFile(expectedPath)).Returns(fileExists);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), fileExists ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        string fileLoggerName;
        using (FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: syncFlush),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object))
        {
            fileLoggerName = fileLogger.FileName;
        }

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, fileExists ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerName);
    }

    [TestMethod]
    [DynamicData(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public async Task Log_WhenSyncFlush_StreamWriterIsCalledOnlyWhenLogLevelAllowsIt(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        using FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: true),
            defaultLogLevel,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);
        fileLogger.Log(currentLogLevel, Message, null, Formatter, Category);

        if (LogTestHelpers.IsLogEnabled(defaultLogLevel, currentLogLevel))
        {
            await _memoryStream.FlushAsync(TestContext.CancellationToken);
            int iteration = 0;
            while (_memoryStream.Length == 0 && iteration < 10)
            {
                iteration++;
                await Task.Delay(200, TestContext.CancellationToken);
            }

            await _memoryStream.FlushAsync(TestContext.CancellationToken);

            _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
            Assert.AreEqual($"[00:00:00.000 Test - {currentLogLevel}] Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
        }
        else
        {
            Assert.AreEqual(0, _memoryStream.Length);
        }
    }

    [TestMethod]
    [DynamicData(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public void Log_WhenAsyncFlush_StreamWriterIsCalledOnlyWhenLogLevelAllowsIt(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        // Ensures that the async flush is completed before the file is read
        using (FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            defaultLogLevel,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object))
        {
            fileLogger.Log(currentLogLevel, Message, null, Formatter, Category);
        }

        if (LogTestHelpers.IsLogEnabled(defaultLogLevel, currentLogLevel))
        {
            Assert.AreEqual($"0001-01-01T00:00:00.0000000+00:00 Test {currentLogLevel.ToString().ToUpperInvariant()} Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
        }
        else
        {
            Assert.AreEqual(0, _memoryStream.Length);
        }
    }

    // Chaos test for https://github.com/dotnet/sdk/issues/55215.
    // Stresses the async-flush path: many threads log concurrently and then the logger is disposed while messages are
    // still queued. Repeated across many iterations to shake out races in consumer-loop startup and shutdown draining.
    // The logger must never crash, must drain the whole queue on Dispose, and must not lose or corrupt any message.
    [TestMethod]
    public void Log_WhenAsyncFlush_ConcurrentLoggingIsDrainedOnDisposeWithoutLoss()
    {
        const int iterations = 50;
        const int producerCount = 8;
        const int messagesPerProducer = 50;

        var clock = new Mock<IClock>();
        clock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2023, 5, 29, 3, 42, 13, TimeSpan.Zero));

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            using var memoryStream = new CustomMemoryStream();

            var mockStream = new Mock<IFileStream>();
            mockStream.Setup(x => x.Stream).Returns(memoryStream);
            mockStream.Setup(x => x.Name).Returns(FileName);
            mockStream.Setup(x => x.Dispose());
#if NETCOREAPP
            mockStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

            var mockFileStreamFactory = new Mock<IFileStreamFactory>();
            mockFileStreamFactory
                .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                .Returns(mockStream.Object);

            var fileLogger = new FileLogger(
                new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
                LogLevel.Trace,
                clock.Object,
                new SystemTask(),
                _mockConsole.Object,
                mockFileSystem.Object,
                mockFileStreamFactory.Object);

            // Release all producers at the same time to maximize contention right after construction.
            using var startGate = new ManualResetEventSlim(false);
            var producers = new Task[producerCount];
            for (int producer = 0; producer < producerCount; producer++)
            {
                int producerId = producer;
                producers[producer] = Task.Run(
                    () =>
                    {
#pragma warning disable CA1416 // ManualResetEventSlim.Wait is unsupported on 'browser' — this test never targets browser
                        startGate.Wait(TestContext.CancellationToken);
#pragma warning restore CA1416
                        for (int message = 0; message < messagesPerProducer; message++)
                        {
                            fileLogger.Log(LogLevel.Trace, $"P{producerId}M{message}", null, Formatter, Category);
                        }
                    },
                    TestContext.CancellationToken);
            }

            startGate.Set();
#pragma warning disable CA1416 // Task.WaitAll is unsupported on 'browser' — this test never targets browser
            Task.WaitAll(producers, TestContext.CancellationToken);
#pragma warning restore CA1416

            // Dispose must drain everything still sitting in the queue without crashing.
            fileLogger.Dispose();

            string content = Encoding.UTF8.GetString(memoryStream.ToArray());
            string[] lines = content.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            Assert.HasCount(producerCount * messagesPerProducer, lines, $"Iteration {iteration}: every queued message must be flushed exactly once.");

            // Compare against the exact set of expected messages. Each log line ends with the message payload
            // (the log format is "<timestamp> <category> <level> <message>"), so we extract the last token and
            // require an exact set match. This detects loss/duplication even for prefix-overlapping IDs such as
            // "P0M1" vs "P0M10", which a substring check would miss.
            var actualMessages = new HashSet<string>(lines.Select(line => line[(line.LastIndexOf(' ') + 1)..]));
            for (int producer = 0; producer < producerCount; producer++)
            {
                for (int message = 0; message < messagesPerProducer; message++)
                {
                    Assert.Contains($"P{producer}M{message}", actualMessages, $"Iteration {iteration}: message P{producer}M{message} was lost or corrupted.");
                }
            }

            Assert.HasCount(producerCount * messagesPerProducer, actualMessages, $"Iteration {iteration}: no duplicate or unexpected messages must be written.");
        }
    }

    // Deterministic guard for the fix in https://github.com/dotnet/sdk/issues/55215.
    // On .NET Framework (the netstandard2.0 build) the consumer loop MUST be started with the synchronous
    // ITask.Run(Action) overload so that Dispose()'s blocking Wait() can never be starved by the thread pool.
    // The previous implementation used the asynchronous ITask.Run(Func<Task>, ...) overload, which is exactly the
    // regression this test locks down.
    [TestMethod]
    public void FileLogger_WhenAsyncFlush_StartsConsumerLoopWithExpectedTaskOverload()
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        var recordingTask = new RecordingTask();
        using (FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Trace,
            _mockClock.Object,
            recordingTask,
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object))
        {
        }

#if NETCOREAPP
        Assert.IsTrue(recordingTask.StartedAsynchronousLoop, "netcore must run the awaited async consumer loop.");
        Assert.IsFalse(recordingTask.StartedSynchronousLoop);
#else
        Assert.IsTrue(recordingTask.StartedSynchronousLoop, "netstandard must run a fully synchronous consumer loop that cannot be thread-pool starved during Dispose.");
        Assert.IsFalse(recordingTask.StartedAsynchronousLoop);
#endif
    }

    [TestMethod]
    public void FileLogger_AfterSuccessfulDispose_ReportsFileHandleReleased()
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);

        fileLogger.Log(LogLevel.Trace, Message, null, Formatter, Category);
        fileLogger.Dispose();

        Assert.IsTrue(fileLogger.IsFileHandleReleased);
    }

    // Deterministic non-fatal-timeout test: the consumer loop never completes (simulating a hung flush), and a short
    // injected flush timeout forces the timeout branch. Dispose must NOT throw, must warn, and must report that the
    // file handle was not released so callers (e.g. FileLoggerProvider) can skip the file move.
    [TestMethod]
#if NETCOREAPP
    public async Task FileLogger_WhenFlushTimesOut_IsNonFatalAndReportsHandleNotReleased()
#else
    public void FileLogger_WhenFlushTimesOut_IsNonFatalAndReportsHandleNotReleased()
#endif
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Trace,
            _mockClock.Object,
            new NeverCompletingTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            flushTimeout: TimeSpan.FromMilliseconds(50));

        fileLogger.Log(LogLevel.Trace, Message, null, Formatter, Category);

        // Must not throw even though the consumer loop never drains.
#if NETCOREAPP
        await fileLogger.DisposeAsync();
#else
        fileLogger.Dispose();
#endif

        Assert.IsFalse(fileLogger.IsFileHandleReleased, "A flush timeout must leave the file handle owned by the still-running consumer.");
        _mockConsole.Verify(x => x.WriteLine(It.Is<string>(s => s.Contains("Failed to flush logs"))), Times.Once);
    }

    void IDisposable.Dispose()
        => _memoryStream.Dispose();

    // ITask that records which overload was used to start the file-logger consumer loop, delegating the actual work
    // to the real SystemTask.
    private sealed class RecordingTask : ITask
    {
        private readonly ITask _inner = new SystemTask();

        public bool StartedSynchronousLoop { get; private set; }

        public bool StartedAsynchronousLoop { get; private set; }

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
        {
            StartedAsynchronousLoop = true;
            return _inner.Run(function, cancellationToken);
        }

        public Task Run(Action action)
        {
            StartedSynchronousLoop = true;
            return _inner.Run(action);
        }

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken)
            => _inner.Run(function, cancellationToken);

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
            => _inner.RunLongRunning(action, name, cancellationToken);

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }

    // ITask whose loop-starting overloads return a task that never completes, simulating a hung consumer so the
    // dispose-time flush timeout is deterministically triggered without running any real loop.
    private sealed class NeverCompletingTask : ITask
    {
        private readonly ITask _inner = new SystemTask();

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
            => new TaskCompletionSource<bool>().Task;

        public Task Run(Action action)
            => new TaskCompletionSource<bool>().Task;

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken)
            => _inner.Run(function, cancellationToken);

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
            => _inner.RunLongRunning(action, name, cancellationToken);

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }

    private sealed class CustomMemoryStream : MemoryStream
    {
        private bool _shouldDispose;

        [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "Don't dispose")]
        protected override void Dispose(bool disposing)
        {
            if (_shouldDispose)
            {
                base.Dispose(disposing);
            }
            else
            {
                _shouldDispose = true;
            }
        }
    }
}

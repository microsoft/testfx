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

    // Moq returns default(DateTimeOffset) from IClock.UtcNow, which the "O" format renders with an explicit +00:00
    // offset, so this is stable across machines, time zones and cultures.
    private const string DefaultClockTimestamp = "0001-01-01T00:00:00.0000000+00:00";

    // The sync-flush path renders only the time part, with the "HH:mm:ss.fff" format.
    private const string SyncFlushDefaultClockTimestamp = "00:00:00.000";

    // Mirrors the private FileLogger.MaxLogFileCreationAttempts. If the production constant changes, the assertions
    // below fail loudly, which is the intended way to notice that the retry budget was deliberately retuned.
    private const int MaxLogFileCreationAttempts = 10;

    // Retry candidates embed the process id so concurrent processes cannot walk the same sequence of candidates.
    // The tests run in-process, so the very same value is expected in the generated names.
    private static readonly int CurrentProcessId = new SystemEnvironment().ProcessId;

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

        // logWritten looks like this: "[15:01:57.130 Category - TRACE] �\r\n"
        Assert.StartsWith("[", logWritten);
        Assert.EndsWith($" Category - TRACE] \uFFFD{Environment.NewLine}", logWritten);
    }

    [TestMethod]
    public void FileLogger_NullFileSyncFlush_FileStreamCreated()
    {
        _mockClock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));

        // The first candidate is taken (e.g. by another process that computed the same timestamp), so the logger
        // must fall back to a discriminated name instead of retrying the very same one until the clock ticks.
        var attemptedPaths = new List<string>();
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Callback((string path, FileMode _1, FileAccess _2, FileShare _3) =>
            {
                attemptedPaths.Add(path);
                _mockStream.Setup(x => x.Name).Returns(Path.GetFileName(path));
            })
            .Returns(() => attemptedPaths.Count == 1 ? throw new IOException() : _mockStream.Object);

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

        Assert.HasCount(2, attemptedPaths);
        Assert.AreEqual(Path.Combine(LogFolder, $"{LogPrefix}_230529034217000.diag"), attemptedPaths[0]);
        Assert.StartsWith(Path.Combine(LogFolder, $"{LogPrefix}_230529034217000_{CurrentProcessId}_"), attemptedPaths[1]);
        Assert.EndsWith(".diag", attemptedPaths[1]);
        Assert.AreEqual(Path.GetFileName(attemptedPaths[1]), fileLoggerName);
    }

    // Every attempt uses a name that is unique by construction, so the loop is bounded by attempt count and can never
    // spin on a stalled or forward-jumping wall clock.
    [TestMethod]
    public void FileLogger_NullFileSyncFlush_EveryAttemptUsesADistinctFileName()
    {
        List<string> attemptedPaths = CaptureAllAttemptedPaths();

        Assert.HasCount(MaxLogFileCreationAttempts, attemptedPaths);
        Assert.HasCount(MaxLogFileCreationAttempts, attemptedPaths.Distinct().ToList());
        Assert.AreEqual(Path.Combine(LogFolder, $"{LogPrefix}_230529034217000.diag"), attemptedPaths[0]);
        foreach (string retryPath in attemptedPaths.Skip(1))
        {
            Assert.StartsWith(Path.Combine(LogFolder, $"{LogPrefix}_230529034217000_{CurrentProcessId}_"), retryPath);
        }
    }

    // Nothing enforces a single FileLogger per process, so two loggers created in the same clock tick must not walk
    // the same ladder of candidate names and race each other through it.
    [TestMethod]
    public void FileLogger_NullFileSyncFlush_ConcurrentLoggersInTheSameTickNeverShareACandidateName()
    {
        List<string> firstLoggerPaths = CaptureAllAttemptedPaths();
        List<string> secondLoggerPaths = CaptureAllAttemptedPaths();

        // Only the very first candidate is shared: that is the one collision the retry loop exists to resolve.
        List<string> shared = [.. firstLoggerPaths.Intersect(secondLoggerPaths)];
        Assert.HasCount(1, shared);
        Assert.AreEqual(Path.Combine(LogFolder, $"{LogPrefix}_230529034217000.diag"), shared[0]);
    }

    // Drives one FileLogger construction in which every attempt fails, returning the full ladder of candidate paths
    // it walked. Uses a dedicated mock so it can be called twice within a single test.
    private static List<string> CaptureAllAttemptedPaths()
    {
        var clock = new Mock<IClock>();
        clock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));

        var attemptedPaths = new List<string>();
        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Callback((string path, FileMode _1, FileAccess _2, FileShare _3) => attemptedPaths.Add(path))
            .Throws(new IOException("There is not enough space on the disk."));

        Assert.ThrowsExactly<IOException>(() => _ = new FileLogger(
            new(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            clock.Object,
            new SystemTask(),
            new Mock<IConsole>().Object,
            new Mock<IFileSystem>().Object,
            fileStreamFactory.Object));

        return attemptedPaths;
    }

    // The retry loop only recovers from a name collision. Any other IOException (disk full, deleted log folder, path
    // too long, ...) fails identically on every attempt, and the original code swallowed it and reported a generic
    // 'cannot create a unique log file', which hid the real cause.
    [TestMethod]
    public void FileLogger_NullFileSyncFlush_WhenEveryAttemptFails_ReportsAndChainsTheLastFailure()
    {
        _mockClock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));

        var diskFull = new IOException("There is not enough space on the disk.");
        var attemptedPaths = new List<string>();
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Callback((string path, FileMode _1, FileAccess _2, FileShare _3) => attemptedPaths.Add(path))
            .Throws(diskFull);

        IOException exception = Assert.ThrowsExactly<IOException>(() => _ = new FileLogger(
            new(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object));

        Assert.AreSame(diskFull, exception.InnerException);

        // The rendered message must carry the underlying failure and the path we failed on: string.Format silently
        // ignores surplus arguments, so dropping a placeholder from the resource string would go unnoticed otherwise.
        Assert.Contains(diskFull.Message, exception.Message);
        Assert.Contains(attemptedPaths[^1], exception.Message);
        Assert.Contains(MaxLogFileCreationAttempts.ToString(CultureInfo.InvariantCulture), exception.Message);
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
            Assert.AreEqual($"[00:00:00.000 Test - {currentLogLevel.ToString().ToUpperInvariant()}] Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
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
            Assert.AreEqual($"{DefaultClockTimestamp} Test {currentLogLevel.ToString().ToUpperInvariant()} Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
        }
        else
        {
            Assert.AreEqual(0, _memoryStream.Length);
        }
    }

    // Explicit expected names, deliberately not derived from logLevel.ToString().ToUpperInvariant(): a level missing
    // from FileLogger's switch silently falls back to that very expression, so an oracle built from it could never
    // fail. ExpectedUpperCaseNames_CoverEveryLogLevel keeps this table exhaustive.
    private static readonly Dictionary<LogLevel, string> ExpectedUpperCaseNames = new()
    {
        [LogLevel.Trace] = "TRACE",
        [LogLevel.Debug] = "DEBUG",
        [LogLevel.Information] = "INFORMATION",
        [LogLevel.Warning] = "WARNING",
        [LogLevel.Error] = "ERROR",
        [LogLevel.Critical] = "CRITICAL",
        [LogLevel.None] = "NONE",
    };

    [TestMethod]
    [DynamicData(nameof(GetExpectedUpperCaseNames))]
    public void Log_WhenAsyncFlush_LogLevelIsWrittenInUpperCase(LogLevel currentLogLevel, string expectedLevelName)
    {
        LogSingleEntryWithAsyncFlush(currentLogLevel);

        _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
        Assert.AreEqual(
            $"{DefaultClockTimestamp} {Category} {expectedLevelName} {Message}{Environment.NewLine}",
            Encoding.Default.GetString(_memoryStream.ToArray()));
    }

    // Comparing the keys, rather than counting them, also catches a duplicated entry in the table above silently
    // overwriting another level. Both sides are ordered because Dictionary key order is an implementation detail.
    [TestMethod]
    public void ExpectedUpperCaseNames_CoverEveryLogLevel()
        => Assert.AreSequenceEqual(
            LogTestHelpers.GetLogLevels().OrderBy(logLevel => logLevel),
            ExpectedUpperCaseNames.Keys.OrderBy(logLevel => logLevel));

    // LogLevel is a public enum and IsEnabled is a plain '>=' comparison, so a value outside the enum reaches
    // BuildLogEntry from user code. It must not throw; it renders as the numeric value, like ToString() always did.
    [TestMethod]
    public void Log_WhenAsyncFlush_UndefinedLogLevelIsWrittenAsItsNumericValue()
    {
        LogSingleEntryWithAsyncFlush((LogLevel)999);

        _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
        Assert.AreEqual(
            $"{DefaultClockTimestamp} {Category} 999 {Message}{Environment.NewLine}",
            Encoding.Default.GetString(_memoryStream.ToArray()));
    }

    public static IEnumerable<object[]> GetExpectedUpperCaseNames()
        => ExpectedUpperCaseNames.Select(pair => new object[] { pair.Key, pair.Value });

    // The sync-flush path formats its own log entry instead of going through BuildLogEntry, so it needs its own
    // coverage: it must render the same upper-case level names as the async path.
    [TestMethod]
    [DynamicData(nameof(GetExpectedUpperCaseNames))]
    public void Log_WhenSyncFlush_LogLevelIsWrittenInUpperCase(LogLevel currentLogLevel, string expectedLevelName)
    {
        LogSingleEntryWithSyncFlush(currentLogLevel);

        _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
        Assert.AreEqual(
            $"[{SyncFlushDefaultClockTimestamp} {Category} - {expectedLevelName}] {Message}{Environment.NewLine}",
            Encoding.Default.GetString(_memoryStream.ToArray()));
    }

    [TestMethod]
    public void Log_WhenSyncFlush_UndefinedLogLevelIsWrittenAsItsNumericValue()
    {
        LogSingleEntryWithSyncFlush((LogLevel)999);

        _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Never);
        Assert.AreEqual(
            $"[{SyncFlushDefaultClockTimestamp} {Category} - 999] {Message}{Environment.NewLine}",
            Encoding.Default.GetString(_memoryStream.ToArray()));
    }

    private void LogSingleEntryWithSyncFlush(LogLevel logLevel)
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        // The sync-flush path writes through an auto-flushing StreamWriter, so the stream is complete once Log returns.
        using FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);
        fileLogger.Log(logLevel, Message, null, Formatter, Category);
    }

    private void LogSingleEntryWithAsyncFlush(LogLevel logLevel)
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        // Disposing the logger drains the channel, so the stream is complete once this method returns.
        using FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);
        fileLogger.Log(logLevel, Message, null, Formatter, Category);
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

        // The warning must identify the affected log file (by its full path) and make clear the file may be
        // incomplete, so a user investigating a hang can find and interpret the right file.
        _mockConsole.Verify(
            x => x.WriteLine(It.Is<string>(s => s.Contains("Failed to flush logs") && s.Contains(FileName) && s.Contains("may be incomplete"))),
            Times.Once);
    }

    // Deterministic guard: an exception thrown from inside the consumer write loop must be reported with the log
    // file's identity and the full exception detail (not just Message), and must never recurse back into the
    // FileLogger's own (now-faulted) sink -- it goes to the injected IConsole instead.
    [TestMethod]
    public async Task FileLogger_WhenWriteLoopThrows_ReportsFileNameAndFullExceptionDetailToConsole()
    {
        _mockFileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        var consoleMessage = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockConsole.Setup(x => x.WriteLine(It.IsAny<string>())).Callback<string>(message => consoleMessage.TrySetResult(message));

        // Make the underlying stream throw once the consumer loop tries to write to it, simulating a real I/O
        // failure (e.g. disk full / access denied) surfacing from inside the write loop.
        _mockStream.Setup(x => x.Stream).Returns(new ThrowingStream());

        FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);

        fileLogger.Log(LogLevel.Trace, Message, null, Formatter, Category);

        Task completedTask = await Task.WhenAny(consoleMessage.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.AreSame(consoleMessage.Task, completedTask, "The write-loop failure must be reported before the timeout.");
        string message = await consoleMessage.Task;
        _mockConsole.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Once);
        Assert.Contains(FileName, message, $"Expected the log file name '{FileName}' in: {message}");
        Assert.Contains("may be incomplete", message, $"Expected an 'incomplete' warning in: {message}");
        Assert.Contains(nameof(NotSupportedException), message, $"Expected the full exception type/detail (not just Message) in: {message}");

#if NETCOREAPP
        await fileLogger.DisposeAsync();
#else
        fileLogger.Dispose();
#endif
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

        [UnsupportedOSPlatform("browser")]
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

        [UnsupportedOSPlatform("browser")]
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

    // Stream whose Write/WriteAsync always throws, used to deterministically fault the FileLogger's consumer write
    // loop and verify the resulting console warning identifies the log file and preserves full exception detail.
    private sealed class ThrowingStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Simulated I/O failure.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotSupportedException("Simulated I/O failure.");

#if NETCOREAPP
        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new NotSupportedException("Simulated I/O failure.");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Simulated I/O failure.");
#endif
    }
}

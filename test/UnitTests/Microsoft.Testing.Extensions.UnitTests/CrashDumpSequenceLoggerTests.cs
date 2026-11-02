// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpSequenceLoggerTests
{
    private readonly Mock<IEnvironment> _mockEnvironment = new();
    private readonly Mock<IClock> _mockClock = new();
    private readonly Mock<ILogger> _mockLogger = new();
    private readonly Mock<ILoggerFactory> _mockLoggerFactory = new();
    private readonly Mock<IOutputDevice> _mockOutputDevice = new();

    public TestContext TestContext { get; set; } = null!;

    public CrashDumpSequenceLoggerTests()
    {
        // The ILoggerFactory mock must be configured before CrashDumpSequenceLogger is constructed:
        // ILoggerFactory.CreateLogger<T>() eagerly resolves and caches the underlying ILogger inside
        // the Logger<T> wrapper's constructor.
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _mockClock.Setup(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        _mockOutputDevice
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private CrashDumpSequenceLogger CreateLogger()
        => new(_mockEnvironment.Object, _mockClock.Object, _mockLoggerFactory.Object, _mockOutputDevice.Object);

    [TestMethod]
    public async Task OnTestSessionStartingAsync_WithoutEnablement_ThrowsInvariantViolation()
    {
        CrashDumpSequenceLogger logger = CreateLogger();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None)));

        Assert.Contains("Unexpected state", exception.Message);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_CreatesNestedDirectoryAndWritesHeader()
    {
        string root = Path.Combine(Path.GetTempPath(), "crash-sequence-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "nested", "sequence.log");
        _mockEnvironment
            .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
            .Returns(path);
        CrashDumpSequenceLogger logger = CreateLogger();

        try
        {
            Assert.IsTrue(await logger.IsEnabledAsync());
            await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

            var writer = (StreamWriter)(typeof(CrashDumpSequenceLogger)
                .GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(logger)!);
            Assert.IsTrue(writer.AutoFlush);
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            Assert.AreSequenceEqual(
                ["# MTP CrashDump test sequence v1 (format: <event>\\t<isoTimestamp>\\t<uid>\\t<displayName-or-state>)"],
                File.ReadAllLines(path));
        }
        finally
        {
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_StartedRecordIsImmediatelyVisibleWithRoundtripTimestamp()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        DateTimeOffset timestamp = new DateTimeOffset(2026, 10, 3, 19, 52, 40, 321, TimeSpan.Zero).AddTicks(9876);
        _mockClock.Setup(x => x.UtcNow).Returns(timestamp);
        _mockEnvironment
            .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
            .Returns(path);
        CrashDumpSequenceLogger logger = CreateLogger();

        try
        {
            Assert.IsTrue(await logger.IsEnabledAsync());
            await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
            await logger.ConsumeAsync(
                null!,
                CreateUpdate(InProgressTestNodeStateProperty.CachedInstance, "started-uid", "Started Test"),
                CancellationToken.None);

#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            string[] lines = File.ReadAllLines(path);
            Assert.HasCount(2, lines);
            Assert.AreEqual(
                $"STARTED\t{timestamp:O}\tstarted-uid\tStarted Test",
                lines[1]);
        }
        finally
        {
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_EachTerminalStateWritesItsSpecificState()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var timestamp = new DateTimeOffset(2026, 10, 3, 19, 52, 40, TimeSpan.Zero);
        _mockClock.Setup(x => x.UtcNow).Returns(timestamp);
        _mockEnvironment
            .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
            .Returns(path);
        CrashDumpSequenceLogger logger = CreateLogger();

        try
        {
            Assert.IsTrue(await logger.IsEnabledAsync());
            await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
#pragma warning disable CS0618, MTP0001 // Cover the same terminal-state set as the production sequence logger.
            (IProperty Property, string ExpectedState)[] terminalStates =
            [
                (PassedTestNodeStateProperty.CachedInstance, "Passed"),
                (new FailedTestNodeStateProperty(), "Failed"),
                (new ErrorTestNodeStateProperty(), "Error"),
                (SkippedTestNodeStateProperty.CachedInstance, "Skipped"),
                (new CancelledTestNodeStateProperty(), "Cancelled"),
                (new TimeoutTestNodeStateProperty(), "Timeout"),
            ];
#pragma warning restore CS0618, MTP0001

            for (int i = 0; i < terminalStates.Length; i++)
            {
                await logger.ConsumeAsync(
                    null!,
                    CreateUpdate(terminalStates[i].Property, $"uid-{i}", $"Test {i}"),
                    CancellationToken.None);
            }

#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            string[] lines = File.ReadAllLines(path);
            Assert.HasCount(terminalStates.Length + 1, lines);
            for (int i = 0; i < terminalStates.Length; i++)
            {
                Assert.AreEqual(
                    $"ENDED\t{timestamp:O}\tuid-{i}\t{terminalStates[i].ExpectedState}",
                    lines[i + 1]);
            }
        }
        finally
        {
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            File.Delete(path);
        }
    }

    [TestMethod]
    public void FormatLine_UsesRoundtripTimestampAndSanitizesUserFields()
    {
        DateTimeOffset timestamp = new DateTimeOffset(2026, 10, 3, 19, 52, 40, 321, TimeSpan.FromHours(2)).AddTicks(9876);

        string line = CrashDumpSequenceLogger.FormatLine("EVENT", timestamp, new TestNodeUid("uid\tone"), "line\r\nvalue");

        Assert.AreEqual($"EVENT\t{timestamp:O}\tuid one\tline  value", line);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_ReleasesQueuedWaiterAndDisposesSemaphore()
    {
        CrashDumpSequenceLogger logger = CreateLogger();
        var semaphore = (SemaphoreSlim)(typeof(CrashDumpSequenceLogger)
            .GetField("_writeSemaphore", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(logger)!);
        semaphore.Wait(TestContext.CancellationToken);
        ValueTask disposal = logger.DisposeAsync();
        Task queuedWait = semaphore.WaitAsync(TestContext.CancellationToken);

        semaphore.Release();
        await disposal;

        await queuedWait.WaitAsync(TestContext.CancellationToken);
        Assert.IsTrue(queuedWait.IsCompletedSuccessfully);
        Assert.ThrowsExactly<ObjectDisposedException>(() => semaphore.Wait(0, TestContext.CancellationToken));
    }
#endif

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OnTestSessionStartingAsync_WhenFileCannotBeOpened_ReportsPathAndFullExceptionViaOutputDevice()
    {
        // File locking semantics differ across platforms; FileShare.None is only honored on Windows
        // in a way that reliably triggers IOException when CrashDumpSequenceLogger opens the file.
        string path = Path.GetTempFileName();
        try
        {
            _mockEnvironment
                .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
                .Returns(path);

            CrashDumpSequenceLogger logger = CreateLogger();
            Assert.IsTrue(await logger.IsEnabledAsync());

            // Hold the file open exclusively so the FileStream opened inside OnTestSessionStartingAsync fails with IOException.
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
            }

            _mockOutputDevice.Verify(
                x => x.DisplayAsync(
                    logger,
                    It.Is<IOutputDeviceData>(data => IsWarningAbout(data, path)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OnTestSessionStartingAsync_WhenWarningDisplayFails_DoesNotFailSessionStart()
    {
        string path = Path.GetTempFileName();
        try
        {
            _mockEnvironment
                .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
                .Returns(path);
            _mockOutputDevice
                .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Output transport unavailable."));
            _mockLogger
                .Setup(x => x.LogAsync(
                    LogLevel.Warning,
                    It.IsAny<string>(),
                    null,
                    It.IsAny<Func<string, Exception?, string>>()))
                .ThrowsAsync(new IOException("Logging sink unavailable."));

            CrashDumpSequenceLogger logger = CreateLogger();
            Assert.IsTrue(await logger.IsEnabledAsync());

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
            }

            _mockLogger.Verify(
                x => x.LogAsync(
                    LogLevel.Warning,
                    It.Is<string>(message =>
                        message.Contains(path)
                        && message.Contains(nameof(IOException))
                        && message.Contains(nameof(InvalidOperationException))),
                    null,
                    It.IsAny<Func<string, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_WhenWriteFails_LogsWarningWithPathAndFullExceptionDetail()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _mockEnvironment
            .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
            .Returns(path);

        CrashDumpSequenceLogger logger = CreateLogger();
        try
        {
            Assert.IsTrue(await logger.IsEnabledAsync());

            // Open the sequence file for real so the happy path (header write) is exercised too, then
            // reflectively swap the private writer for one backed by a stream that always throws
            // IOException on write. There is no dependency-injected file-system abstraction to fake a
            // write failure through the public surface, so reflection is the only practical way to
            // deterministically reach ConsumeAsync's IOException branch without relying on flaky,
            // environment-specific tricks (e.g. filling up a disk).
            await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
            FieldInfo writerField = typeof(CrashDumpSequenceLogger).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not resolve CrashDumpSequenceLogger._writer via reflection.");
#if NETCOREAPP
            if (writerField.GetValue(logger) is StreamWriter existingWriter)
            {
                await existingWriter.DisposeAsync();
            }
#else
            ((StreamWriter?)writerField.GetValue(logger))?.Dispose();
#endif
            writerField.SetValue(logger, new StreamWriter(new ThrowingStream()) { AutoFlush = true });

            TestNode testNode = new()
            {
                Uid = "uid1",
                DisplayName = "Test1",
                Properties = new PropertyBag(InProgressTestNodeStateProperty.CachedInstance),
            };
            var update = new TestNodeUpdateMessage(new SessionUid("session"), testNode);

            await logger.ConsumeAsync(null!, update, CancellationToken.None);

            _mockLogger.Verify(
                x => x.LogAsync(
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains(path) && message.Contains(nameof(IOException)) && message.Contains(nameof(ThrowingStream))),
                    null,
                    It.IsAny<Func<string, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Bypass the (now-throwing) writer during disposal by clearing the field directly.
            typeof(CrashDumpSequenceLogger).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(logger, null);
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_ExecutionCompleted_WritesEndedRecord()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _mockEnvironment
            .Setup(x => x.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName))
            .Returns(path);
        CrashDumpSequenceLogger logger = CreateLogger();

        try
        {
            Assert.IsTrue(await logger.IsEnabledAsync());
            await logger.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
            await logger.ConsumeAsync(null!, CreateUpdate(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
            await logger.ConsumeAsync(null!, CreateUpdate(TestNodeExecutionCompletedProperty.CachedInstance), CancellationToken.None);
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif

            string[] lines = File.ReadAllLines(path);
            Assert.Contains(
                line => line.StartsWith($"{CrashDumpSequenceLogger.EndedEvent}\t", StringComparison.Ordinal)
                    && line.EndsWith("\tuid\tCompleted", StringComparison.Ordinal),
                lines);
        }
        finally
        {
#if NETCOREAPP
            await logger.DisposeAsync();
#else
            logger.Dispose();
#endif
            File.Delete(path);
        }
    }

    private static TestNodeUpdateMessage CreateUpdate(IProperty property, string uid = "uid", string displayName = "DroppedTest")
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = displayName,
                Properties = new PropertyBag(property),
            });

    private static bool IsWarningAbout(IOutputDeviceData data, string path)
        => data is WarningMessageOutputDeviceData warning
        && warning.Message.Contains(path)
        && warning.Message.Contains(nameof(IOException))
        && warning.Message.Contains(nameof(CrashDumpSequenceLogger.OnTestSessionStartingAsync));

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException($"Cannot set stream position to {value}.");
        }

        // No-op: constructing the StreamWriter with AutoFlush = true triggers an immediate Flush()
        // even with an empty buffer, which would otherwise fail before ConsumeAsync ever calls Write.
        // Only Write() needs to throw to simulate the write failure ConsumeAsync's IOException catch
        // is meant to handle.
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Simulated write failure.");
    }
}

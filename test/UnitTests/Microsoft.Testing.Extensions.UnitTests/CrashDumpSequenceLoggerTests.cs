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
                    It.Is<string>(message => message.Contains(path) && message.Contains(nameof(InvalidOperationException))),
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
                    It.Is<string>(message => message.Contains(path) && message.Contains(nameof(IOException)) && message.Contains("   at ")),
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

    private static bool IsWarningAbout(IOutputDeviceData data, string path)
        => data is WarningMessageOutputDeviceData warning
        && warning.Message.Contains(path)
        && warning.Message.Contains(nameof(IOException))
        && warning.Message.Contains("   at ");

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

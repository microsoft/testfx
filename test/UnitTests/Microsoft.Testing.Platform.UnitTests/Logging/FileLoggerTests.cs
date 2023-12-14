// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

#if NETCOREAPP
using System.Threading.Channels;
#endif

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class FileLoggerTests : TestBase
{
    private const string LogFolder = "aaa";
    private const string LogPrefix = "bbb";
    private const string FileName = "ccc";

    private readonly Mock<IClock> _mockClock = new();
    private readonly Mock<ITask> _mockTask = new();
    private readonly Mock<IConsole> _mockConsole = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
#if NETCOREAPP
    private readonly Mock<IChannel<string>> _mockProducerConsumer = new();
#else
    private readonly Mock<IBlockingCollection<string>> _mockProducerConsumer = new();
#endif
    private readonly Mock<IProducerConsumerFactory<string>> _mockProducerConsumerFactory = new();
    private readonly Mock<IFileStream> _mockStream = new();
    private readonly Mock<IFileStreamFactory> _mockFileStreamFactory = new();
    private readonly Mock<IStreamWriter> _mockStreamWriter = new();
    private readonly Mock<IStreamWriterFactory> _mockStreamWriterFactory = new();

    public FileLoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
#if NETCOREAPP
        _mockProducerConsumerFactory.Setup(x => x.Create(It.IsAny<UnboundedChannelOptions>())).Returns(_mockProducerConsumer.Object);
#else
        _mockProducerConsumerFactory.Setup(x => x.Create()).Returns(_mockProducerConsumer.Object);
#endif
    }

    public void Write_IfMalformedUTF8_ShouldNotCrash()
    {
        using TempDirectory tempDirectory = new(nameof(Write_IfMalformedUTF8_ShouldNotCrash));
        using FileLogger fileLogger = new(
            new FileLoggerOptions(tempDirectory.DirectoryPath, "Test", fileName: null, true),
            LogLevel.Trace,
            new SystemClock(),
            new SystemTask(),
            new SystemConsole(),
            new SystemFileSystem(),
            new SystemProducerConsumerFactory<string>(),
            new SystemFileStreamFactory(),
            new SystemStreamWriterFactory());
        fileLogger.Log(LogLevel.Trace, "\uD886", null, LoggingExtensions.Formatter, "Category");
    }

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

        string expectedFileName = $"{LogPrefix}_{_mockClock.Object.UtcNow.ToString("MMddHHssfff", CultureInfo.InvariantCulture)}.diag";
        _mockStream.Setup(x => x.Name).Returns(expectedFileName);
        _mockFileStreamFactory
            .SetupSequence(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Throws<IOException>()
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        using FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            _mockTask.Object,
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockProducerConsumerFactory.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object);

        _mockFileStreamFactory.Verify(
            x => x.Create(Path.Combine(LogFolder, expectedFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(expectedFileName, fileLogger.FileName);
    }

    public void FileLogger_NullFileSyncFlush_FileStreamCreationThrows()
    {
        // First return is to compute the expected file name. It's ok that first time is greater
        // than all following ones.
        _mockClock.SetupSequence(x => x.UtcNow)
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)))
            .Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 17)));
        _mockFileStreamFactory
            .SetupSequence(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Throws<IOException>()
            .Returns(_mockStream.Object);

        Assert.Throws<InvalidOperationException>(() => _ = new FileLogger(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            _mockTask.Object,
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockProducerConsumerFactory.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object));
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class FileLoggerTests : TestBase, IDisposable
{
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
    private readonly MemoryStream _memoryStream;

    public FileLoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockStream.Setup(x => x.Dispose());
#if NETCOREAPP
        _mockStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif

        _mockStream.Setup(x => x.Name).Returns(FileName);
        _memoryStream = new MemoryStream();
        _mockStream.Setup(x => x.Stream).Returns(_memoryStream);
    }

    public void Write_IfMalformedUTF8_ShouldNotCrash()
    {
        using TempDirectory tempDirectory = new(nameof(Write_IfMalformedUTF8_ShouldNotCrash));
        using FileLogger fileLogger = new(
            new FileLoggerOptions(tempDirectory.Path, "Test", fileName: null),
            LogLevel.Trace,
            new SystemClock(),
            new SystemTask(),
            new SystemConsole(),
            new SystemFileSystem(),
            new SystemFileStreamFactory());
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

        Assert.Throws<InvalidOperationException>(() => _ = new FileLogger(
            new(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object));
    }

    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public void FileLogger_ValidFileName_FileStreamCreatedSuccessfully(bool syncFlush, bool fileExists)
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(fileExists);
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

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public async Task Log_WhenSyncFlush_StreamWriterIsCalledOnlyWhenLogLevelAllowsIt(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        _mockFileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
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
            if (_memoryStream.Length == 0)
            {
                await Task.Delay(1000);
            }

            await _memoryStream.FlushAsync();
            Assert.AreEqual($"[00:00:00.000 Test - {currentLogLevel}] Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
        }
        else
        {
            Assert.AreEqual(0, _memoryStream.Length);
        }
    }

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public async Task Log_WhenAsyncFlush_StreamWriterIsCalledOnlyWhenLogLevelAllowsIt(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        _mockFileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        using FileLogger fileLogger = new(
            new(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            defaultLogLevel,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object);
        fileLogger.Log(currentLogLevel, Message, null, Formatter, Category);

        if (LogTestHelpers.IsLogEnabled(defaultLogLevel, currentLogLevel))
        {
            if (_memoryStream.Length == 0)
            {
                await Task.Delay(1000);
            }

            Assert.AreEqual($"0001-01-01T00:00:00.0000000+00:00 Test {currentLogLevel.ToString().ToUpperInvariant()} Message{Environment.NewLine}", Encoding.Default.GetString(_memoryStream.ToArray()));
        }
        else
        {
            Assert.AreEqual(0, _memoryStream.Length);
        }
    }

    public void Dispose() => _memoryStream.Dispose();
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

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
    private readonly Mock<IStreamWriter> _mockStreamWriter = new();
    private readonly Mock<IStreamWriterFactory> _mockStreamWriterFactory = new();

    public FileLoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockStream.Setup(x => x.Dispose());
        _mockStreamWriter.Setup(x => x.Flush());
        _mockStreamWriter.Setup(x => x.Dispose());
        _mockStreamWriter.Setup(x => x.WriteLine(It.IsAny<string>()));
        _mockStreamWriter.Setup(x => x.WriteLineAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
#if NETCOREAPP
        _mockStream.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _mockStreamWriter.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
#endif

        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);
    }

    internal static IEnumerable<(LogLevel DefaultLevel, LogLevel CurrentLevel, bool ShouldLog, bool SyncFlush)> GetLogLevelCombinations()
    {
        yield return (LogLevel.Trace, LogLevel.Trace, true, true);
        yield return (LogLevel.Trace, LogLevel.Debug, true, true);
        yield return (LogLevel.Trace, LogLevel.Information, true, true);
        yield return (LogLevel.Trace, LogLevel.Warning, true, true);
        yield return (LogLevel.Trace, LogLevel.Error, true, true);
        yield return (LogLevel.Trace, LogLevel.Critical, true, true);
        yield return (LogLevel.Debug, LogLevel.Trace, false, true);
        yield return (LogLevel.Debug, LogLevel.Debug, true, true);
        yield return (LogLevel.Debug, LogLevel.Information, true, true);
        yield return (LogLevel.Debug, LogLevel.Warning, true, true);
        yield return (LogLevel.Debug, LogLevel.Error, true, true);
        yield return (LogLevel.Debug, LogLevel.Critical, true, true);
        yield return (LogLevel.Information, LogLevel.Trace, false, true);
        yield return (LogLevel.Information, LogLevel.Debug, false, true);
        yield return (LogLevel.Information, LogLevel.Information, true, true);
        yield return (LogLevel.Information, LogLevel.Warning, true, true);
        yield return (LogLevel.Information, LogLevel.Error, true, true);
        yield return (LogLevel.Information, LogLevel.Critical, true, true);
        yield return (LogLevel.Warning, LogLevel.Trace, false, true);
        yield return (LogLevel.Warning, LogLevel.Debug, false, true);
        yield return (LogLevel.Warning, LogLevel.Information, false, true);
        yield return (LogLevel.Warning, LogLevel.Warning, true, true);
        yield return (LogLevel.Warning, LogLevel.Error, true, true);
        yield return (LogLevel.Warning, LogLevel.Critical, true, true);
        yield return (LogLevel.Error, LogLevel.Trace, false, true);
        yield return (LogLevel.Error, LogLevel.Debug, false, true);
        yield return (LogLevel.Error, LogLevel.Information, false, true);
        yield return (LogLevel.Error, LogLevel.Warning, false, true);
        yield return (LogLevel.Error, LogLevel.Error, true, true);
        yield return (LogLevel.Error, LogLevel.Critical, true, true);
        yield return (LogLevel.Critical, LogLevel.Trace, false, true);
        yield return (LogLevel.Critical, LogLevel.Debug, false, true);
        yield return (LogLevel.Critical, LogLevel.Information, false, true);
        yield return (LogLevel.Critical, LogLevel.Warning, false, true);
        yield return (LogLevel.Critical, LogLevel.Error, false, true);
        yield return (LogLevel.Critical, LogLevel.Critical, true, true);
        yield return (LogLevel.None, LogLevel.Trace, false, true);
        yield return (LogLevel.None, LogLevel.Debug, false, true);
        yield return (LogLevel.None, LogLevel.Information, false, true);
        yield return (LogLevel.None, LogLevel.Warning, false, true);
        yield return (LogLevel.None, LogLevel.Error, false, true);
        yield return (LogLevel.None, LogLevel.Critical, false, true);
        yield return (LogLevel.Trace, LogLevel.Trace, true, false);
        yield return (LogLevel.Trace, LogLevel.Debug, true, false);
        yield return (LogLevel.Trace, LogLevel.Information, true, false);
        yield return (LogLevel.Trace, LogLevel.Warning, true, false);
        yield return (LogLevel.Trace, LogLevel.Error, true, false);
        yield return (LogLevel.Trace, LogLevel.Critical, true, false);
        yield return (LogLevel.Debug, LogLevel.Trace, false, false);
        yield return (LogLevel.Debug, LogLevel.Debug, true, false);
        yield return (LogLevel.Debug, LogLevel.Information, true, false);
        yield return (LogLevel.Debug, LogLevel.Warning, true, false);
        yield return (LogLevel.Debug, LogLevel.Error, true, false);
        yield return (LogLevel.Debug, LogLevel.Critical, true, false);
        yield return (LogLevel.Information, LogLevel.Trace, false, false);
        yield return (LogLevel.Information, LogLevel.Debug, false, false);
        yield return (LogLevel.Information, LogLevel.Information, true, false);
        yield return (LogLevel.Information, LogLevel.Warning, true, false);
        yield return (LogLevel.Information, LogLevel.Error, true, false);
        yield return (LogLevel.Information, LogLevel.Critical, true, false);
        yield return (LogLevel.Warning, LogLevel.Trace, false, false);
        yield return (LogLevel.Warning, LogLevel.Debug, false, false);
        yield return (LogLevel.Warning, LogLevel.Information, false, false);
        yield return (LogLevel.Warning, LogLevel.Warning, true, false);
        yield return (LogLevel.Warning, LogLevel.Error, true, false);
        yield return (LogLevel.Warning, LogLevel.Critical, true, false);
        yield return (LogLevel.Error, LogLevel.Trace, false, false);
        yield return (LogLevel.Error, LogLevel.Debug, false, false);
        yield return (LogLevel.Error, LogLevel.Information, false, false);
        yield return (LogLevel.Error, LogLevel.Warning, false, false);
        yield return (LogLevel.Error, LogLevel.Error, true, false);
        yield return (LogLevel.Error, LogLevel.Critical, true, false);
        yield return (LogLevel.Critical, LogLevel.Trace, false, false);
        yield return (LogLevel.Critical, LogLevel.Debug, false, false);
        yield return (LogLevel.Critical, LogLevel.Information, false, false);
        yield return (LogLevel.Critical, LogLevel.Warning, false, false);
        yield return (LogLevel.Critical, LogLevel.Error, false, false);
        yield return (LogLevel.Critical, LogLevel.Critical, true, false);
        yield return (LogLevel.None, LogLevel.Trace, false, false);
        yield return (LogLevel.None, LogLevel.Debug, false, false);
        yield return (LogLevel.None, LogLevel.Information, false, false);
        yield return (LogLevel.None, LogLevel.Warning, false, false);
        yield return (LogLevel.None, LogLevel.Error, false, false);
        yield return (LogLevel.None, LogLevel.Critical, false, false);
    }

    public void Write_IfMalformedUTF8_ShouldNotCrash()
    {
        using TempDirectory tempDirectory = new(nameof(Write_IfMalformedUTF8_ShouldNotCrash));
        using FileLogger fileLogger = new(
            new FileLoggerOptions(tempDirectory.Path, "Test", fileName: null, true),
            LogLevel.Trace,
            new SystemClock(),
            new SystemTask(),
            new SystemConsole(),
            new SystemFileSystem(),
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

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object))
        {
            fileLoggerName = fileLogger.FileName;
        }

        _mockFileStreamFactory.Verify(
            x => x.Create(Path.Combine(LogFolder, expectedFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
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
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: null, syncFlush: true),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object));
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

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: syncFlush),
            LogLevel.Trace,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object))
        {
            fileLoggerName = fileLogger.FileName;
        }

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, fileExists ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerName);
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinations))]
    public void FileLogger_LogTest(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldLog, bool syncFlush)
    {
        _mockFileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: syncFlush),
            defaultLogLevel,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object))
        {
            fileLogger.Log(currentLogLevel, Message, null, Formatter, Category);
        }

        if (syncFlush)
        {
            _mockStreamWriter.Verify(x => x.WriteLine(It.IsAny<string>()), shouldLog ? Times.Once : Times.Never);
        }
        else
        {
            _mockStreamWriter.Verify(x => x.WriteLineAsync(It.IsAny<string>()), shouldLog ? Times.Once : Times.Never);
        }
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinations))]
    public async Task FileLogger_LogAsyncTest(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldLog, bool syncFlush)
    {
        _mockFileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: syncFlush),
            defaultLogLevel,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object))
        {
            await fileLogger.LogAsync(currentLogLevel, Message, null, Formatter, Category);
        }

        _mockStreamWriter.Verify(x => x.WriteLineAsync(It.IsAny<string>()), shouldLog ? Times.Once : Times.Never);
    }
}

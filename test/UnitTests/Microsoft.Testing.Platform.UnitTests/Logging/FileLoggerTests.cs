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
        _mockStreamWriter.Setup(x => x.WriteLineAsync(It.IsAny<string>()));
#if NETCOREAPP
        _mockStream.Setup(x => x.DisposeAsync());
        _mockStreamWriter.Setup(x => x.DisposeAsync());
#endif
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
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

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

    [Arguments(true)]
    [Arguments(false)]
    public void FileLogger_ValidFileNameSyncFlush_FileStreamCreatedSuccessfully(bool fileExists)
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(fileExists);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), fileExists ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: true),
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

    public void FileLogger_ValidFileNameAsyncFlush_FileStreamCreatedSuccessfully()
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        string fileLoggerName = string.Empty;
        using (FileLogger fileLogger = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
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
            x => x.Create(expectedPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerName);
    }

    public void FileLogger_ValidFileNameSyncFlush_FileStreamWrite()
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        using FileLoggerProvider fileLoggerProvider = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: true),
            LogLevel.Information,
            customDirectory: true,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object);

        FileLoggerCategory fileLoggerCategory = (fileLoggerProvider.CreateLogger(Category) as FileLoggerCategory)!;

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerProvider.FileLogger.FileName);

        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Information));
        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Critical));
        Assert.IsFalse(fileLoggerCategory.IsEnabled(LogLevel.Trace));

        fileLoggerCategory.Log(LogLevel.Trace, Message, null, Formatter);

        fileLoggerCategory.Log(LogLevel.Information, Message, null, Formatter);
        _mockStreamWriter.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Once);

        Assert.Throws<InvalidOperationException>(() =>
            fileLoggerCategory.Log(LogLevel.Information, Message, null, Formatter));
    }

    public void FileLogger_ValidFileNameAsyncFlush_FileStreamWrite()
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        using FileLoggerProvider fileLoggerProvider = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Information,
            customDirectory: true,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object);

        FileLoggerCategory fileLoggerCategory = (fileLoggerProvider.CreateLogger(Category) as FileLoggerCategory)!;

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerProvider.FileLogger.FileName);

        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Information));
        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Critical));
        Assert.IsFalse(fileLoggerCategory.IsEnabled(LogLevel.Trace));

        fileLoggerCategory.Log(LogLevel.Trace, Message, null, Formatter);

        fileLoggerCategory.Log(LogLevel.Information, Message, null, Formatter);

#if NETCOREAPP
        Assert.Throws<InvalidOperationException>(() =>
            fileLoggerCategory.Log(LogLevel.Information, Message, null, Formatter));
#endif
    }

    public async ValueTask FileLogger_ValidFileNameSyncFlush_FileStreamWriteAsync()
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileSystem.Setup(x => x.Move(It.IsAny<string>(), It.IsAny<string>()));
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        using FileLoggerProvider fileLoggerProvider = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: true),
            LogLevel.Information,
            customDirectory: false,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object);

        await fileLoggerProvider.CheckLogFolderAndMoveToTheNewIfNeededAsync("TestFolder");
        _mockFileSystem.Verify(x => x.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        FileLoggerCategory fileLoggerCategory = (fileLoggerProvider.CreateLogger(Category) as FileLoggerCategory)!;

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Exactly(2));
        Assert.AreEqual(FileName, fileLoggerProvider.FileLogger.FileName);

        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Information));
        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Critical));
        Assert.IsFalse(fileLoggerCategory.IsEnabled(LogLevel.Trace));

        await fileLoggerCategory.LogAsync(LogLevel.Trace, Message, null, Formatter);

        await fileLoggerCategory.LogAsync(LogLevel.Information, Message, null, Formatter);
        _mockStreamWriter.Verify(x => x.WriteLineAsync(It.IsAny<string>()), Times.Once);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fileLoggerCategory.LogAsync(LogLevel.Information, Message, null, Formatter));
    }

    public async ValueTask FileLogger_ValidFileNameAsyncFlush_FileStreamWriteAsync()
    {
        string expectedPath = Path.Combine(LogFolder, FileName);
        _mockStream.Setup(x => x.Name).Returns(FileName);
        _mockFileSystem.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileSystem.Setup(x => x.Move(It.IsAny<string>(), It.IsAny<string>()));
        _mockFileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(_mockStream.Object);
        _mockStreamWriterFactory
            .Setup(x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true))
            .Returns(_mockStreamWriter.Object);

        using FileLoggerProvider fileLoggerProvider = new(
            new FileLoggerOptions(LogFolder, LogPrefix, fileName: FileName, syncFlush: false),
            LogLevel.Information,
            customDirectory: true,
            _mockClock.Object,
            new SystemTask(),
            _mockConsole.Object,
            _mockFileSystem.Object,
            _mockFileStreamFactory.Object,
            _mockStreamWriterFactory.Object);

        await fileLoggerProvider.CheckLogFolderAndMoveToTheNewIfNeededAsync("TestFolder");
        _mockFileSystem.Verify(x => x.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        FileLoggerCategory fileLoggerCategory = (fileLoggerProvider.CreateLogger(Category) as FileLoggerCategory)!;

        _mockFileStreamFactory.Verify(
            x => x.Create(expectedPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Times.Once);
        _mockStreamWriterFactory.Verify(
            x => x.CreateStreamWriter(_mockStream.Object, It.IsAny<UTF8Encoding>(), true),
            Times.Once);
        Assert.AreEqual(FileName, fileLoggerProvider.FileLogger.FileName);

        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Information));
        Assert.IsTrue(fileLoggerCategory.IsEnabled(LogLevel.Critical));
        Assert.IsFalse(fileLoggerCategory.IsEnabled(LogLevel.Trace));

        await fileLoggerCategory.LogAsync(LogLevel.Trace, Message, null, Formatter);

        await fileLoggerCategory.LogAsync(LogLevel.Information, Message, null, Formatter);

#if NETCOREAPP
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fileLoggerCategory.LogAsync(LogLevel.Information, Message, null, Formatter));
#endif
    }
}

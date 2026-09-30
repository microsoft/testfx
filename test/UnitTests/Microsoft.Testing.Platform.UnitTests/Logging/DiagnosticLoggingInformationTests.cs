// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DiagnosticLoggingInformationTests
{
    [TestMethod]
    public async Task LogFileReflectsFileLoggerRelocation()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";
        string initialPath = Path.Combine(initialDirectory, FileName);
        string finalPath = Path.Combine(resultsDirectory, FileName);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) => CreateFileStream(path));

        using FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
        var information = new DiagnosticLoggingInformation(provider);
        ILogger logger = provider.CreateLogger("test");

        Assert.IsTrue(information.SynchronousWrite);
        Assert.AreEqual(LogLevel.Debug, information.LogLevel);
        Assert.AreEqual(Path.GetFullPath(initialPath), information.LogFile.FullName);

        await provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory);

        Assert.AreEqual(Path.GetFullPath(finalPath), information.LogFile.FullName);
        Assert.AreEqual(Path.GetFullPath(resultsDirectory), information.LogFile.DirectoryName);
        fileSystem.Verify(x => x.MoveFile(initialPath, finalPath, false), Times.Once);

        logger.LogDebug("Written after relocation.");
    }

    private static IFileStream CreateFileStream(string path)
    {
        var memoryStream = new MemoryStream();
        var fileStream = new Mock<IFileStream>();
        fileStream.SetupGet(x => x.Name).Returns(path);
        fileStream.SetupGet(x => x.Stream).Returns(memoryStream);
        fileStream.Setup(x => x.Dispose()).Callback(memoryStream.Dispose);
#if NETCOREAPP
        fileStream.Setup(x => x.DisposeAsync()).Returns(() =>
        {
            memoryStream.Dispose();
            return ValueTask.CompletedTask;
        });
#endif
        return fileStream.Object;
    }
}

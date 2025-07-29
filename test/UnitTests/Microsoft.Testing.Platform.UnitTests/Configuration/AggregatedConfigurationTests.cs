// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AggregatedConfigurationTests
{
    private const string ExpectedPath = "a/b/c";
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformResultDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetAndNoConfigurationProviders(string key)
    {
        _testApplicationModuleInfoMock.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns("TestAppDir");
        string? expected = key switch
        {
            PlatformConfigurationConstants.PlatformResultDirectory => Path.Combine("TestAppDir", "TestResults"),
            PlatformConfigurationConstants.PlatformCurrentWorkingDirectory => "TestAppDir",
            PlatformConfigurationConstants.PlatformTestHostWorkingDirectory => "TestAppDir",
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        Assert.AreEqual(expected, new AggregatedConfiguration([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, new(null, [], []))[key]);
    }

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNull(string key)
    {
        Mock<IConfigurationProvider> mockProvider = new();

        AggregatedConfiguration aggregatedConfiguration = new([mockProvider.Object], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, new(null, [], []));
        Assert.IsNull(aggregatedConfiguration[key]);
    }

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformResultDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNotNull(string key)
    {
        AggregatedConfiguration aggregatedConfiguration = new([new FakeConfigurationProvider(ExpectedPath)], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, new(null, [], []));
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[key]);
    }

    [TestMethod]
    public void IndexerTest_ResultDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void IndexerTest_CurrentWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, new(null, [], []));

        aggregatedConfiguration.SetCurrentWorkingDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromCommandLineProvider()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(Path.GetDirectoryName(ExpectedPath) ?? AppContext.BaseDirectory);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromStore()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(Path.GetDirectoryName(ExpectedPath) ?? AppContext.BaseDirectory);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDefaultDirectory()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(Path.GetDirectoryName(ExpectedPath) ?? AppContext.BaseDirectory);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, new(null, [], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        string expectedPath = "a" + Path.DirectorySeparatorChar + "b" + Path.DirectorySeparatorChar + "TestResults";
        mockFileSystem.Verify(x => x.CreateDirectory(expectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(expectedPath), Times.Once);
        Assert.AreEqual(expectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }
}

internal sealed class FakeConfigurationProvider : IConfigurationProvider
{
    private readonly string _path;

    public FakeConfigurationProvider(string path) => _path = path;

    public Task LoadAsync() => throw new NotImplementedException();

    public bool TryGet(string key, out string? value)
    {
        value = null;
        switch (key)
        {
            case PlatformConfigurationConstants.PlatformResultDirectory:
            case PlatformConfigurationConstants.PlatformCurrentWorkingDirectory:
            case PlatformConfigurationConstants.PlatformTestHostWorkingDirectory:
                value = _path;
                return true;

            default:
                return false;
        }
    }
}

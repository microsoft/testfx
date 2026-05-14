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
    private readonly Mock<IEnvironment> _environmentMock = new();

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

        Assert.AreEqual(expected, new AggregatedConfiguration([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []))[key]);
    }

    [TestMethod]
    public void IndexerTest_DotnetCliTestCommandWorkingDirectorySet_UsedAsWorkingDirectoryBase()
    {
        const string dotnetTestWorkingDir = "DotnetTestWorkingDir";
        _environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(dotnetTestWorkingDir);

        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []));
        Assert.AreEqual(Path.Combine(dotnetTestWorkingDir, "TestResults"), aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(dotnetTestWorkingDir, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
        Assert.AreEqual(dotnetTestWorkingDir, aggregatedConfiguration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void IndexerTest_DotnetCliTestCommandWorkingDirectoryIsWhitespace_FallsBackToTestApplicationDirectory(string envVarValue)
    {
        const string appDirectory = "AppDirectory";
        _environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(envVarValue);
        _testApplicationModuleInfoMock.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(appDirectory);

        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []));
        Assert.AreEqual(Path.Combine(appDirectory, "TestResults"), aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(appDirectory, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
        Assert.AreEqual(appDirectory, aggregatedConfiguration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory]);
    }

    [TestMethod]
    public void IndexerTest_ResultsDirectoryCliArgTakesPrecedenceOverDotnetCliTestCommandWorkingDirectory()
    {
        const string dotnetTestWorkingDir = "DotnetTestWorkingDir";
        _environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(dotnetTestWorkingDir);

        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));

        // --results-directory CLI arg should win over DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        // Current working directory still comes from env var
        Assert.AreEqual(dotnetTestWorkingDir, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNull(string key)
    {
        Mock<IConfigurationProvider> mockProvider = new();

        AggregatedConfiguration aggregatedConfiguration = new([mockProvider.Object], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []));
        Assert.IsNull(aggregatedConfiguration[key]);
    }

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformResultDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNotNull(string key)
    {
        AggregatedConfiguration aggregatedConfiguration = new([new FakeConfigurationProvider(ExpectedPath)], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []));
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[key]);
    }

    [TestMethod]
    public void IndexerTest_ResultDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void IndexerTest_CurrentWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object, _environmentMock.Object, new(null, [], []));

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

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, _environmentMock.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
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

        // Results directory comes from configuration provider (store), not CLI args
        AggregatedConfiguration aggregatedConfiguration = new([new FakeConfigurationProvider(ExpectedPath)], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, _environmentMock.Object, new(null, [], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        // PlatformCurrentWorkingDirectory also comes from the configuration provider
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
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

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, _environmentMock.Object, new(null, [], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        string expectedPath = "a" + Path.DirectorySeparatorChar + "b" + Path.DirectorySeparatorChar + "TestResults";
        mockFileSystem.Verify(x => x.CreateDirectory(expectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(expectedPath), Times.Once);
        Assert.AreEqual(expectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_DotnetCliTestCommandWorkingDirectorySet_UsedAsResultDirectoryBase()
    {
        const string dotnetTestWorkingDir = "DotnetTestWorkingDir";
        _environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(dotnetTestWorkingDir);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, mockFileSystem.Object, _environmentMock.Object, new(null, [], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        string expectedPath = Path.Combine(dotnetTestWorkingDir, "TestResults");
        mockFileSystem.Verify(x => x.CreateDirectory(expectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(expectedPath), Times.Once);
        Assert.AreEqual(expectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(dotnetTestWorkingDir, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    [TestMethod]
    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryCliArgTakesPrecedenceOverDotnetCliTestCommandWorkingDirectory()
    {
        const string dotnetTestWorkingDir = "DotnetTestWorkingDir";
        _environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(dotnetTestWorkingDir);

        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(Path.GetDirectoryName(ExpectedPath) ?? AppContext.BaseDirectory);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        // --results-directory CLI arg takes precedence over DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY
        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object, _environmentMock.Object, new(null, [new CommandLineParseOption("results-directory", [ExpectedPath])], []));
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        // Current working directory still comes from env var
        Assert.AreEqual(dotnetTestWorkingDir, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class AggregatedConfigurationTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    private const string ExpectedPath = "a/b/c";
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetAndNoConfigurationProviders_DirectoryIsNull(string key)
        => Assert.IsNull(new AggregatedConfiguration([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object)[key]);

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNull(string key)
    {
        Mock<IConfigurationProvider> mockProvider = new();

        AggregatedConfiguration aggregatedConfiguration = new([mockProvider.Object], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object);
        Assert.IsNull(aggregatedConfiguration[key]);
    }

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNotNull(string key)
    {
        AggregatedConfiguration aggregatedConfiguration = new([new FakeConfigurationProvider(ExpectedPath)], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[key]);
    }

    public void IndexerTest_ResultDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object);

        aggregatedConfiguration.SetResultDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    public void IndexerTest_CurrentWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object);

        aggregatedConfiguration.SetCurrentWorkingDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public void IndexerTest_TestHostWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new([], _testApplicationModuleInfoMock.Object, _fileSystemMock.Object);

        aggregatedConfiguration.SetTestHostWorkingDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromCommandLineProvider()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object);
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(
            new FakeCommandLineOptions(ExpectedPath), mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromStore()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object);
        aggregatedConfiguration.SetResultDirectory(ExpectedPath);
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(new FakeCommandLineOptions(ExpectedPath), mockFileLogger.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual("a" + Path.DirectorySeparatorChar + "b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDefaultDirectory()
    {
        Mock<ITestApplicationModuleInfo> mockTestApplicationModuleInfo = new();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<IFileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        AggregatedConfiguration aggregatedConfiguration = new([], mockTestApplicationModuleInfo.Object, mockFileSystem.Object);
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(
            new FakeCommandLineOptions(ExpectedPath, bypass: true), mockFileLogger.Object);

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

    public FakeConfigurationProvider(string path)
    {
        _path = path;
    }

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

internal sealed class FakeCommandLineOptions : ICommandLineOptions
{
    private readonly string _path;
    private readonly bool _bypass;

    public FakeCommandLineOptions(string path, bool bypass = false)
    {
        _path = path;
        _bypass = bypass;
    }

    public bool IsOptionSet(string optionName) => throw new NotImplementedException();

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        arguments = null;
        if (_bypass)
        {
            return false;
        }

        switch (optionName)
        {
            case PlatformCommandLineProvider.ResultDirectoryOptionKey:
                arguments = [_path];
                return true;
            default:
                return false;
        }
    }
}

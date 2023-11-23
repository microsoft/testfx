// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class AggregatedConfigurationTests : TestBase
{
    private const string ExpectedPath = "a/b/c";

    public AggregatedConfigurationTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetAndNoConfigurationProviders_DirectoryIsNull(string key)
        => Assert.IsNull(new AggregatedConfiguration(Array.Empty<IConfigurationProvider>())[key]);

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNull(string key)
    {
        Mock<IConfigurationProvider> mockProvider = new();

        AggregatedConfiguration aggregatedConfiguration = new(new IConfigurationProvider[] { mockProvider.Object });
        Assert.IsNull(aggregatedConfiguration[key]);
    }

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void IndexerTest_DirectoryNotSetButConfigurationProvidersPresent_DirectoryIsNotNull(string key)
    {
        AggregatedConfiguration aggregatedConfiguration = new(new IConfigurationProvider[] { new FakeConfigurationProvider(ExpectedPath) });
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[key]);
    }

    public void IndexerTest_ResultDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());

        aggregatedConfiguration.SetResultDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    public void IndexerTest_CurrentWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());

        aggregatedConfiguration.SetCurrentWorkingDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public void IndexerTest_TestHostWorkingDirectorySet_DirectoryIsNotNull()
    {
        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());

        aggregatedConfiguration.SetTestHostWorkingDirectory(ExpectedPath);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromCommandLineProvider()
    {
        Mock<ITestApplicationModuleInfo> mockModuleInfo = new();
        mockModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IRuntime> mockRuntime = new();
        mockRuntime.Setup(x => x.GetCurrentModuleInfo()).Returns(mockModuleInfo.Object);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<FileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        Mock<ServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetServicesInternal(typeof(FileLoggerProvider), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new List<FileLoggerProvider>() { mockFileLogger.Object });

        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(
            new FakeCommandLineOptions(ExpectedPath), mockRuntime.Object, mockFileSystem.Object, serviceProviderMock.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(@"a\b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDirectoryFromStore()
    {
        Mock<ITestApplicationModuleInfo> mockModuleInfo = new();
        mockModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IRuntime> mockRuntime = new();
        mockRuntime.Setup(x => x.GetCurrentModuleInfo()).Returns(mockModuleInfo.Object);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<FileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        Mock<ServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetServicesInternal(typeof(FileLoggerProvider), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new List<FileLoggerProvider>() { mockFileLogger.Object });

        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());
        aggregatedConfiguration.SetResultDirectory(ExpectedPath);
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(
            new FakeCommandLineOptions(ExpectedPath), mockRuntime.Object, mockFileSystem.Object, serviceProviderMock.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(ExpectedPath), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(ExpectedPath), Times.Once);
        Assert.AreEqual(ExpectedPath, aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(@"a\b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
    }

    public async ValueTask CheckTestResultsDirectoryOverrideAndCreateItAsync_ResultsDirectoryIsNull_GetDefaultDirectory()
    {
        Mock<ITestApplicationModuleInfo> mockModuleInfo = new();
        mockModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(ExpectedPath);

        Mock<IRuntime> mockRuntime = new();
        mockRuntime.Setup(x => x.GetCurrentModuleInfo()).Returns(mockModuleInfo.Object);

        Mock<IFileSystem> mockFileSystem = new();
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns((string path) => path);

        Mock<FileLoggerProvider> mockFileLogger = new();
        mockFileLogger.Setup(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(It.IsAny<string>())).Callback(() => { });

        Mock<ServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetServicesInternal(typeof(FileLoggerProvider), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new List<FileLoggerProvider>() { mockFileLogger.Object });

        AggregatedConfiguration aggregatedConfiguration = new(Array.Empty<IConfigurationProvider>());
        await aggregatedConfiguration.CheckTestResultsDirectoryOverrideAndCreateItAsync(
            new FakeCommandLineOptions(ExpectedPath, bypass: true), mockRuntime.Object, mockFileSystem.Object, serviceProviderMock.Object);

        mockFileSystem.Verify(x => x.CreateDirectory(@"a\b\TestResults"), Times.Once);
        mockFileLogger.Verify(x => x.CheckLogFolderAndMoveToTheNewIfNeededAsync(@"a\b\TestResults"), Times.Once);
        Assert.AreEqual(@"a\b\TestResults", aggregatedConfiguration[PlatformConfigurationConstants.PlatformResultDirectory]);
        Assert.AreEqual(@"a\b", aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
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
                arguments = new string[] { _path };
                return true;
            default:
                return false;
        }
    }
}

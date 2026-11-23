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
public sealed class JsonConfigurationProviderLoadingTests
{
    [TestMethod]
    public async Task LoadAsync_ExistingDefaultFile_LogsInformation()
    {
        Mock<ILogger> logger = new();
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(CreateFileSystem("{}"), logger.Object);

        await provider.LoadAsync();

        logger.Verify(l => l.LogAsync(LogLevel.Information, It.IsAny<string>(), null, LoggingExtensions.Formatter), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_InvalidJson_LogsErrorAndRethrows()
    {
        Mock<ILogger> logger = new();
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(CreateFileSystem("{ not json"), logger.Object);

        Exception exception = await Assert.ThrowsAsync<Exception>(provider.LoadAsync);

        logger.Verify(l => l.LogAsync(LogLevel.Error, It.IsAny<string>(), exception, LoggingExtensions.Formatter), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_DefaultFileName_IsBuiltFromTestApplicationPathWithoutExtension()
    {
        string expected = Path.Combine("dir", "My.Tests") + PlatformConfigurationConstants.PlatformConfigSuffixFileName;
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(f => f.ExistFile(expected)).Returns(false);
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(fileSystem.Object, null);

        await provider.LoadAsync();

        fileSystem.Verify(f => f.ExistFile(expected), Times.Once);
        fileSystem.VerifyNoOtherCalls();
        Assert.IsNull(provider.ConfigurationFile);
    }

    private static IFileSystem CreateFileSystem(string json)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes(json)));
        return fileSystem.Object;
    }

    private static JsonConfigurationSource.JsonConfigurationProvider CreateProvider(IFileSystem fileSystem, ILogger? logger)
    {
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.TryGetCurrentTestApplicationFullPath()).Returns(Path.Combine("dir", "My.Tests.dll"));
        return new JsonConfigurationSource.JsonConfigurationProvider(module.Object, fileSystem, CommandLineParseResult.Empty, logger);
    }
}

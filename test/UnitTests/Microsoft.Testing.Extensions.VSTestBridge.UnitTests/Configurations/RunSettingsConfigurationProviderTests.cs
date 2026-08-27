// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Configurations;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Configurations;

[TestClass]
public sealed class RunSettingsConfigurationProviderTests
{
    private const string RunSettingsOptionName = "settings";

    private const string RunSettingsWithResultsDirectory = """
        <?xml version="1.0" encoding="utf-8"?>
        <RunSettings>
            <RunConfiguration>
                <ResultsDirectory>C:\MyResults</ResultsDirectory>
            </RunConfiguration>
        </RunSettings>
        """;

    private const string RunSettingsWithoutResultsDirectory = """
        <?xml version="1.0" encoding="utf-8"?>
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
        """;

    [TestMethod]
    public void TryGet_BeforeBuildAsync_ReturnsFalse()
    {
        // Arrange
        var provider = new RunSettingsConfigurationProvider(new Mock<IFileSystem>(MockBehavior.Strict).Object);

        // Act
        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGet_AfterBuildAsyncWithResultsDirectory_ReturnsTrueAndValue()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);

        // Act
        await provider.BuildAsync(parseResult);
        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual("""C:\MyResults""", value);
    }

    [TestMethod]
    public async Task TryGet_AfterBuildAsyncWithoutResultsDirectory_ReturnsFalse()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithoutResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);

        // Act
        await provider.BuildAsync(parseResult);
        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGet_ForUnrelatedKey_ReturnsFalseEvenWhenResultsDirectoryIsPresent()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);

        // Act
        await provider.BuildAsync(parseResult);
        bool result = provider.TryGet("someOtherKey", out string? value);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGetScalar_DelegatesToTryGet()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);

        // Act
        await provider.BuildAsync(parseResult);
        bool result = provider.TryGetScalar(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual("""C:\MyResults""", value);
    }

    [TestMethod]
    public async Task GetChildKeys_WhenResultsDirectoryPresent_ReturnsExpectedHierarchy()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);
        await provider.BuildAsync(parseResult);

        // Act & Assert
        Assert.AreSequenceEqual(["platformOptions"], provider.GetChildKeys(null).ToArray());
        Assert.AreSequenceEqual(["resultDirectory"], provider.GetChildKeys("platformOptions").ToArray());
        Assert.IsEmpty(provider.GetChildKeys("somethingElse"));
    }

    [TestMethod]
    public async Task GetChildKeys_WhenResultsDirectoryAbsent_ReturnsEmpty()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(RunSettingsWithoutResultsDirectory);

        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CreateParseResult(filePath);
        await provider.BuildAsync(parseResult);

        // Act & Assert
        Assert.IsEmpty(provider.GetChildKeys(null));
    }

    [TestMethod]
    public async Task BuildAsync_WhenNoRunSettingsFile_TryGetReturnsFalse()
    {
        // Arrange
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var provider = new RunSettingsConfigurationProvider(fileSystem.Object);
        CommandLineParseResult parseResult = CommandLineParseResult.Empty;

        // Act
        IConfigurationProvider builtProvider = await provider.BuildAsync(parseResult);
        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        // Assert
        Assert.AreSame(provider, builtProvider);
        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task IsEnabledAsync_AlwaysReturnsTrue()
    {
        // Arrange
        var provider = new RunSettingsConfigurationProvider(new Mock<IFileSystem>(MockBehavior.Strict).Object);

        // Act
        bool result = await provider.IsEnabledAsync();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Identity_PropertiesAreSet()
    {
        // Arrange
        var provider = new RunSettingsConfigurationProvider(new Mock<IFileSystem>(MockBehavior.Strict).Object);

        // Act & Assert
        Assert.AreEqual(nameof(RunSettingsConfigurationProvider), provider.Uid);
        Assert.IsFalse(string.IsNullOrEmpty(provider.Version));
        Assert.IsFalse(string.IsNullOrEmpty(provider.DisplayName));
        Assert.IsFalse(string.IsNullOrEmpty(provider.Description));
        Assert.AreEqual(2, provider.Order);
    }

    private static CommandLineParseResult CreateParseResult(string runSettingsFilePath)
        => new(
            toolName: null,
            options: [new CommandLineParseOption(RunSettingsOptionName, [runSettingsFilePath])],
            errors: []);
}

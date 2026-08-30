// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.CommandLine;

[TestClass]
public sealed class RunSettingsCommandLineOptionsProviderTests
{
    [TestMethod]
    public async Task RunSettingsOption_WhenFileDoesNotExist_IsNotValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(false);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RunsettingsFileDoesNotExist, filePath), result.ErrorMessage);
    }

    [TestMethod]
    public async Task RunSettingsOption_WhenFileCannotBeOpen_IsNotValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Throws(new IOException());

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RunsettingsFileCannotBeRead, filePath), result.ErrorMessage);
    }

    [TestMethod]
    public async Task RunSettingsOption_WhenFileCannotBeReadDueToPermissions_IsNotValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Throws(new UnauthorizedAccessException());

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RunsettingsFileCannotBeRead, filePath), result.ErrorMessage);
    }

    [TestMethod]
    public async Task RunSettingsOption_WhenFileExistsAndCanBeOpen_IsValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(new Mock<IFileStream>().Object);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_WhenNotRunningOnBrowser_IsValidRegardlessOfRunsettingsContentAsync()
    {
        // Arrange
        // OperatingSystem.IsBrowser() is always false for the unit test host (net8.0/net462), so this exercises the
        // non-browser branch of ValidateCommandLineOptionsAsync, which previously had no direct coverage. The
        // browser-specific <EnvironmentVariables> rejection branch cannot be exercised from a non-browser test host.
        const string filePath = "test.runsettings";
        const string runSettingsWithEnvironmentVariables = """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
                <RunConfiguration>
                    <EnvironmentVariables>
                        <TEST_ENV>TestValue</TEST_ENV>
                    </EnvironmentVariables>
                </RunConfiguration>
            </RunSettings>
            """;

        var commandLineOptions = new Mock<ICommandLineOptions>();
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = [filePath];
                return true;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.ExistFile(filePath)).Returns(true);
        var fileStream = new Mock<IFileStream>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(runSettingsWithEnvironmentVariables));
        fileStream.Setup(x => x.Stream).Returns(stream);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(fileStream.Object);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);

        // Act
        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(commandLineOptions.Object);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_WhenNoRunsettingsProvided_IsValidAsync()
    {
        // Arrange
        var commandLineOptions = new Mock<ICommandLineOptions>();
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);

        // Act
        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(commandLineOptions.Object);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }
}

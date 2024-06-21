// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.CommandLine;

[TestGroup]
public sealed class RunSettingsCommandLineOptionsProviderTests(ITestExecutionContext testExecutionContext)
    : TestBase(testExecutionContext)
{
    public async Task RunSettingsOption_WhenFileDoesNotExist_IsNotValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.Exists(It.IsAny<string>())).Returns(false);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RunsettingsFileDoesNotExist, filePath), result.ErrorMessage);
    }

    public async Task RunSettingsOption_WhenFileCannotBeOpen_IsNotValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.Exists(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Throws(new IOException());

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RunsettingsFileCannotBeRead, filePath), result.ErrorMessage);
    }

    public async Task RunSettingsOption_WhenFileExistsAndCanBeOpen_IsValid()
    {
        // Arrange
        const string filePath = "file";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.Exists(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(new Mock<IFileStream>().Object);

        var provider = new RunSettingsCommandLineOptionsProvider(new TestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [filePath]);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }
}

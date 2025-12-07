// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.TestHostControllers;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.TestHostControllers;

[TestClass]
public sealed class RunSettingsEnvironmentVariableProviderTests
{
    private const string RunSettingsWithEnvironmentVariables = """
        <?xml version="1.0" encoding="utf-8"?>
        <RunSettings>
            <RunConfiguration>
                <EnvironmentVariables>
                    <TEST_ENV>TestValue</TEST_ENV>
                    <ANOTHER_VAR>AnotherValue</ANOTHER_VAR>
                </EnvironmentVariables>
            </RunConfiguration>
        </RunSettings>
        """;

    private const string RunSettingsWithoutEnvironmentVariables = """
        <?xml version="1.0" encoding="utf-8"?>
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
        """;

    [TestMethod]
    public async Task IsEnabledAsync_WhenCommandLineOptionProvided_ReturnsTrue()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = [filePath];
                return true;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(RunSettingsWithEnvironmentVariables));
        fileStream.Setup(x => x.Stream).Returns(stream);
        fileSystem.Setup(x => x.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(fileStream.Object);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act
        bool result = await provider.IsEnabledAsync();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsEnabledAsync_WhenCommandLineOptionProvidedButNoEnvironmentVariables_ReturnsFalse()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = [filePath];
                return true;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(RunSettingsWithoutEnvironmentVariables));
        fileStream.Setup(x => x.Stream).Returns(stream);
        fileSystem.Setup(x => x.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(fileStream.Object);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act
        bool result = await provider.IsEnabledAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsEnabledAsync_WhenEnvironmentVariableWithContentProvided_ReturnsTrue()
    {
        // Arrange
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act & Assert
        try
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", RunSettingsWithEnvironmentVariables);
            bool result = await provider.IsEnabledAsync();
            Assert.IsTrue(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", null);
        }
    }

    [TestMethod]
    public async Task IsEnabledAsync_WhenEnvironmentVariableWithContentProvidedButNoEnvironmentVariables_ReturnsFalse()
    {
        // Arrange
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act & Assert
        try
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", RunSettingsWithoutEnvironmentVariables);
            bool result = await provider.IsEnabledAsync();
            Assert.IsFalse(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", null);
        }
    }

    [TestMethod]
    public async Task IsEnabledAsync_WhenEnvironmentVariableWithFilePathProvided_ReturnsTrue()
    {
        // Arrange
        const string filePath = "test.runsettings";
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(RunSettingsWithEnvironmentVariables));
        fileStream.Setup(x => x.Stream).Returns(stream);
        fileSystem.Setup(x => x.NewFileStream(filePath, FileMode.Open, FileAccess.Read)).Returns(fileStream.Object);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act & Assert
        try
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE", filePath);
            bool result = await provider.IsEnabledAsync();
            Assert.IsTrue(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE", null);
        }
    }

    [TestMethod]
    public async Task IsEnabledAsync_WhenNoRunsettingsProvided_ReturnsFalse()
    {
        // Arrange
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        // Act
        bool result = await provider.IsEnabledAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateAsync_SetsEnvironmentVariablesFromRunsettings()
    {
        // Arrange
        var commandLineOptions = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? value) =>
            {
                value = null;
                return false;
            });

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var provider = new RunSettingsEnvironmentVariableProvider(new TestExtension(), commandLineOptions.Object, fileSystem.Object);

        var environmentVariables = new Mock<IEnvironmentVariables>(MockBehavior.Strict);
        var capturedVariables = new List<EnvironmentVariable>();
        environmentVariables.Setup(x => x.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(capturedVariables.Add);

        // Act
        try
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", RunSettingsWithEnvironmentVariables);
            await provider.IsEnabledAsync();
            await provider.UpdateAsync(environmentVariables.Object);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS", null);
        }

        // Assert
        Assert.AreEqual(2, capturedVariables.Count);
        Assert.IsTrue(capturedVariables.Any(v => v.Variable == "TEST_ENV" && v.Value == "TestValue"));
        Assert.IsTrue(capturedVariables.Any(v => v.Variable == "ANOTHER_VAR" && v.Value == "AnotherValue"));
    }
}

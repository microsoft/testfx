// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;

[TestClass]
public sealed class RunSettingsHelpersTests
{
    private const string ContentEnvironmentVariable = "TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS";
    private const string FileEnvironmentVariable = "TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE";
    private const string RunSettings = "<RunSettings><RunConfiguration /></RunSettings>";

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void ReadRunSettings_WhenContentEnvironmentVariableIsSet_ReturnsItsValue()
    {
        string? originalContent = Environment.GetEnvironmentVariable(ContentEnvironmentVariable);
        string? originalFile = Environment.GetEnvironmentVariable(FileEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, RunSettings);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, null);

            string result = RunSettingsHelpers.ReadRunSettings(
                CommandLineParseResult.Empty,
                new Mock<IFileSystem>(MockBehavior.Strict).Object);

            Assert.AreEqual(RunSettings, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, originalContent);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, originalFile);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void ReadRunSettings_WhenFileEnvironmentVariablePointsToExistingFile_ReadsItsValue()
    {
        string? originalContent = Environment.GetEnvironmentVariable(ContentEnvironmentVariable);
        string? originalFile = Environment.GetEnvironmentVariable(FileEnvironmentVariable);
        string existingFilePath = typeof(RunSettingsHelpersTests).Assembly.Location;
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ReadAllText(existingFilePath)).Returns(RunSettings);

        try
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, existingFilePath);

            string result = RunSettingsHelpers.ReadRunSettings(CommandLineParseResult.Empty, fileSystem.Object);

            Assert.AreEqual(RunSettings, result);
            fileSystem.Verify(x => x.ReadAllText(existingFilePath), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, originalContent);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, originalFile);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void ReadRunSettings_WhenFileEnvironmentVariablePointsToMissingFile_ReturnsEmpty()
    {
        string? originalContent = Environment.GetEnvironmentVariable(ContentEnvironmentVariable);
        string? originalFile = Environment.GetEnvironmentVariable(FileEnvironmentVariable);
        string missingFilePath = Path.Combine(Environment.CurrentDirectory, $"{Guid.NewGuid():N}.runsettings");

        try
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, missingFilePath);

            string result = RunSettingsHelpers.ReadRunSettings(
                CommandLineParseResult.Empty,
                new Mock<IFileSystem>(MockBehavior.Strict).Object);

            Assert.AreEqual(string.Empty, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentEnvironmentVariable, originalContent);
            Environment.SetEnvironmentVariable(FileEnvironmentVariable, originalFile);
        }
    }
}

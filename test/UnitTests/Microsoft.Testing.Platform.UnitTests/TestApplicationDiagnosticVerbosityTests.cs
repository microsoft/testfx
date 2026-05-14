// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestApplicationDiagnosticVerbosityTests
{
    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenLowercaseValue_ReturnsParsedValue()
    {
        bool hasValue = TestApplication.TryParseDiagnosticVerbosity("trace", out LogLevel parsedLogLevel);

        Assert.IsTrue(hasValue);
        Assert.AreEqual(LogLevel.Trace, parsedLogLevel);
    }

    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenNullValue_ReturnsFalse()
    {
        bool hasValue = TestApplication.TryParseDiagnosticVerbosity(null, out LogLevel parsedLogLevel);

        Assert.IsFalse(hasValue);
        Assert.AreEqual(LogLevel.None, parsedLogLevel);
    }

    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenInvalidValue_ThrowsNotSupportedException()
        => Assert.ThrowsExactly<NotSupportedException>(() => _ = TestApplication.TryParseDiagnosticVerbosity("invalid", out _));

    [TestMethod]
    public void GetDiagnosticDefaultDirectory_WhenDotnetCliTestCommandWorkingDirectorySet_UsesEnvVarAsBase()
    {
        const string dotnetTestWorkingDir = "DotnetTestWorkingDir";
        Mock<IEnvironment> environmentMock = new();
        environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(dotnetTestWorkingDir);
        Mock<ITestApplicationModuleInfo> moduleInfoMock = new();

        string result = TestApplication.GetDiagnosticDefaultDirectory(environmentMock.Object, moduleInfoMock.Object);

        Assert.AreEqual(Path.Combine(dotnetTestWorkingDir, AggregatedConfiguration.DefaultTestResultFolderName), result);
        moduleInfoMock.Verify(x => x.GetCurrentTestApplicationDirectory(), Times.Never);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GetDiagnosticDefaultDirectory_WhenDotnetCliTestCommandWorkingDirectoryNullOrWhitespace_FallsBackToAppDirectory(string? envVarValue)
    {
        const string appDirectory = "AppDirectory";
        Mock<IEnvironment> environmentMock = new();
        environmentMock.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY))
            .Returns(envVarValue);
        Mock<ITestApplicationModuleInfo> moduleInfoMock = new();
        moduleInfoMock.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(appDirectory);

        string result = TestApplication.GetDiagnosticDefaultDirectory(environmentMock.Object, moduleInfoMock.Object);

        Assert.AreEqual(Path.Combine(appDirectory, AggregatedConfiguration.DefaultTestResultFolderName), result);
    }
}

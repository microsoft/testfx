﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TelemetryManagerTests : TestBase
{
    public TelemetryManagerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    // When set to 1 or true it should suppress the message.
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER, "1")]
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER, "true")]
    [Arguments(EnvironmentVariableConstants.DOTNET_NOLOGO, "1")]
    [Arguments(EnvironmentVariableConstants.DOTNET_NOLOGO, "true")]

    // When set to 0 it should write the message.
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER, "0")]
    [Arguments(EnvironmentVariableConstants.DOTNET_NOLOGO, "0")]
    public async Task TelemetryManager_UsingNoLogoShouldSuppressTelemetryMessage(string variable, string value)
    {
        // Arrange
        var options = new TestApplicationOptions();
        Assert.IsTrue(options.EnableTelemetry);

        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var outputDevice = new Mock<IOutputDevice>();
        var commandLineOptions = new Mock<ICommandLineOptions>();

        var testApplicationModuleInfoMock = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfoMock.Setup(a => a.GetCurrentTestApplicationFullPath()).Returns("directory/myExe.exe");

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(commandLineOptions.Object);
        serviceProvider.AddService(fileSystemMock.Object);
        serviceProvider.AddService(environmentMock.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(testApplicationModuleInfoMock.Object);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var telemetryManager = new TelemetryManager();

        // Act
        environmentMock.Setup(e => e.GetEnvironmentVariable(variable)).Returns(value);
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);

        // Assert
        if (value != "0")
        {
            // Message is suppressed.
            outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        }
        else
        {
            // Message is not suppressed.
            outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
        }
    }

    // When set to 1 or true it should suppress the message.
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1")]
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "true")]
    [Arguments(EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "1")]
    [Arguments(EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "true")]

    // When set to 0 it should write the message.
    [Arguments(EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "0")]
    [Arguments(EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "0")]
    public async Task TelemetryManager_UsingTelemetryOptOutShouldDisableTelemetry(string variable, string value)
    {
        // Arrange
        var options = new TestApplicationOptions();
        Assert.IsTrue(options.EnableTelemetry);

        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var outputDevice = new Mock<IOutputDevice>();
        var commandLineOptions = new Mock<ICommandLineOptions>();

        var testApplicationModuleInfoMock = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfoMock.Setup(a => a.GetCurrentTestApplicationFullPath()).Returns("directory/myExe.exe");

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(commandLineOptions.Object);
        serviceProvider.AddService(fileSystemMock.Object);
        serviceProvider.AddService(environmentMock.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(testApplicationModuleInfoMock.Object);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var telemetryManager = new TelemetryManager();

        // Act
        environmentMock.Setup(e => e.GetEnvironmentVariable(variable)).Returns(value);
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);

        // Assert
        ITelemetryInformation telemetryInformation = serviceProvider.GetRequiredService<ITelemetryInformation>();

        if (value != "0")
        {
            // Telemetry is suppressed.
            Assert.IsFalse(telemetryInformation.IsEnabled);
            outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        }
        else
        {
            // Telemetry is not suppressed.
            Assert.IsTrue(telemetryInformation.IsEnabled);
            outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
        }
    }

    [Arguments("LOCALAPPDATA")]
    [Arguments("HOME")]
    public async Task TelemetryManager_SentinelIsWrittenPerUserAndAvoidsShowingNoticeOnSubsequentRuns(string variable)
    {
        // Arrange
        var options = new TestApplicationOptions();
        Assert.IsTrue(options.EnableTelemetry);

        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var commandLineOptions = new Mock<ICommandLineOptions>();
        environmentMock.Setup(s => s.GetEnvironmentVariable(variable)).Returns("sentinelDir");
        var outputDevice = new Mock<IOutputDevice>();

        var testApplicationModuleInfoMock = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfoMock.Setup(a => a.GetCurrentTestApplicationFullPath()).Returns("directory/myExe.exe");

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(commandLineOptions.Object);
        serviceProvider.AddService(fileSystemMock.Object);
        serviceProvider.AddService(environmentMock.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(testApplicationModuleInfoMock.Object);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var telemetryManager = new TelemetryManager();

        // Act
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);

        // Assert
        ITelemetryInformation telemetryInformation = serviceProvider.GetRequiredService<ITelemetryInformation>();

        Assert.IsTrue(telemetryInformation.IsEnabled, "telemetry is enabled");

        // Combination of where LOCALAPPDATA or HOME is, the name of the exe and our file extension.
        string path = Path.Combine("sentinelDir", "Microsoft", "TestingPlatform", "myExe.testingPlatformFirstTimeUseSentinel");
        fileSystemMock.Verify(f => f.Exists(path), Times.Once);

        // Message was written to screen.
        outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);

        // And sentinel was written to filesystem.
        fileSystemMock.Verify(f => f.NewFileStream(path, It.IsAny<FileMode>(), It.IsAny<FileAccess>()), Times.Once);

        // Act - on next run the file already exists.
        outputDevice.Invocations.Clear();
        fileSystemMock.Invocations.Clear();

        fileSystemMock.Setup(f => f.Exists(path)).Returns(true);
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);
        fileSystemMock.Verify(f => f.Exists(path), Times.Once);

        // Message is not written to screen.
        outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);

        // And sentinel is not written to filesystem, because it is already there.
        fileSystemMock.Verify(f => f.NewFileStream(path, It.IsAny<FileMode>(), It.IsAny<FileAccess>()), Times.Never);
    }

    [Arguments("LOCALAPPDATA")]
    [Arguments("HOME")]
    public async Task TelemetryManager_SentinelIsWrittenOnlyWhenUserWouldSeeTheMessage(string variable)
    {
        // Arrange
        var options = new TestApplicationOptions();
        Assert.IsTrue(options.EnableTelemetry);

        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var commandLineOptions = new Mock<ICommandLineOptions>();
        environmentMock.Setup(s => s.GetEnvironmentVariable(variable)).Returns("sentinelDir");
        var outputDevice = new Mock<IOutputDevice>();

        var testApplicationModuleInfoMock = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfoMock.Setup(a => a.GetCurrentTestApplicationFullPath()).Returns("directory/myExe.exe");

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(commandLineOptions.Object);
        serviceProvider.AddService(fileSystemMock.Object);
        serviceProvider.AddService(environmentMock.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(testApplicationModuleInfoMock.Object);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var telemetryManager = new TelemetryManager();

        // Act
        // Disable showing the telemetry message.
        environmentMock.Setup(s => s.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER)).Returns("1");
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);

        // Assert
        ITelemetryInformation telemetryInformation = serviceProvider.GetRequiredService<ITelemetryInformation>();

        Assert.IsTrue(telemetryInformation.IsEnabled, "telemetry is enabled");

        // Combination of where LOCALAPPDATA or HOME is, the name of the exe and our file extension.
        string path = Path.Combine("sentinelDir", "Microsoft", "TestingPlatform", "myExe.testingPlatformFirstTimeUseSentinel");

        // We should not check for the sentinel, because we disabled the logo.
        fileSystemMock.Verify(f => f.Exists(path), Times.Never);

        // Message was not written to screen.
        outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);

        // And sentinel was not written to filesystem.
        fileSystemMock.Verify(f => f.NewFileStream(path, It.IsAny<FileMode>(), It.IsAny<FileAccess>()), Times.Never);

        // Act - on next run the file should not exist, and we should show it to the user, unless they specify NO_LOGO again.
        outputDevice.Invocations.Clear();
        fileSystemMock.Invocations.Clear();

        // Enable showing the telemetry message.
        environmentMock.Setup(s => s.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER)).Returns("0");

        fileSystemMock.Setup(f => f.Exists(path)).Returns(false);
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);
        fileSystemMock.Verify(f => f.Exists(path), Times.Once);

        // Message is written to screen.
        outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);

        // And sentinel is written to filesystem, because in the first run the user would not see the message, so we should not write sentinel.
        fileSystemMock.Verify(f => f.NewFileStream(path, It.IsAny<FileMode>(), It.IsAny<FileAccess>()), Times.Once);
    }

    public async Task TelemetryManager_UsingNoBannerCommandLine_ShouldSuppressTelemetryMessage()
    {
        var options = new TestApplicationOptions();
        Assert.IsTrue(options.EnableTelemetry);

        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var outputDevice = new Mock<IOutputDevice>();
        var commandLineOptions = new Mock<ICommandLineOptions>();
        commandLineOptions.Setup(c => c.IsOptionSet(PlatformCommandLineProvider.NoBannerOptionKey)).Returns(true);

        var testApplicationModuleInfoMock = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfoMock.Setup(a => a.GetCurrentTestApplicationFullPath()).Returns("directory/myExe.exe");

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(commandLineOptions.Object);
        serviceProvider.AddService(fileSystemMock.Object);
        serviceProvider.AddService(environmentMock.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(testApplicationModuleInfoMock.Object);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var telemetryManager = new TelemetryManager();
        await telemetryManager.BuildAsync(serviceProvider, loggerFactoryMock.Object, options);

        outputDevice.Verify(c => c.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
    }
}

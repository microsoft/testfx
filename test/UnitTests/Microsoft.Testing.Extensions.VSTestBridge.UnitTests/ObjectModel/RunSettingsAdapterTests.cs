// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public sealed class RunSettingsAdapterTests
{
    [TestMethod]
    [DataRow("DotnetHostPath")]
    [DataRow("MaxCpuCount")]
    [DataRow("TargetFrameworkVersion")]
    [DataRow("TargetPlatform")]
    [DataRow("TestAdaptersPaths")]
    [DataRow("TestSessionTimeout")]
    [DataRow("TreatNoTestsAsError")]
    [DataRow("TreatTestAdapterErrorsAsWarnings")]
    public void Constructor_WhenRunConfigurationContainsUnsupportedSetting_LogsWarning(string settingName)
    {
        const string filePath = "test.runsettings";
        string runSettings = $"""
            <RunSettings>
                <RunConfiguration>
                    <{settingName}>value</{settingName}>
                </RunConfiguration>
            </RunSettings>
            """;
        var commandLineOptions = new Mock<ICommandLineOptions>();
        commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(It.IsAny<string>(), out It.Ref<string[]?>.IsAny))
            .Returns((string optionName, out string[]? arguments) =>
            {
                arguments = optionName == RunSettingsCommandLineOptionsProvider.RunSettingsOptionName
                    ? [filePath]
                    : null;
                return arguments is not null;
            });
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(filePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllText(filePath)).Returns(runSettings);
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("TestResults");
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var messageLogger = new Mock<IMessageLogger>();

        var adapter = new RunSettingsAdapter(
            commandLineOptions.Object,
            fileSystem.Object,
            configuration.Object,
            new ClientInfoService("client", "1.0.0", new ClientCapabilitiesService(DeclaredIsStateful: false)),
            loggerFactory.Object,
            messageLogger.Object);

        messageLogger.Verify(
            x => x.SendMessage(
                TestMessageLevel.Warning,
                string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, settingName)),
            Times.Once);
        logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                $"Execution will use the following runsettings:{Environment.NewLine}{adapter.SettingsXml}",
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }
}

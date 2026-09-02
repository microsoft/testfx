// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestRunSettingsTests : TestContainer
{
    public void StatefulNonVisualStudioClientSetsDesignMode()
        => GetDesignMode("custom-client", isStateful: true).Should().BeTrue();

    public void StatelessNonVisualStudioClientDoesNotSetDesignMode()
        => GetDesignMode("custom-client", isStateful: false).Should().BeFalse();

    public void StatelessVisualStudioClientSetsDesignModeForBackwardCompatibility()
        => GetDesignMode(WellKnownClients.VisualStudio, isStateful: false).Should().BeTrue();

    private static bool GetDesignMode(string clientId, bool isStateful)
    {
        const string RunSettingsFilePath = "settings.runsettings";
        string[]? runSettingsFilePaths = [RunSettingsFilePath];
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions
            .Setup(options => options.TryGetOptionArgumentList(
                MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName,
                out runSettingsFilePaths))
            .Returns(true);

        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(fileSystem => fileSystem.ExistFile(RunSettingsFilePath)).Returns(true);
        fileSystem.Setup(fileSystem => fileSystem.ReadAllText(RunSettingsFilePath)).Returns("<RunSettings />");

        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns("TestResults");

        MSTestRunSettings runSettings = new(
            commandLineOptions.Object,
            fileSystem.Object,
            configuration.Object,
            new ClientInfoService(clientId, "1.0.0", new ClientCapabilitiesService(isStateful)),
            new Mock<IMessageLogger>().Object);

        var document = XDocument.Parse(runSettings.SettingsXml!);
        return bool.Parse(document.XPathSelectElement("RunSettings/RunConfiguration/DesignMode")!.Value);
    }
}
#endif

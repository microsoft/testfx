// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestRunSettingsTests : TestContainer
{
    public void TestParametersOverrideExactNamesPreserveEqualsAndRetainUnchangedXml()
    {
        const string Settings = """
            <RunSettings>
              <RunConfiguration>
                <DesignMode>false</DesignMode>
                <CollectSourceInformation>true</CollectSourceInformation>
                <ResultsDirectory>chosen-results</ResultsDirectory>
              </RunConfiguration>
              <TestRunParameters>
                <!-- preserved -->
                <Parameter name="existing" value="old" custom="retained" />
                <Parameter name="untouched" value="original" />
                <Parameter value="nameless" />
              </TestRunParameters>
            </RunSettings>
            """;
        var logger = new Mock<IMessageLogger>(MockBehavior.Strict);
        MSTestRunSettings settings = CreateRunSettings(
            Settings, logger.Object, testRunParameters: ["existing=first=second", "new=one=two", "Existing=case-sensitive"]);
        var document = XDocument.Parse(settings.SettingsXml!);
        XElement configuration = document.Root!.Element("RunConfiguration")!;
        configuration.Elements().Select(element => (element.Name.LocalName, element.Value)).Should().Equal(
            ("DesignMode", "false"), ("CollectSourceInformation", "true"), ("ResultsDirectory", "chosen-results"));
        configuration.Nodes().OfType<XComment>().Should().BeEmpty();
        XElement parameters = document.Root.Element("TestRunParameters")!;
        parameters.Elements().Select(element => (element.Attribute("name")?.Value, element.Attribute("value")!.Value)).Should().Equal(
            ("existing", "first=second"), ("untouched", "original"), (null, "nameless"),
            ("new", "one=two"), ("Existing", "case-sensitive"));
        parameters.Element("Parameter")!.Attribute("custom")!.Value.Should().Be("retained");
        parameters.Nodes().OfType<XComment>().Select(comment => comment.Value.Trim()).Should().Equal("preserved");
        logger.VerifyNoOtherCalls();
    }

    public void TestParametersCreateMissingSectionWithoutReplacingMtpDefaults()
    {
        MSTestRunSettings settings = CreateRunSettings(
            "<RunSettings />", new Mock<IMessageLogger>(MockBehavior.Strict).Object,
            testRunParameters: ["empty=", "url=value=tail"]);
        var document = XDocument.Parse(settings.SettingsXml!);
        document.Root!.Elements().Select(element => element.Name.LocalName).Should().Equal("RunConfiguration", "TestRunParameters");
        document.Root.Element("TestRunParameters")!.Elements()
            .Select(element => (element.Attribute("name")!.Value, element.Attribute("value")!.Value))
            .Should().Equal(("empty", string.Empty), ("url", "value=tail"));
        XElement configuration = document.Root.Element("RunConfiguration")!;
        configuration.Element("DesignMode")!.Value.Should().Be("false");
        configuration.Element("CollectSourceInformation")!.Value.Should().Be("false");
        configuration.Element("ResultsDirectory")!.Value.Should().Be("TestResults");
        configuration.Nodes().OfType<XComment>().Select(comment => comment.Value).Should().Equal(
            "Default configuration added by Microsoft Testing Platform", "End");
    }

    public void MissingRunSettingsRootReportsExactContractError()
    {
        Action construct = () => CreateRunSettings("<Unexpected />", new Mock<IMessageLogger>(MockBehavior.Strict).Object);
        construct.Should().ThrowExactly<InvalidOperationException>().WithMessage(PlatformAdapterResources.MissingRunSettingsAttribute);
    }

    public void StatefulNonVisualStudioClientSetsDesignMode()
        => GetDesignMode("custom-client", isStateful: true).Should().BeTrue();

    public void StatelessNonVisualStudioClientDoesNotSetDesignMode()
        => GetDesignMode("custom-client", isStateful: false).Should().BeFalse();

    public void UndeclaredNonVisualStudioClientDoesNotSetDesignMode()
        => GetDesignMode("custom-client", isStateful: null).Should().BeFalse();

    public void UndeclaredVisualStudioClientSetsDesignModeForBackwardCompatibility()
        => GetDesignMode(WellKnownClients.VisualStudio, isStateful: null).Should().BeTrue();

    public void StatelessVisualStudioClientDoesNotSetDesignMode()
        => GetDesignMode(WellKnownClients.VisualStudio, isStateful: false).Should().BeFalse();

    public void UnsupportedCustomSectionsAreWarnedAndPreserved()
    {
        const string SettingsXml = """
            <RunSettings>
              <!-- Only top-level sections should be reported. -->
              <Playwright>
                <BrowserName>firefox</BrowserName>
              </Playwright>
              <CustomSettings>
                <NestedSettings />
              </CustomSettings>
            </RunSettings>
            """;
        List<string> warnings = [];
        Mock<IMessageLogger> messageLogger = new(MockBehavior.Strict);
        messageLogger
            .Setup(logger => logger.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()))
            .Callback<TestMessageLevel, string>((_, message) => warnings.Add(message));

        MSTestRunSettings runSettings = CreateRunSettings(SettingsXml, messageLogger.Object);

        warnings.Should().Equal(
            string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.UnsupportedRunsettingsSection, "Playwright"),
            string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.UnsupportedRunsettingsSection, "CustomSettings"));
        var document = XDocument.Parse(runSettings.SettingsXml!);
        document.Element("RunSettings")!.Element("Playwright")!.ToString()
            .Should().Be(XDocument.Parse(SettingsXml).Root!.Element("Playwright")!.ToString());
        document.Element("RunSettings")!.Element("CustomSettings")!.ToString()
            .Should().Be(XDocument.Parse(SettingsXml).Root!.Element("CustomSettings")!.ToString());
    }

    public void ConsumedSectionsDoNotProduceWarnings()
    {
        foreach (string settingsName in new[] { "MSTest", "MSTestV2", "mstest", "mstestv2" })
        {
            string settingsXml = $"""
                <RunSettings>
                  <RunConfiguration>
                    <TestCaseFilter>TestCategory=Smoke</TestCaseFilter>
                  </RunConfiguration>
                  <{settingsName}>
                    <CaptureTraceOutput>false</CaptureTraceOutput>
                  </{settingsName}>
                  <TestRunParameters>
                    <Parameter name="browser" value="firefox" />
                  </TestRunParameters>
                  <testrunparameters />
                </RunSettings>
                """;
            Mock<IMessageLogger> messageLogger = new(MockBehavior.Strict);

            _ = CreateRunSettings(settingsXml, messageLogger.Object);

            messageLogger.VerifyNoOtherCalls();
        }
    }

    public void EmptyRunSettingsDoNotProduceWarnings()
    {
        Mock<IMessageLogger> messageLogger = new(MockBehavior.Strict);

        _ = CreateRunSettings("<RunSettings />", messageLogger.Object);

        messageLogger.VerifyNoOtherCalls();
    }

    public void KnownUnsupportedSectionsKeepExistingWarnings()
    {
        foreach ((string loggerSection, string dataCollectorSection) in new[]
        {
            ("LoggerRunSettings", "DataCollectionRunSettings"),
            ("loggerrunsettings", "datacollectionrunsettings"),
            ("LoGgErRuNsEtTiNgS", "DaTaCoLlEcTiOnRuNsEtTiNgS"),
        })
        {
            string settingsXml = $"""
                <RunSettings>
                  <{loggerSection} />
                  <{dataCollectorSection} />
                  <RunConfiguration>
                    <MaxCpuCount>2</MaxCpuCount>
                  </RunConfiguration>
                </RunSettings>
                """;
            List<string> warnings = [];
            Mock<IMessageLogger> messageLogger = new(MockBehavior.Strict);
            messageLogger
                .Setup(logger => logger.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()))
                .Callback<TestMessageLevel, string>((_, message) => warnings.Add(message));

            _ = CreateRunSettings(settingsXml, messageLogger.Object);

            warnings.Should().Equal(
                PlatformAdapterResources.UnsupportedRunsettingsLoggers,
                PlatformAdapterResources.UnsupportedRunsettingsDatacollectors,
                string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.UnsupportedRunconfigurationSetting, "MaxCpuCount"));
        }
    }

    private static bool GetDesignMode(string clientId, bool? isStateful)
    {
        MSTestRunSettings runSettings = CreateRunSettings("<RunSettings />", new Mock<IMessageLogger>().Object, clientId, isStateful);
        var document = XDocument.Parse(runSettings.SettingsXml!);
        return bool.Parse(document.XPathSelectElement("RunSettings/RunConfiguration/DesignMode")!.Value);
    }

    private static MSTestRunSettings CreateRunSettings(string settingsXml, IMessageLogger messageLogger, string clientId = "custom-client", bool? isStateful = null, string[]? testRunParameters = null)
    {
        const string RunSettingsFilePath = "settings.runsettings";
        string[]? runSettingsFilePaths = [RunSettingsFilePath];
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions
            .Setup(options => options.TryGetOptionArgumentList(
                MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName,
                out runSettingsFilePaths))
            .Returns(true);
        commandLineOptions.Setup(options => options.TryGetOptionArgumentList(
            MSTestTestRunParametersCommandLineOptionsProvider.TestRunParameterOptionName, out testRunParameters))
            .Returns(testRunParameters is not null);

        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(fileSystem => fileSystem.ExistFile(RunSettingsFilePath)).Returns(true);
        fileSystem.Setup(fileSystem => fileSystem.ReadAllText(RunSettingsFilePath)).Returns(settingsXml);

        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns("TestResults");

        return new(
            commandLineOptions.Object,
            fileSystem.Object,
            configuration.Object,
            new ClientInfoService(clientId, "1.0.0", new ClientCapabilitiesService(isStateful)),
            messageLogger);
    }
}
#endif

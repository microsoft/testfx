﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class RunConfigurationSettingsTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IDiscoveryContext> _mockDiscoveryContext;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    public RunConfigurationSettingsTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _mockMessageLogger = new Mock<IMessageLogger>();

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    #region ConfigurationSettings tests

    public void ConfigurationSettingsShouldReturnDefaultSettingsIfNotSet()
    {
        MSTestSettings.Reset();
        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;

        settings.Should().NotBeNull();

        // Validating the default value of a random setting.
        settings.ExecutionApartmentState.Should().BeNull();
    }

    #endregion

    #region PopulateSettings tests.

    public void PopulateSettingsShouldInitializeDefaultConfigurationSettingsWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings(null, _mockMessageLogger.Object, null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        settings.ExecutionApartmentState.Should().BeNull();
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
    {
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        settings.ExecutionApartmentState.Should().BeNull();
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
    {
        _mockDiscoveryContext.Setup(md => md.RunSettings!.SettingsXml).Returns(string.Empty);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        settings.ExecutionApartmentState.Should().BeNull();
    }

    public void PopulateSettingsShouldInitializeSettingsToDefaultIfNotSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <FooUnit>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </FooUnit>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        settings.Should().NotBeNull();

        // Validating the default value of a random setting.
        settings.ExecutionApartmentState.Should().BeNull();
    }

    public void PopulateSettingsShouldInitializeSettingsFromRunConfigurationSection()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <RunConfiguration>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
                <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
              </RunConfiguration>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        settings.Should().NotBeNull();

        // Validating the default value of a random setting.
        settings.ExecutionApartmentState.Should().Be(ApartmentState.STA);
    }

    #endregion

    #region ConfigJson
    public void PopulateRunConfigurationSettingsFromJson_ShouldInitializeSettingsCorrectly()
    {
        // Arrange
        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:execution:executionApartmentState", "STA" },
        };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        var settings = new RunConfigurationSettings();

        // Act
        RunConfigurationSettings.SetRunConfigurationSettingsFromConfig(mockConfig.Object, settings);

        // Assert
        settings.Should().NotBeNull();
        settings.ExecutionApartmentState.Should().Be(ApartmentState.STA);
    }

    #endregion
}

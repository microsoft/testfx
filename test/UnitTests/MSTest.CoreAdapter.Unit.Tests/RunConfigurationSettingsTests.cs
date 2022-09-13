// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

public class RunConfigurationSettingsTests : TestContainer
{
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private Mock<IDiscoveryContext> _mockDiscoveryContext;
    private Mock<IRunSettings> _mockRunSettings;
    private Mock<IMessageLogger> _mockMessageLogger;

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
        PlatformServiceProvider.Instance = null;
    }

    #region Property validation.

    public void CollectSourceInformationIsByDefaultTrueWhenNotSpecified()
    {
        string runSettingxml =
            @"<RunSettings>
                  <RunConfiguration>
                  </RunConfiguration>
                  </RunSettings>";

        RunConfigurationSettings configurationSettings = RunConfigurationSettings.GetSettings(runSettingxml, RunConfigurationSettings.SettingsName);
        Verify(configurationSettings.CollectSourceInformation);
    }

    public void CollectSourceInformationShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <CollectSourceInformation>false</CollectSourceInformation>
                     </RunConfiguration>
                </RunSettings>";

        RunConfigurationSettings configurationSettings = RunConfigurationSettings.GetSettings(runSettingxml, RunConfigurationSettings.SettingsName);
        Verify(!configurationSettings.CollectSourceInformation);
    }

    #endregion

    #region ConfigurationSettings tests

    public void ConfigurationSettingsShouldReturnDefaultSettingsIfNotSet()
    {
        MSTestSettings.Reset();
        var settings = MSTestSettings.RunConfigurationSettings;

        Verify(settings is not null);

        // Validating the default value of a random setting.
        Verify(settings.CollectSourceInformation);
    }

    #endregion

    #region PopulateSettings tests.

    public void PopulateSettingsShouldInitializeDefaultConfigurationSettingsWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings((IDiscoveryContext)null);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        Verify(settings.CollectSourceInformation);
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
    {
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        Verify(settings.CollectSourceInformation);
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
    {
        _mockDiscoveryContext.Setup(md => md.RunSettings.SettingsXml).Returns(string.Empty);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        Verify(settings.CollectSourceInformation);
    }

    public void PopulateSettingsShouldInitializeSettingsToDefaultIfNotSpecified()
    {
        string runSettingxml =
        @"<RunSettings>
                 <FooUnit>   
                  <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                 </FooUnit>
               </RunSettings>";

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        Verify(settings is not null);

        // Validating the default value of a random setting.
        Verify(settings.CollectSourceInformation);
    }

    public void PopulateSettingsShouldInitializeSettingsFromRunConfigurationSection()
    {
        string runSettingxml =
        @"<RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <CollectSourceInformation>false</CollectSourceInformation>
                     </RunConfiguration>
              </RunSettings>";

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object);

        RunConfigurationSettings settings = MSTestSettings.RunConfigurationSettings;
        Verify(settings is not null);

        // Validating the default value of a random setting.
        Verify(!settings.CollectSourceInformation);
    }

    #endregion
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class RunConfigurationSettingsTests
    {
        private TestablePlatformServiceProvider testablePlatformServiceProvider;
        private Mock<IDiscoveryContext> mockDiscoveryContext;
        private Mock<IRunSettings> mockRunSettings;
        private Mock<IMessageLogger> mockMessageLogger;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.mockDiscoveryContext = new Mock<IDiscoveryContext>();
            this.mockRunSettings = new Mock<IRunSettings>();
            this.mockMessageLogger = new Mock<IMessageLogger>();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        #region Property validation.

        [TestMethod]
        public void CollectSourceInformationIsByDefaultTrueWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                  <RunConfiguration>
                  </RunConfiguration>
                  </RunSettings>";

            RunConfigurationSettings configurationSettings = RunConfigurationSettings.GetSettings(runSettingxml, RunConfigurationSettings.SettingsName);
            Assert.AreEqual(configurationSettings.CollectSourceInformation, true);
        }

        [TestMethod]
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
            Assert.AreEqual(configurationSettings.CollectSourceInformation, false);
        }

        #endregion

        #region ConfigurationSettings tests

        [TestMethod]
        public void ConfigurationSettingsShouldReturnDefaultSettingsIfNotSet()
        {
            RunConfigurationSettings.Reset();
            var settings = RunConfigurationSettings.ConfigurationSettings;

            Assert.IsNotNull(settings);

            // Validating the default value of a random setting.
            Assert.AreEqual(settings.CollectSourceInformation, true);
        }

        #endregion

        #region PopulateSettings tests.

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultConfigurationSettingsWhenDiscoveryContextIsNull()
        {
            RunConfigurationSettings.PopulateSettings((IDiscoveryContext)null);

            RunConfigurationSettings settings = RunConfigurationSettings.ConfigurationSettings;
            Assert.AreEqual(settings.CollectSourceInformation, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
        {
            RunConfigurationSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            RunConfigurationSettings settings = RunConfigurationSettings.ConfigurationSettings;
            Assert.AreEqual(settings.CollectSourceInformation, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
        {
            this.mockDiscoveryContext.Setup(md => md.RunSettings.SettingsXml).Returns(string.Empty);
            RunConfigurationSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            RunConfigurationSettings settings = RunConfigurationSettings.ConfigurationSettings;
            Assert.AreEqual(settings.CollectSourceInformation, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeSettingsToDefaultIfNotSpecified()
        {
            string runSettingxml =
            @"<RunSettings>
                 <FooUnit>   
                  <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                 </FooUnit>
               </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            RunConfigurationSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            RunConfigurationSettings settings = RunConfigurationSettings.ConfigurationSettings;
            Assert.IsNotNull(settings);

            // Validating the default value of a random setting.
            Assert.AreEqual(settings.CollectSourceInformation, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeSettingsFromRunConfigurationSection()
        {
            string runSettingxml =
            @"<RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <CollectSourceInformation>false</CollectSourceInformation>
                     </RunConfiguration>
              </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            RunConfigurationSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            RunConfigurationSettings settings = RunConfigurationSettings.ConfigurationSettings;
            Assert.IsNotNull(settings);

            // Validating the default value of a random setting.
            Assert.AreEqual(settings.CollectSourceInformation, false);
        }

        #endregion
    }
}
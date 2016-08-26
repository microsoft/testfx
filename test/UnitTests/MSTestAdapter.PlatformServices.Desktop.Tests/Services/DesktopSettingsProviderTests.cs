// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    using System;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using MSTestAdapter.TestUtilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopSettingsProviderTests
    {
        private MSTestSettingsProvider settingsProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.settingsProvider = new MSTestSettingsProvider();
            MSTestSettingsProvider.Reset();
        }

        [TestMethod]
        public void GetPropertiesShouldReturnDeploymentInformation()
        {
            var properties = this.settingsProvider.GetProperties();

            Assert.IsNotNull(properties);
            Assert.IsTrue(properties.Count > 0);
        }

        [TestMethod]
        public void SettingsShouldReturnDefaultSettingsIfNotInitialized()
        {
            var settings = MSTestSettingsProvider.Settings;

            Assert.IsNotNull(settings);
            Assert.AreEqual(true, settings.DeploymentEnabled);
        }

        [TestMethod]
        public void SettingsShouldReturnInitializedSettings()
        {
            string runSettingxml =
                @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            this.settingsProvider.Load(reader);
            Assert.AreEqual(MSTestSettingsProvider.Settings.DeploymentEnabled, false);
        }

        [TestMethod]
        public void LoadShouldThrowIfReaderIsNull()
        {
            ActionUtility.ActionShouldThrowExceptionOfType(() => this.settingsProvider.Load(null), typeof(ArgumentNullException));
        }

        [TestMethod]
        public void LoadShouldReadAndFillInSettings()
        {
            string runSettingxml =
                @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            this.settingsProvider.Load(reader);
            Assert.AreEqual(MSTestSettingsProvider.Settings.DeploymentEnabled, false);
        }

        [TestMethod]
        public void SettingsNameShouldBeMSTestV2()
        {
            Assert.AreEqual("MSTestV2", MSTestSettingsProvider.SettingsName);
        }
    }
}

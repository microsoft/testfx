// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using MSTestAdapter.TestUtilities;

    [TestClass]
#pragma warning disable SA1649 // File name must match first type name
    public class DesktopSettingsProviderTests
#pragma warning restore SA1649 // File name must match first type name
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
            // this is a base case and we just validating that properties does not remain un-initialized,
            // so passing 'null' source will also suffice.
            var properties = this.settingsProvider.GetProperties(null);

            Assert.IsNotNull(properties);
            Assert.IsTrue(properties.Count > 0);
        }

        [TestMethod]
        public void SettingsShouldReturnDefaultSettingsIfNotInitialized()
        {
            var settings = MSTestSettingsProvider.Settings;

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.DeploymentEnabled);
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
            Assert.IsFalse(MSTestSettingsProvider.Settings.DeploymentEnabled);
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
            Assert.IsFalse(MSTestSettingsProvider.Settings.DeploymentEnabled);
        }
    }
}

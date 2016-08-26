// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
    using Moq;
    using System;
    using System.IO;
    using System.Xml;

    [TestClass]
    public class MSTestv1SettingsTests
    {
        private Mock<IMessageLogger> mockMessageLogger;
        private Mock<IDiscoveryContext> mockDiscoveryContext;
        private Mock<IRunSettings> mockRunSettings;

        [TestInitialize]
        public void TestInit()
        {
            this.mockMessageLogger = new Mock<IMessageLogger>();
            this.mockDiscoveryContext = new Mock<IDiscoveryContext>();
            this.mockRunSettings = new Mock<IRunSettings>();
        }

        [TestMethod]
        public void ToSettingsShouldNotThrowExceptionWhenRunSettingsXmlUnderTagMSTestIsWrong()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTest>
                        <ForcedLegacyMode>true</ForcedLegacyMode>
                    </MSTest>
                  </RunSettings>";

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();

            MSTestv1Settings.ToSettings(reader);
        }

        [TestMethod]
        public void ToSettingsReturnsDefaultSettingsWhenMSTestTagIsAbsent()
        {
            string runSettingxml =
                  @"<RunSettings>
                        <xunit>
                        </xunit>
                    </RunSettings>";

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();

            MSTestv1Settings settings = MSTestv1Settings.ToSettings(reader);
            Assert.AreEqual(settings.ForcedLegacyMode, false);
        }

        [TestMethod]
        public void ForcedLegacyModeIsByDefaultFalseWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTest>
                    </MSTest>
                 </RunSettings>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestv1Settings adapterSettings = MSTestv1Settings.ToSettings(reader);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, false);
        }

        [TestMethod]
        public void ForcedLegacyModeShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTest>
                        <ForcedLegacyMode>true</ForcedLegacyMode>
                    </MSTest>
                  </RunSettings>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestv1Settings adapterSettings = MSTestv1Settings.ToSettings(reader);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, true);
        }

        [TestMethod]
        public void TestSettingsFileIsByDefaultNullWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTest>
                    </MSTest>
                 </RunSettings>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestv1Settings adapterSettings = MSTestv1Settings.ToSettings(reader);
            Assert.IsNull(adapterSettings.SettingsFile);
        }

        [TestMethod]
        public void TestSettingsFileShouldNotBeNullWhenSpecifiedInRunSettings()
        {
            string runSettingxml =
            @"<RunSettings>   
			        <MSTest>   
				        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
			        </MSTest>
		    </RunSettings>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestv1Settings adapterSettings = MSTestv1Settings.ToSettings(reader);
            Assert.IsNotNull(adapterSettings.SettingsFile);
        }

        [TestMethod]
        public void isTestSettingsGivenReturnsFalseWhenDiscoveryContextIsNull()
        {
            Assert.IsFalse(MSTestv1Settings.isTestSettingsGiven(null, null));

        }

        [TestMethod]
        public void isTestSettingsGivenReturnsFalseWhenForcedLegacyModeIsSetToFalse()
        {
            string runSettingxml =
            @"<RunSettings>   
			        <MSTest>   
				        <ForcedLegacyMode>False</ForcedLegacyMode> 
			        </MSTest>
		    </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            Assert.IsFalse(MSTestv1Settings.isTestSettingsGiven(this.mockDiscoveryContext.Object, this.mockMessageLogger.Object));
        }

        [TestMethod]
        public void isTestSettingsGivenReturnsTrueWhenForcedLegacyModeIsSetToTrue()
        {
            string runSettingxml =
            @"<RunSettings>   
			        <MSTest>   
				        <ForcedLegacyMode>true</ForcedLegacyMode> 
			        </MSTest>
		    </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            Assert.IsTrue(MSTestv1Settings.isTestSettingsGiven(this.mockDiscoveryContext.Object, this.mockMessageLogger.Object));
        }

        [TestMethod]
        public void isTestSettingsGivenReturnsTrueWhenTestSettingsFileIsGiven()
        {
            string runSettingxml =
            @"<RunSettings>   
			        <MSTest>   
				        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile> 
			        </MSTest>
		    </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            Assert.IsTrue(MSTestv1Settings.isTestSettingsGiven(this.mockDiscoveryContext.Object, this.mockMessageLogger.Object));
        }
    }
}

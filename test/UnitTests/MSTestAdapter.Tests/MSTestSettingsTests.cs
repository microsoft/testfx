using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
    using Moq;
    using MSTest.TestAdapter;
    using System.IO;
    using System.Xml;
    using TestableImplementations;
    using TestPlatform.ObjectModel.Adapter;
    using TestPlatform.ObjectModel.Utilities;

    [TestClass]
    public class MSTestAdapterSettingsProvidersTests
    {
        private TestablePlatformServiceProvider testablePlatformServiceProvider;
        private Mock<IDiscoveryContext> mockDiscoveryContext;
        private Mock<IRunSettings> mockRunSettings;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.mockDiscoveryContext = new Mock<IDiscoveryContext>();
            this.mockRunSettings = new Mock<IRunSettings>();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }


        [TestMethod]
        public void MapInconclusiveToFailedIsByDefaultFalseWhenNotSpecified()
        {
            string runSettingxml =
                @"<MSTestV2>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettings adapterSettings = MSTestSettings.ToSettings(reader);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);

        }

        [TestMethod]
        public void MapInconclusiveToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<MSTestV2>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettings adapterSettings = MSTestSettings.ToSettings(reader);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);

        }

        [TestMethod]
        public void LoadIsCalledForPlatformSpecificSettings()
        {
            string runSettingxml =
                 @"<MSTestV2>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                  </MSTestV2>";

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader actualReader) =>
                {
                    if (actualReader != null)
                    {
                        actualReader.Read();
                        actualReader.ReadInnerXml();
                    }
                });

            MSTestSettings adapterSettings = MSTestSettings.ToSettings(reader);
            this.testablePlatformServiceProvider.MockSettingsProvider.Verify(sp => sp.Load(It.IsAny<XmlReader>()), Times.Once);
        }

        [TestMethod]
        public void LoadReceivesOnlySubTreeOfOneElement()
        {
            string runSettingxml =
                  @"<MSTestV2>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

            string expectedrunSettingxml = @"<DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>";
            StringReader expectedStringReader = new StringReader(expectedrunSettingxml);
            XmlReader expectedXmlReader = XmlReader.Create(expectedStringReader, XmlRunSettingsUtilities.ReaderSettings);
            expectedXmlReader.Read();


            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader actualReader) => 
                {
                    if (actualReader != null)
                    {
                        actualReader.Read();
                        Assert.AreEqual(actualReader.ReadOuterXml(), expectedXmlReader.ReadOuterXml());
                    }
                 }); 

            MSTestSettings adapterSettings = MSTestSettings.ToSettings(reader);

        }

        [TestMethod]
        public void GetSettingsReturnsDefaultAdapterSettingsWhenDiscoveryContextIsNull()
        {
            MSTestSettings adapterSettings = MSTestSettings.GetSettings(null);
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);

        }

        [TestMethod]
        public void GetSettingsReturnsDefaultSettingsWhenRunSettingsIsNull()
        {
            MSTestSettings adapterSettings = MSTestSettings.GetSettings(this.mockDiscoveryContext.Object);
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
        }

        [TestMethod]
        public void GetSettingsReturnsDefaultSettingIfSettingsProviderIsNull()
        {
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            MSTestAdapterSettingsProvider settingsProvider = null;
            this.mockRunSettings.Setup(rs => rs.GetSettings("MSTestV2")).Returns(settingsProvider);
            MSTestSettings adapterSettings = MSTestSettings.GetSettings(this.mockDiscoveryContext.Object);
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
        }

        [TestMethod]
        public void GetSettingsReturnsProvidedSettings()
        {
            string runSettingxml =
                  @"<MSTestV2>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                  </MSTestV2>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            MSTestAdapterSettingsProvider settingsProvider = new MSTestAdapterSettingsProvider();
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            settingsProvider.Load(reader);
            this.mockRunSettings.Setup(rs => rs.GetSettings("MSTestV2")).Returns(settingsProvider);
            MSTestSettings adapterSettings = MSTestSettings.GetSettings(this.mockDiscoveryContext.Object);
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
        }

    }
}

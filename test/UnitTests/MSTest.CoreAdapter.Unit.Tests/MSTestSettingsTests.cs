// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Xml;
    using global::MSTestAdapter.TestUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MSTestSettingsTests
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
        public void MapInconclusiveToFailedIsByDefaultFalseWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
        }

        [TestMethod]
        public void MapInconclusiveToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
        }

        [TestMethod]
        public void ForcedLegacyModeIsByDefaultFalseWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTest>
                    </MSTest>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

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

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

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

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            Assert.IsNull(adapterSettings.TestSettingsFile);
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

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            Assert.IsNotNull(adapterSettings.TestSettingsFile);
        }

        [TestMethod]
        public void EnableBaseClassTestMethodsFromOtherAssembliesIsByDefaulTrueWhenNotSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
        }

        [TestMethod]
        public void EnableBaseClassTestMethodsFromOtherAssembliesShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <EnableBaseClassTestMethodsFromOtherAssemblies>True</EnableBaseClassTestMethodsFromOtherAssemblies>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
        }

        [TestMethod]
        public void CaptureDebugTracesShouldBeTrueByDefault()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
        }

        [TestMethod]
        public void CaptureDebugTracesShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <CaptureTraceOutput>False</CaptureTraceOutput>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(adapterSettings.CaptureDebugTraces, false);
        }

        [TestMethod]
        public void ParallelizationSettingsShouldNotBeSetByDefault()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.IsFalse(adapterSettings.ParallelizationWorkers.HasValue);
            Assert.IsFalse(adapterSettings.ParallelizationScope.HasValue);
        }

        [TestMethod]
        public void GetSettingsShouldThrowIfParallelizationWorkersIsNotInt()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>GoneFishing</Workers>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            var exception = ActionUtility.PerformActionAndReturnException(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof(AdapterSettingsException).FullName, exception.GetType().FullName);
            StringAssert.Contains(exception.Message, "Invalid value 'GoneFishing' specified for 'Workers'. The value should be a non-negative integer.");
        }

        [TestMethod]
        public void GetSettingsShouldThrowIfParallelizationWorkersIsNegative()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>-1</Workers>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            var exception = ActionUtility.PerformActionAndReturnException(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof(AdapterSettingsException).FullName, exception.GetType().FullName);
            StringAssert.Contains(exception.Message, "Invalid value '-1' specified for 'Workers'. The value should be a non-negative integer.");
        }

        [TestMethod]
        public void ParallelizationWorkersShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>2</Workers>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(2, adapterSettings.ParallelizationWorkers);
        }

        [TestMethod]
        public void ParallelizationWorkersShouldBeSetToProcessorCountWhenSetToZero()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>0</Workers>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(Environment.ProcessorCount, adapterSettings.ParallelizationWorkers);
        }

        [TestMethod]
        public void ParallelizationSettingsShouldBeSetToDefaultsWhenNotSet()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(Environment.ProcessorCount, adapterSettings.ParallelizationWorkers);
            Assert.AreEqual(UTF.ExecutionScope.ClassLevel, adapterSettings.ParallelizationScope);
        }

        [TestMethod]
        public void ParallelizationSettingsShouldBeSetToDefaultsOnAnEmptyParalleizeSetting()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize/>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(Environment.ProcessorCount, adapterSettings.ParallelizationWorkers);
            Assert.AreEqual(UTF.ExecutionScope.ClassLevel, adapterSettings.ParallelizationScope);
        }

        [TestMethod]
        public void ParallelizationSettingsShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>127</Workers>
                            <Scope>MethodLevel</Scope>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(127, adapterSettings.ParallelizationWorkers);
            Assert.AreEqual(UTF.ExecutionScope.MethodLevel, adapterSettings.ParallelizationScope);
        }

        [TestMethod]
        public void GetSettingsShouldThrowIfParallelizationScopeIsNotValid()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Scope>JustParallelizeWillYou</Scope>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            var exception = ActionUtility.PerformActionAndReturnException(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof(AdapterSettingsException).FullName, exception.GetType().FullName);
            StringAssert.Contains(exception.Message, "Invalid value 'JustParallelizeWillYou' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel.");
        }

        [TestMethod]
        public void ParallelizationScopeShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Scope>MethodLevel</Scope>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.AreEqual(UTF.ExecutionScope.MethodLevel, adapterSettings.ParallelizationScope);
        }

        [TestMethod]
        public void GetSettingsShouldThrowWhenParallelizeHasInvalidElements()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Hola>Hi</Hola>
                        </Parallelize>
                    </MSTestV2>
                  </RunSettings>";

            var exception = ActionUtility.PerformActionAndReturnException(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof(AdapterSettingsException).FullName, exception.GetType().FullName);
            StringAssert.Contains(exception.Message, "Invalid settings 'Parallelize'. Unexpected XmlElement: 'Hola'.");
        }

        [TestMethod]
        public void GetSettingsShouldBeAbleToReadAfterParallelizationSettings()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                        </Parallelize>
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.IsNotNull(adapterSettings.TestSettingsFile);
        }

        [TestMethod]
        public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsWithData()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize>
                            <Workers>127</Workers>
                            <Scope>MethodLevel</Scope>
                        </Parallelize>
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.IsNotNull(adapterSettings.TestSettingsFile);
            Assert.AreEqual(127, adapterSettings.ParallelizationWorkers);
            Assert.AreEqual(UTF.ExecutionScope.MethodLevel, adapterSettings.ParallelizationScope);
        }

        [TestMethod]
        public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsOnEmptyParallelizationNode()
        {
            string runSettingxml =
                @"<RunSettings>
                    <MSTestV2>
                        <Parallelize/>
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            Assert.IsNotNull(adapterSettings.TestSettingsFile);
        }

        [TestMethod]
        public void DisableParallelizationShouldBeFalseByDefault()
        {
            string runSettingxml =
                @"<RunSettings>
                  </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            Assert.IsFalse(MSTestSettings.CurrentSettings.DisableParallelization);
        }

        [TestMethod]
        public void DisableParallelizationShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<RunSettings>
                    <RunConfiguration>
                        <DisableParallelization>True</DisableParallelization>
                    </RunConfiguration>
                  </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            Assert.IsTrue(MSTestSettings.CurrentSettings.DisableParallelization);
        }

        #endregion

        #region GetSettings Tests

        [TestMethod]
        public void GetSettingsShouldProbePlatformSpecificSettingsAlso()
        {
            string runSettingxml =
                 @"<RunSettings>
                     <MSTest>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                     </MSTest>
                   </RunSettings>";

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader actualReader) =>
                {
                    if (actualReader != null)
                    {
                        actualReader.Read();
                        actualReader.ReadInnerXml();
                    }
                });

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
            this.testablePlatformServiceProvider.MockSettingsProvider.Verify(sp => sp.Load(It.IsAny<XmlReader>()), Times.Once);
        }

        [TestMethod]
        public void GetSettingsShouldOnlyPassTheElementSubTreeToPlatformService()
        {
            string runSettingxml =
                  @"<RunSettings>
                      <MSTest>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                      </MSTest>
                    </RunSettings>";

            string expectedrunSettingxml = @"<DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>";
            string observedxml = null;

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader actualReader) =>
                {
                    if (actualReader != null)
                    {
                        actualReader.Read();
                        observedxml = actualReader.ReadOuterXml();
                    }
                 });

            MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
            Assert.AreEqual(expectedrunSettingxml, observedxml);
        }

        [TestMethod]
        public void GetSettingsShouldBeAbleToReadSettingsAfterThePlatformServiceReadsItsSettings()
        {
            string runSettingxml =
                  @"<RunSettings>
                      <MSTest>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                      </MSTest>
                    </RunSettings>";

            bool dummyPlatformSpecificSetting = false;

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader reader) =>
                {
                    if (reader != null)
                    {
                        reader.Read();
                        while (reader.NodeType == XmlNodeType.Element)
                        {
                            bool result;
                            string elementName = reader.Name.ToUpperInvariant();
                            switch (elementName)
                            {
                                case "DUMMYPLATFORMSPECIFICSETTING":
                                    {
                                        if (bool.TryParse(reader.ReadInnerXml(), out result))
                                        {
                                            dummyPlatformSpecificSetting = result;
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        reader.Skip();
                                        break;
                                    }
                            }
                        }
                    }
                });

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            // Assert.
            Assert.IsTrue(dummyPlatformSpecificSetting);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual("DummyPath\\\\TestSettings1.testsettings", adapterSettings.TestSettingsFile);
        }

        [TestMethod]
        public void GetSettingsShouldBeAbleToReadSettingsIfThePlatformServiceDoesNotUnderstandASetting()
        {
            string runSettingxml =
                  @"<RunSettings>
                      <MSTest>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <UnReadableSetting>foobar</UnReadableSetting>
                        <ForcedLegacyMode>true</ForcedLegacyMode>
                        <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                      </MSTest>
                    </RunSettings>";

            bool dummyPlatformSpecificSetting = false;

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader reader) =>
                {
                    if (reader != null)
                    {
                        reader.Read();
                        while (reader.NodeType == XmlNodeType.Element)
                        {
                            bool result;
                            string elementName = reader.Name.ToUpperInvariant();
                            switch (elementName)
                            {
                                case "DUMMYPLATFORMSPECIFICSETTING":
                                    {
                                        if (bool.TryParse(reader.ReadInnerXml(), out result))
                                        {
                                            dummyPlatformSpecificSetting = result;
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        reader.Skip();
                                        break;
                                    }
                            }
                        }
                    }
                });

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            // Assert.
            Assert.IsTrue(dummyPlatformSpecificSetting);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, true);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
            Assert.AreEqual("DummyPath\\\\TestSettings1.testsettings", adapterSettings.TestSettingsFile);
        }

        [TestMethod]
        public void GetSettingsShouldOnlyReadTheAdapterSection()
        {
            string runSettingxml =
                  @"<RunSettings>
                      <MSTest>
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                      </MSTest>
                      <BadElement>Bad</BadElement>
                    </RunSettings>";

            var outOfScopeCall = false;

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader reader) =>
                {
                    if (reader != null)
                    {
                        reader.Read();
                        while (reader.NodeType == XmlNodeType.Element)
                        {
                            string elementName = reader.Name.ToUpperInvariant();
                            switch (elementName)
                            {
                                case "BADELEMENT":
                                    {
                                        reader.ReadInnerXml();
                                        outOfScopeCall = true;
                                    }
                                    break;
                                default:
                                    {
                                        reader.Skip();
                                        break;
                                    }
                            }
                        }
                    }
                });

            MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            // Assert.
            Assert.IsFalse(outOfScopeCall);
        }

        [TestMethod]
        public void GetSettingsShouldWorkIfThereAreCommentsInTheXML()
        {
            string runSettingxml =
                  @"<RunSettings>
                      <!-- MSTest runsettings -->
                      <MSTest>
                        <!-- Map inconclusive -->
                        <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                        <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                        <!-- Force Legacy mode -->
                        <ForcedLegacyMode>true</ForcedLegacyMode>
                        <!-- Enable base class test methods from other assemblies -->
                        <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                      </MSTest>
                      <BadElement>Bad</BadElement>
                    </RunSettings>";

            bool dummyPlatformSpecificSetting = false;

            this.testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
                .Callback((XmlReader reader) =>
                {
                    if (reader != null)
                    {
                        reader.Read();
                        while (reader.NodeType == XmlNodeType.Element)
                        {
                            bool result;
                            string elementName = reader.Name.ToUpperInvariant();
                            switch (elementName)
                            {
                                case "DUMMYPLATFORMSPECIFICSETTING":
                                    {
                                        if (bool.TryParse(reader.ReadInnerXml(), out result))
                                        {
                                            dummyPlatformSpecificSetting = result;
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        reader.Skip();
                                        break;
                                    }
                            }
                        }
                    }
                });

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);

            // Assert.
            Assert.IsTrue(dummyPlatformSpecificSetting);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, true);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
        }

        #endregion

        #region CurrentSettings tests

        [TestMethod]
        public void CurrentSettingShouldReturnDefaultSettingsIfNotSet()
        {
            MSTestSettings.Reset();
            var adapterSettings = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);

            // Validating the default value of a random setting.
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, false);
        }

        [TestMethod]
        public void CurrentSettingShouldReturnCachedLoadedSettings()
        {
            string runSettingxml =
            @"<RunSettings>
                 <MSTest>   
                    <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                 </MSTest>
               </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            var adapterSettings = MSTestSettings.CurrentSettings;
            var adapterSettings2 = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);
            Assert.IsFalse(string.IsNullOrEmpty(adapterSettings.TestSettingsFile));

            Assert.AreEqual(adapterSettings, adapterSettings2);
        }

        #endregion

        #region PopulateSettings tests.

        [TestMethod]
        public void PopulateSettingsShouldFillInSettingsFromSettingsObject()
        {
            string runsettingsXml =
            @"<RunSettings>
                 <MSTest>
                   <CaptureTraceOutput>False</CaptureTraceOutput> 
                   <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                   <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                   <ForcedLegacyMode>true</ForcedLegacyMode>
                   <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                 </MSTest>
               </RunSettings>";

            var settings = MSTestSettings.GetSettings(runsettingsXml, MSTestSettings.SettingsName);

            MSTestSettings.PopulateSettings(settings);

            Assert.AreEqual(MSTestSettings.CurrentSettings.CaptureDebugTraces, false);
            Assert.AreEqual(MSTestSettings.CurrentSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(MSTestSettings.CurrentSettings.ForcedLegacyMode, true);
            Assert.AreEqual(MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
            Assert.IsFalse(string.IsNullOrEmpty(MSTestSettings.CurrentSettings.TestSettingsFile));
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultAdapterSettingsWhenDiscoveryContextIsNull()
        {
            MSTestSettings.PopulateSettings((IDiscoveryContext)null);

            MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
        {
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
        {
            this.mockDiscoveryContext.Setup(md => md.RunSettings.SettingsXml).Returns(string.Empty);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, false);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
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
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            var adapterSettings = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);

            // Validating the default value of a random setting.
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, false);
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeSettingsFromMSTestSection()
        {
            string runSettingxml =
            @"<RunSettings>
                 <MSTest>
                   <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                   <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                   <ForcedLegacyMode>true</ForcedLegacyMode>
                   <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                 </MSTest>
               </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            var adapterSettings = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);

            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, true);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
            Assert.IsFalse(string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeSettingsFromMSTestV2Section()
        {
            string runSettingxml =
            @"<RunSettings>
                 <MSTestV2>
                   <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                   <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                   <ForcedLegacyMode>true</ForcedLegacyMode>
                   <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                 </MSTestV2>
               </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            var adapterSettings = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);

            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, true);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
            Assert.IsFalse(string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
        }

        [TestMethod]
        public void PopulateSettingsShouldInitializeSettingsFromMSTestV2OverMSTestV1Section()
        {
            string runSettingxml =
            @"<RunSettings>
                 <MSTestV2>
                   <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                   <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                 </MSTestV2>
                 <MSTest>
                   <CaptureDebugTraces>False</CaptureDebugTraces>
                   <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                   <ForcedLegacyMode>true</ForcedLegacyMode>
                 </MSTest>
               </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);

            var adapterSettings = MSTestSettings.CurrentSettings;

            Assert.IsNotNull(adapterSettings);

            Assert.AreEqual(adapterSettings.MapInconclusiveToFailed, true);
            Assert.AreEqual(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies, true);
            Assert.AreEqual(adapterSettings.ForcedLegacyMode, false);
            Assert.AreEqual(adapterSettings.CaptureDebugTraces, true);
            Assert.IsTrue(string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
        }

        #endregion

        #region IsLegacyScenario tests

        [TestMethod]
        public void IsLegacyScenarioReturnsFalseWhenDiscoveryContextIsNull()
        {
            MSTestSettings.PopulateSettings((IDiscoveryContext)null);
            Assert.IsFalse(MSTestSettings.IsLegacyScenario(null));
        }

        [TestMethod]
        public void IsLegacyScenarioReturnsFalseWhenForcedLegacyModeIsSetToFalse()
        {
            string runSettingxml =
            @"<RunSettings>   
               <MSTest>   
                <ForcedLegacyMode>False</ForcedLegacyMode> 
               </MSTest>
          </RunSettings>";

            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);
            Assert.IsFalse(MSTestSettings.IsLegacyScenario(this.mockMessageLogger.Object));
        }

        [TestMethod]
        public void IsLegacyScenarioReturnsTrueWhenForcedLegacyModeIsSetToTrue()
        {
            string runSettingxml =
            @"<RunSettings>   
               <MSTest>   
                <ForcedLegacyMode>true</ForcedLegacyMode> 
               </MSTest>
          </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);
            Assert.IsTrue(MSTestSettings.IsLegacyScenario(this.mockMessageLogger.Object));
        }

        [TestMethod]
        public void IsLegacyScenarioReturnsTrueWhenTestSettingsFileIsGiven()
        {
            string runSettingxml =
            @"<RunSettings>   
               <MSTest>   
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile> 
               </MSTest>
          </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);
            Assert.IsTrue(MSTestSettings.IsLegacyScenario(this.mockMessageLogger.Object));
        }

        [TestMethod]
        public void LegacyScenariosNotSupportedWarningIsPrintedWhenVsmdiFileIsGiven()
        {
            string runSettingxml =
            @"<RunSettings>   
               <MSTest>   
                <SettingsFile>DummyPath\\vsmdiFile.vsmdi</SettingsFile> 
               </MSTest>
          </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);
            Assert.IsTrue(MSTestSettings.IsLegacyScenario(this.mockMessageLogger.Object));
            this.mockMessageLogger.Verify(logger => logger.SendMessage(TestMessageLevel.Warning, Resource.LegacyScenariosNotSupportedWarning), Times.Once);
        }

        #endregion
    }
}

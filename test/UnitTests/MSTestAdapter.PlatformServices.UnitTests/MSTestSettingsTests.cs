// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestSettingsTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IDiscoveryContext> _mockDiscoveryContext;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    public MSTestSettingsTests()
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

    #region Property validation.

    public void MapInconclusiveToFailedIsByDefaultFalseWhenNotSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
    }

    public void MapNotRunnableToFailedIsByDefaultTrueWhenNotSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
    }

    public void MapInconclusiveToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
    }

    public void RunSettings_WithInvalidValues_GettingAWarningForEachInvalidSetting()
    {
        string runSettingsXml =
            """
            <RunSettings>
                <MSTestV2>
                    <CooperativeCancellationTimeout>3</CooperativeCancellationTimeout>
                    <ConsiderFixturesAsSpecialTests>3</ConsiderFixturesAsSpecialTests>
                    <TestCleanupTimeout>timeout</TestCleanupTimeout>
                    <TestInitializeTimeout>timeout</TestInitializeTimeout>
                    <ClassCleanupTimeout>timeout</ClassCleanupTimeout>
                    <ClassInitializeTimeout>timeout</ClassInitializeTimeout>
                    <AssemblyInitializeTimeout>timeout</AssemblyInitializeTimeout>
                    <ConsiderEmptyDataSourceAsInconclusive>3</ConsiderEmptyDataSourceAsInconclusive>
                    <AssemblyCleanupTimeout>timeout</AssemblyCleanupTimeout>
                    <SettingsFile></SettingsFile>
                    <CaptureTraceOutput>3</CaptureTraceOutput>
                    <MapInconclusiveToFailed>3</MapInconclusiveToFailed>
                    <MapNotRunnableToFailed>3</MapNotRunnableToFailed>
                    <TreatDiscoveryWarningsAsErrors>3</TreatDiscoveryWarningsAsErrors>
                    <EnableBaseClassTestMethodsFromOtherAssemblies>3</EnableBaseClassTestMethodsFromOtherAssemblies>
                    <TestTimeout>timeout</TestTimeout>
               </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(16));
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'CooperativeCancellationTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'ClassCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'AssemblyCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'CaptureTraceOutput', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'MapNotRunnableToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'EnableBaseClassTestMethodsFromOtherAssemblies', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'ClassInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'AssemblyInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '' for runsettings entry 'SettingsFile', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'TreatDiscoveryWarningsAsErrors', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'MapInconclusiveToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'ConsiderEmptyDataSourceAsInconclusive', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'ConsiderFixturesAsSpecialTests', setting will be ignored."), Times.Once);
    }

    public void MapNotRunnableToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
    }

    public void TestSettingsFileIsByDefaultNullWhenNotSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
              </MSTest>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        adapterSettings.TestSettingsFile.Should().BeNull();
    }

    public void TestSettingsFileShouldNotBeNullWhenSpecifiedInRunSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        adapterSettings.TestSettingsFile.Should().NotBeNull();
    }

    public void EnableBaseClassTestMethodsFromOtherAssembliesIsByDefaulTrueWhenNotSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
    }

    public void EnableBaseClassTestMethodsFromOtherAssembliesShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>True</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
    }

    public void CaptureDebugTracesShouldBeTrueByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.CaptureDebugTraces.Should().BeTrue();
    }

    public void CaptureDebugTracesShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                  <CaptureTraceOutput>False</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.CaptureDebugTraces.Should().BeFalse();
    }

    public void TestTimeoutShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                  <TestTimeout>4000</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TestTimeout.Should().Be(4000);
    }

    public void TestTimeoutShouldBeSetToZeroIfNotSpecifiedInRunSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TestTimeout.Should().Be(0);
    }

    public void TreatDiscoveryWarningsAsErrorsShouldBeTrueByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
    }

    public void TreatDiscoveryWarningsAsErrorsShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <TreatDiscoveryWarningsAsErrors>True</TreatDiscoveryWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
    }

    public void ParallelizationSettingsShouldNotBeSetByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.HasValue.Should().BeFalse();
        adapterSettings.ParallelizationScope.HasValue.Should().BeFalse();
    }

    public void GetSettingsShouldThrowIfParallelizationWorkersIsNotInt()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>GoneFishing</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Action act = () => MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);
        AdapterSettingsException exception = act.Should().Throw<AdapterSettingsException>().Which;

        exception.Message.Should().Contain("Invalid value 'GoneFishing' specified for 'Workers'. The value should be a non-negative integer.");
    }

    public void GetSettingsShouldThrowIfParallelizationWorkersIsNegative()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>-1</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Action act = () => MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);
        AdapterSettingsException exception = act.Should().Throw<AdapterSettingsException>().Which;
        exception.Message.Should().Contain("Invalid value '-1' specified for 'Workers'. The value should be a non-negative integer.");
    }

    public void ParallelizationWorkersShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>2</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.Should().Be(2);
    }

    public void ParallelizationWorkersShouldBeSetToProcessorCountWhenSetToZero()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>0</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.Should().Be(Environment.ProcessorCount);
    }

    public void ParallelizationSettingsShouldBeSetToDefaultsWhenNotSet()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.Should().Be(Environment.ProcessorCount);
        adapterSettings.ParallelizationScope.Should().Be(ExecutionScope.ClassLevel);
    }

    public void ParallelizationSettingsShouldBeSetToDefaultsOnAnEmptyParalleizeSetting()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize/>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.Should().Be(Environment.ProcessorCount);
        adapterSettings.ParallelizationScope.Should().Be(ExecutionScope.ClassLevel);
    }

    public void ParallelizationSettingsShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>127</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationWorkers.Should().Be(127);
        adapterSettings.ParallelizationScope.Should().Be(ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldThrowIfParallelizationScopeIsNotValid()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Scope>JustParallelizeWillYou</Scope>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Action act = () => MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);
        AdapterSettingsException exception = act.Should().Throw<AdapterSettingsException>().Which;
        exception.Message.Should().Contain("Invalid value 'JustParallelizeWillYou' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel.");
    }

    public void ParallelizationScopeShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.ParallelizationScope.Should().Be(ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldThrowWhenParallelizeHasInvalidElements()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Hola>Hi</Hola>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Action act = () => MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);
        AdapterSettingsException exception = act.Should().Throw<AdapterSettingsException>().Which;
        exception.Message.Should().Contain("MSTestAdapter encountered an unexpected element 'Hola' in its settings 'Parallelize'. Remove this element and try again.");
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                </Parallelize>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TestSettingsFile.Should().NotBeNull();
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsWithData()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>127</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TestSettingsFile.Should().NotBeNull();
        adapterSettings.ParallelizationWorkers.Should().Be(127);
        adapterSettings.ParallelizationScope.Should().Be(ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsOnEmptyParallelizationNode()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize/>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        adapterSettings.TestSettingsFile.Should().NotBeNull();
    }

    public void DisableParallelizationShouldBeFalseByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings.CurrentSettings.DisableParallelization.Should().BeFalse();
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Never);
    }

    public void DisableParallelizationShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableParallelization>True</DisableParallelization>
              </RunConfiguration>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings.CurrentSettings.DisableParallelization.Should().BeTrue();
    }

    public void DisableParallelization_WithInvalidValue_GettingAWarning()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableParallelization>3</DisableParallelization>
              </RunConfiguration>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'DisableParallelization', setting will be ignored."), Times.Once);
    }

    #endregion

    #region GetSettings Tests

    public void GetSettingsShouldProbePlatformSpecificSettingsAlso()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                 <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
              </MSTest>
            </RunSettings>
            """;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                actualReader.Read();
                actualReader.ReadInnerXml();
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        _testablePlatformServiceProvider.MockSettingsProvider.Verify(sp => sp.Load(It.IsAny<XmlReader>()), Times.Once);
    }

    public void GetSettingsShouldOnlyPassTheElementSubTreeToPlatformService()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
              </MSTest>
            </RunSettings>
            """;

        string expectedRunSettingXml = "<DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>";
        string? observedXml = null;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                actualReader.Read();
                observedXml = actualReader.ReadOuterXml();
            });

        MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        (expectedRunSettingXml == observedXml).Should().BeTrue();
    }

    public void GetSettingsShouldBeAbleToReadSettingsAfterThePlatformServiceReadsItsSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        bool dummyPlatformSpecificSetting = false;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader reader) =>
            {
                reader.Read();
                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "DUMMYPLATFORMSPECIFICSETTING":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out bool result))
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
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        // Assert.
        dummyPlatformSpecificSetting.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.TestSettingsFile.Should().Be("DummyPath\\\\TestSettings1.testsettings");
    }

    public void GetSettingsShouldBeAbleToReadSettingsIfThePlatformServiceDoesNotUnderstandASetting()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <UnReadableSetting>foobar</UnReadableSetting>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        bool dummyPlatformSpecificSetting = false;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader reader) =>
            {
                reader.Read();
                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "DUMMYPLATFORMSPECIFICSETTING":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out bool result))
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
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        // Assert.
        dummyPlatformSpecificSetting.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TestSettingsFile.Should().Be("DummyPath\\\\TestSettings1.testsettings");
    }

    public void GetSettingsShouldOnlyReadTheAdapterSection()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
              </MSTest>
              <BadElement>Bad</BadElement>
            </RunSettings>
            """;

        bool outOfScopeCall = false;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader reader) =>
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
            });

        MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        // Assert.
        outOfScopeCall.Should().BeFalse();
    }

    public void GetSettingsShouldWorkIfThereAreCommentsInTheXML()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <!-- MSTest runsettings -->
              <MSTest>
                <!-- Map inconclusive -->
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                <!-- Enable base class test methods from other assemblies -->
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTest>
              <BadElement>Bad</BadElement>
            </RunSettings>
            """;

        bool dummyPlatformSpecificSetting = false;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader reader) =>
            {
                reader.Read();
                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "DUMMYPLATFORMSPECIFICSETTING":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out bool result))
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
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        // Assert.
        dummyPlatformSpecificSetting.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
    }

    #endregion

    #region CurrentSettings tests

    public void CurrentSettingShouldReturnDefaultSettingsIfNotSet()
    {
        MSTestSettings.Reset();
        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();

        // Validating the default value of a random setting.
        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
    }

    public void CurrentSettingShouldReturnCachedLoadedSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                 <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        MSTestSettings adapterSettings2 = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();
        adapterSettings.TestSettingsFile.Should().NotBeNullOrEmpty();

        (adapterSettings == adapterSettings2).Should().BeTrue();
    }

    #endregion

    #region PopulateSettings tests.

    public void PopulateSettingsShouldFillInSettingsFromSettingsObject()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <CaptureTraceOutput>False</CaptureTraceOutput>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTest>
            </RunSettings>
            """;

        MSTestSettings settings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;

        MSTestSettings.PopulateSettings(settings);

        MSTestSettings.CurrentSettings.CaptureDebugTraces.Should().BeFalse();
        MSTestSettings.CurrentSettings.MapInconclusiveToFailed.Should().BeTrue();
        MSTestSettings.CurrentSettings.MapNotRunnableToFailed.Should().BeTrue();
        MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        MSTestSettings.CurrentSettings.TestSettingsFile.Should().NotBeNullOrEmpty();
    }

    public void PopulateSettingsShouldInitializeDefaultAdapterSettingsWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings(null, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        adapterSettings.CaptureDebugTraces.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
    {
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        adapterSettings.CaptureDebugTraces.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
    {
        _mockDiscoveryContext.Setup(md => md.RunSettings!.SettingsXml).Returns(string.Empty);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        adapterSettings.CaptureDebugTraces.Should().BeTrue();
        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
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

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();

        // Validating the default value of a random setting.
        adapterSettings.MapInconclusiveToFailed.Should().BeFalse();
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestSection()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();

        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TestSettingsFile.Should().NotBeNullOrEmpty();
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestV2Section()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();

        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.TestSettingsFile.Should().NotBeNullOrEmpty();
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestV2OverMSTestV1Section()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
              <MSTest>
                <CaptureDebugTraces>False</CaptureDebugTraces>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        adapterSettings.Should().NotBeNull();

        adapterSettings.MapInconclusiveToFailed.Should().BeTrue();
        adapterSettings.MapNotRunnableToFailed.Should().BeTrue();
        adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        adapterSettings.CaptureDebugTraces.Should().BeTrue();
        adapterSettings.TestSettingsFile.Should().BeNullOrEmpty();
    }

    #endregion

    #region IsLegacyScenario tests

    public void IsLegacyScenarioReturnsFalseWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings(null, _mockMessageLogger.Object, null);
        MSTestSettings.IsLegacyScenario(null!).Should().BeFalse();
    }

    public void IsLegacyScenarioReturnsFalseWhenTestSettingsFileIsNotGiven()
    {
        string runSettingsXml =
            """
            <RunSettings>
               <MSTest>
               </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);
        MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object).Should().BeFalse();
    }

    public void IsLegacyScenarioReturnsTrueWhenTestSettingsFileIsGiven()
    {
        string runSettingsXml =
            """
            <RunSettings>
               <MSTest>
                 <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
               </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);
        MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object).Should().BeTrue();
    }

    public void LegacyScenariosNotSupportedWarningIsPrintedWhenVsmdiFileIsGiven()
    {
        string runSettingsXml =
            """
            <RunSettings>
               <MSTest>
                <SettingsFile>DummyPath\\vsmdiFile.vsmdi</SettingsFile>
               </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);
        MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object).Should().BeTrue();
        _mockMessageLogger.Verify(logger => logger.SendMessage(TestMessageLevel.Warning, Resource.LegacyScenariosNotSupportedWarning), Times.Once);
    }

    #endregion

    #region ConfigJson
    public void ConfigJson_WithInvalidValues_GettingAWarningForEachInvalidSetting()
    {
        // Arrange - setting up invalid configuration values
        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:timeout:assemblyInitialize", "timeout" },
            { "mstest:timeout:assemblyCleanup", "timeout" },
            { "mstest:timeout:classInitialize", "timeout" },
            { "mstest:timeout:classCleanup", "timeout" },
            { "mstest:timeout:testInitialize", "timeout" },
            { "mstest:timeout:testCleanup", "timeout" },
            { "mstest:timeout:test", "timeout" },
            { "mstest:timeout:useCooperativeCancellation", "3" },
            { "mstest:execution:mapInconclusiveToFailed", "3" },
            { "mstest:execution:mapNotRunnableToFailed", "3" },
            { "mstest:execution:treatDiscoveryWarningsAsErrors", "3" },
            { "mstest:execution:considerEmptyDataSourceAsInconclusive", "3" },
            { "mstest:execution:considerFixturesAsSpecialTests", "3" },
            { "mstest:enableBaseClassTestMethodsFromOtherAssemblies", "3" },
        };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        var settings = new MSTestSettings();
        MSTestSettings.SetSettingsFromConfig(mockConfig.Object, _mockMessageLogger.Object, settings);

        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(14));
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'timeout:useCooperativeCancellation', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:assemblyInitialize', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:assemblyCleanup', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:classInitialize', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'execution:mapNotRunnableToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'enableBaseClassTestMethodsFromOtherAssemblies', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:classCleanup', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:testInitialize', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:testCleanup', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'timeout:test', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'execution:treatDiscoveryWarningsAsErrors', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'execution:mapInconclusiveToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'execution:considerEmptyDataSourceAsInconclusive', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'execution:considerFixturesAsSpecialTests', setting will be ignored."), Times.Once);
    }

    public void ConfigJson_WithValidValues_ValuesAreSetCorrectly()
    {
        // Arrange - setting up valid configuration values
        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:timeout:assemblyInitialize", "300" },
            { "mstest:timeout:assemblyCleanup", "300" },
            { "mstest:timeout:classInitialize", "200" },
            { "mstest:timeout:classCleanup", "200" },
            { "mstest:timeout:testInitialize", "100" },
            { "mstest:timeout:testCleanup", "100" },
            { "mstest:timeout:test", "60" },
            { "mstest:timeout:useCooperativeCancellation", "true" },
            { "mstest:parallelism:enabled", "true" },
            { "mstest:parallelism:workers", "4" },
            { "mstest:parallelism:scope", "class" },
            { "mstest:execution:mapInconclusiveToFailed", "true" },
            { "mstest:execution:mapNotRunnableToFailed", "true" },
            { "mstest:execution:treatDiscoveryWarningsAsErrors", "true" },
            { "mstest:execution:considerEmptyDataSourceAsInconclusive", "true" },
            { "mstest:execution:considerFixturesAsSpecialTests", "true" },
            { "mstest:enableBaseClassTestMethodsFromOtherAssemblies", "true" },
            { "mstest:orderTestsByNameInClass", "true" },
            { "mstest:output:captureTrace", "true" },
        };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        var settings = new MSTestSettings();

        // Act
        MSTestSettings.SetSettingsFromConfig(mockConfig.Object, _mockMessageLogger.Object, settings);

        // Assert
        settings.EnableBaseClassTestMethodsFromOtherAssemblies.Should().BeTrue();
        settings.OrderTestsByNameInClass.Should().BeTrue();
        settings.CaptureDebugTraces.Should().BeTrue();
        settings.CooperativeCancellationTimeout.Should().BeTrue();
        settings.MapInconclusiveToFailed.Should().BeTrue();
        settings.MapNotRunnableToFailed.Should().BeTrue();
        settings.TreatDiscoveryWarningsAsErrors.Should().BeTrue();
        settings.ConsiderEmptyDataSourceAsInconclusive.Should().BeTrue();
        settings.ConsiderFixturesAsSpecialTests.Should().BeTrue();

        settings.TestTimeout.Should().Be(60);
        settings.AssemblyInitializeTimeout.Should().Be(300);
        settings.AssemblyCleanupTimeout.Should().Be(300);
        settings.ClassInitializeTimeout.Should().Be(200);
        settings.ClassCleanupTimeout.Should().Be(200);
        settings.TestInitializeTimeout.Should().Be(100);
        settings.TestCleanupTimeout.Should().Be(100);

        settings.DisableParallelization.Should().BeFalse();
        settings.ParallelizationWorkers.Should().Be(4);
        settings.ParallelizationScope.Should().Be(ExecutionScope.ClassLevel);
    }

    public void ConfigJson_Parllelism_Enabled_True() => ConfigJson_Parllelism_Enabled_Core(true);

    public void ConfigJson_Parllelism_Enabled_False() => ConfigJson_Parllelism_Enabled_Core(false);

    private void ConfigJson_Parllelism_Enabled_Core(bool parallelismEnabled)
    {
        // Arrange - setting up valid configuration values
        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:parallelism:enabled", parallelismEnabled.ToString().ToLowerInvariant() },
            { "mstest:parallelism:workers", "4" },
            { "mstest:parallelism:scope", "class" },
        };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        var settings = new MSTestSettings();

        // Act
        MSTestSettings.SetSettingsFromConfig(mockConfig.Object, _mockMessageLogger.Object, settings);

        // Assert
        settings.DisableParallelization.Should().Be(!parallelismEnabled);
        settings.ParallelizationWorkers.Should().Be(4);
        settings.ParallelizationScope.Should().Be(ExecutionScope.ClassLevel);
    }

    public void ConfigJson_WithValidValues_MethodScope()
    {
        // Arrange - setting up valid configuration values
        var configDictionary = new Dictionary<string, string> { { "mstest:parallelism:scope", "method" } };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
            .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        var settings = new MSTestSettings();

        // Act
        MSTestSettings.SetSettingsFromConfig(mockConfig.Object, _mockMessageLogger.Object, settings);

        // Assert
        settings.ParallelizationScope.Should().Be(ExecutionScope.MethodLevel);
    }

    #endregion
}

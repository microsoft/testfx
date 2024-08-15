// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

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
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(!adapterSettings.MapInconclusiveToFailed);
    }

    public void MapNotRunnableToFailedIsByDefaultTrueWhenNotSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.MapNotRunnableToFailed);
    }

    public void MapInconclusiveToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.MapInconclusiveToFailed);
    }

    public void RunSettings_WithInvalidValues_GettingAWarningForEachInvalidSetting()
    {
        string runSettingxml =
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
                    <TreatClassAndAssemblyCleanupWarningsAsErrors>3</TreatClassAndAssemblyCleanupWarningsAsErrors>
                    <ForcedLegacyMode>3</ForcedLegacyMode>
               </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(18));
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'CooperativeCancellationTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'ClassCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'AssemblyCleanupTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'CaptureTraceOutput', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'MapNotRunnableToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'EnableBaseClassTestMethodsFromOtherAssemblies', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'TreatClassAndAssemblyCleanupWarningsAsErrors', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'ClassInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'AssemblyInitializeTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value 'timeout' for runsettings entry 'TestTimeout', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '' for runsettings entry 'SettingsFile', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'ForcedLegacyMode', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'TreatDiscoveryWarningsAsErrors', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'MapInconclusiveToFailed', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'ConsiderEmptyDataSourceAsInconclusive', setting will be ignored."), Times.Once);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'ConsiderFixturesAsSpecialTests', setting will be ignored."), Times.Once);
    }

    public void MapNotRunnableToFailedShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.MapNotRunnableToFailed);
    }

    public void ForcedLegacyModeIsByDefaultFalseWhenNotSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
              </MSTest>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        Verify(!adapterSettings.ForcedLegacyMode);
    }

    public void ForcedLegacyModeShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                <ForcedLegacyMode>true</ForcedLegacyMode>
              </MSTest>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        Verify(adapterSettings.ForcedLegacyMode);
    }

    public void TestSettingsFileIsByDefaultNullWhenNotSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
              </MSTest>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        Verify(adapterSettings.TestSettingsFile is null);
    }

    public void TestSettingsFileShouldNotBeNullWhenSpecifiedInRunSettings()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        Verify(adapterSettings.TestSettingsFile is not null);
    }

    public void EnableBaseClassTestMethodsFromOtherAssembliesIsByDefaulTrueWhenNotSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
    }

    public void EnableBaseClassTestMethodsFromOtherAssembliesShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>True</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
    }

    public void CaptureDebugTracesShouldBeTrueByDefault()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.CaptureDebugTraces);
    }

    public void CaptureDebugTracesShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                  <CaptureTraceOutput>False</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(!adapterSettings.CaptureDebugTraces);
    }

    public void TestTimeoutShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                  <TestTimeout>4000</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TestTimeout == 4000);
    }

    public void TestTimeoutShouldBeSetToZeroIfNotSpecifiedInRunSettings()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TestTimeout == 0);
    }

    public void TreatClassCleanupWarningsAsErrorsShouldBeFalseByDefault()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(!adapterSettings.TreatClassAndAssemblyCleanupWarningsAsErrors);
    }

    public void TreatClassCleanupWarningsAsErrorsShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <TreatClassAndAssemblyCleanupWarningsAsErrors>True</TreatClassAndAssemblyCleanupWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TreatClassAndAssemblyCleanupWarningsAsErrors);
    }

    public void TreatDiscoveryWarningsAsErrorsShouldBeFalseByDefault()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(!adapterSettings.TreatDiscoveryWarningsAsErrors);
    }

    public void TreatDiscoveryWarningsAsErrorsShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <TreatDiscoveryWarningsAsErrors>True</TreatDiscoveryWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TreatDiscoveryWarningsAsErrors);
    }

    public void ParallelizationSettingsShouldNotBeSetByDefault()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(!adapterSettings.ParallelizationWorkers.HasValue);
        Verify(!adapterSettings.ParallelizationScope.HasValue);
    }

    public void GetSettingsShouldThrowIfParallelizationWorkersIsNotInt()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>GoneFishing</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Exception exception = VerifyThrows(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object));

        Verify(exception is not null);
        Verify(typeof(AdapterSettingsException).FullName == exception.GetType().FullName);
        Verify(exception.Message.Contains("Invalid value 'GoneFishing' specified for 'Workers'. The value should be a non-negative integer."));
    }

    public void GetSettingsShouldThrowIfParallelizationWorkersIsNegative()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>-1</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Exception exception = VerifyThrows(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object));

        Verify(exception is not null);
        Verify(typeof(AdapterSettingsException).FullName == exception.GetType().FullName);
        Verify(exception.Message.Contains("Invalid value '-1' specified for 'Workers'. The value should be a non-negative integer."));
    }

    public void ParallelizationWorkersShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>2</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.ParallelizationWorkers == 2);
    }

    public void ParallelizationWorkersShouldBeSetToProcessorCountWhenSetToZero()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Workers>0</Workers>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(Environment.ProcessorCount == adapterSettings.ParallelizationWorkers);
    }

    public void ParallelizationSettingsShouldBeSetToDefaultsWhenNotSet()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(Environment.ProcessorCount == adapterSettings.ParallelizationWorkers);
        Verify(adapterSettings.ParallelizationScope == UTF.ExecutionScope.ClassLevel);
    }

    public void ParallelizationSettingsShouldBeSetToDefaultsOnAnEmptyParalleizeSetting()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize/>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(Environment.ProcessorCount == adapterSettings.ParallelizationWorkers);
        Verify(adapterSettings.ParallelizationScope == UTF.ExecutionScope.ClassLevel);
    }

    public void ParallelizationSettingsShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
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

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.ParallelizationWorkers == 127);
        Verify(adapterSettings.ParallelizationScope == UTF.ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldThrowIfParallelizationScopeIsNotValid()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Scope>JustParallelizeWillYou</Scope>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Exception exception = VerifyThrows(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object));

        Verify(exception is not null);
        Verify(typeof(AdapterSettingsException).FullName == exception.GetType().FullName);
        Verify(exception.Message.Contains("Invalid value 'JustParallelizeWillYou' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."));
    }

    public void ParallelizationScopeShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.ParallelizationScope == UTF.ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldThrowWhenParallelizeHasInvalidElements()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                  <Hola>Hi</Hola>
                </Parallelize>
              </MSTestV2>
            </RunSettings>
            """;

        Exception exception = VerifyThrows(() => MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object));

        Verify(exception is not null);
        Verify(typeof(AdapterSettingsException).FullName == exception.GetType().FullName);
        Verify(exception.Message.Contains("Invalid settings 'Parallelize'. Unexpected XmlElement: 'Hola'."));
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettings()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize>
                </Parallelize>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TestSettingsFile is not null);
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsWithData()
    {
        string runSettingxml =
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

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TestSettingsFile is not null);
        Verify(adapterSettings.ParallelizationWorkers == 127);
        Verify(adapterSettings.ParallelizationScope == UTF.ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldBeAbleToReadAfterParallelizationSettingsOnEmptyParallelizationNode()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <Parallelize/>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        Verify(adapterSettings.TestSettingsFile is not null);
    }

    public void DisableParallelizationShouldBeFalseByDefault()
    {
        string runSettingxml =
            """
            <RunSettings>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        Verify(!MSTestSettings.CurrentSettings.DisableParallelization);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Never);
    }

    public void DisableParallelizationShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableParallelization>True</DisableParallelization>
              </RunConfiguration>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        Verify(MSTestSettings.CurrentSettings.DisableParallelization);
    }

    public void DisableParallelization_WithInvalidValue_GettingAWarning()
    {
        string runSettingxml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableParallelization>3</DisableParallelization>
              </RunConfiguration>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, "Invalid value '3' for runsettings entry 'DisableParallelization', setting will be ignored."), Times.Once);
    }

    #endregion

    #region GetSettings Tests

    public void GetSettingsShouldProbePlatformSpecificSettingsAlso()
    {
        string runSettingxml =
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
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        _testablePlatformServiceProvider.MockSettingsProvider.Verify(sp => sp.Load(It.IsAny<XmlReader>()), Times.Once);
    }

    public void GetSettingsShouldOnlyPassTheElementSubTreeToPlatformService()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
              </MSTest>
            </RunSettings>
            """;

        string expectedrunSettingxml = "<DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>";
        string observedxml = null;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                if (actualReader != null)
                {
                    actualReader.Read();
                    observedxml = actualReader.ReadOuterXml();
                }
            });

        MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        Verify(expectedrunSettingxml == observedxml);
    }

    public void GetSettingsShouldBeAbleToReadSettingsAfterThePlatformServiceReadsItsSettings()
    {
        string runSettingxml =
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
                if (reader != null)
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
                }
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        // Assert.
        Verify(dummyPlatformSpecificSetting);
        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.TestSettingsFile == "DummyPath\\\\TestSettings1.testsettings");
    }

    public void GetSettingsShouldBeAbleToReadSettingsIfThePlatformServiceDoesNotUnderstandASetting()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <UnReadableSetting>foobar</UnReadableSetting>
                <ForcedLegacyMode>true</ForcedLegacyMode>
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
                if (reader != null)
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
                }
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        // Assert.
        Verify(dummyPlatformSpecificSetting);
        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.ForcedLegacyMode);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(adapterSettings.TestSettingsFile == "DummyPath\\\\TestSettings1.testsettings");
    }

    public void GetSettingsShouldOnlyReadTheAdapterSection()
    {
        string runSettingxml =
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

        MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        // Assert.
        Verify(!outOfScopeCall);
    }

    public void GetSettingsShouldWorkIfThereAreCommentsInTheXML()
    {
        string runSettingxml =
            """
            <RunSettings>
              <!-- MSTest runsettings -->
              <MSTest>
                <!-- Map inconclusive -->
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <DummyPlatformSpecificSetting>True</DummyPlatformSpecificSetting>
                <!-- Force Legacy mode -->
                <ForcedLegacyMode>true</ForcedLegacyMode>
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
                if (reader != null)
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
                }
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        // Assert.
        Verify(dummyPlatformSpecificSetting);
        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.ForcedLegacyMode);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
    }

    #endregion

    #region CurrentSettings tests

    public void CurrentSettingShouldReturnDefaultSettingsIfNotSet()
    {
        MSTestSettings.Reset();
        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);

        // Validating the default value of a random setting.
        Verify(!adapterSettings.ForcedLegacyMode);
    }

    public void CurrentSettingShouldReturnCachedLoadedSettings()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                 <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        MSTestSettings adapterSettings2 = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);
        Verify(!string.IsNullOrEmpty(adapterSettings.TestSettingsFile));

        Verify(adapterSettings == adapterSettings2);
    }

    #endregion

    #region PopulateSettings tests.

    public void PopulateSettingsShouldFillInSettingsFromSettingsObject()
    {
        string runsettingsXml =
            """
            <RunSettings>
              <MSTest>
                <CaptureTraceOutput>False</CaptureTraceOutput>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <ForcedLegacyMode>true</ForcedLegacyMode>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
                <TreatClassAndAssemblyCleanupWarningsAsErrors>true</TreatClassAndAssemblyCleanupWarningsAsErrors>
              </MSTest>
            </RunSettings>
            """;

        var settings = MSTestSettings.GetSettings(runsettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);

        MSTestSettings.PopulateSettings(settings);

        Verify(!MSTestSettings.CurrentSettings.CaptureDebugTraces);
        Verify(MSTestSettings.CurrentSettings.MapInconclusiveToFailed);
        Verify(MSTestSettings.CurrentSettings.MapNotRunnableToFailed);
        Verify(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Verify(MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(MSTestSettings.CurrentSettings.TreatClassAndAssemblyCleanupWarningsAsErrors);
        Verify(!string.IsNullOrEmpty(MSTestSettings.CurrentSettings.TestSettingsFile));
    }

    public void PopulateSettingsShouldInitializeDefaultAdapterSettingsWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings(null, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        Verify(adapterSettings.CaptureDebugTraces);
        Verify(!adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!adapterSettings.TreatDiscoveryWarningsAsErrors);
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsIsNull()
    {
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        Verify(adapterSettings.CaptureDebugTraces);
        Verify(!adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!adapterSettings.TreatDiscoveryWarningsAsErrors);
    }

    public void PopulateSettingsShouldInitializeDefaultSettingsWhenRunSettingsXmlIsEmpty()
    {
        _mockDiscoveryContext.Setup(md => md.RunSettings.SettingsXml).Returns(string.Empty);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;
        Verify(adapterSettings.CaptureDebugTraces);
        Verify(!adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!adapterSettings.TreatDiscoveryWarningsAsErrors);
    }

    public void PopulateSettingsShouldInitializeSettingsToDefaultIfNotSpecified()
    {
        string runSettingxml =
            """
            <RunSettings>
              <FooUnit>
               <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
              </FooUnit>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);

        // Validating the default value of a random setting.
        Verify(!adapterSettings.ForcedLegacyMode);
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestSection()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTest>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <ForcedLegacyMode>true</ForcedLegacyMode>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);

        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.ForcedLegacyMode);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestV2Section()
    {
        string runSettingxml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>True</MapNotRunnableToFailed>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <ForcedLegacyMode>true</ForcedLegacyMode>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);

        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.ForcedLegacyMode);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
    }

    public void PopulateSettingsShouldInitializeSettingsFromMSTestV2OverMSTestV1Section()
    {
        string runSettingxml =
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
                <ForcedLegacyMode>true</ForcedLegacyMode>
              </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);

        MSTestSettings adapterSettings = MSTestSettings.CurrentSettings;

        Verify(adapterSettings is not null);

        Verify(adapterSettings.MapInconclusiveToFailed);
        Verify(adapterSettings.MapNotRunnableToFailed);
        Verify(adapterSettings.EnableBaseClassTestMethodsFromOtherAssemblies);
        Verify(!adapterSettings.ForcedLegacyMode);
        Verify(adapterSettings.CaptureDebugTraces);
        Verify(string.IsNullOrEmpty(adapterSettings.TestSettingsFile));
    }

    #endregion

    #region IsLegacyScenario tests

    public void IsLegacyScenarioReturnsFalseWhenDiscoveryContextIsNull()
    {
        MSTestSettings.PopulateSettings(null, _mockMessageLogger.Object);
        Verify(!MSTestSettings.IsLegacyScenario(null));
    }

    public void IsLegacyScenarioReturnsFalseWhenForcedLegacyModeIsSetToFalse()
    {
        string runSettingxml =
            """
            <RunSettings>
               <MSTest>
                 <ForcedLegacyMode>False</ForcedLegacyMode>
               </MSTest>
            </RunSettings>
            """;

        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);
        Verify(!MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object));
    }

    public void IsLegacyScenarioReturnsFalseWhenForcedLegacyModeIsSetToTrue()
    {
        string runSettingxml =
            """
            <RunSettings>
               <MSTest>
                 <ForcedLegacyMode>true</ForcedLegacyMode>
               </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);
        Verify(!MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object));
    }

    public void IsLegacyScenarioReturnsTrueWhenTestSettingsFileIsGiven()
    {
        string runSettingxml =
            """
            <RunSettings>
               <MSTest>
                 <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
               </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);
        Verify(MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object));
    }

    public void LegacyScenariosNotSupportedWarningIsPrintedWhenVsmdiFileIsGiven()
    {
        string runSettingxml =
            """
            <RunSettings>
               <MSTest>
                <SettingsFile>DummyPath\\vsmdiFile.vsmdi</SettingsFile>
               </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object);
        Verify(MSTestSettings.IsLegacyScenario(_mockMessageLogger.Object));
        _mockMessageLogger.Verify(logger => logger.SendMessage(TestMessageLevel.Warning, Resource.LegacyScenariosNotSupportedWarning), Times.Once);
    }

    #endregion
}

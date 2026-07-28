// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestExecutionManagerTests : TestContainer
{
    private readonly TestableFrameworkHandle _frameworkHandle;
    private readonly TestRunCancellationToken _cancellationToken;
    private readonly TestExecutionManager _testExecutionManager;
    private readonly Mock<IMessageLogger> _mockMessageLogger;
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler;

    private readonly TestProperty[] _tcmKnownProperties =
    [
        AdapterTestProperties.TestRunIdProperty,
        AdapterTestProperties.TestPlanIdProperty,
        AdapterTestProperties.BuildConfigurationIdProperty,
        AdapterTestProperties.BuildDirectoryProperty,
        AdapterTestProperties.BuildFlavorProperty,
        AdapterTestProperties.BuildNumberProperty,
        AdapterTestProperties.BuildPlatformProperty,
        AdapterTestProperties.BuildUriProperty,
        AdapterTestProperties.TfsServerCollectionUrlProperty,
        AdapterTestProperties.TfsTeamProjectProperty,
        AdapterTestProperties.IsInLabEnvironmentProperty,
        AdapterTestProperties.TestCaseIdProperty,
        AdapterTestProperties.TestConfigurationIdProperty,
        AdapterTestProperties.TestConfigurationNameProperty,
        AdapterTestProperties.TestPointIdProperty,
    ];

    private TestableRunContextTestExecutionTests _runContext;
    private List<string> _callers = [];
    private int _enqueuedParallelTestsCount;

    /// <summary>
    /// Builds the VSTest-backed result recorder the execution engine now receives at its boundary, recording into
    /// <see cref="_frameworkHandle"/> exactly as the adapter does in production.
    /// </summary>
    private ITestResultRecorder TestResultRecorder
        => _frameworkHandle.ToTestResultRecorder(EnvironmentWrapper.Instance.MachineName, MSTestSettings.CurrentSettings);

    /// <summary>
    /// Builds the neutral run inputs (test-run directory + run settings XML) the execution engine now receives at
    /// its boundary, extracted from <see cref="_runContext"/> exactly as the adapter does in production.
    /// </summary>
    private DeploymentContext CurrentDeploymentContext
        => new(_runContext.TestRunDirectory, _runContext.RunSettings?.SettingsXml);

    public TestExecutionManagerTests()
    {
        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression(_ => true));
        _frameworkHandle = new TestableFrameworkHandle();
        _cancellationToken = new TestRunCancellationToken();
        _mockMessageLogger = new Mock<IMessageLogger>();
        _mockTestSourceHandler = new Mock<ITestSourceHandler>();

        _testExecutionManager = new TestExecutionManager(
            task =>
            {
                _enqueuedParallelTestsCount++;
                return task();
            });
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    #region RunTests on a list of tests

    public async Task RunTestsForTestWithFilterErrorShouldSendZeroResults()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        // Causing the FilterExpressionError
        _runContext = new TestableRunContextTestExecutionTests(() => throw new TestPlatformFormatException());

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _cancellationToken);

        // No Results
        _frameworkHandle.TestCaseStartList.Count.Should().Be(0);
        _frameworkHandle.ResultsList.Count.Should().Be(0);
        _frameworkHandle.TestCaseEndList.Count.Should().Be(0);
    }

    public async Task RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression(p => p.DisplayName == "PassingTest"));

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _cancellationToken);

        // FailingTest should be skipped because it does not match the filter criteria.
        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList).Should().BeTrue();
        expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList).Should().BeTrue();
        expectedResultList.SequenceEqual(_frameworkHandle.ResultsList).Should().BeTrue();
    }

    public async Task SendTestResults_WhenUnitTestResultsIsEmpty_RecordsEndWithoutResult()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[] unitTestResults = [];

        await _testExecutionManager.SendTestResultsAsync(ToUnitTestElement(testCase), unitTestResults, DateTimeOffset.Now, DateTimeOffset.Now, _frameworkHandle.ToTestResultRecorder(EnvironmentWrapper.Instance.MachineName, MSTestSettings.CurrentSettings));

        _frameworkHandle.TestCaseEndList.Should().Equal("PassingTest:None");
        _frameworkHandle.ResultsList.Should().BeEmpty();
    }

    public async Task RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "IgnoredTest");
        TestCase[] tests = [testCase];

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _cancellationToken);

        _frameworkHandle.TestCaseStartList[0].Should().Be("IgnoredTest");
        _frameworkHandle.TestCaseEndList[0].Should().Be("IgnoredTest:Skipped");
        _frameworkHandle.ResultsList[0].Should().Be("IgnoredTest  Skipped");
    }

    public async Task RunTestsForASingleTestShouldSendSingleResult()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList).Should().BeTrue();
        expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList).Should().BeTrue();
        expectedResultList.SequenceEqual(_frameworkHandle.ResultsList).Should().BeTrue();
    }

    public async Task RunTestsForMultipleTestShouldSendMultipleResults()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _cancellationToken);

        List<string> expectedTestCaseStartList = ["PassingTest", "FailingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed", "FailingTest:Failed"];
        List<string> expectedResultList = ["PassingTest  Passed", "FailingTest  Failed\r\n  Message: Assertion failed."];

        expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList).Should().BeTrue();
        expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList).Should().BeTrue();
        _frameworkHandle.ResultsList[0].Should().Be(expectedResultList[0]);
        _frameworkHandle.ResultsList[1].Should().Contain(expectedResultList[1]);
    }

    public async Task RunTestsForCancellationTokenCanceledSetToTrueShouldSendZeroResults()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        // Cancel the test run
        _cancellationToken.Cancel();
        Func<Task> func = () => _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _cancellationToken);
        await func.Should().ThrowAsync<OperationCanceledException>();

        // No Results
        _frameworkHandle.TestCaseStartList.Should().BeEmpty();
        _frameworkHandle.ResultsList.Should().BeEmpty();
        _frameworkHandle.TestCaseEndList.Should().BeEmpty();
    }

#if !WINDOWS_UWP && !WIN_UI
    public async Task RunTestsForTestShouldDeployBeforeExecution()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(It.IsAny<IEnumerable<UnitTestElement>>(), It.IsAny<DeploymentContext>(), It.IsAny<IAdapterMessageLogger>())).Callback(() => SetCaller("Deploy"));

        await _testExecutionManager.RunTestsAsync(
            ToUnitTestElements(tests),
            CurrentDeploymentContext,
            _frameworkHandle.ToAdapterMessageLogger(),
            TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        _callers[0].Should().Be("Deploy", "Deploy should be called before execution.");
        _callers[1].Should().Be("LoadAssembly", "Deploy should be called before execution.");
    }
#endif

    public async Task RunTestsForTestShouldCleanupAfterExecution()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();

#if !WINDOWS_UWP && !WIN_UI
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Cleanup()).Callback(() => SetCaller("Cleanup"));
#endif

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        _callers[0].Should().Be("LoadAssembly", "Cleanup should be called after execution.");

#if !WINDOWS_UWP && !WIN_UI
        _callers.LastOrDefault().Should().Be("Cleanup", "Cleanup should be called after execution.");
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    public async Task RunTestsForTestShouldNotCleanupOnTestFailure()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        testablePlatformService.MockTestDeployment.Verify(td => td.Cleanup(), Times.Never);
    }

    public async Task RunTestsForTestShouldLoadSourceFromDeploymentDirectoryIfDeployed()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();

        // Setup mocks.
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(It.IsAny<IEnumerable<UnitTestElement>>(), It.IsAny<DeploymentContext>(), It.IsAny<IAdapterMessageLogger>())).Returns(true);
        testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
            .Returns(@"C:\temp");

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        testablePlatformService.MockFileOperations.Verify(
            fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp"))),
            Times.AtLeastOnce);
    }
#endif

    public async Task RunTestsForTestShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
            <RunConfiguration>
              <DisableAppDomain>True</DisableAppDomain>
            </RunConfiguration>
            <TestRunParameters>
              <Parameter name="webAppUrl" value="http://localhost" />
              <Parameter name = "webAppUserName" value="Admin" />
              </TestRunParameters>
            </RunSettings>
            """);

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        DummyTestClass.TestContextProperties!.Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")).Should().BeTrue();
    }

    public async Task RunTestsForTestShouldPassInTcmPropertiesAsPropertiesToTheTest()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        object[] propertiesValue = [32, 534, 5, "sample build directory", "sample build flavor", "132456", "sample build platform", "http://sampleBuildUti/", "http://samplecollectionuri/", "sample team project", false, 1401, 54, "sample configuration name", 345];
        SetTestCaseProperties(testCase, propertiesValue);

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        VerifyTcmProperties(DummyTestClass.TestContextProperties, testCase);
    }

    public async Task RunTestsShouldKeepTcmPropertiesOutOfClassLifecycleAndSiblingTestContexts()
    {
        TestCase testWithTcmProperty = GetTestCase(typeof(DummyTestClassWithScopedTcmProperties), nameof(DummyTestClassWithScopedTcmProperties.TestWithTcmProperty));
        testWithTcmProperty.SetPropertyValue(AdapterTestProperties.TestCaseIdProperty, 1401);
        TestCase siblingTest = GetTestCase(typeof(DummyTestClassWithScopedTcmProperties), nameof(DummyTestClassWithScopedTcmProperties.SiblingTest));

        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockSettingsProvider
            .Setup(sp => sp.GetProperties(It.IsAny<string>()))
            .Returns(new Dictionary<string, object> { ["SourceProperty"] = "SourceValue" });
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        await _testExecutionManager.RunTestsAsync(
            ToUnitTestElements(testWithTcmProperty, siblingTest),
            CurrentDeploymentContext,
            _frameworkHandle.ToAdapterMessageLogger(),
            TestResultRecorder,
            new TestElementFilterProvider(_runContext),
            new TestRunCancellationToken());

        _frameworkHandle.TestCaseEndList.Should().Equal(
            $"{nameof(DummyTestClassWithScopedTcmProperties.TestWithTcmProperty)}:Passed",
            $"{nameof(DummyTestClassWithScopedTcmProperties.SiblingTest)}:Passed");
    }

    public async Task RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        testablePlatformService.MockSettingsProvider.Verify(sp => sp.GetProperties(It.IsAny<string>()), Times.Once);
    }

    public async Task RunTestsShouldClearSessionParametersAcrossRuns()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <TestRunParameters>
                <Parameter name="webAppUrl" value="http://localhost" />
                <Parameter name = "webAppUserName" value="Admin" />
              </TestRunParameters>
            </RunSettings>
            """);

        // Trigger First Run
        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        // Update runsettings to have different values for similar keys
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <TestRunParameters>
                <Parameter name="webAppUrl" value="http://updatedLocalHost" />
                <Parameter name = "webAppUserName" value="Admin" />
              </TestRunParameters>
            </RunSettings>
            """);

        // Trigger another Run
        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        "http://updatedLocalHost".Equals(DummyTestClass.TestContextProperties!["webAppUrl"]).Should().BeTrue();
    }

    #endregion

    #region Run Tests on Sources

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForSourceShouldRunTestsInASource()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        await _testExecutionManager.RunTestsAsync(sources, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _mockTestSourceHandler.Object, false, _cancellationToken);

        _frameworkHandle.TestCaseStartList.Contains("PassingTest").Should().BeTrue();
        _frameworkHandle.TestCaseEndList.Contains("PassingTest:Passed").Should().BeTrue();
        _frameworkHandle.ResultsList.Contains("PassingTest  Passed").Should().BeTrue();
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForSourceShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <TestRunParameters>
                <Parameter name="webAppUrl" value="http://localhost" />
                <Parameter name = "webAppUserName" value="Admin" />
              </TestRunParameters>
            </RunSettings>
            """);

        await _testExecutionManager.RunTestsAsync(sources, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _mockTestSourceHandler.Object, false, _cancellationToken);

        DummyTestClass.TestContextProperties!.Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")).Should().BeTrue();
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        await _testExecutionManager.RunTestsAsync(sources, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _mockTestSourceHandler.Object, false, _cancellationToken);

        DummyTestClass.TestContextProperties.Should().NotBeNull();
    }

    public async Task RunTestsForMultipleSourcesShouldRunEachTestJustOnce()
    {
        int testsCount = 0;
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location };
        TestableTestExecutionManager testableTestExecutionManager = new()
        {
            ExecuteTestsWrapper = (tests, runContext, frameworkHandle, testResultRecorder, isDeploymentDone) => testsCount += tests.Count(),
        };

        await testableTestExecutionManager.RunTestsAsync(sources, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), _mockTestSourceHandler.Object, false, _cancellationToken);
        testsCount.Should().Be(4);
    }

    #endregion

    #region Parallel tests

    public async Task RunTestsForTestShouldRunTestsInParallelWhenEnabledInRunsettings()
    {
        TestCase testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");
        TestCase testCase21 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        TestCase testCase22 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod2");

        TestCase[] tests = [testCase11, testCase12, testCase21, testCase22];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            DummyTestClassForParallelize.ThreadIds.Count.Should().Be(1);
            DummyTestClassForParallelize2.ThreadIds.Count.Should().Be(1);
            _enqueuedParallelTestsCount.Should().Be(2);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
            DummyTestClassForParallelize2.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldRunTestsByMethodLevelWhenSpecified()
    {
        TestCase testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase11, testCase12];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                 <Parallelize>
                   <Workers>2</Workers>
                   <Scope>MethodLevel</Scope>
                 </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            _enqueuedParallelTestsCount.Should().Be(2);

            // Run on 1 or 2 threads
            DummyTestClassForParallelize.ThreadIds.Count.Should().BeOneOf(1, 2);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldRunTestsWithSpecifiedNumberOfWorkers()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        TestCase testCase3 = GetTestCase(typeof(DummyTestClassForParallelize3), "TestMethod1");

        TestCase[] tests = [testCase1, testCase2, testCase3];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>3</Workers>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            DummyTestClassForParallelize.ThreadIds.Count.Should().Be(1);
            DummyTestClassForParallelize2.ThreadIds.Count.Should().Be(1);
            DummyTestClassForParallelize3.ThreadIds.Count.Should().Be(1);

            _enqueuedParallelTestsCount.Should().Be(3);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
            DummyTestClassForParallelize2.Cleanup();
            DummyTestClassForParallelize3.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromRunsettings()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml)
            .Returns(
                """
                <RunSettings>
                  <RunConfiguration>
                    <DisableParallelization>true</DisableParallelization>
                  </RunConfiguration>
                </RunSettings>
                """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new ParallelizeAttribute { Workers = 10, Scope = ExecutionScope.MethodLevel }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>())).
                Returns((MemberInfo memberInfo) => originalReflectionOperation.GetCustomAttributes(memberInfo));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
                .Returns((string assemblyName) => originalFileOperation.LoadAssembly(assemblyName));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            DummyTestClassForParallelize.ThreadIds.Count.Should().Be(1);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromSource()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(DoNotParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new DoNotParallelizeAttribute()]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>())).
                Returns((MemberInfo memberInfo) => originalReflectionOperation.GetCustomAttributes(memberInfo));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
                .Returns((string assemblyName) => originalFileOperation.LoadAssembly(assemblyName));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            DummyTestClassForParallelize.ThreadIds.Count.Should().Be(1);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldRunNonParallelizableTestsSeparately()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        TestCase testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        TestCase testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        testCase3.SetPropertyValue(AdapterTestProperties.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(AdapterTestProperties.DoNotParallelizeProperty, true);

        TestCase[] tests = [testCase1, testCase2, testCase3, testCase4];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            _enqueuedParallelTestsCount.Should().Be(2);
            DummyTestClassWithDoNotParallelizeMethods.ParallelizableTestsThreadIds.Count.Should().BeOneOf(1, 2);
            DummyTestClassWithDoNotParallelizeMethods.UnParallelizableTestsThreadIds.Count.Should().Be(1);
            (DummyTestClassWithDoNotParallelizeMethods.LastParallelizableTestRun.TimeOfDay.TotalMilliseconds <= DummyTestClassWithDoNotParallelizeMethods.FirstUnParallelizableTestRun.TimeOfDay.TotalMilliseconds).Should().BeTrue();
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    /// <summary>
    /// A chunk that throws outside the test body (here: a host-side recording failure) must not strand the
    /// workers waiting on it. Before the scheduler released its dependents in a <c>finally</c>, an exception
    /// skipped both the dependent release and the completion count, so every other worker blocked forever on
    /// a permit that would never be issued and the whole run hung instead of failing.
    /// </summary>
    public async Task RunTestsWhenADependencyChunkThrowsShouldNotHang()
    {
        TestCase root = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase branchA = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        TestCase branchB = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod2");

        _frameworkHandle.ThrowOnRecordStartForTest = root.DisplayName;

        TestCase[] tests = [root, branchA, branchB];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        UnitTestElement[] elements = ToUnitTestElements(tests);

        // BranchA and BranchB both wait for the chunk that is about to throw, so with the bug present there
        // is nothing left to release them and the second worker waits forever. The dependency is set on the
        // element directly rather than through the transport property because these elements are already
        // materialized (the property round-trip has its own tests); what is under test here is the scheduler.
        TestDependencyInfo[] dependsOnRoot = [new TestDependencyInfo(typeof(DummyTestClassForParallelize).FullName, "TestMethod1", proceedOnFailure: false)];
        elements[1].Dependencies = dependsOnRoot;
        elements[2].Dependencies = dependsOnRoot;

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);

            Task run = _testExecutionManager.RunTestsAsync(elements, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            // The run must finish - failing is fine, hanging is not. The timeout is what actually asserts the
            // fix: without it the test would deadlock rather than fail.
            Task completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(60)));
            completed.Should().BeSameAs(run, "the run must not hang when a chunk throws");

            // Reading Exception is what actually observes the fault - awaiting a continuation that ignores
            // its antecedent does not, leaving the run to resurface as an UnobservedTaskException later and
            // destabilize unrelated tests. It doubles as the assertion that the injected failure propagates
            // out of the run rather than being swallowed by the scheduler.
            run.IsFaulted.Should().BeTrue("the injected RecordStart failure must surface, not be swallowed");
            run.Exception.Should().NotBeNull();
        }
        finally
        {
            _frameworkHandle.ThrowOnRecordStartForTest = null;
            DummyTestClassForParallelize.Cleanup();
            DummyTestClassForParallelize2.Cleanup();
        }
    }

    /// <summary>
    /// A dependency graph must not switch <c>OrderTestsByNameInClass</c> off for the whole source. Tests that
    /// are simultaneously ready still run in name order; the graph only constrains the pairs that actually
    /// declare an edge.
    /// </summary>
    public async Task RunTestsWithDependenciesShouldStillOrderUnconstrainedTestsByName()
    {
        // Passed in the order 2, 1, 4 so that declaration order and name order disagree. Only TestMethod4 is
        // constrained (it waits for TestMethod2), leaving 1 and 2 simultaneously ready - and they must run in
        // name order, not the order they were handed over in.
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        TestCase testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
              </MSTest>
            </RunSettings>
            """);

        UnitTestElement[] elements = ToUnitTestElements(testCase2, testCase1, testCase4);
        elements[2].Dependencies = [new TestDependencyInfo(typeof(DummyTestClassWithDoNotParallelizeMethods).FullName, "TestMethod2", proceedOnFailure: false)];

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(elements, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            _frameworkHandle.TestCaseStartList.Should().Equal("TestMethod1", "TestMethod2", "TestMethod4");
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    /// <summary>
    /// A dependency-skipped test is still one of the tests the class-cleanup countdown was built from, so it
    /// has to be accounted for even though it never runs. Otherwise the class never completes: its
    /// <c>[ClassCleanup]</c> is silently lost, and because end-of-assembly cleanup waits on every class
    /// completing, the assembly's <c>[AssemblyCleanup]</c> is lost with it.
    /// </summary>
    public async Task RunTestsWhenADependentIsSkippedShouldStillRunClassCleanup()
    {
        TestCase prereq = GetTestCase(typeof(DummyTestClassWithCleanupAndDependency), "Prereq");
        TestCase dependent = GetTestCase(typeof(DummyTestClassWithCleanupAndDependency), "Dependent");

        UnitTestElement[] elements = ToUnitTestElements(prereq, dependent);
        elements[1].Dependencies = [new TestDependencyInfo(typeof(DummyTestClassWithCleanupAndDependency).FullName, "Prereq", proceedOnFailure: false)];

        // The assertion reads a static of the test class, which lives in whichever app domain ran it, so the
        // run has to stay in this one - the same reason every other static-observing test here disables them.
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(elements, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            // Prereq ran (and failed), so [ClassInitialize] executed and [ClassCleanup] is genuinely owed.
            DummyTestClassWithCleanupAndDependency.ClassCleanupCount.Should().Be(1);

            // The skip itself must still be reported - the cleanup accounting must not swallow it.
            _frameworkHandle.TestCaseEndList.Should().Contain("Dependent:Skipped");
        }
        finally
        {
            DummyTestClassWithCleanupAndDependency.Cleanup();
        }
    }

    /// <summary>
    /// Same accounting requirement for tests failed by a dependency cycle: they are reported without ever
    /// reaching the runner, so the countdown still owes their decrement.
    /// </summary>
    public async Task RunTestsWhenTestsAreBrokenByACycleShouldStillRunClassCleanup()
    {
        TestCase ok = GetTestCase(typeof(DummyTestClassWithCleanupAndCycle), "Ok");
        TestCase inCycleA = GetTestCase(typeof(DummyTestClassWithCleanupAndCycle), "InCycleA");
        TestCase inCycleB = GetTestCase(typeof(DummyTestClassWithCleanupAndCycle), "InCycleB");

        UnitTestElement[] elements = ToUnitTestElements(ok, inCycleA, inCycleB);
        string className = typeof(DummyTestClassWithCleanupAndCycle).FullName!;
        elements[1].Dependencies = [new TestDependencyInfo(className, "InCycleB", proceedOnFailure: false)];
        elements[2].Dependencies = [new TestDependencyInfo(className, "InCycleA", proceedOnFailure: false)];

        // See the sibling test: the static counter is only observable when the run stays in this app domain.
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(elements, CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            // Ok is not in the cycle, so it runs and initializes the class; the two cycle members never reach
            // the runner, yet the class must still complete.
            DummyTestClassWithCleanupAndCycle.ClassCleanupCount.Should().Be(1);
            _frameworkHandle.TestCaseEndList.Should().Contain("InCycleA:Failed").And.Contain("InCycleB:Failed");
        }
        finally
        {
            DummyTestClassWithCleanupAndCycle.Cleanup();
        }
    }

    public async Task RunTestsForTestShouldPreferParallelSettingsFromRunSettingsOverAssemblyLevelAttributes()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new ParallelizeAttribute { Workers = 1 }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>())).
                Returns((MemberInfo memberInfo) => originalReflectionOperation.GetCustomAttributes(memberInfo));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>())).
                Returns(typeof(DummyTestClassForParallelize));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
                .Returns((string assemblyName) => originalFileOperation.LoadAssembly(assemblyName));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            _enqueuedParallelTestsCount.Should().Be(2);

            // Run on 1 or 2 threads
            DummyTestClassForParallelize.ThreadIds.Count.Should().BeOneOf(1, 2);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    // This is tracked by https://github.com/Microsoft/testfx/issues/320.
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForTestShouldRunTestsInTheParentDomainsApartmentState()
    {
        TestCase testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        TestCase testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        TestCase testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        TestCase testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        testCase4.SetPropertyValue(AdapterTestProperties.DoNotParallelizeProperty, true);

        TestCase[] tests = [testCase1, testCase2, testCase3, testCase4];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Workers>3</Workers>
                  <Scope>MethodLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        try
        {
            MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
            await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

            DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.Count.Should().Be(1);
            DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.ToArray()[0].Should().Be(Thread.CurrentThread.GetApartmentState());
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    public async Task RunTestsWithRandomizeTestOrderAndFixedSeedShouldProduceDeterministicOrder()
    {
        static TestCase[] BuildTests() =>
        [
            GetTestCase(typeof(DummyTestClassForOrder), "TestA"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestB"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestC"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestD"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestE"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestF"),
        ];

        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <RandomizeTestOrder>True</RandomizeTestOrder>
                <RandomTestOrderSeed>12345</RandomTestOrderSeed>
                <Parallelize>
                  <Workers>1</Workers>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);

        var firstHandle = new TestableFrameworkHandle();
        var firstManager = new TestExecutionManager(task => task());
        await firstManager.RunTestsAsync(ToUnitTestElements(BuildTests()), CurrentDeploymentContext, firstHandle.ToAdapterMessageLogger(), firstHandle.ToTestResultRecorder(EnvironmentWrapper.Instance.MachineName, MSTestSettings.CurrentSettings), new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        var secondHandle = new TestableFrameworkHandle();
        var secondManager = new TestExecutionManager(task => task());
        await secondManager.RunTestsAsync(ToUnitTestElements(BuildTests()), CurrentDeploymentContext, secondHandle.ToAdapterMessageLogger(), secondHandle.ToTestResultRecorder(EnvironmentWrapper.Instance.MachineName, MSTestSettings.CurrentSettings), new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        // Same seed must produce the same order across separate runs.
        firstHandle.TestCaseStartList.Should().Equal(secondHandle.TestCaseStartList);
        firstHandle.TestCaseStartList.Should().HaveCount(6);

        // Order must differ from the original input (extremely unlikely with seed 12345 across 6 items;
        // this guards against accidentally turning randomization off).
        string[] originalOrder = ["TestA", "TestB", "TestC", "TestD", "TestE", "TestF"];
        firstHandle.TestCaseStartList.Should().NotEqual(originalOrder);
    }

    public async Task RunTestsWithoutRandomizeTestOrderShouldPreserveInputOrder()
    {
        TestCase[] tests =
        [
            GetTestCase(typeof(DummyTestClassForOrder), "TestA"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestB"),
            GetTestCase(typeof(DummyTestClassForOrder), "TestC"),
        ];

        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>1</Workers>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """);

        MSTestSettings.PopulateSettings(_runContext.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);

        await _testExecutionManager.RunTestsAsync(ToUnitTestElements(tests), CurrentDeploymentContext, _frameworkHandle.ToAdapterMessageLogger(), TestResultRecorder, new TestElementFilterProvider(_runContext), new TestRunCancellationToken());

        _frameworkHandle.TestCaseStartList.Should().Equal("TestA", "TestB", "TestC");
    }

    #endregion

    #region private methods

    private static TestCase GetTestCase(Type typeOfClass, string testName)
    {
        MethodInfo methodInfo = typeOfClass.GetMethod(testName)!;
        var testMethod = new TestMethod(methodInfo.Name, hierarchyValues: null, methodInfo.Name, typeOfClass.FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null);
        UnitTestElement element = new(testMethod);
        return element.ToTestCase();
    }

    // Mirrors the conversion the adapter performs at the execution boundary (see MSTestExecutor): each host
    // test case becomes a neutral UnitTestElement carrying the host execution-context (TCM) properties and the
    // originating test case as an opaque recording handle, so recorded results are reported against the exact
    // same TestCase instances the tests build (keeping the framework-handle Verify assertions meaningful).
    private static UnitTestElement[] ToUnitTestElements(params TestCase[] tests)
        => [.. tests.Select(ToUnitTestElement)];

    private static UnitTestElement ToUnitTestElement(TestCase testCase)
    {
        UnitTestElement element = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);
        element.ExecutionContextProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);
        element.HostRecordingHandle = testCase;
        return element;
    }

    private TestablePlatformServiceProvider SetupTestablePlatformService()
    {
        var testablePlatformService = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = testablePlatformService;

        testablePlatformService.MockFileOperations.Setup(td => td.LoadAssembly(It.IsAny<string>()))
            .Returns(
                (string assemblyName) =>
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
                    return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
                }).Callback(() => SetCaller("LoadAssembly"));

        testablePlatformService.MockTestSourceHost.Setup(
            tsh =>
            tsh.CreateInstanceForType(
                It.IsAny<Type>(),
                It.IsAny<object[]>()))
            .Returns(
                (Type type, object[] args) => Activator.CreateInstance(type, args));

        testablePlatformService.MockSettingsProvider.Setup(sp => sp.GetProperties(It.IsAny<string>()))
            .Returns(new Dictionary<string, object>());

        return testablePlatformService;
    }

    private void SetCaller(string caller)
    {
        _callers ??= [];

        _callers.Add(caller);
    }

    private void VerifyTcmProperties(IDictionary<string, object>? tcmProperties, TestCase testCase)
    {
        foreach (TestProperty property in _tcmKnownProperties)
        {
            testCase.GetPropertyValue(property)!.Equals(tcmProperties![property.Id]).Should().BeTrue();
        }
    }

    private void SetTestCaseProperties(TestCase testCase, object[] propertiesValue)
    {
        IEnumerator tcmKnownPropertiesEnumerator = _tcmKnownProperties.GetEnumerator();

        IEnumerator propertiesValueEnumerator = propertiesValue.GetEnumerator();
        while (tcmKnownPropertiesEnumerator.MoveNext() && propertiesValueEnumerator.MoveNext())
        {
            object? property = tcmKnownPropertiesEnumerator.Current;
            object? value = propertiesValueEnumerator.Current;
            testCase.SetPropertyValue((property as TestProperty)!, value);
        }
    }

    #endregion

    #region Dummy implementation

    [DummyTestClass]
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is a MSTest sample class so it's expected to use MSTest assertions")]
    internal class DummyTestClass
    {
        public static IDictionary<string, object>? TestContextProperties { get; set; }

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("Foo")]
        public void PassingTest() => TestContextProperties = TestContext.Properties as IDictionary<string, object>;

        [TestMethod]
        [TestCategory("Bar")]
        public void FailingTest() => Assert.Fail();

        [TestMethod]
        [Ignore]
        public void IgnoredTest() => Assert.Fail();
    }

    [DummyTestClass]
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is a MSTest sample class so it's expected to use MSTest assertions")]
    private sealed class DummyTestClassWithScopedTcmProperties : IDisposable
    {
        private readonly TestContext _testContext;

        public DummyTestClassWithScopedTcmProperties(TestContext testContext)
        {
            _testContext = testContext;
            AssertExpectedTestProperties(testContext);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            AssertSourceProperty(context);
            Assert.IsFalse(context.Properties.ContainsKey(AdapterTestProperties.TestCaseIdProperty.Id));
        }

        [TestInitialize]
        public void TestInitialize() => AssertExpectedTestProperties(_testContext);

        [TestMethod]
        public void TestWithTcmProperty() => AssertExpectedTestProperties(_testContext);

        [TestMethod]
        public void SiblingTest() => AssertExpectedTestProperties(_testContext);

        [TestCleanup]
        public void TestCleanup() => AssertExpectedTestProperties(_testContext);

        public void Dispose() => AssertExpectedTestProperties(_testContext);

        [ClassCleanup]
        public static void ClassCleanup(TestContext context)
        {
            AssertSourceProperty(context);
            Assert.IsFalse(context.Properties.ContainsKey(AdapterTestProperties.TestCaseIdProperty.Id));
        }

        private static void AssertSourceProperty(TestContext context)
            => Assert.AreEqual("SourceValue", context.Properties["SourceProperty"]);

        private static void AssertExpectedTestProperties(TestContext context)
        {
            AssertSourceProperty(context);
            if (context.TestName == nameof(TestWithTcmProperty))
            {
                Assert.AreEqual(1401, context.Properties[AdapterTestProperties.TestCaseIdProperty.Id]);
            }
            else
            {
                Assert.AreEqual(nameof(SiblingTest), context.TestName);
                Assert.IsFalse(context.Properties.ContainsKey(AdapterTestProperties.TestCaseIdProperty.Id));
            }
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithFailingCleanupMethods
    {
        [ClassCleanup]
        public static void ClassCleanup() => throw new Exception("ClassCleanupException");

        [TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupAndDependency
    {
        public static int ClassCleanupCount { get; private set; }

        public static void Cleanup() => ClassCleanupCount = 0;

        [ClassCleanup]
        public static void ClassCleanup() => ClassCleanupCount++;

        [TestMethod]
        public void Prereq() => throw new Exception("Prereq failed on purpose");

        [TestMethod]
        public void Dependent()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupAndCycle
    {
        public static int ClassCleanupCount { get; private set; }

        public static void Cleanup() => ClassCleanupCount = 0;

        [ClassCleanup]
        public static void ClassCleanup() => ClassCleanupCount++;

        [TestMethod]
        public void Ok()
        {
        }

        [TestMethod]
        public void InCycleA()
        {
        }

        [TestMethod]
        public void InCycleB()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public static int ClassCleanupCount { get; private set; }

        public static void Cleanup() => ClassCleanupCount = 0;

        [ClassCleanup]
        public static void ClassCleanup() => ClassCleanupCount++;

        [TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup() => ThreadIds.Clear();

        [TestMethod]
        public void TestMethod1() => ThreadIds.Add(Environment.CurrentManagedThreadId);

        [TestMethod]
        public void TestMethod2() => ThreadIds.Add(Environment.CurrentManagedThreadId);
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize2
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup() => ThreadIds.Clear();

        [TestMethod]
        public void TestMethod1() => ThreadIds.Add(Environment.CurrentManagedThreadId);

        [TestMethod]
        public void TestMethod2() => ThreadIds.Add(Environment.CurrentManagedThreadId);
    }

    [DummyTestClass]
    private sealed class DummyTestClassForParallelize3
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup() => ThreadIds.Clear();

        [TestMethod]
        public void TestMethod1() => ThreadIds.Add(Environment.CurrentManagedThreadId);
    }

    [DummyTestClass]
    private sealed class DummyTestClassForOrder
    {
        [TestMethod]
        public void TestA()
        {
        }

        [TestMethod]
        public void TestB()
        {
        }

        [TestMethod]
        public void TestC()
        {
        }

        [TestMethod]
        public void TestD()
        {
        }

        [TestMethod]
        public void TestE()
        {
        }

        [TestMethod]
        public void TestF()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithDoNotParallelizeMethods
    {
        private static bool s_isFirstUnParallelizedTestRunTimeSet;

        public static HashSet<int> ParallelizableTestsThreadIds { get; private set; } = [];

        public static HashSet<int> UnParallelizableTestsThreadIds { get; private set; } = [];

        public static HashSet<ApartmentState> ThreadApartmentStates { get; private set; } = [];

        public static DateTime LastParallelizableTestRun { get; set; }

        public static DateTime FirstUnParallelizableTestRun { get; set; }

        public static void Cleanup()
        {
            ParallelizableTestsThreadIds.Clear();
            UnParallelizableTestsThreadIds.Clear();
            ThreadApartmentStates.Clear();
            s_isFirstUnParallelizedTestRunTimeSet = false;
        }

        [TestMethod]
        public void TestMethod1()
        {
            ParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [TestMethod]
        public void TestMethod2()
        {
            ParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMethod3()
        {
            if (!s_isFirstUnParallelizedTestRunTimeSet)
            {
                FirstUnParallelizableTestRun = DateTime.Now;
            }

            UnParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMethod4()
        {
            if (!s_isFirstUnParallelizedTestRunTimeSet)
            {
                FirstUnParallelizableTestRun = DateTime.Now;
            }

            UnParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());
        }
    }

    private class DummyTestClassAttribute : TestClassAttribute;

    #endregion
}

#region Testable implementations

internal sealed class TestableFrameworkHandle : IFrameworkHandle
{
    public TestableFrameworkHandle()
    {
        MessageList = [];
        ResultsList = [];
        TestCaseStartList = [];
        TestCaseEndList = [];
        TestDisplayNameList = [];
    }

    public bool EnableShutdownAfterTestRun { get; set; }

    public List<string> MessageList { get; }

    public List<string> ResultsList { get; }

    public List<string> TestCaseStartList { get; }

    public List<string> TestCaseEndList { get; }

    public List<string> TestDisplayNameList { get; }

    /// <summary>
    /// When set, <see cref="RecordStart"/> throws for the test whose display name matches. Used to simulate a
    /// chunk that fails outside the test body (a host-side recording failure), which the dependency scheduler
    /// must survive without stranding the workers waiting on that chunk.
    /// </summary>
    public string? ThrowOnRecordStartForTest { get; set; }

    public void RecordResult(TestResult testResult)
    {
        ResultsList.Add(testResult.ToString());
        TestDisplayNameList.Add(testResult.DisplayName!);
    }

    public void SendMessage(TestMessageLevel testMessageLevel, string message) => MessageList.Add($"{testMessageLevel}:{message}");

    public void RecordStart(TestCase testCase)
    {
        if (ThrowOnRecordStartForTest is { } failing && string.Equals(testCase.DisplayName, failing, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Simulated host failure while recording the start of '{failing}'.");
        }

        TestCaseStartList.Add(testCase.DisplayName);
    }

    public void RecordEnd(TestCase testCase, TestOutcome outcome) => TestCaseEndList.Add($"{testCase.DisplayName}:{outcome}");

    public void RecordAttachments(IList<AttachmentSet> attachmentSets) => throw new NotImplementedException();

    public int LaunchProcessWithDebuggerAttached(
        string filePath,
        string? workingDirectory,
        string? arguments,
        IDictionary<string, string?>? environmentVariables) => throw new NotImplementedException();
}

internal sealed class TestableRunContextTestExecutionTests : IRunContext
{
    private readonly Func<ITestCaseFilterExpression> _getFilter;

    public TestableRunContextTestExecutionTests(Func<ITestCaseFilterExpression> getFilter)
    {
        _getFilter = getFilter;
        MockRunSettings = new Mock<IRunSettings>();
    }

    public Mock<IRunSettings> MockRunSettings { get; set; }

    public IRunSettings RunSettings => MockRunSettings.Object;

    public bool KeepAlive { get; }

    public bool InIsolation { get; }

    public bool IsDataCollectionEnabled { get; }

    public bool IsBeingDebugged { get; }

    public string? TestRunDirectory { get; }

    public string? SolutionDirectory { get; }

    public ITestCaseFilterExpression GetTestCaseFilter(
        IEnumerable<string>? supportedProperties,
        Func<string, TestProperty?> propertyProvider) => _getFilter();
}

internal sealed class TestableTestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly Func<TestCase, bool> _matchTest;

    public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase) => _matchTest = matchTestCase;

    public string TestCaseFilterValue => null!;

    public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider) => _matchTest(testCase);
}

internal class TestableTestExecutionManager : TestExecutionManager
{
    internal Action<IEnumerable<UnitTestElement>, DeploymentContext, IAdapterMessageLogger, ITestResultRecorder, bool> ExecuteTestsWrapper { get; set; } = null!;

    internal override Task ExecuteTestsAsync(IEnumerable<UnitTestElement> tests, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, bool isDeploymentDone)
    {
        ExecuteTestsWrapper?.Invoke(tests, deploymentContext, messageLogger, testResultRecorder, isDeploymentDone);
        return Task.CompletedTask;
    }

    internal override UnitTestDiscoverer GetUnitTestDiscoverer(ITestSourceHandler testSourceHandler) => new TestableUnitTestDiscoverer(testSourceHandler);
}
#endregion

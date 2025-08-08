// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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

    private readonly TestProperty[] _tcmKnownProperties =
    [
        EngineConstants.TestRunIdProperty,
        EngineConstants.TestPlanIdProperty,
        EngineConstants.BuildConfigurationIdProperty,
        EngineConstants.BuildDirectoryProperty,
        EngineConstants.BuildFlavorProperty,
        EngineConstants.BuildNumberProperty,
        EngineConstants.BuildPlatformProperty,
        EngineConstants.BuildUriProperty,
        EngineConstants.TfsServerCollectionUrlProperty,
        EngineConstants.TfsTeamProjectProperty,
        EngineConstants.IsInLabEnvironmentProperty,
        EngineConstants.TestCaseIdProperty,
        EngineConstants.TestConfigurationIdProperty,
        EngineConstants.TestConfigurationNameProperty,
        EngineConstants.TestPointIdProperty,
    ];

    private TestableRunContextTestExecutionTests _runContext;
    private List<string> _callers = [];
    private int _enqueuedParallelTestsCount;

    public TestExecutionManagerTests()
    {
        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression(_ => true));
        _frameworkHandle = new TestableFrameworkHandle();
        _cancellationToken = new TestRunCancellationToken();
        _mockMessageLogger = new Mock<IMessageLogger>(MockBehavior.Loose);

        _testExecutionManager = new TestExecutionManager(
            new EnvironmentWrapper(),
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

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, _cancellationToken);

        // No Results
        Verify(_frameworkHandle.TestCaseStartList.Count == 0);
        Verify(_frameworkHandle.ResultsList.Count == 0);
        Verify(_frameworkHandle.TestCaseEndList.Count == 0);
    }

    public async Task RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression(p => p.DisplayName == "PassingTest"));

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, _cancellationToken);

        // FailingTest should be skipped because it does not match the filter criteria.
        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList.SequenceEqual(_frameworkHandle.ResultsList));
    }

    public async Task RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "IgnoredTest");
        TestCase[] tests = [testCase];

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, _cancellationToken);

        Verify(_frameworkHandle.TestCaseStartList[0] == "IgnoredTest");
        Verify(_frameworkHandle.TestCaseEndList[0] == "IgnoredTest:Skipped");
        Verify(_frameworkHandle.ResultsList[0] == "IgnoredTest  Skipped");
    }

    public async Task RunTestsForASingleTestShouldSendSingleResult()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList.SequenceEqual(_frameworkHandle.ResultsList));
    }

    public async Task RunTestsForMultipleTestShouldSendMultipleResults()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, _cancellationToken);

        List<string> expectedTestCaseStartList = ["PassingTest", "FailingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed", "FailingTest:Failed"];
        List<string> expectedResultList = ["PassingTest  Passed", "FailingTest  Failed\r\n  Message: Assert.Fail failed."];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList[0] == _frameworkHandle.ResultsList[0]);
        Verify(_frameworkHandle.ResultsList[1].Contains(expectedResultList[1]));
    }

    public async Task RunTestsForCancellationTokenCanceledSetToTrueShouldSendZeroResults()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        // Cancel the test run
        _cancellationToken.Cancel();
        Exception exception = await VerifyThrowsAsync(() => _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, _cancellationToken));

        Verify(exception is OperationCanceledException);

        // No Results
        Verify(_frameworkHandle.TestCaseStartList.Count == 0);
        Verify(_frameworkHandle.ResultsList.Count == 0);
        Verify(_frameworkHandle.TestCaseEndList.Count == 0);
    }

    public async Task RunTestsForTestShouldDeployBeforeExecution()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Callback(() => SetCaller("Deploy"));

        await _testExecutionManager.RunTestsAsync(
            tests,
            _runContext,
            _frameworkHandle,
            new TestRunCancellationToken());

        Verify(_callers[0] == "Deploy", "Deploy should be called before execution.");
        Verify(_callers[1] == "LoadAssembly", "Deploy should be called before execution.");
    }

    public async Task RunTestsForTestShouldCleanupAfterExecution()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Cleanup()).Callback(() => SetCaller("Cleanup"));

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify(_callers[0] == "LoadAssembly", "Cleanup should be called after execution.");
        Verify(_callers.LastOrDefault() == "Cleanup", "Cleanup should be called after execution.");
    }

    public async Task RunTestsForTestShouldNotCleanupOnTestFailure()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Returns(true);
        testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
            .Returns(@"C:\temp");

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockFileOperations.Verify(
            fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp")), It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

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

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify(DummyTestClass.TestContextProperties!.Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
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

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        VerifyTcmProperties(DummyTestClass.TestContextProperties, testCase);
    }

    public async Task RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        TestCase testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();

        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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
        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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
        await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify("http://updatedLocalHost".Equals(DummyTestClass.TestContextProperties!["webAppUrl"]));
    }

    #endregion

    #region Run Tests on Sources

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForSourceShouldRunTestsInASource()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        await _testExecutionManager.RunTestsAsync(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(_frameworkHandle.TestCaseStartList.Contains("PassingTest"));
        Verify(_frameworkHandle.TestCaseEndList.Contains("PassingTest:Passed"));
        Verify(_frameworkHandle.ResultsList.Contains("PassingTest  Passed"));
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

        await _testExecutionManager.RunTestsAsync(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(
            DummyTestClass.TestContextProperties!.Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private async Task RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        await _testExecutionManager.RunTestsAsync(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(DummyTestClass.TestContextProperties is not null);
    }

    public async Task RunTestsForMultipleSourcesShouldRunEachTestJustOnce()
    {
        int testsCount = 0;
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location };
        TestableTestExecutionManager testableTestExecutionManager = new()
        {
            ExecuteTestsWrapper = (tests, runContext, frameworkHandle, isDeploymentDone) => testsCount += tests.Count(),
        };

        await testableTestExecutionManager.RunTestsAsync(sources, _runContext, _frameworkHandle, _cancellationToken);
        Verify(testsCount == 4);
    }

    #endregion

    #region SendTestResults tests

    public void SendTestResultsShouldFillInDataRowIndexIfTestIsDataDriven()
    {
        var testCase = new TestCase("DummyTest", new Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        TestTools.UnitTesting.TestResult unitTestResult1 = new() { DatarowIndex = 0, DisplayName = "DummyTest" };
        TestTools.UnitTesting.TestResult unitTestResult2 = new() { DatarowIndex = 1, DisplayName = "DummyTest" };
        _testExecutionManager.SendTestResults(testCase, [unitTestResult1, unitTestResult2], default, default, _frameworkHandle);
        Verify(_frameworkHandle.TestDisplayNameList[0] == "DummyTest (Data Row 0)");
        Verify(_frameworkHandle.TestDisplayNameList[1] == "DummyTest (Data Row 1)");
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
            Verify(DummyTestClassForParallelize2.ThreadIds.Count == 1);
            Verify(_enqueuedParallelTestsCount == 2);
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(_enqueuedParallelTestsCount == 2);

            // Run on 1 or 2 threads
            Verify(DummyTestClassForParallelize.ThreadIds.Count is 1 or 2);
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
            Verify(DummyTestClassForParallelize2.ThreadIds.Count == 1);
            Verify(DummyTestClassForParallelize3.ThreadIds.Count == 1);

            Verify(_enqueuedParallelTestsCount == 3);
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations2();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new ParallelizeAttribute { Workers = 10, Scope = ExecutionScope.MethodLevel }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) => originalReflectionOperation.GetCustomAttributes(memberInfo, inherit));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((string assemblyName, bool reflectionOnly) => originalFileOperation.LoadAssembly(assemblyName, reflectionOnly));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations2();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(DoNotParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new DoNotParallelizeAttribute()]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) => originalReflectionOperation.GetCustomAttributes(memberInfo, inherit));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((string assemblyName, bool reflectionOnly) => originalFileOperation.LoadAssembly(assemblyName, reflectionOnly));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
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

        testCase3.SetPropertyValue(EngineConstants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(EngineConstants.DoNotParallelizeProperty, true);

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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(_enqueuedParallelTestsCount == 2);
            Verify(DummyTestClassWithDoNotParallelizeMethods.ParallelizableTestsThreadIds.Count is 1 or 2);
            Verify(DummyTestClassWithDoNotParallelizeMethods.UnParallelizableTestsThreadIds.Count == 1);
            Verify(DummyTestClassWithDoNotParallelizeMethods.LastParallelizableTestRun.TimeOfDay.TotalMilliseconds <= DummyTestClassWithDoNotParallelizeMethods.FirstUnParallelizableTestRun.TimeOfDay.TotalMilliseconds);
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            TestablePlatformServiceProvider testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations2();
            var originalFileOperation = new FileOperations();

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetDeclaredConstructors(It.IsAny<Type>()))
                .Returns((Type classType) => originalReflectionOperation.GetDeclaredConstructors(classType));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) => type.FullName!.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? [new ParallelizeAttribute { Workers = 1 }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) => originalReflectionOperation.GetCustomAttributes(memberInfo, inherit));

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>())).
                Returns(typeof(DummyTestClassForParallelize));

            testablePlatformService.MockReflectionOperations.Setup(ro => ro.GetType(It.IsAny<Assembly>(), It.IsAny<string>()))
                .Returns((Assembly asm, string m) => originalReflectionOperation.GetType(asm, m));

            testablePlatformService.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((string assemblyName, bool reflectionOnly) => originalFileOperation.LoadAssembly(assemblyName, reflectionOnly));

            testablePlatformService.MockReflectionOperations.Setup(fo => fo.GetRuntimeMethods(It.IsAny<Type>()))
                .Returns((Type t) => originalReflectionOperation.GetRuntimeMethods(t));

            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(_enqueuedParallelTestsCount == 2);

            // Run on 1 or 2 threads
            Verify(DummyTestClassForParallelize.ThreadIds.Count is 1 or 2);
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

        testCase4.SetPropertyValue(EngineConstants.DoNotParallelizeProperty, true);

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
            MSTestSettings.PopulateSettings(_runContext, _mockMessageLogger.Object, null);
            await _testExecutionManager.RunTestsAsync(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.Count == 1);
            Verify(Thread.CurrentThread.GetApartmentState() == DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.ToArray()[0]);
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    #endregion

    #region private methods

    private static TestCase GetTestCase(Type typeOfClass, string testName)
    {
        MethodInfo methodInfo = typeOfClass.GetMethod(testName)!;
        var testMethod = new TestMethod(methodInfo.Name, typeOfClass.FullName!, Assembly.GetExecutingAssembly().Location, isAsync: false);
        UnitTestElement element = new(testMethod);
        return element.ToTestCase();
    }

    private TestablePlatformServiceProvider SetupTestablePlatformService()
    {
        var testablePlatformService = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = testablePlatformService;

        testablePlatformService.MockFileOperations.Setup(td => td.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(
                (string assemblyName, bool reflectionOnly) =>
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
            Verify(testCase.GetPropertyValue(property)!.Equals(tcmProperties![property.Id]));
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

    public void RecordResult(TestResult testResult)
    {
        ResultsList.Add(testResult.ToString());
        TestDisplayNameList.Add(testResult.DisplayName!);
    }

    public void SendMessage(TestMessageLevel testMessageLevel, string message) => MessageList.Add($"{testMessageLevel}:{message}");

    public void RecordStart(TestCase testCase) => TestCaseStartList.Add(testCase.DisplayName);

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
        MockRunSettings = new Mock<IRunSettings>(MockBehavior.Loose);
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
    internal Action<IEnumerable<TestCase>, IRunContext?, IFrameworkHandle, bool> ExecuteTestsWrapper { get; set; } = null!;

    internal override Task ExecuteTestsAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
    {
        ExecuteTestsWrapper?.Invoke(tests, runContext, frameworkHandle, isDeploymentDone);
        return Task.CompletedTask;
    }

    internal override UnitTestDiscoverer GetUnitTestDiscoverer() => new TestableUnitTestDiscoverer();
}
#endregion

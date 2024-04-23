// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using TestAdapterConstants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;
using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestExecutionManagerTests : TestContainer
{
    private readonly TestableFrameworkHandle _frameworkHandle;
    private readonly TestRunCancellationToken _cancellationToken;
    private readonly TestExecutionManager _testExecutionManager;
    private readonly TestProperty[] _tcmKnownProperties =
    [
        TestAdapterConstants.TestRunIdProperty,
        TestAdapterConstants.TestPlanIdProperty,
        TestAdapterConstants.BuildConfigurationIdProperty,
        TestAdapterConstants.BuildDirectoryProperty,
        TestAdapterConstants.BuildFlavorProperty,
        TestAdapterConstants.BuildNumberProperty,
        TestAdapterConstants.BuildPlatformProperty,
        TestAdapterConstants.BuildUriProperty,
        TestAdapterConstants.TfsServerCollectionUrlProperty,
        TestAdapterConstants.TfsTeamProjectProperty,
        TestAdapterConstants.IsInLabEnvironmentProperty,
        TestAdapterConstants.TestCaseIdProperty,
        TestAdapterConstants.TestConfigurationIdProperty,
        TestAdapterConstants.TestConfigurationNameProperty,
        TestAdapterConstants.TestPointIdProperty,
    ];

    private TestableRunContextTestExecutionTests _runContext;
    private List<string> _callers;
    private int _enqueuedParallelTestsCount;

    public TestExecutionManagerTests()
    {
        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => true));
        _frameworkHandle = new TestableFrameworkHandle();
        _cancellationToken = new TestRunCancellationToken();

        _testExecutionManager = new TestExecutionManager(
            new EnvironmentWrapper(),
            action => Task.Factory.StartNew(
                () =>
                {
                    _enqueuedParallelTestsCount++;
                    action();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default));
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

    public void RunTestsForTestWithFilterErrorShouldSendZeroResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        // Causing the FilterExpressionError
        _runContext = new TestableRunContextTestExecutionTests(() => { throw new TestPlatformFormatException(); });

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // No Results
        Verify(_frameworkHandle.TestCaseStartList.Count == 0);
        Verify(_frameworkHandle.ResultsList.Count == 0);
        Verify(_frameworkHandle.TestCaseEndList.Count == 0);
    }

    public void RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => p.DisplayName == "PassingTest"));

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // FailingTest should be skipped because it does not match the filter criteria.
        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList.SequenceEqual(_frameworkHandle.ResultsList));
    }

    public void RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "IgnoredTest", ignore: true);
        TestCase[] tests = [testCase];

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        Verify(_frameworkHandle.TestCaseStartList[0] == "IgnoredTest");
        Verify(_frameworkHandle.TestCaseEndList[0] == "IgnoredTest:Skipped");
        Verify(_frameworkHandle.ResultsList[0] == "IgnoredTest  Skipped");
    }

    public void RunTestsForASingleTestShouldSendSingleResult()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        List<string> expectedTestCaseStartList = ["PassingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed"];
        List<string> expectedResultList = ["PassingTest  Passed"];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList.SequenceEqual(_frameworkHandle.ResultsList));
    }

    public void RunTestsForMultipleTestShouldSendMultipleResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        List<string> expectedTestCaseStartList = ["PassingTest", "FailingTest"];
        List<string> expectedTestCaseEndList = ["PassingTest:Passed", "FailingTest:Failed"];
        List<string> expectedResultList = ["PassingTest  Passed", "FailingTest  Failed\r\n  Message: Assert.Fail failed."];

        Verify(expectedTestCaseStartList.SequenceEqual(_frameworkHandle.TestCaseStartList));
        Verify(expectedTestCaseEndList.SequenceEqual(_frameworkHandle.TestCaseEndList));
        Verify(expectedResultList[0] == _frameworkHandle.ResultsList[0]);
        Verify(_frameworkHandle.ResultsList[1].Contains(expectedResultList[1]));
    }

    public void RunTestsForCancellationTokenCanceledSetToTrueShouldSendZeroResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];

        // Cancel the test run
        _cancellationToken.Cancel();
        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // No Results
        Verify(_frameworkHandle.TestCaseStartList.Count == 0);
        Verify(_frameworkHandle.ResultsList.Count == 0);
        Verify(_frameworkHandle.TestCaseEndList.Count == 0);
    }

    public void RunTestsForTestShouldDeployBeforeExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Callback(() => SetCaller("Deploy"));

        _testExecutionManager.RunTests(
            tests,
            _runContext,
            _frameworkHandle,
            new TestRunCancellationToken());

        Verify(_callers[0] == "Deploy", "Deploy should be called before execution.");
        Verify(_callers[1] == "LoadAssembly", "Deploy should be called before execution.");
    }

    public void RunTestsForTestShouldCleanupAfterExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Cleanup()).Callback(() => SetCaller("Cleanup"));

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify(_callers[0] == "LoadAssembly", "Cleanup should be called after execution.");
        Verify(_callers.LastOrDefault() == "Cleanup", "Cleanup should be called after execution.");
    }

    public void RunTestsForTestShouldNotCleanupOnTestFailure()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        var testablePlatformService = SetupTestablePlatformService();
        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockTestDeployment.Verify(td => td.Cleanup(), Times.Never);
    }

    public void RunTestsForTestShouldLoadSourceFromDeploymentDirectoryIfDeployed()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = [testCase, failingTestCase];

        var testablePlatformService = SetupTestablePlatformService();

        // Setup mocks.
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Returns(true);
        testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
            .Returns(@"C:\temp");

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockFileOperations.Verify(
            fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp")), It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

    public void RunTestsForTestShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings>
                                            <RunConfiguration>  
                                                <DisableAppDomain>True</DisableAppDomain>   
                                            </RunConfiguration>  
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify(DummyTestClass.TestContextProperties.ToList().Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
    }

    public void RunTestsForTestShouldPassInTcmPropertiesAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var propertiesValue = new object[] { 32, 534, 5, "sample build directory", "sample build flavor", "132456", "sample build platform", "http://sampleBuildUti/", "http://samplecollectionuri/", "sample team project", false, 1401, 54, "sample configuration name", 345 };
        SetTestCaseProperties(testCase, propertiesValue);

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>  
            </RunSettings>");

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        VerifyTcmProperties(DummyTestClass.TestContextProperties, testCase);
    }

    public void RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = [testCase];

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();

        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockSettingsProvider.Verify(sp => sp.GetProperties(It.IsAny<string>()), Times.Once);
    }

    public void RunTestsShouldClearSessionParametersAcrossRuns()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = [testCase];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            @"<RunSettings>
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>
                <TestRunParameters>
                    <Parameter name=""webAppUrl"" value=""http://localhost"" />
                    <Parameter name = ""webAppUserName"" value=""Admin"" />
                </TestRunParameters>
            </RunSettings>");

        // Trigger First Run
        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        // Update runsettings to have different values for similar keys
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
            @"<RunSettings>
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>
                <TestRunParameters>
                    <Parameter name=""webAppUrl"" value=""http://updatedLocalHost"" />
                    <Parameter name = ""webAppUserName"" value=""Admin"" />
                </TestRunParameters>
            </RunSettings>");

        // Trigger another Run
        _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify("http://updatedLocalHost".Equals(DummyTestClass.TestContextProperties["webAppUrl"]));
    }

    #endregion

    #region Run Tests on Sources

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private void RunTestsForSourceShouldRunTestsInASource()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        _testExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(_frameworkHandle.TestCaseStartList.Contains("PassingTest"));
        Verify(_frameworkHandle.TestCaseEndList.Contains("PassingTest:Passed"));
        Verify(_frameworkHandle.ResultsList.Contains("PassingTest  Passed"));
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private void RunTestsForSourceShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        _testExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(
            DummyTestClass.TestContextProperties.ToList().Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private void RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        _testExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(DummyTestClass.TestContextProperties is not null);
    }

    public void RunTestsForMultipleSourcesShouldRunEachTestJustOnce()
    {
        int testsCount = 0;
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location };
        TestableTestExecutionManager testableTestExecutionManager = new()
        {
            ExecuteTestsWrapper = (tests, runContext, frameworkHandle, isDeploymentDone) =>
            {
                testsCount += tests.Count();
            },
        };

        testableTestExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);
        Verify(testsCount == 4);
    }

    #endregion

    #region SendTestResults tests

    public void SendTestResultsShouldFillInDataRowIndexIfTestIsDataDriven()
    {
        var testCase = new TestCase("DummyTest", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        UnitTestResult unitTestResult1 = new() { DatarowIndex = 0, DisplayName = "DummyTest" };
        UnitTestResult unitTestResult2 = new() { DatarowIndex = 1, DisplayName = "DummyTest" };
        _testExecutionManager.SendTestResults(testCase, new UnitTestResult[] { unitTestResult1, unitTestResult2 }, default, default, _frameworkHandle);
        Verify(_frameworkHandle.TestDisplayNameList[0] == "DummyTest (Data Row 0)");
        Verify(_frameworkHandle.TestDisplayNameList[1] == "DummyTest (Data Row 1)");
    }

    #endregion

    #region Parallel tests

    public void RunTestsForTestShouldRunTestsInParallelWhenEnabledInRunsettings()
    {
        var testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");
        var testCase21 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        var testCase22 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod2");

        TestCase[] tests = [testCase11, testCase12, testCase21, testCase22];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings>
                                              <RunConfiguration>  
                                                  <DisableAppDomain>True</DisableAppDomain>   
                                              </RunConfiguration>
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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

    public void RunTestsForTestShouldRunTestsByMethodLevelWhenSpecified()
    {
        var testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase11, testCase12];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings>
                                              <RunConfiguration>  
                                                  <DisableAppDomain>True</DisableAppDomain>   
                                              </RunConfiguration>
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                   <Scope>MethodLevel</Scope>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(_enqueuedParallelTestsCount == 2);

            // Run on 1 or 2 threads
            Verify(DummyTestClassForParallelize.ThreadIds.Count is 1 or 2);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public void RunTestsForTestShouldRunTestsWithSpecifiedNumberOfWorkers()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        var testCase3 = GetTestCase(typeof(DummyTestClassForParallelize3), "TestMethod1");

        TestCase[] tests = [testCase1, testCase2, testCase3];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings>
                                              <RunConfiguration>  
                                                  <DisableAppDomain>True</DisableAppDomain>   
                                              </RunConfiguration>
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>3</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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

    public void RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromRunsettings()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <RunConfiguration>
                                                 <DisableParallelization>true</DisableParallelization>
                                              </RunConfiguration>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            var testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) =>
                {
                    return type.FullName.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? (object[])[new ParallelizeAttribute { Workers = 10, Scope = ExecutionScope.MethodLevel }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public void RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromSource()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                   <Scope>MethodLevel</Scope>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            var testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) =>
                {
                    return type.FullName.Equals(typeof(DoNotParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? (object[])[new DoNotParallelizeAttribute()]
                        : originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(DummyTestClassForParallelize.ThreadIds.Count == 1);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    public void RunTestsForTestShouldRunNonParallelizableTestsSeparately()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        var testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        var testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        testCase3.SetPropertyValue(TestAdapterConstants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(TestAdapterConstants.DoNotParallelizeProperty, true);

        TestCase[] tests = [testCase1, testCase2, testCase3, testCase4];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings>
                                              <RunConfiguration>  
                                                  <DisableAppDomain>True</DisableAppDomain>   
                                              </RunConfiguration>  
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                   <Scope>MethodLevel</Scope>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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

    public void RunTestsForTestShouldPreferParallelSettingsFromRunSettingsOverAssemblyLevelAttributes()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = [testCase1, testCase2];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                   <Scope>MethodLevel</Scope>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            var testablePlatformService = SetupTestablePlatformService();
            testablePlatformService.SetupMockReflectionOperations();

            var originalReflectionOperation = new ReflectionOperations();

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>())).
                Returns((Assembly asm, Type type) =>
                {
                    return type.FullName.Equals(typeof(ParallelizeAttribute).FullName, StringComparison.Ordinal)
                        ? (object[])[new ParallelizeAttribute { Workers = 1 }]
                        : originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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
    private void RunTestsForTestShouldRunTestsInTheParentDomainsApartmentState()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        var testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        var testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        ////testCase3.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(TestAdapterConstants.DoNotParallelizeProperty, true);

        TestCase[] tests = [testCase1, testCase2, testCase3, testCase4];
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>3</Workers>
                                                   <Scope>MethodLevel</Scope>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            _testExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

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

    private static TestCase GetTestCase(Type typeOfClass, string testName, bool ignore = false)
    {
        var methodInfo = typeOfClass.GetMethod(testName);
        var testMethod = new TestMethod(methodInfo.Name, typeOfClass.FullName, Assembly.GetExecutingAssembly().Location, isAsync: false);
        UnitTestElement element = new(testMethod)
        {
            Ignored = ignore,
        };
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
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
                    return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
                }).Callback(() => SetCaller("LoadAssembly"));

        testablePlatformService.MockTestSourceHost.Setup(
            tsh =>
            tsh.CreateInstanceForType(
                It.IsAny<Type>(),
                It.IsAny<object[]>()))
            .Returns(
                (Type type, object[] args) =>
                    {
                        return Activator.CreateInstance(type, args);
                    });

        testablePlatformService.MockSettingsProvider.Setup(sp => sp.GetProperties(It.IsAny<string>()))
            .Returns(new Dictionary<string, object>());

        return testablePlatformService;
    }

    private void SetCaller(string caller)
    {
        _callers ??= [];

        _callers.Add(caller);
    }

    private void VerifyTcmProperties(IDictionary<string, object> tcmProperties, TestCase testCase)
    {
        foreach (var property in _tcmKnownProperties)
        {
            Verify(testCase.GetPropertyValue(property).Equals(tcmProperties[property.Id]));
        }
    }

    private void SetTestCaseProperties(TestCase testCase, object[] propertiesValue)
    {
        var tcmKnownPropertiesEnumerator = _tcmKnownProperties.GetEnumerator();

        var propertiesValueEnumerator = propertiesValue.GetEnumerator();
        while (tcmKnownPropertiesEnumerator.MoveNext() && propertiesValueEnumerator.MoveNext())
        {
            var property = tcmKnownPropertiesEnumerator.Current;
            var value = propertiesValueEnumerator.Current;
            testCase.SetPropertyValue(property as TestProperty, value);
        }
    }

    #endregion

    #region Dummy implementation

    [DummyTestClass]
    internal class DummyTestClass
    {
        public static IDictionary<string, object> TestContextProperties
        {
            get;
            set;
        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestCategory("Foo")]
        public void PassingTest()
        {
            TestContextProperties = TestContext.Properties as IDictionary<string, object>;
        }

        [TestMethod]
        [TestCategory("Bar")]
        public void FailingTest()
        {
            Assert.Fail();
        }

        [TestMethod]
        [Ignore]
        public void IgnoredTest()
        {
            Assert.Fail();
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithFailingCleanupMethods
    {
        [ClassCleanup]
        public static void ClassCleanup()
        {
            throw new Exception("ClassCleanupException");
        }

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
        public static void ClassCleanup()
        {
            ClassCleanupCount++;
        }

        [TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [TestMethod]
        public void TestMethod1()
        {
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }

        [TestMethod]
        public void TestMethod2()
        {
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize2
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [TestMethod]
        public void TestMethod1()
        {
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }

        [TestMethod]
        public void TestMethod2()
        {
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    [DummyTestClass]
    private sealed class DummyTestClassForParallelize3
    {
        public static HashSet<int> ThreadIds { get; } = [];

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [TestMethod]
        public void TestMethod1()
        {
            ThreadIds.Add(Environment.CurrentManagedThreadId);
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

    private class DummyTestClassAttribute : TestClassAttribute
    {
    }

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
        TestDisplayNameList.Add(testResult.DisplayName);
    }

    public void SendMessage(TestMessageLevel testMessageLevel, string message)
    {
        MessageList.Add($"{testMessageLevel}:{message}");
    }

    public void RecordStart(TestCase testCase)
    {
        TestCaseStartList.Add(testCase.DisplayName);
    }

    public void RecordEnd(TestCase testCase, TestOutcome outcome)
    {
        TestCaseEndList.Add($"{testCase.DisplayName}:{outcome}");
    }

    public void RecordAttachments(IList<AttachmentSet> attachmentSets)
    {
        throw new NotImplementedException();
    }

    public int LaunchProcessWithDebuggerAttached(
        string filePath,
        string workingDirectory,
        string arguments,
        IDictionary<string, string> environmentVariables)
    {
        throw new NotImplementedException();
    }
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

    public IRunSettings RunSettings
    {
        get
        {
            return MockRunSettings.Object;
        }
    }

    public bool KeepAlive { get; }

    public bool InIsolation { get; }

    public bool IsDataCollectionEnabled { get; }

    public bool IsBeingDebugged { get; }

    public string TestRunDirectory { get; }

    public string SolutionDirectory { get; }

    public ITestCaseFilterExpression GetTestCaseFilter(
        IEnumerable<string> supportedProperties,
        Func<string, TestProperty> propertyProvider)
    {
        return _getFilter();
    }
}

internal sealed class TestableTestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly Func<TestCase, bool> _matchTest;

    public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase)
    {
        _matchTest = matchTestCase;
    }

    public string TestCaseFilterValue { get; }

    public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
    {
        return _matchTest(testCase);
    }
}

internal class TestableTestExecutionManager : TestExecutionManager
{
    internal Action<IEnumerable<TestCase>, IRunContext, IFrameworkHandle, bool> ExecuteTestsWrapper { get; set; }

    internal override void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
    {
        ExecuteTestsWrapper?.Invoke(tests, runContext, frameworkHandle, isDeploymentDone);
    }

    internal override UnitTestDiscoverer GetUnitTestDiscoverer()
    {
        return new TestableUnitTestDiscoverer();
    }
}
#endregion

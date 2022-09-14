// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Moq;
using MSTest.TestAdapter;

using TestFramework.ForTestingMSTest;

using TestAdapterConstants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;
using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestExecutionManagerTests : TestContainer
{
    private TestableRunContextTestExecutionTests _runContext;

    private TestableFrameworkHandle _frameworkHandle;

    private TestRunCancellationToken _cancellationToken;

    private List<string> _callers;

    private TestExecutionManager TestExecutionManager { get; set; }

    private readonly TestProperty[] _tcmKnownProperties = new TestProperty[]
    {
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
        TestAdapterConstants.TestPointIdProperty
    };

    public TestExecutionManagerTests()
    {
        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => true));
        _frameworkHandle = new TestableFrameworkHandle();
        _cancellationToken = new TestRunCancellationToken();

        TestExecutionManager = new TestExecutionManager();
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

        TestCase[] tests = new[] { testCase };

        // Causing the FilterExpressionError
        _runContext = new TestableRunContextTestExecutionTests(() => { throw new TestPlatformFormatException(); });

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // No Results
        Verify(0 == _frameworkHandle.TestCaseStartList.Count);
        Verify(0 == _frameworkHandle.ResultsList.Count);
        Verify(0 == _frameworkHandle.TestCaseEndList.Count);
    }

    public void RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        _runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => (p.DisplayName == "PassingTest")));

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // FailingTest should be skipped because it does not match the filter criteria.
        List<string> expectedTestCaseStartList = new() { "PassingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed" };
        List<string> expectedResultList = new() { "PassingTest  Passed" };

        VerifyCollectionsAreEqual(expectedTestCaseStartList, _frameworkHandle.TestCaseStartList);
        VerifyCollectionsAreEqual(expectedTestCaseEndList, _frameworkHandle.TestCaseEndList);
        VerifyCollectionsAreEqual(expectedResultList, _frameworkHandle.ResultsList);
    }

    public void RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "IgnoredTest", ignore: true);
        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        Verify("IgnoredTest" == _frameworkHandle.TestCaseStartList[0]);
        Verify("IgnoredTest:Skipped" == _frameworkHandle.TestCaseEndList[0]);
        Verify("IgnoredTest  Skipped" == _frameworkHandle.ResultsList[0]);
    }

    public void RunTestsForASingleTestShouldSendSingleResult()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        List<string> expectedTestCaseStartList = new() { "PassingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed" };
        List<string> expectedResultList = new() { "PassingTest  Passed" };

        VerifyCollectionsAreEqual(expectedTestCaseStartList, _frameworkHandle.TestCaseStartList);
        VerifyCollectionsAreEqual(expectedTestCaseEndList, _frameworkHandle.TestCaseEndList);
        VerifyCollectionsAreEqual(expectedResultList, _frameworkHandle.ResultsList);
    }

    public void RunTestsForMultipleTestShouldSendMultipleResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        List<string> expectedTestCaseStartList = new() { "PassingTest", "FailingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed", "FailingTest:Failed" };
        List<string> expectedResultList = new() { "PassingTest  Passed", "FailingTest  Failed\r\n  Message: Assert.Fail failed." };

        VerifyCollectionsAreEqual(expectedTestCaseStartList, _frameworkHandle.TestCaseStartList);
        VerifyCollectionsAreEqual(expectedTestCaseEndList, _frameworkHandle.TestCaseEndList);
        Verify(expectedResultList[0] == _frameworkHandle.ResultsList[0]);
        Verify(_frameworkHandle.ResultsList[1].Contains(expectedResultList[1]));
    }

    public void RunTestsForCancellationTokencanceledSetToTrueShouldSendZeroResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };

        // Cancel the test run
        _cancellationToken.Cancel();
        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, _cancellationToken);

        // No Results
        Verify(0 == _frameworkHandle.TestCaseStartList.Count);
        Verify(0 == _frameworkHandle.ResultsList.Count);
        Verify(0 == _frameworkHandle.TestCaseEndList.Count);
    }

    public void RunTestsShouldLogResultCleanupWarningsAsErrorsWhenTreatClassCleanupWarningsAsErrorsIsTrue()
    {
        // Arrange
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <TreatClassAndAssemblyCleanupWarningsAsErrors>true</TreatClassAndAssemblyCleanupWarningsAsErrors>
                                              </MSTest>
                                            </RunSettings>");
        MSTestSettings.PopulateSettings(_runContext);
        var testCase = GetTestCase(typeof(DummyTestClassWithFailingCleanupMethods), "TestMethod");
        TestCase[] tests = new[] { testCase };

        // Act
        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        // Assert
        Verify(_frameworkHandle.TestCaseEndList.Contains("TestMethod:Passed"));
        Verify(1 == _frameworkHandle.MessageList.Count);
        Verify(_frameworkHandle.MessageList[0].StartsWith("Error"));
        Verify(_frameworkHandle.MessageList[0].Contains("ClassCleanupException"));
    }

    public void RunTestsShouldLogResultOutput()
    {
        var testCase = GetTestCase(typeof(DummyTestClassWithFailingCleanupMethods), "TestMethod");
        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        // Warnings should get logged.
        Verify(1 == _frameworkHandle.MessageList.Count);
        Verify(_frameworkHandle.MessageList[0].StartsWith("Warning"));
        Verify(_frameworkHandle.MessageList[0].Contains("ClassCleanupException"));
    }

    public void RunTestsForTestShouldDeployBeforeExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Callback(() => SetCaller("Deploy"));

        TestExecutionManager.RunTests(
            tests,
            _runContext,
            _frameworkHandle,
            new TestRunCancellationToken());

        Verify("Deploy" == _callers[0], "Deploy should be called before execution.");
        Verify("LoadAssembly" == _callers[1], "Deploy should be called before execution.");
    }

    public void RunTestsForTestShouldCleanupAfterExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Cleanup()).Callback(() => SetCaller("Cleanup"));

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify("LoadAssembly" == _callers[0], "Cleanup should be called after execution.");
        Verify("Cleanup" == _callers.LastOrDefault(), "Cleanup should be called after execution.");
    }

    public void RunTestsForTestShouldNotCleanupOnTestFailure()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        var testablePlatformService = SetupTestablePlatformService();
        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockTestDeployment.Verify(td => td.Cleanup(), Times.Never);
    }

    public void RunTestsForTestShouldLoadSourceFromDeploymentDirectoryIfDeployed()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        var testablePlatformService = SetupTestablePlatformService();

        // Setup mocks.
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, _runContext, _frameworkHandle)).Returns(true);
        testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
            .Returns(@"C:\temp");

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockFileOperations.Verify(
            fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp")), It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

    public void RunTestsForTestShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify(DummyTestClass.TestContextProperties.ToList().Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
    }

    public void RunTestsForTestShouldPassInTcmPropertiesAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var propertiesValue = new object[] { 32, 534, 5, "sample build directory", "sample build flavor", "132456", "sample build platform", "http://sampleBuildUti/", "http://samplecollectionuri/", "sample team project", false, 1401, 54, "sample configuration name", 345 };
        SetTestCaseProperties(testCase, propertiesValue);

        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        VerifyTcmProperties(DummyTestClass.TestContextProperties, testCase);
    }

    public void RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();

        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockSettingsProvider.Verify(sp => sp.GetProperties(It.IsAny<string>()), Times.Once);
    }

    public void RunTestsShouldClearSessionParametersAcrossRuns()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        // Trigger First Run
        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        // Update runsettings to have different values for similar keys
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                         @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://updatedLocalHost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        // Trigger another Run
        TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

        Verify("http://updatedLocalHost" == DummyTestClass.TestContextProperties["webAppUrl"] as string);
    }

    #endregion

    #region Run Tests on Sources

    // TODO: This tests needs to be mocked.

    private void RunTestsForSourceShouldRunTestsInASource()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        TestExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(_frameworkHandle.TestCaseStartList.Contains("PassingTest"));
        Verify(_frameworkHandle.TestCaseEndList.Contains("PassingTest:Passed"));
        Verify(_frameworkHandle.ResultsList.Contains("PassingTest  Passed"));
    }

    // TODO: This tests needs to be mocked.

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

        TestExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(
            DummyTestClass.TestContextProperties.ToList().Contains(
            new KeyValuePair<string, object>("webAppUrl", "http://localhost")));
    }

    // Todo: This tests needs to be mocked.

    private void RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        TestExecutionManager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);

        Verify(DummyTestClass.TestContextProperties is not null);
    }

    public void RunTestsForMultipleSourcesShouldRunEachTestJustOnce()
    {
        int testsCount = 0;
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location };
        TestableTestExecutionManager testableTestExecutionmanager = new()
        {
            ExecuteTestsWrapper = (tests, runContext, frameworkHandle, isDeploymentDone) =>
            {
                testsCount += tests.Count();
            }
        };

        testableTestExecutionmanager.RunTests(sources, _runContext, _frameworkHandle, _cancellationToken);
        Verify(4 == testsCount);
    }

    #endregion

    #region SendTestResults tests

    public void SendTestResultsShouldFillInDataRowIndexIfTestIsDataDriven()
    {
        var testCase = new TestCase("DummyTest", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        UnitTestResult unitTestResult1 = new() { DatarowIndex = 0, DisplayName = "DummyTest" };
        UnitTestResult unitTestResult2 = new() { DatarowIndex = 1, DisplayName = "DummyTest" };
        TestExecutionManager.SendTestResults(testCase, new UnitTestResult[] { unitTestResult1, unitTestResult2 }, default, default, _frameworkHandle);
        Verify("DummyTest (Data Row 0)" == _frameworkHandle.TestDisplayNameList[0]);
        Verify("DummyTest (Data Row 1)" == _frameworkHandle.TestDisplayNameList[1]);
    }

    #endregion

    #region Parallel tests

    public void RunTestsForTestShouldRunTestsInParallelWhenEnabledInRunsettings()
    {
        var testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");
        var testCase21 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        var testCase22 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod2");

        TestCase[] tests = new[] { testCase11, testCase12, testCase21, testCase22 };
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(1 == DummyTestClassForParallelize.ThreadIds.Count);
            Verify(1 == DummyTestClassForParallelize2.ThreadIds.Count);

            var allThreadIds = new HashSet<int>(DummyTestClassForParallelize.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize2.ThreadIds);
            Verify(2 == allThreadIds.Count);
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

        TestCase[] tests = new[] { testCase11, testCase12 };
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
            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(2 == DummyTestClassForParallelize.ThreadIds.Count);
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

        TestCase[] tests = new[] { testCase1, testCase2, testCase3 };
        _runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>3</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(_runContext);
            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            var allThreadIds = new HashSet<int>(DummyTestClassForParallelize.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize2.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize3.ThreadIds);

            Verify(3 == allThreadIds.Count);
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

        TestCase[] tests = new[] { testCase1, testCase2 };
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
                    if (type.FullName.Equals(typeof(UTF.ParallelizeAttribute).FullName))
                    {
                        return new object[] { new UTF.ParallelizeAttribute { Workers = 10, Scope = UTF.ExecutionScope.MethodLevel } };
                    }

                    return originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(1 == DummyTestClassForParallelize.ThreadIds.Count);
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

        TestCase[] tests = new[] { testCase1, testCase2 };
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
                    if (type.FullName.Equals(typeof(UTF.DoNotParallelizeAttribute).FullName))
                    {
                        return new object[] { new UTF.DoNotParallelizeAttribute() };
                    }

                    return originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(1 == DummyTestClassForParallelize.ThreadIds.Count);
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

        TestCase[] tests = new[] { testCase1, testCase2, testCase3, testCase4 };
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
            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(2 == DummyTestClassWithDoNotParallelizeMethods.ParallelizableTestsThreadIds.Count);
            Verify(1 == DummyTestClassWithDoNotParallelizeMethods.UnParallelizableTestsThreadIds.Count);
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

        TestCase[] tests = new[] { testCase1, testCase2 };
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
                    if (type.FullName.Equals(typeof(UTF.ParallelizeAttribute).FullName))
                    {
                        return new object[] { new UTF.ParallelizeAttribute { Workers = 1 } };
                    }

                    return originalReflectionOperation.GetCustomAttributes(asm, type);
                });

            testablePlatformService.MockReflectionOperations.Setup(
                ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>())).
                Returns((MemberInfo memberInfo, bool inherit) =>
                {
                    return originalReflectionOperation.GetCustomAttributes(memberInfo, inherit);
                });

            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(2 == DummyTestClassForParallelize.ThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    // This is tracked by https://github.com/Microsoft/testfx/issues/320.

    private void RunTestsForTestShouldRunTestsInTheParentDomainsApartmentState()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        var testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        var testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        ////testCase3.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(TestAdapterConstants.DoNotParallelizeProperty, true);

        TestCase[] tests = new[] { testCase1, testCase2, testCase3, testCase4 };
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
            TestExecutionManager.RunTests(tests, _runContext, _frameworkHandle, new TestRunCancellationToken());

            Verify(1 == DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.Count);
            Verify(Thread.CurrentThread.GetApartmentState() == DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.ToArray()[0]);
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    #endregion

    #region private methods

    private TestCase GetTestCase(Type typeOfClass, string testName, bool ignore = false)
    {
        var methodInfo = typeOfClass.GetMethod(testName);
        var testMethod = new TestMethod(methodInfo.Name, typeOfClass.FullName, Assembly.GetExecutingAssembly().FullName, isAsync: false);
        UnitTestElement element = new(testMethod)
        {
            Ignored = ignore
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

        testablePlatformService.MockThreadOperations.Setup(ao => ao.ExecuteWithAbortSafety(It.IsAny<Action>()))
            .Callback((Action action) => action.Invoke());

        return testablePlatformService;
    }

    private void SetCaller(string caller)
    {
        _callers ??= new List<string>();

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

        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.TestMethod]
        [UTF.TestCategory("Foo")]
        public void PassingTest()
        {
            TestContextProperties = TestContext.Properties as IDictionary<string, object>;
        }

        [UTF.TestMethod]
        [UTF.TestCategory("Bar")]
        public void FailingTest()
        {
            UTF.Assert.Fail();
        }

        [UTF.TestMethod]
        [UTF.Ignore]
        public void IgnoredTest()
        {
            UTF.Assert.Fail();
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithFailingCleanupMethods
    {
        [UTF.ClassCleanup]
        public static void ClassCleanup()
        {
            throw new Exception("ClassCleanupException");
        }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public static int ClassCleanupCount { get; private set; } = 0;

        public static void Cleanup() => ClassCleanupCount = 0;

        [UTF.ClassCleanup]
        public static void ClassCleanup()
        {
            ClassCleanupCount++;
        }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize
    {
        public static HashSet<int> ThreadIds { get; } = new();

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize2
    {
        public static HashSet<int> ThreadIds { get; } = new();

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize3
    {
        public static HashSet<int> ThreadIds { get; } = new();

        public static void Cleanup()
        {
            ThreadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithDoNotParallelizeMethods
    {
        private static bool s_isFirstUnParallelizedTestRunTimeSet = false;

        public static HashSet<int> ParallelizableTestsThreadIds { get; private set; } = new();

        public static HashSet<int> UnParallelizableTestsThreadIds { get; private set; } = new();

        public static HashSet<ApartmentState> ThreadApartmentStates { get; private set; } = new();

        public static DateTime LastParallelizableTestRun { get; set; }

        public static DateTime FirstUnParallelizableTestRun { get; set; }

        public static void Cleanup()
        {
            ParallelizableTestsThreadIds.Clear();
            UnParallelizableTestsThreadIds.Clear();
            ThreadApartmentStates.Clear();
            s_isFirstUnParallelizedTestRunTimeSet = false;
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            ParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [UTF.TestMethod]
        [UTF.DoNotParallelize]
        public void TestMethod3()
        {
            if (!s_isFirstUnParallelizedTestRunTimeSet)
            {
                FirstUnParallelizableTestRun = DateTime.Now;
            }

            UnParallelizableTestsThreadIds.Add(Environment.CurrentManagedThreadId);
            ThreadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());
        }

        [UTF.TestMethod]
        [UTF.DoNotParallelize]
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

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }

    #endregion
}

#region Testable implementations

internal class TestableFrameworkHandle : IFrameworkHandle
{
    private readonly List<string> _messageList;
    private readonly List<string> _resultsList;
    private readonly List<string> _testCaseStartList;
    private readonly List<string> _testCaseEndList;
    private readonly List<string> _testDisplayNameList;

    public TestableFrameworkHandle()
    {
        _messageList = new List<string>();
        _resultsList = new List<string>();
        _testCaseStartList = new List<string>();
        _testCaseEndList = new List<string>();
        _testDisplayNameList = new List<string>();
    }

    public bool EnableShutdownAfterTestRun { get; set; }

    public List<string> MessageList => _messageList;

    public List<string> ResultsList => _resultsList;

    public List<string> TestCaseStartList => _testCaseStartList;

    public List<string> TestCaseEndList => _testCaseEndList;

    public List<string> TestDisplayNameList => _testDisplayNameList;

    public void RecordResult(TestResult testResult)
    {
        ResultsList.Add(testResult.ToString());
        TestDisplayNameList.Add(testResult.DisplayName);
    }

    public void SendMessage(TestMessageLevel testMessageLevel, string message)
    {
        MessageList.Add(string.Format("{0}:{1}", testMessageLevel, message));
    }

    public void RecordStart(TestCase testCase)
    {
        TestCaseStartList.Add(testCase.DisplayName);
    }

    public void RecordEnd(TestCase testCase, TestOutcome outcome)
    {
        TestCaseEndList.Add(string.Format("{0}:{1}", testCase.DisplayName, outcome));
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

internal class TestableRunContextTestExecutionTests : IRunContext
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

internal class TestableTestCaseFilterExpression : ITestCaseFilterExpression
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

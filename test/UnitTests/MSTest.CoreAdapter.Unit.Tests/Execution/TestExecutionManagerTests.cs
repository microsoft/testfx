// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

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
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using Ignore = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestAdapterConstants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestExecutionManagerTests
{
    private TestableRunContextTestExecutionTests runContext;

    private TestableFrameworkHandle frameworkHandle;

    private TestRunCancellationToken cancellationToken;

    private List<string> callers;

    private TestExecutionManager TestExecutionManager { get; set; }

    private readonly TestProperty[] tcmKnownProperties = new TestProperty[]
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

    [TestInitialize]
    public void TestInit()
    {
        runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => true));
        frameworkHandle = new TestableFrameworkHandle();
        cancellationToken = new TestRunCancellationToken();

        TestExecutionManager = new TestExecutionManager();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        PlatformServiceProvider.Instance = null;
        MSTestSettings.Reset();
    }

    #region RunTests on a list of tests

    [TestMethodV1]
    public void RunTestsForTestWithFilterErrorShouldSendZeroResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };

        // Causing the FilterExpressionError
        runContext = new TestableRunContextTestExecutionTests(() => { throw new TestPlatformFormatException(); });

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, cancellationToken);

        // No Results
        Assert.AreEqual(0, frameworkHandle.TestCaseStartList.Count);
        Assert.AreEqual(0, frameworkHandle.ResultsList.Count);
        Assert.AreEqual(0, frameworkHandle.TestCaseEndList.Count);
    }

    [TestMethodV1]
    public void RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        runContext = new TestableRunContextTestExecutionTests(() => new TestableTestCaseFilterExpression((p) => (p.DisplayName == "PassingTest")));

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, cancellationToken);

        // FailingTest should be skipped because it does not match the filter criteria.
        List<string> expectedTestCaseStartList = new() { "PassingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed" };
        List<string> expectedResultList = new() { "PassingTest  Passed" };

        CollectionAssert.AreEqual(expectedTestCaseStartList, frameworkHandle.TestCaseStartList);
        CollectionAssert.AreEqual(expectedTestCaseEndList, frameworkHandle.TestCaseEndList);
        CollectionAssert.AreEqual(expectedResultList, frameworkHandle.ResultsList);
    }

    [TestMethodV1]
    public void RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "IgnoredTest", ignore: true);
        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, cancellationToken);

        Assert.AreEqual("IgnoredTest", frameworkHandle.TestCaseStartList[0]);
        Assert.AreEqual("IgnoredTest:Skipped", frameworkHandle.TestCaseEndList[0]);
        Assert.AreEqual("IgnoredTest  Skipped", frameworkHandle.ResultsList[0]);
    }

    [TestMethodV1]
    public void RunTestsForASingleTestShouldSendSingleResult()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        List<string> expectedTestCaseStartList = new() { "PassingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed" };
        List<string> expectedResultList = new() { "PassingTest  Passed" };

        CollectionAssert.AreEqual(expectedTestCaseStartList, frameworkHandle.TestCaseStartList);
        CollectionAssert.AreEqual(expectedTestCaseEndList, frameworkHandle.TestCaseEndList);
        CollectionAssert.AreEqual(expectedResultList, frameworkHandle.ResultsList);
    }

    [TestMethodV1]
    public void RunTestsForMultipleTestShouldSendMultipleResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, cancellationToken);

        List<string> expectedTestCaseStartList = new() { "PassingTest", "FailingTest" };
        List<string> expectedTestCaseEndList = new() { "PassingTest:Passed", "FailingTest:Failed" };
        List<string> expectedResultList = new() { "PassingTest  Passed", "FailingTest  Failed\r\n  Message: Assert.Fail failed." };

        CollectionAssert.AreEqual(expectedTestCaseStartList, frameworkHandle.TestCaseStartList);
        CollectionAssert.AreEqual(expectedTestCaseEndList, frameworkHandle.TestCaseEndList);
        Assert.AreEqual(expectedResultList[0], frameworkHandle.ResultsList[0]);
        StringAssert.Contains(frameworkHandle.ResultsList[1], expectedResultList[1]);
    }

    [TestMethodV1]
    public void RunTestsForCancellationTokencanceledSetToTrueShouldSendZeroResults()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };

        // Cancel the test run
        cancellationToken.Cancel();
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, cancellationToken);

        // No Results
        Assert.AreEqual(0, frameworkHandle.TestCaseStartList.Count);
        Assert.AreEqual(0, frameworkHandle.ResultsList.Count);
        Assert.AreEqual(0, frameworkHandle.TestCaseEndList.Count);
    }

    [TestMethodV1]
    public void RunTestsShouldLogResultCleanupWarningsAsErrorsWhenTreatClassCleanupWarningsAsErrorsIsTrue()
    {
        // Arrange
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <TreatClassAndAssemblyCleanupWarningsAsErrors>true</TreatClassAndAssemblyCleanupWarningsAsErrors>
                                              </MSTest>
                                            </RunSettings>");
        MSTestSettings.PopulateSettings(runContext);
        var testCase = GetTestCase(typeof(DummyTestClassWithFailingCleanupMethods), "TestMethod");
        TestCase[] tests = new[] { testCase };

        // Act
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        // Assert
        CollectionAssert.Contains(frameworkHandle.TestCaseEndList, "TestMethod:Passed");
        Assert.AreEqual(1, frameworkHandle.MessageList.Count);
        StringAssert.StartsWith(frameworkHandle.MessageList[0], "Error");
        Assert.IsTrue(frameworkHandle.MessageList[0].Contains("ClassCleanupException"));
    }

    [TestMethodV1]
    public void RunTestsShouldLogResultOutput()
    {
        var testCase = GetTestCase(typeof(DummyTestClassWithFailingCleanupMethods), "TestMethod");
        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        // Warnings should get logged.
        Assert.AreEqual(1, frameworkHandle.MessageList.Count);
        StringAssert.StartsWith(frameworkHandle.MessageList[0], "Warning");
        Assert.IsTrue(frameworkHandle.MessageList[0].Contains("ClassCleanupException"));
    }

    [TestMethodV1]
    public void RunTestsForTestShouldDeployBeforeExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, runContext, frameworkHandle)).Callback(() => SetCaller("Deploy"));

        TestExecutionManager.RunTests(
            tests,
            runContext,
            frameworkHandle,
            new TestRunCancellationToken());

        Assert.AreEqual("Deploy", callers[0], "Deploy should be called before execution.");
        Assert.AreEqual("LoadAssembly", callers[1], "Deploy should be called before execution.");
    }

    [TestMethodV1]
    public void RunTestsForTestShouldCleanupAfterExecution()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Cleanup()).Callback(() => SetCaller("Cleanup"));

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        Assert.AreEqual("LoadAssembly", callers[0], "Cleanup should be called after execution.");
        Assert.AreEqual("Cleanup", callers.LastOrDefault(), "Cleanup should be called after execution.");
    }

    [TestMethodV1]
    public void RunTestsForTestShouldNotCleanupOnTestFailure()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        var testablePlatformService = SetupTestablePlatformService();
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockTestDeployment.Verify(td => td.Cleanup(), Times.Never);
    }

    [TestMethodV1]
    public void RunTestsForTestShouldLoadSourceFromDeploymentDirectoryIfDeployed()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var failingTestCase = GetTestCase(typeof(DummyTestClass), "FailingTest");
        TestCase[] tests = new[] { testCase, failingTestCase };

        var testablePlatformService = SetupTestablePlatformService();

        // Setup mocks.
        testablePlatformService.MockTestDeployment.Setup(
            td => td.Deploy(tests, runContext, frameworkHandle)).Returns(true);
        testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
            .Returns(@"C:\temp");

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockFileOperations.Verify(
            fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp")), It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

    [TestMethodV1]
    public void RunTestsForTestShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        CollectionAssert.Contains(
            DummyTestClass.TestContextProperties.ToList(),
            new KeyValuePair<string, object>("webAppUrl", "http://localhost"));
    }

    [TestMethodV1]
    public void RunTestsForTestShouldPassInTcmPropertiesAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        var propertiesValue = new object[] { 32, 534, 5, "sample build directory", "sample build flavor", "132456", "sample build platform", "http://sampleBuildUti/", "http://samplecollectionuri/", "sample team project", false, 1401, 54, "sample configuration name", 345 };
        SetTestCaseProperties(testCase, propertiesValue);

        TestCase[] tests = new[] { testCase };

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        VerifyTcmProperties(DummyTestClass.TestContextProperties, testCase);
    }

    [TestMethodV1]
    public void RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");
        TestCase[] tests = new[] { testCase };

        // Setup mocks.
        var testablePlatformService = SetupTestablePlatformService();

        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        testablePlatformService.MockSettingsProvider.Verify(sp => sp.GetProperties(It.IsAny<string>()), Times.Once);
    }

    [TestMethodV1]
    public void RunTestsShouldClearSessionParametersAcrossRuns()
    {
        var testCase = GetTestCase(typeof(DummyTestClass), "PassingTest");

        TestCase[] tests = new[] { testCase };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        // Trigger First Run
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        // Update runsettings to have different values for similar keys
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                         @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://updatedLocalHost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        // Trigger another Run
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

        Assert.AreEqual("http://updatedLocalHost", DummyTestClass.TestContextProperties["webAppUrl"]);
    }

    #endregion

    #region Run Tests on Sources

    // TODO: This tests needs to be mocked.
    [Ignore]
    [TestMethodV1]
    public void RunTestsForSourceShouldRunTestsInASource()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        TestExecutionManager.RunTests(sources, runContext, frameworkHandle, cancellationToken);

        CollectionAssert.Contains(frameworkHandle.TestCaseStartList, "PassingTest");
        CollectionAssert.Contains(frameworkHandle.TestCaseEndList, "PassingTest:Passed");
        CollectionAssert.Contains(frameworkHandle.ResultsList, "PassingTest  Passed");
    }

    // TODO: This tests needs to be mocked.
    [Ignore]
    [TestMethodV1]
    public void RunTestsForSourceShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

        TestExecutionManager.RunTests(sources, runContext, frameworkHandle, cancellationToken);

        CollectionAssert.Contains(
            DummyTestClass.TestContextProperties.ToList(),
            new KeyValuePair<string, object>("webAppUrl", "http://localhost"));
    }

    // Todo: This tests needs to be mocked.
    [TestMethodV1]
    [Ignore]
    public void RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

        TestExecutionManager.RunTests(sources, runContext, frameworkHandle, cancellationToken);

        Assert.IsNotNull(DummyTestClass.TestContextProperties);
    }

    [TestMethodV1]
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

        testableTestExecutionmanager.RunTests(sources, runContext, frameworkHandle, cancellationToken);
        Assert.AreEqual(4, testsCount);
    }

    #endregion

    #region SendTestResults tests

    [TestMethodV1]
    public void SendTestResultsShouldFillInDataRowIndexIfTestIsDataDriven()
    {
        var testCase = new TestCase("DummyTest", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        UnitTestResult unitTestResult1 = new() { DatarowIndex = 0, DisplayName = "DummyTest" };
        UnitTestResult unitTestResult2 = new() { DatarowIndex = 1, DisplayName = "DummyTest" };
        TestExecutionManager.SendTestResults(testCase, new UnitTestResult[] { unitTestResult1, unitTestResult2 }, default, default, frameworkHandle);
        Assert.AreEqual("DummyTest (Data Row 0)", frameworkHandle.TestDisplayNameList[0]);
        Assert.AreEqual("DummyTest (Data Row 1)", frameworkHandle.TestDisplayNameList[1]);
    }

    #endregion

    #region Parallel tests

    [TestMethodV1]
    public void RunTestsForTestShouldRunTestsInParallelWhenEnabledInRunsettings()
    {
        var testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");
        var testCase21 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        var testCase22 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod2");

        TestCase[] tests = new[] { testCase11, testCase12, testCase21, testCase22 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>2</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(runContext);
            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(1, DummyTestClassForParallelize.ThreadIds.Count);
            Assert.AreEqual(1, DummyTestClassForParallelize2.ThreadIds.Count);

            var allThreadIds = new HashSet<int>(DummyTestClassForParallelize.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize2.ThreadIds);
            Assert.AreEqual(2, allThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
            DummyTestClassForParallelize2.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldRunTestsByMethodLevelWhenSpecified()
    {
        var testCase11 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase12 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = new[] { testCase11, testCase12 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
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
            MSTestSettings.PopulateSettings(runContext);
            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(2, DummyTestClassForParallelize.ThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldRunTestsWithSpecifiedNumberOfWorkers()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize2), "TestMethod1");
        var testCase3 = GetTestCase(typeof(DummyTestClassForParallelize3), "TestMethod1");

        TestCase[] tests = new[] { testCase1, testCase2, testCase3 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <MSTest>
                                                 <Parallelize>
                                                   <Workers>3</Workers>
                                                 </Parallelize>
                                              </MSTest>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(runContext);
            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            var allThreadIds = new HashSet<int>(DummyTestClassForParallelize.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize2.ThreadIds);
            allThreadIds.UnionWith(DummyTestClassForParallelize3.ThreadIds);

            Assert.AreEqual(3, allThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
            DummyTestClassForParallelize2.Cleanup();
            DummyTestClassForParallelize3.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromRunsettings()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = new[] { testCase1, testCase2 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                     @"<RunSettings> 
                                              <RunConfiguration>
                                                 <DisableParallelization>true</DisableParallelization>
                                              </RunConfiguration>
                                            </RunSettings>");

        try
        {
            MSTestSettings.PopulateSettings(runContext);
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

            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(1, DummyTestClassForParallelize.ThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldNotRunTestsInParallelWhenDisabledFromSource()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = new[] { testCase1, testCase2 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
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
            MSTestSettings.PopulateSettings(runContext);
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

            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(1, DummyTestClassForParallelize.ThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldRunNonParallelizableTestsSeparately()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        var testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        var testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        testCase3.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);

        TestCase[] tests = new[] { testCase1, testCase2, testCase3, testCase4 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
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
            MSTestSettings.PopulateSettings(runContext);
            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(2, DummyTestClassWithDoNotParallelizeMethods.ParallelizableTestsThreadIds.Count);
            Assert.AreEqual(1, DummyTestClassWithDoNotParallelizeMethods.UnParallelizableTestsThreadIds.Count);
            Assert.IsTrue(DummyTestClassWithDoNotParallelizeMethods.LastParallelizableTestRun.TimeOfDay.TotalMilliseconds <= DummyTestClassWithDoNotParallelizeMethods.FirstUnParallelizableTestRun.TimeOfDay.TotalMilliseconds);
        }
        finally
        {
            DummyTestClassWithDoNotParallelizeMethods.Cleanup();
        }
    }

    [TestMethodV1]
    public void RunTestsForTestShouldPreferParallelSettingsFromRunSettingsOverAssemblyLevelAttributes()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassForParallelize), "TestMethod2");

        TestCase[] tests = new[] { testCase1, testCase2 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
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
            MSTestSettings.PopulateSettings(runContext);
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

            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(2, DummyTestClassForParallelize.ThreadIds.Count);
        }
        finally
        {
            DummyTestClassForParallelize.Cleanup();
        }
    }

    // This is tracked by https://github.com/Microsoft/testfx/issues/320.
    [TestMethodV1]
    [Ignore]
    public void RunTestsForTestShouldRunTestsInTheParentDomainsApartmentState()
    {
        var testCase1 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod1");
        var testCase2 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod2");
        var testCase3 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod3");
        var testCase4 = GetTestCase(typeof(DummyTestClassWithDoNotParallelizeMethods), "TestMethod4");

        ////testCase3.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);
        testCase4.SetPropertyValue(MSTest.TestAdapter.Constants.DoNotParallelizeProperty, true);

        TestCase[] tests = new[] { testCase1, testCase2, testCase3, testCase4 };
        runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
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
            MSTestSettings.PopulateSettings(runContext);
            TestExecutionManager.RunTests(tests, runContext, frameworkHandle, new TestRunCancellationToken());

            Assert.AreEqual(1, DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.Count);
            Assert.AreEqual(Thread.CurrentThread.GetApartmentState(), DummyTestClassWithDoNotParallelizeMethods.ThreadApartmentStates.ToArray()[0]);
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
        callers ??= new List<string>();

        callers.Add(caller);
    }

    private void VerifyTcmProperties(IDictionary<string, object> tcmProperties, TestCase testCase)
    {
        foreach (var property in tcmKnownProperties)
        {
            Assert.AreEqual(testCase.GetPropertyValue(property), tcmProperties[property.Id]);
        }
    }

    private void SetTestCaseProperties(TestCase testCase, object[] propertiesValue)
    {
        var tcmKnownPropertiesEnumerator = tcmKnownProperties.GetEnumerator();

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
        public static int ClassCleanupCount => classCleanupCount;

        public static void Cleanup() => classCleanupCount = 0;

        private static int classCleanupCount = 0;

        [UTF.ClassCleanup]
        public static void ClassCleanup()
        {
            classCleanupCount++;
        }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize
    {
        private static HashSet<int> threadIds = new();

        public static HashSet<int> ThreadIds
        {
            get
            {
                return threadIds;
            }
        }

        public static void Cleanup()
        {
            threadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            System.Threading.Thread.Sleep(2000);
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            System.Threading.Thread.Sleep(2000);
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize2
    {
        private static HashSet<int> threadIds = new();

        public static HashSet<int> ThreadIds
        {
            get
            {
                return threadIds;
            }
        }

        public static void Cleanup()
        {
            threadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            System.Threading.Thread.Sleep(2000);
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            System.Threading.Thread.Sleep(2000);
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassForParallelize3
    {
        private static HashSet<int> threadIds = new();

        public static HashSet<int> ThreadIds
        {
            get
            {
                return threadIds;
            }
        }

        public static void Cleanup()
        {
            threadIds.Clear();
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithDoNotParallelizeMethods
    {
        private static HashSet<int> parallelizableTestsThreadIds = new();
        private static HashSet<int> unParallelizableTestsThreadIds = new();
        private static HashSet<ApartmentState> threadApartmentStates = new();
        private static bool isFirstUnParallelizedTestRunTimeSet = false;

        public static HashSet<int> ParallelizableTestsThreadIds
        {
            get
            {
                return parallelizableTestsThreadIds;
            }
        }

        public static HashSet<int> UnParallelizableTestsThreadIds
        {
            get
            {
                return unParallelizableTestsThreadIds;
            }
        }

        public static HashSet<ApartmentState> ThreadApartmentStates
        {
            get
            {
                return threadApartmentStates;
            }
        }

        public static DateTime LastParallelizableTestRun { get; set; }

        public static DateTime FirstUnParallelizableTestRun { get; set; }

        public static void Cleanup()
        {
            parallelizableTestsThreadIds.Clear();
            unParallelizableTestsThreadIds.Clear();
            threadApartmentStates.Clear();
            isFirstUnParallelizedTestRunTimeSet = false;
        }

        [UTF.TestMethod]
        public void TestMethod1()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            parallelizableTestsThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            threadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [UTF.TestMethod]
        public void TestMethod2()
        {
            // Ensures stability.. for the thread to be not used for another test method
            Thread.Sleep(2000);
            parallelizableTestsThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            threadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());

            LastParallelizableTestRun = DateTime.Now;
        }

        [UTF.TestMethod]
        [UTF.DoNotParallelize]
        public void TestMethod3()
        {
            if (!isFirstUnParallelizedTestRunTimeSet)
            {
                FirstUnParallelizableTestRun = DateTime.Now;
            }

            unParallelizableTestsThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            threadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());
        }

        [UTF.TestMethod]
        [UTF.DoNotParallelize]
        public void TestMethod4()
        {
            if (!isFirstUnParallelizedTestRunTimeSet)
            {
                FirstUnParallelizableTestRun = DateTime.Now;
            }

            unParallelizableTestsThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            threadApartmentStates.Add(Thread.CurrentThread.GetApartmentState());
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
    private readonly List<string> messageList;
    private readonly List<string> resultsList;
    private readonly List<string> testCaseStartList;
    private readonly List<string> testCaseEndList;
    private readonly List<string> testDisplayNameList;

    public TestableFrameworkHandle()
    {
        messageList = new List<string>();
        resultsList = new List<string>();
        testCaseStartList = new List<string>();
        testCaseEndList = new List<string>();
        testDisplayNameList = new List<string>();
    }

    public bool EnableShutdownAfterTestRun { get; set; }

    public List<string> MessageList => messageList;

    public List<string> ResultsList => resultsList;

    public List<string> TestCaseStartList => testCaseStartList;

    public List<string> TestCaseEndList => testCaseEndList;

    public List<string> TestDisplayNameList => testDisplayNameList;

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
    private readonly Func<ITestCaseFilterExpression> getFilter;

    public TestableRunContextTestExecutionTests(Func<ITestCaseFilterExpression> getFilter)
    {
        this.getFilter = getFilter;
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
        return getFilter();
    }
}

internal class TestableTestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly Func<TestCase, bool> matchTest;

    public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase)
    {
        matchTest = matchTestCase;
    }

    public string TestCaseFilterValue { get; }

    public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
    {
        return matchTest(testCase);
    }
}

internal class TestableTestExecutionManager : TestExecutionManager
{
    internal Action<IEnumerable<TestCase>, IRunContext, IFrameworkHandle, bool> ExecuteTestsWrapper { get; set; }

    internal override void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
    {
        if (ExecuteTestsWrapper != null)
        {
            ExecuteTestsWrapper.Invoke(tests, runContext, frameworkHandle, isDeploymentDone);
        }
    }

    internal override UnitTestDiscoverer GetUnitTestDiscoverer()
    {
        return new TestableUnitTestDiscoverer();
    }
}
#endregion

//   Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using Ignore = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    using Moq;

    using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;
    using MSTest.TestAdapter;

    [TestClass]
    public class TestExecutionManagerTests
    {
        private TestableRunContext runContext;

        private TestableFrameworkHandle frameworkHandle;

        private TestRunCancellationToken cancellationToken;
        
        private List<string> callers;

        private IPlatformServiceProvider originalPlatformService;

        private TestExecutionManager TestExecutionManager { get; set; }
        
        [TestInitialize]
        public void TestInit()
        {
            this.runContext = new TestableRunContext(() => new TestableTestCaseFilterExpression((p) => true));
            this.frameworkHandle = new TestableFrameworkHandle();
            this.cancellationToken = new TestRunCancellationToken();

            this.TestExecutionManager = new TestExecutionManager();

            // Cache the platform service so that it can be reset. 
            // This prevents tests that need a testable platform service from failing other tests.
            this.originalPlatformService = PlatformServiceProvider.Instance;
        }

        [TestCleanup]
        public void Testcleanup()
        {
            PlatformServiceProvider.Instance = this.originalPlatformService;
        }

        #region RunTests on a list of tests

        [TestMethodV1]
        public void RunTestsForTestWithFilterErrorShouldSendZeroResults()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");

            TestCase[] tests = new[] { testCase };

            // Causing the FilterExpressionError
            this.runContext = new TestableRunContext(() => { throw new TestPlatformFormatException(); });

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, this.cancellationToken);

            // No Results
            Assert.AreEqual(0, this.frameworkHandle.TestCaseStartList.Count);
            Assert.AreEqual(0, this.frameworkHandle.ResultsList.Count);
            Assert.AreEqual(0, this.frameworkHandle.TestCaseEndList.Count);
        }
        
        [TestMethodV1]
        public void RunTestsForTestWithFilterShouldSendResultsForFilteredTests()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            var failingTestCase = this.GetTestCase(typeof(DummyTestClass), "FailingTest");
            TestCase[] tests = new[] { testCase, failingTestCase };

            this.runContext = new TestableRunContext(() => new TestableTestCaseFilterExpression((p) => (p.DisplayName == "PassingTest")));

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, this.cancellationToken);

            // FailingTest should be skipped because it does not match the filter criteria.
            List<string> expectedTestCaseStartList = new List<string>() { "PassingTest" };
            List<string> expectedTestCaseEndList = new List<string>() { "PassingTest:Passed"};
            List<string> expectedResultList = new List<string>() { "PassingTest  Passed" };

            CollectionAssert.AreEqual(expectedTestCaseStartList, this.frameworkHandle.TestCaseStartList);
            CollectionAssert.AreEqual(expectedTestCaseEndList, this.frameworkHandle.TestCaseEndList);
            CollectionAssert.AreEqual(expectedResultList, this.frameworkHandle.ResultsList);
        }
        
        [TestMethodV1]
        public void RunTestsForIgnoredTestShouldSendResultsMarkingIgnoredTestsAsSkipped()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "IgnoredTest", ignore: true);
            TestCase[] tests = new[] { testCase };

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, this.cancellationToken);

            Assert.AreEqual("IgnoredTest", this.frameworkHandle.TestCaseStartList[0]);
            Assert.AreEqual("IgnoredTest:Skipped", this.frameworkHandle.TestCaseEndList[0]);
            Assert.AreEqual("IgnoredTest  Skipped", this.frameworkHandle.ResultsList[0]);
        }

        [TestMethodV1]
        public void RunTestsForASingleTestShouldSendSingleResult()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");

            TestCase[] tests = new[] { testCase };

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());

            List<string> expectedTestCaseStartList = new List<string>() { "PassingTest" };
            List<string> expectedTestCaseEndList = new List<string>() { "PassingTest:Passed" };
            List<string> expectedResultList = new List<string>() { "PassingTest  Passed" };

            CollectionAssert.AreEqual(expectedTestCaseStartList, this.frameworkHandle.TestCaseStartList);
            CollectionAssert.AreEqual(expectedTestCaseEndList, this.frameworkHandle.TestCaseEndList);
            CollectionAssert.AreEqual(expectedResultList, this.frameworkHandle.ResultsList);
        }

        [TestMethodV1]
        public void RunTestsForMultipleTestShouldSendMultipleResults()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            var failingTestCase = this.GetTestCase(typeof(DummyTestClass), "FailingTest");
            TestCase[] tests = new[] { testCase, failingTestCase };
            
            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, this.cancellationToken);

            List<string> expectedTestCaseStartList = new List<string>() { "PassingTest", "FailingTest" };
            List<string> expectedTestCaseEndList = new List<string>() { "PassingTest:Passed", "FailingTest:Failed" };
            List<string> expectedResultList = new List<string>() { "PassingTest  Passed", "FailingTest  Failed\r\n  Message: (null)" };

            CollectionAssert.AreEqual(expectedTestCaseStartList, this.frameworkHandle.TestCaseStartList);
            CollectionAssert.AreEqual(expectedTestCaseEndList, this.frameworkHandle.TestCaseEndList);
            Assert.AreEqual("PassingTest  Passed", this.frameworkHandle.ResultsList[0]);
            StringAssert.Contains(
                this.frameworkHandle.ResultsList[1],
                "FailingTest  Failed\r\n  Message: Assert.Fail failed. \r\n  StackTrace:\r\n   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestExecutionManagerTests.DummyTestClass.FailingTest()");
        }
        
        [TestMethodV1]
        public void RunTestsForCancellationTokenCancelledSetToTrueShouldSendZeroResults()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");

            TestCase[] tests = new[] { testCase };
            
            // Cancel the test run
            this.cancellationToken.Cancel();
            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, this.cancellationToken);

            // No Results
            Assert.AreEqual(0, this.frameworkHandle.TestCaseStartList.Count);
            Assert.AreEqual(0, this.frameworkHandle.ResultsList.Count);
            Assert.AreEqual(0, this.frameworkHandle.TestCaseEndList.Count);
        }
        
        [TestMethodV1]
        public void RunTestsShouldLogResultCleanupWarnings()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClassWithCleanupMethods), "TestMethod");
            TestCase[] tests = new[] { testCase };

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());
            
            // Warnings should get logged.
            Assert.AreEqual(1, this.frameworkHandle.MessageList.Count);
            Assert.IsTrue(this.frameworkHandle.MessageList[0].Contains("ClassCleanupException"));
        }

        [TestMethodV1]
        public void RunTestsForTestShouldDeployBeforeExecution()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            TestCase[] tests = new[] { testCase };

            // Setup mocks.
            var testablePlatformService = this.SetupTestablePlatformService();
            testablePlatformService.MockTestDeployment.Setup(
                td => td.Deploy(tests, this.runContext, this.frameworkHandle)).Callback(() => this.SetCaller("Deploy"));

            this.TestExecutionManager.RunTests(
                tests,
                this.runContext,
                this.frameworkHandle,
                new TestRunCancellationToken());

            Assert.AreEqual("Deploy", this.callers[0], "Deploy should be called before execution.");
            Assert.AreEqual("LoadAssembly", this.callers[1], "Deploy should be called before execution.");
        }

        [TestMethodV1]
        public void RunTestsForTestShouldCleanupAfterExecution()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            TestCase[] tests = new[] { testCase };

            // Setup mocks.
            var testablePlatformService = this.SetupTestablePlatformService();
            testablePlatformService.MockTestDeployment.Setup(
                td => td.Cleanup()).Callback(() => this.SetCaller("Cleanup"));

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());
            
            Assert.AreEqual("LoadAssembly", this.callers[0], "Cleanup should be called after execution.");
            Assert.AreEqual("Cleanup", this.callers[1], "Cleanup should be called after execution.");
        }

        [TestMethodV1]
        public void RunTestsForTestShouldNotCleanupOnTestFailure()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            var failingTestCase = this.GetTestCase(typeof(DummyTestClass), "FailingTest");
            TestCase[] tests = new[] { testCase, failingTestCase };

            var testablePlatformService = this.SetupTestablePlatformService();
            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());
            
            testablePlatformService.MockTestDeployment.Verify(td => td.Cleanup(), Times.Never);
        }

        [TestMethodV1]
        public void RunTestsForTestShouldLoadSourceFromDeploymentDirectoryIfDeployed()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            var failingTestCase = this.GetTestCase(typeof(DummyTestClass), "FailingTest");
            TestCase[] tests = new[] { testCase, failingTestCase };

            var testablePlatformService = this.SetupTestablePlatformService();
            
            // Setup mocks.
            testablePlatformService.MockTestDeployment.Setup(
                td => td.Deploy(tests, this.runContext, this.frameworkHandle)).Returns(true);
            testablePlatformService.MockTestDeployment.Setup(td => td.GetDeploymentDirectory())
                .Returns(@"C:\temp");

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());

            testablePlatformService.MockFileOperations.Verify(
                fo => fo.LoadAssembly(It.Is<string>(s => s.StartsWith("C:\\temp"))),
                Times.Once);
        }

        [TestMethodV1]
        public void RunTestsForTestShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");

            TestCase[] tests = new[] { testCase };
            this.runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                         @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());

            CollectionAssert.Contains(
                DummyTestClass.TestContextProperties.ToList(),
                new KeyValuePair<string, object>("webAppUrl", "http://localhost"));
        }

        [TestMethodV1]
        public void RunTestsForTestShouldPassInDeploymentInformationAsPropertiesToTheTest()
        {
            var testCase = this.GetTestCase(typeof(DummyTestClass), "PassingTest");
            TestCase[] tests = new[] { testCase };

            // Setup mocks.
            var testablePlatformService = this.SetupTestablePlatformService();
            
            this.TestExecutionManager.RunTests(tests, this.runContext, this.frameworkHandle, new TestRunCancellationToken());

            testablePlatformService.MockSettingsProvider.Verify(sp => sp.GetProperties(It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region Run Tests on Sources

        // Todo: This tests needs to be mocked.
        [Ignore]
        [TestMethodV1]
        public void RunTestsForSourceShouldRunTestsInASource()
        {
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

            this.TestExecutionManager.RunTests(sources, this.runContext, this.frameworkHandle, this.cancellationToken);

            CollectionAssert.Contains(this.frameworkHandle.TestCaseStartList, "PassingTest");
            CollectionAssert.Contains(this.frameworkHandle.TestCaseEndList, "PassingTest:Passed");
            CollectionAssert.Contains(this.frameworkHandle.ResultsList, "PassingTest  Passed");
        }

        // Todo: This tests needs to be mocked.
        [Ignore]
        [TestMethodV1]
        public void RunTestsForSourceShouldPassInTestRunParametersInformationAsPropertiesToTheTest()
        {
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
            
            this.runContext.MockRunSettings.Setup(rs => rs.SettingsXml).Returns(
                                         @"<RunSettings> 
                                            <TestRunParameters>
                                              <Parameter name=""webAppUrl"" value=""http://localhost"" />
                                              <Parameter name = ""webAppUserName"" value=""Admin"" />
                                              </TestRunParameters>
                                            </RunSettings>");

            this.TestExecutionManager.RunTests(sources, this.runContext, this.frameworkHandle, this.cancellationToken);

            CollectionAssert.Contains(
                DummyTestClass.TestContextProperties.ToList(),
                new KeyValuePair<string, object>("webAppUrl", "http://localhost"));
        }

        [TestMethodV1]
        public void RunTestsForSourceShouldPassInDeploymentInformationAsPropertiesToTheTest()
        {
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location };

            this.TestExecutionManager.RunTests(sources, this.runContext, this.frameworkHandle, this.cancellationToken);

            Assert.IsNotNull(DummyTestClass.TestContextProperties);
        }

        [TestMethodV1]
        public void RunTestsForMultipleSourcesShouldRunEachTestJustOnce()
        {
            int testsCount = 0;
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location };
            TestableTestExecutionManager testableTestExecutionmanager = new TestableTestExecutionManager();

            testableTestExecutionmanager.ExecuteTestsWrapper = (tests, runContext, frameworkHandle, isDeploymentDone) =>
            {
                testsCount += tests.Count();
            };

            testableTestExecutionmanager.RunTests(sources, this.runContext, this.frameworkHandle, this.cancellationToken);
            Assert.AreEqual(testsCount, 4);
        }

        #endregion

        [TestMethodV1]
        public void SendTestResultsShouldFillInDataRowIndexIfTestIsDataDriven()
        {
            var testCase = new TestCase("DummyTest", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
            UnitTestResult unitTestResult1 = new UnitTestResult() { DatarowIndex = 0, DisplayName = "DummyTest"};
            UnitTestResult unitTestResult2 = new UnitTestResult() { DatarowIndex = 1, DisplayName = "DummyTest" };
            this.TestExecutionManager.SendTestResults(testCase,new UnitTestResult[] {unitTestResult1,unitTestResult2},new DateTimeOffset(), new DateTimeOffset(), this.frameworkHandle);
            Assert.AreEqual(frameworkHandle.TestDisplayNameList[0],"DummyTest (Data Row 0)");
            Assert.AreEqual(frameworkHandle.TestDisplayNameList[1],"DummyTest (Data Row 1)");
        }

        #region private methods

        private TestCase GetTestCase(Type typeOfClass, string testName, bool ignore = false)
        {
            var methodInfo = typeOfClass.GetMethod(testName);
            var testMethod = new TestMethod(methodInfo.Name, typeOfClass.FullName, Assembly.GetExecutingAssembly().FullName, isAsync: false);
            UnitTestElement element = new UnitTestElement(testMethod);
            element.Ignored = ignore;
            return element.ToTestCase();
        }

        private TestablePlatformServiceProvider SetupTestablePlatformService()
        {
            var testablePlatformService = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = testablePlatformService;
            
            testablePlatformService.MockFileOperations.Setup(td => td.LoadAssembly(It.IsAny<string>()))
                .Returns(
                    (string assemblyName) =>
                    {
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
                        return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
                    }).Callback(() => this.SetCaller("LoadAssembly"));

            testablePlatformService.MockTestSourceHost.Setup(
                tsh =>
                tsh.CreateInstanceForType(
                    It.IsAny<Type>(),
                    It.IsAny<object[]>(),
                    It.IsAny<string>(),
                    It.IsAny<IRunSettings>()))
                .Returns(
                    (Type type, object[] args, string sourceFileName, IRunSettings runSettings) =>
                        {
                            return Activator.CreateInstance(type, args);
                        });

            testablePlatformService.MockSettingsProvider.Setup(sp => sp.GetProperties(It.IsAny<string>()))
                .Returns(new Dictionary<string, object>());

            return testablePlatformService;
        }

        private void SetCaller(string caller)
        {
            if (this.callers == null)
            {
                this.callers = new List<string>();
            }

            this.callers.Add(caller);
        }

        #endregion

        #region Dummy implementation

        [UTF.TestClass]
        internal class DummyTestClass
        {
            public UTFExtension.TestContext TestContext { get; set; }

            [UTF.TestMethod]
            [UTF.TestCategory("Foo")]
            public void PassingTest()
            {
                TestContextProperties = this.TestContext.Properties;
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

            public static IDictionary<string,object> TestContextProperties
            {
                get;
                set;
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithCleanupMethods
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

        #endregion

        #region Testable implementations

        private class TestableFrameworkHandle : IFrameworkHandle
        {
            public readonly List<string> MessageList;
            public readonly List<string> ResultsList;
            public readonly List<string> TestCaseStartList;
            public readonly List<string> TestCaseEndList;
            public readonly List<string> TestDisplayNameList;
            public TestableFrameworkHandle()
            {
                this.MessageList = new List<string>();
                this.ResultsList = new List<string>();
                this.TestCaseStartList = new List<string>();
                this.TestCaseEndList = new List<string>();
                this.TestDisplayNameList = new List<string>();
            }

            public bool EnableShutdownAfterTestRun { get; set; }

            public void SendMessage(TestMessageLevel testMessageLevel, string message)
            {
               this.MessageList.Add(string.Format("{0}:{1}", testMessageLevel, message));
            }

            public void RecordResult(TestResult testResult)
            {
               this.ResultsList.Add(testResult.ToString());
               this.TestDisplayNameList.Add(testResult.DisplayName);
            }

            public void RecordStart(TestCase testCase)
            {
                this.TestCaseStartList.Add(testCase.DisplayName);
            }

            public void RecordEnd(TestCase testCase, TestOutcome outcome)
            {
               this.TestCaseEndList.Add(string.Format("{0}:{1}", testCase.DisplayName, outcome));
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

        private class TestableRunContext : IRunContext
        {
            private readonly Func<ITestCaseFilterExpression> GetFilter;

            public TestableRunContext(Func<ITestCaseFilterExpression> getFilter)
            {
                this.GetFilter = getFilter;
                this.MockRunSettings = new Mock<IRunSettings>();
            }

            public Mock<IRunSettings> MockRunSettings { get; set; }

            public IRunSettings RunSettings
            {
                get
                {
                    return this.MockRunSettings.Object;
                }
            }

            public ITestCaseFilterExpression GetTestCaseFilter(
                IEnumerable<string> supportedProperties,
                Func<string, TestProperty> propertyProvider)
            {
                return this.GetFilter();
            }

            public bool KeepAlive { get; }
            public bool InIsolation { get; }
            public bool IsDataCollectionEnabled { get; }
            public bool IsBeingDebugged { get; }
            public string TestRunDirectory { get; }
            public string SolutionDirectory { get; }
        }

        private class TestableTestCaseFilterExpression : ITestCaseFilterExpression
        {
            private readonly Func<TestCase, bool> matchTest;
            
            public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase)
            {
                this.matchTest = matchTestCase;
            }

            public string TestCaseFilterValue { get; }

            public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
            {
                return this.matchTest(testCase);
            }
        }

        internal class TestableTestExecutionManager : TestExecutionManager
        {
            internal override void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext,
                IFrameworkHandle frameworkHandle, bool isDeploymentDone)
            {
                if (this.ExecuteTestsWrapper != null)
                {
                    this.ExecuteTestsWrapper.Invoke(tests, runContext, frameworkHandle, isDeploymentDone);
                }
            }

            internal override UnitTestDiscoverer GetUnitTestDiscoverer()
            {
                return new TestableUnitTestDiscoverer();
            }

            internal Action<IEnumerable<TestCase>, IRunContext, IFrameworkHandle, bool> ExecuteTestsWrapper;
        }
        #endregion
    }
}

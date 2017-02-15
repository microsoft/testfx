// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Moq;
    using MSTest.TestAdapter;
    using PlatformServices.Interface;
    using TestableImplementations;
    using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestMethodRunnerTests
    {
        private readonly TestMethodRunner globalTestMethodRunner;

        private readonly MethodInfo methodInfo;

        private readonly UTF.TestMethodAttribute testMethodAttribute;

        private readonly TestContextImplementation testContextImplementation;

        private readonly TestClassInfo testClassInfo;

        private readonly TestMethod testMethod;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        public TestMethodRunnerTests()
        {
            var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            this.methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
            var classAttribute = new UTF.TestClassAttribute();
            this.testMethodAttribute = new UTF.TestMethodAttribute();
            var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            var testAssemblyInfo = new TestAssemblyInfo();
            this.testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
            this.testContextImplementation = new TestContextImplementation(this.testMethod, null, new Dictionary<string, object>());
            this.testClassInfo = new TestClassInfo(
                type: typeof(DummyTestClass),
                constructor: constructorInfo,
                testContextProperty: testContextProperty,
                classAttribute: classAttribute,
                parent: testAssemblyInfo);
            var globalTestMethodInfo = new TestMethodInfo(
                this.methodInfo,
                timeout: 3600 * 1000,
                executor: this.testMethodAttribute,
                expectedException: null,
                parent: this.testClassInfo,
                testContext: this.testContextImplementation);
            this.globalTestMethodRunner = new TestMethodRunner(globalTestMethodInfo, this.testMethod, this.testContextImplementation, false);

            // Reset test hooks
            DummyTestClass.TestConstructorMethodBody = () => { };
            DummyTestClass.TestContextSetterBody = value => { };
            DummyTestClass.TestInitializeMethodBody = value => { };
            DummyTestClass.TestMethodBody = instance => { };
            DummyTestClass.TestCleanupMethodBody = value => { };
        }

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        [TestMethodV1]
        public void ExecuteForAssemblyInitializeThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            // Arrange.
            var tai = new TestAssemblyInfo();
            tai.AssemblyInitializeMethod = typeof(TestMethodRunnerTests).GetMethod(
                "InitMethodThrowingException",
                BindingFlags.Static | BindingFlags.NonPublic);

            var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            var classAttribute = new UTF.TestClassAttribute();
            var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            var tci = new TestClassInfo(
                type: typeof(DummyTestClass),
                constructor: constructorInfo,
                testContextProperty: testContextProperty,
                classAttribute: classAttribute,
                parent: tai);
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, tci, this.testContextImplementation, () => { throw new Exception("DummyException"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            // Act.
            var results = testMethodRunner.Execute();

            // Assert.
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "System.ArgumentException: Value does not fall within the expected range.. Aborting test execution.");
        }

        [TestMethodV1]
        public void ExecuteForClassInitializeThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            // Arrange.
            var tai = new TestAssemblyInfo();

            var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            var classAttribute = new UTF.TestClassAttribute();
            var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            var tci = new TestClassInfo(
                type: typeof(DummyTestClass),
                constructor: constructorInfo,
                testContextProperty: testContextProperty,
                classAttribute: classAttribute,
                parent: tai);

            tci.ClassInitializeMethod = typeof(TestMethodRunnerTests).GetMethod(
                "InitMethodThrowingException",
                BindingFlags.Static | BindingFlags.NonPublic);

            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, tci, this.testContextImplementation, () => { throw new Exception("DummyException"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            // Act.
            var results = testMethodRunner.Execute();

            // Assert.
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "System.ArgumentException: Value does not fall within the expected range.");
        }

        [TestMethodV1]
        public void ExecuteForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => { throw new Exception("DummyException"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethodV1]
        public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void ExecuteShouldFillInDebugAndTraceLogsIfDebugTraceEnabled()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, true);

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].DebugTrace, "DummyTrace");
        }

        [TestMethodV1]
        public void ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].DebugTrace, string.Empty);
        }

        [TestMethodV1]
        public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => { throw new Exception("Dummy Exception"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.RunTestMethod();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethodV1]
        public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunTestMethodChecksIfTestsAreDataDriven()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            // setup mocks
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.HasDataDrivenTests(testMethodInfo));
            var results = testMethodRunner.Execute();

            // Since data is not provided, tests run normally giving passed as outcome.
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunTestMethodRunsDataDrivenTestsWhenDataIsProvided()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult());
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            // Set outcome to be failed
            var result = new UTF.TestResult();
            result.Outcome = UTF.UnitTestOutcome.Failed;

            // setup mocks
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.HasDataDrivenTests(It.IsAny<TestMethodInfo>())).Returns(true);
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.RunDataDrivenTest(It.IsAny<UTFExtension.TestContext>(), It.IsAny<TestMethodInfo>(), It.IsAny<TestMethod>(), It.IsAny<UTF.TestMethodAttribute>()))
                .Returns(new UTF.TestResult[] { result });

            var results = testMethodRunner.Execute();

            // check for outcome
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForPassedTestResultsConvertsToPassedUnitTestResults()
        {
           var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed } };
           var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Passed, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForFailedTestResultsConvertsToFailedUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForInProgressTestResultsConvertsToInProgressUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.InProgress } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.InProgress, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Timeout } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Timeout, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForUnknownTestResultsConvertsToErrorUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Unknown } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Error, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception() } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception(), Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultForTestResultShouldSetLoggingDatatForConvertedUnitTestResults()
        {
            var timespan = default(TimeSpan);
            var results = new[]
            {
                new UTF.TestResult()
                {
                    DebugTrace = "debugTrace", DisplayName = "displayName", Duration = timespan, LogOutput = "logOutput",
                                                         LogError = "logError", DatarowIndex = 1
                }
            };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual("logOutput", convertedResults[0].StandardOut);
            Assert.AreEqual("logError", convertedResults[0].StandardError);
            Assert.AreEqual("displayName", convertedResults[0].DisplayName);
            Assert.AreEqual("debugTrace", convertedResults[0].DebugTrace);
            Assert.AreEqual(timespan, convertedResults[0].Duration);
            Assert.AreEqual(1, convertedResults[0].DatarowIndex);
        }

        [TestMethodV1]
        public void ConvertTestResultToUnitTestResultShouldHaveResultsFileProvidedToTestResult()
        {
            var results = new[] { new UTF.TestResult() { ResultFiles = new List<string>() { "DummyFile.txt" } } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);
            Assert.AreEqual("DummyFile.txt", convertedResults[0].ResultFiles[0]);
        }

        #region Test data

        private static void InitMethodThrowingException(UTFExtension.TestContext tc)
        {
            throw new ArgumentException();
        }

        public class TestableTestmethodInfo : TestMethodInfo
        {
            private Func<UTF.TestResult> invokeTest;

            internal TestableTestmethodInfo(MethodInfo testMethod, int timeout, UTF.TestMethodAttribute executor, TestClassInfo parent, TestContextImplementation testContext, Func<UTF.TestResult> invoke)
                : base(testMethod, timeout, executor, null, parent, testContext)
            {
                this.invokeTest = invoke;
            }

            public override UTF.TestResult Invoke(object[] arguments)
            {
                // Ignore args for now
                return this.invokeTest();
            }
        }

        public class DummyTestClassBase
        {
            public static Action<DummyTestClassBase> BaseTestClassMethodBody { get; set; }

            public void DummyBaseTestClassMethod()
            {
                BaseTestClassMethodBody(this);
            }
        }

        public class DummyTestClass : DummyTestClassBase
        {
            public DummyTestClass()
            {
                TestConstructorMethodBody();
            }

            public static Action TestConstructorMethodBody { get; set; }

            public static Action<object> TestContextSetterBody { get; set; }

            public static Action<DummyTestClass> TestInitializeMethodBody { get; set; }

            public static Action<DummyTestClass> TestMethodBody { get; set; }

            public static Action<DummyTestClass> TestCleanupMethodBody { get; set; }

            public static Func<Task> DummyAsyncTestMethodBody { get; set; }

            public UTFExtension.TestContext TestContext
            {
                get
                {
                    throw new NotImplementedException();
                }

                set
                {
                    TestContextSetterBody(value);
                }
            }

            public void DummyTestInitializeMethod()
            {
                TestInitializeMethodBody(this);
            }

            public void DummyTestCleanupMethod()
            {
                TestCleanupMethodBody(this);
            }

            public void DummyTestMethod()
            {
                TestMethodBody(this);
            }

            public Task DummyAsyncTestMethod()
            {
                // We use this method to validate async TestInitialize, TestCleanup, TestMethod
                return DummyAsyncTestMethodBody();
            }
        }

        public class DummyTestClassWithParameterizedCtor
        {
            public DummyTestClassWithParameterizedCtor(int x)
            {
            }
        }

        public class DummyTestClassWithTestContextWithoutSetter
        {
            public UTFExtension.TestContext TestContext { get; }
        }

        #endregion
    }
}

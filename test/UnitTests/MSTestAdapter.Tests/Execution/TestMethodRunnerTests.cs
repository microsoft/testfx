// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestMethodRunnerTests.cs" company="Microsoft">
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestableImplementations;
    using MSTest.TestAdapter;
    using Moq;
    using PlatformServices.Interface;

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
            this.testContextImplementation = new TestContextImplementation(testMethod, null, new Dictionary<string, object>());
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void ExecuteForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => { throw new Exception("DummyException"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethod]
        public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethod]
        public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => { throw new Exception("Dummy Exception"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.RunTestMethod();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethod]
        public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethod]
        public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        }

        [TestMethod]
        public void RunTestMethodChecksIfTestsAreDataDriven()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            //setup mocks
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.HasDataDrivenTests(testMethodInfo));
            var results = testMethodRunner.Execute();

            // Since data is not provided, tests run normally giving passed as outcome.
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethod]
        public void RunTestMethodRunsDataDrivenTestsWhenDataIsProvided()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, this.testContextImplementation, () => new UTF.TestResult());
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            //Set outcome to be failed
            var result = new UTF.TestResult();
            result.Outcome = UTF.UnitTestOutcome.Failed;

            //setup mocks
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.HasDataDrivenTests(It.IsAny<TestMethodInfo>())).Returns(true);
            this.testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.RunDataDrivenTest(It.IsAny<UTF.TestContext>(), It.IsAny<TestMethodInfo>(),It.IsAny<TestMethod>(),It.IsAny<UTF.TestMethodAttribute>()))
                .Returns(new UTF.TestResult[] { result });

            var results = testMethodRunner.Execute();

            //check for outcome
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForPassedTestResultsConvertsToPassedUnitTestResults()
        {
           var results = new[] { new UTF.TestResult() {Outcome = UTF.UnitTestOutcome.Passed} };
           var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Passed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForFailedTestResultsConvertsToFailedUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForInProgressTestResultsConvertsToInProgressUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.InProgress } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.InProgress, convertedResults[0].Outcome);
        }
        [TestMethod]
        public void ConvertTestResultToUnitTestResultForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Timeout } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Timeout, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForUnknownTestResultsConvertsToErrorUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Unknown } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Error, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception() } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception(), Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultForTestResultShouldSetLoggingDatatForConvertedUnitTestResults()
        {
            var timespan = new TimeSpan();
            var results = new[] { new UTF.TestResult() { DebugTrace = "debugTrace", DisplayName = "displayName", Duration = timespan, LogOutput = "logOutput" } };
            var convertedResults = this.globalTestMethodRunner.ConvertTestResultToUnitTestResult(results);

            Assert.AreEqual("logOutput", convertedResults[0].StandardOut);
            Assert.AreEqual("displayName", convertedResults[0].DisplayName);
            Assert.AreEqual("debugTrace", convertedResults[0].DebugTrace);
            Assert.AreEqual(timespan, convertedResults[0].Duration);
        }

        [TestMethod]
        public void ConvertTestResultToUnitTestResultShouldHaveResultsFileProvidedToTestContext()
        {
            Mock<ITestContext> testContext = new Mock<ITestContext>();
            testContext.Setup(tc => tc.GetResultFiles()).Returns(new List<string>() { "DummyFile.txt" });

            var results = new[] { new UTF.TestResult() { } };
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, 200, this.testMethodAttribute, this.testClassInfo, testContext.Object as TestContextImplementation, () => new UTF.TestResult());
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, testContext.Object, false);

            var convertedResults = testMethodRunner.ConvertTestResultToUnitTestResult(results);
            Assert.AreEqual("DummyFile.txt", convertedResults[0].ResultFiles[0]);
        }


        #region Test data

        private static void InitMethodThrowingException(UTF.TestContext tc)
        {
            throw new ArgumentException();
        }

        public class TestableTestmethodInfo : TestMethodInfo
        {
            private Func<UTF.TestResult> InvokeTest; 

            internal TestableTestmethodInfo(MethodInfo testMethod, int timeout, UTF.TestMethodAttribute executor, TestClassInfo parent, TestContextImplementation testContext, Func<UTF.TestResult> invoke)
                : base(testMethod, timeout, executor, parent, testContext)
            {
                this.InvokeTest = invoke;
            }

            public override UTF.TestResult Invoke(object[] arguments)
            {
                // Ignore args for now
                return this.InvokeTest();
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

            public TestTools.UnitTesting.TestContext TestContext
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
            public TestTools.UnitTesting.TestContext TestContext { get; }
        }
        
        #endregion
    }
}

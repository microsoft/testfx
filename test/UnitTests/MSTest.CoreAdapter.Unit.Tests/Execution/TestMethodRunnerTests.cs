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
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Moq;
    using MSTest.TestAdapter;
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

        private TestMethodOptions globaltestMethodOptions;
        private TestMethodOptions testMethodOptions;
        private Mock<ReflectHelper> mockReflectHelper;

        public TestMethodRunnerTests()
        {
            var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            this.methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
            var classAttribute = new UTF.TestClassAttribute();
            this.testMethodAttribute = new UTF.TestMethodAttribute();
            var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            var testAssemblyInfo = new TestAssemblyInfo();
            this.testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
            this.testContextImplementation = new TestContextImplementation(this.testMethod, new StringWriter(), new Dictionary<string, object>());
            this.testClassInfo = new TestClassInfo(
                type: typeof(DummyTestClass),
                constructor: constructorInfo,
                testContextProperty: testContextProperty,
                classAttribute: classAttribute,
                parent: testAssemblyInfo);

            this.globaltestMethodOptions = new TestMethodOptions()
            {
                Timeout = 3600 * 1000,
                Executor = this.testMethodAttribute,
                TestContext = this.testContextImplementation,
                ExpectedException = null
            };
            var globalTestMethodInfo = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.globaltestMethodOptions);
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            this.globalTestMethodRunner = new TestMethodRunner(globalTestMethodInfo, this.testMethod, this.testContextImplementation, false);

            this.testMethodOptions = new TestMethodOptions()
            {
                Timeout = 200,
                Executor = this.testMethodAttribute,
                TestContext = this.testContextImplementation,
                ExpectedException = null
            };

            this.mockReflectHelper = new Mock<ReflectHelper>();

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
        public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(typeof(DummyTestClass).GetTypeInfo())).Returns("IgnoreTestClassMessage");

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, "IgnoreTestClassMessage");
        }

        [TestMethodV1]
        public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(typeof(DummyTestClass).GetTypeInfo())).Returns(string.Empty);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, string.Empty);
        }

        [TestMethodV1]
        public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(this.methodInfo)).Returns("IgnoreMethodMessage");

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, "IgnoreMethodMessage");
        }

        [TestMethodV1]
        public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(this.methodInfo)).Returns(string.Empty);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, string.Empty);
        }

        [TestMethodV1]
        public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);

            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(typeof(DummyTestClass).GetTypeInfo())).Returns("IgnoreTestClassMessage");
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(this.methodInfo)).Returns("IgnoreMethodMessage");

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, "IgnoreTestClassMessage");
        }

        [TestMethodV1]
        public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, null);
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false, this.mockReflectHelper.Object);

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(this.methodInfo, typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);

            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(typeof(DummyTestClass).GetTypeInfo())).Returns(string.Empty);
            this.mockReflectHelper.Setup(rh => rh.GetIgnoreMessage(this.methodInfo)).Returns("IgnoreMethodMessage");

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].Outcome, AdapterTestOutcome.Ignored);
            Assert.AreEqual(results[0].ErrorMessage, "IgnoreMethodMessage");
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
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, tci, this.testMethodOptions, () => { throw new Exception("DummyException"); });
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

            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, tci, this.testMethodOptions, () => { throw new Exception("DummyException"); });
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
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => { throw new Exception("DummyException"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethodV1]
        public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void ExecuteShouldFillInDebugAndTraceLogsFromClassInitialize()
        {
            StringWriter writer = new StringWriter(new StringBuilder());
            DummyTestClass.ClassInitializeMethodBody = (UTFExtension.TestContext tc) =>
                                                            {
                                                                writer.Write("ClassInit trace");
                                                            };
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("DummyClassInit");
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, true);

            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();
            Assert.AreEqual("ClassInit trace", results[0].DebugTrace);
        }

        [TestMethodV1]
        public void ExecuteShouldFillInDebugAndTraceLogsFromAssemblyInitialize()
        {
            StringWriter writer = new StringWriter(new StringBuilder());
            DummyTestClass.AssemblyInitializeMethodBody = (UTFExtension.TestContext tc) =>
            {
                writer.Write("AssemblyInit trace");
            };
            this.testClassInfo.Parent.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAssemblyInit");
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, true);

            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();
            Assert.AreEqual("AssemblyInit trace", results[0].DebugTrace);
        }

        [TestMethodV1]
        public void ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(results[0].DebugTrace, string.Empty);
        }

        [TestMethodV1]
        public void ExecuteShouldNotFillInDebugAndTraceLogsFromRunningTestMethod()
        {
            StringWriter writer = new StringWriter(new StringBuilder());
            var testMethodInfo = new TestableTestmethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions,
                () =>
                {
                    writer.Write("InTestMethod");
                    return new UTF.TestResult()
                    {
                        Outcome = UTF.UnitTestOutcome.Passed
                    };
                });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, true);
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();

            Assert.AreEqual(string.Empty, results[0].DebugTrace);
        }

        [TestMethodV1]
        public void ExecuteShouldFillInLogsIfAssemblyInitializeThrows()
        {
            StringWriter writer = new StringWriter(new StringBuilder());
            DummyTestClass.AssemblyInitializeMethodBody = (UTFExtension.TestContext tc) =>
            {
                writer.Write("Hills");
                tc.WriteLine("Valleys");
                throw new ArgumentException();
            };
            this.testClassInfo.Parent.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAssemblyInit");
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, true);

            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            var results = testMethodRunner.Execute();

            Assert.AreEqual("Hills", results[0].DebugTrace);
            StringAssert.Contains(results[0].TestContextMessages, "Valleys");
        }

        [TestMethodV1]
        public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => { throw new Exception("Dummy Exception"); });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.RunTestMethod();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
            StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
        }

        [TestMethodV1]
        public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo,  this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
            var testMethodRunner = new TestMethodRunner(testMethodInfo, this.testMethod, this.testContextImplementation, false);

            var results = testMethodRunner.Execute();
            Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunTestMethodChecksIfTestsAreDataDriven()
        {
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
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
            var testMethodInfo = new TestableTestmethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions, () => new UTF.TestResult());
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

        #region Test data

        private static void InitMethodThrowingException(UTFExtension.TestContext tc)
        {
            throw new ArgumentException();
        }

        public class TestableTestmethodInfo : TestMethodInfo
        {
            private Func<UTF.TestResult> invokeTest;

            internal TestableTestmethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<UTF.TestResult> invoke)
                : base(testMethod, parent, testMethodOptions)
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

            public static Action<UTFExtension.TestContext> AssemblyInitializeMethodBody { get; set; }

            public static Action<UTFExtension.TestContext> ClassInitializeMethodBody { get; set; }

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

            public static void DummyAssemblyInit(UTFExtension.TestContext tc)
            {
                AssemblyInitializeMethodBody(tc);
            }

            public static void DummyClassInit(UTFExtension.TestContext tc)
            {
                ClassInitializeMethodBody(tc);
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

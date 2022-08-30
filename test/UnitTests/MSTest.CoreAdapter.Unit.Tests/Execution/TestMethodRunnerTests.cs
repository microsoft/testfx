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
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
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
    private readonly TestMethodRunner _globalTestMethodRunner;

    private readonly MethodInfo _methodInfo;

    private readonly UTF.TestMethodAttribute _testMethodAttribute;

    private readonly TestContextImplementation _testContextImplementation;

    private readonly TestClassInfo _testClassInfo;

    private readonly TestMethod _testMethod;

    private readonly TestMethodOptions _globaltestMethodOptions;

    private readonly TestMethodOptions _testMethodOptions;

    private readonly Mock<ReflectHelper> _mockReflectHelper;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TestMethodRunnerTests()
    {
        var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
        var classAttribute = new UTF.TestClassAttribute();
        _testMethodAttribute = new UTF.TestMethodAttribute();
        var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        var testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);
        _testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(_testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>());
        _testClassInfo = new TestClassInfo(
            type: typeof(DummyTestClass),
            constructor: constructorInfo,
            testContextProperty: testContextProperty,
            classAttribute: classAttribute,
            parent: testAssemblyInfo);

        _globaltestMethodOptions = new TestMethodOptions()
        {
            Timeout = 3600 * 1000,
            Executor = _testMethodAttribute,
            TestContext = _testContextImplementation,
            ExpectedException = null
        };
        var globalTestMethodInfo = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _globaltestMethodOptions);
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, null);
        _globalTestMethodRunner = new TestMethodRunner(globalTestMethodInfo, _testMethod, _testContextImplementation, false);

        _testMethodOptions = new TestMethodOptions()
        {
            Timeout = 200,
            Executor = _testMethodAttribute,
            TestContext = _testContextImplementation,
            ExpectedException = null
        };

        _mockReflectHelper = new Mock<ReflectHelper>();

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
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup() => PlatformServiceProvider.Instance = null;

    [TestMethodV1]
    public void ExecuteForAssemblyInitializeThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        // Arrange.
        var tai = new TestAssemblyInfo(typeof(TestMethodRunnerTests).Assembly)
        {
            AssemblyInitializeMethod = typeof(TestMethodRunnerTests).GetMethod(
            "InitMethodThrowingException",
            BindingFlags.Static | BindingFlags.NonPublic)
        };

        var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
        var classAttribute = new UTF.TestClassAttribute();
        var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        var tci = new TestClassInfo(
            type: typeof(DummyTestClass),
            constructor: constructorInfo,
            testContextProperty: testContextProperty,
            classAttribute: classAttribute,
            parent: tai);
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, tci, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

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
        var tai = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);

        var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
        var classAttribute = new UTF.TestClassAttribute();
        var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        var tci = new TestClassInfo(
            type: typeof(DummyTestClass),
            constructor: constructorInfo,
            testContextProperty: testContextProperty,
            classAttribute: classAttribute,
            parent: tai)
        {
            ClassInitializeMethod = typeof(TestMethodRunnerTests).GetMethod(
            "InitMethodThrowingException",
            BindingFlags.Static | BindingFlags.NonPublic)
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, tci, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        // Act.
        var results = testMethodRunner.Execute();

        // Assert.
        Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        StringAssert.Contains(results[0].ErrorMessage, "System.ArgumentException: Value does not fall within the expected range.");
    }

    [TestMethodV1]
    public void ExecuteForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
    }

    [TestMethodV1]
    public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
    }

    [TestMethodV1]
    public void ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        StringWriter writer = new(new StringBuilder("DummyTrace"));
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(results[0].DebugTrace, string.Empty);
    }

    [TestMethodV1]
    public void ExecuteShouldNotFillInDebugAndTraceLogsFromRunningTestMethod()
    {
        StringWriter writer = new(new StringBuilder());
        var testMethodInfo = new TestableTestmethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () =>
            {
                writer.Write("InTestMethod");
                return new UTF.TestResult()
                {
                    Outcome = UTF.UnitTestOutcome.Passed
                };
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, true);
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        var results = testMethodRunner.Execute();

        Assert.AreEqual(string.Empty, results[0].DebugTrace);
    }

    [TestMethodV1]
    public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("Dummy Exception"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();
        Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
        StringAssert.Contains(results[0].ErrorMessage, "Exception thrown while executing test");
    }

    [TestMethodV1]
    public void RunTestMethodForMultipleResultsReturnMultipleResults()
    {
        var testMethodAttributeMock = new Mock<UTF.TestMethodAttribute>();
        testMethodAttributeMock.Setup(_ => _.Execute(It.IsAny<UTF.ITestMethod>())).Returns(new[]
        {
            new UTF.TestResult { Outcome = UTF.UnitTestOutcome.Passed },
            new UTF.TestResult { Outcome = UTF.UnitTestOutcome.Failed }
        });

        var localTestMethodOptions = new TestMethodOptions
        {
            Timeout = 200,
            Executor = testMethodAttributeMock.Object,
            TestContext = _testContextImplementation,
            ExpectedException = null
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, localTestMethodOptions, null);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(2, results.Length);

        Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        Assert.AreEqual(AdapterTestOutcome.Failed, results[1].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodShouldGiveTestResultAsPassedWhenTestMethodPasses()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodShouldGiveTestResultAsFailedWhenTestMethodFails()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Assert.AreEqual(AdapterTestOutcome.Failed, results[0].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attribs = new Attribute[] { dataSourceAttribute };

        TestDataSource testDataSource = new();

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns(new object[] { 1, 2, 3 });

        var results = testMethodRunner.RunTestMethod();

        // check for outcome
        Assert.AreEqual(AdapterTestOutcome.Passed, results[0].Outcome);
        Assert.AreEqual(AdapterTestOutcome.Passed, results[1].Outcome);
        Assert.AreEqual(AdapterTestOutcome.Passed, results[2].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataRowAttribute()
    {
        UTF.TestResult testResult = new()
        {
            Outcome = UTF.UnitTestOutcome.Inconclusive
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false, _mockReflectHelper.Object);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();
        Assert.AreEqual(AdapterTestOutcome.Inconclusive, results[0].Outcome);
    }

    [TestMethodV1]
    public void RunTestMethodShouldSetDataRowIndexForDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attribs = new Attribute[] { dataSourceAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns(new object[] { 1, 2, 3 });

        var results = testMethodRunner.RunTestMethod();

        // check for datarowIndex
        Assert.AreEqual(0, results[0].DatarowIndex);
        Assert.AreEqual(1, results[1].DatarowIndex);
        Assert.AreEqual(2, results[2].DatarowIndex);
    }

    [TestMethodV1]
    public void RunTestMethodShoudlRunOnlyDataSourceTestsWhenBothDataSourceAndDataRowAreProvided()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");
        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attribs = new Attribute[] { dataSourceAttribute, dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns(new object[] { 1, 2, 3 });

        var results = testMethodRunner.RunTestMethod();

        // check for datarowIndex as only DataSource Tests are Run
        Assert.AreEqual(0, results[0].DatarowIndex);
        Assert.AreEqual(1, results[1].DatarowIndex);
        Assert.AreEqual(2, results[2].DatarowIndex);
    }

    [TestMethodV1]
    public void RunTestMethodShouldFillInDisplayNameWithDataRowDisplayNameIfProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false, _mockReflectHelper.Object);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(dummyIntData, dummyStringData)
        {
            DisplayName = "DataRowTestDisplayName"
        };

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();

        Assert.AreEqual(1, results.Length);
        Assert.AreEqual("DataRowTestDisplayName", results[0].DisplayName);
    }

    [TestMethodV1]
    public void RunTestMethodShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false, _mockReflectHelper.Object);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();

        Assert.AreEqual(1, results.Length);
        Assert.AreEqual("DummyTestMethod (2,DummyString)", results[0].DisplayName);
    }

    [TestMethodV1]
    public void RunTestMethodShouldSetResultFilesIfPresentForDataDrivenTests()
    {
        UTF.TestResult testResult = new()
        {
            ResultFiles = new List<string>() { "C:\\temp.txt" }
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false, _mockReflectHelper.Object);

        int dummyIntData1 = 1;
        int dummyIntData2 = 2;
        UTF.DataRowAttribute dataRowAttribute1 = new(dummyIntData1);
        UTF.DataRowAttribute dataRowAttribute2 = new(dummyIntData2);

        var attribs = new Attribute[] { dataRowAttribute1, dataRowAttribute2 };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();
        CollectionAssert.Contains(results[0].ResultFiles.ToList(), "C:\\temp.txt");
        CollectionAssert.Contains(results[1].ResultFiles.ToList(), "C:\\temp.txt");
    }

    #region Test data

    private static void InitMethodThrowingException(UTFExtension.TestContext tc) => throw new ArgumentException();

    public class TestableTestmethodInfo : TestMethodInfo
    {
        private readonly Func<UTF.TestResult> _invokeTest;

        internal TestableTestmethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<UTF.TestResult> invoke)
            : base(testMethod, parent, testMethodOptions) => _invokeTest = invoke;

        public override UTF.TestResult Invoke(object[] arguments) =>
            // Ignore args for now
            _invokeTest();
    }

    public class DummyTestClassBase
    {
        public static Action<DummyTestClassBase> BaseTestClassMethodBody { get; set; }

        public void DummyBaseTestClassMethod() => BaseTestClassMethodBody(this);
    }

    public class DummyTestClass : DummyTestClassBase
    {
        public DummyTestClass() => TestConstructorMethodBody();

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
            get => throw new NotImplementedException();

            set => TestContextSetterBody(value);
        }

        public static void DummyAssemblyInit(UTFExtension.TestContext tc) => AssemblyInitializeMethodBody(tc);

        public static void DummyClassInit(UTFExtension.TestContext tc) => ClassInitializeMethodBody(tc);

        public void DummyTestInitializeMethod() => TestInitializeMethodBody(this);

        public void DummyTestCleanupMethod() => TestCleanupMethodBody(this);

        public void DummyTestMethod() => TestMethodBody(this);

        public Task DummyAsyncTestMethod() =>
            // We use this method to validate async TestInitialize, TestCleanup, TestMethod
            DummyAsyncTestMethodBody();
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

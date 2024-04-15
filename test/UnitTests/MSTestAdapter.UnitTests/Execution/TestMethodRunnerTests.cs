// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestMethodRunnerTests : TestContainer
{
    private readonly MethodInfo _methodInfo;

    private readonly UTF.TestMethodAttribute _testMethodAttribute;

    private readonly TestContextImplementation _testContextImplementation;

    private readonly TestClassInfo _testClassInfo;

    private readonly TestMethod _testMethod;

    private readonly TestMethodOptions _globalTestMethodOptions;

    private readonly TestMethodOptions _testMethodOptions;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TestMethodRunnerTests()
    {
        var constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod", StringComparison.Ordinal));
        var classAttribute = new UTF.TestClassAttribute();
        _testMethodAttribute = new UTF.TestMethodAttribute();
        var testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        var testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);
        _testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(_testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>(), null);
        _testClassInfo = new TestClassInfo(
            type: typeof(DummyTestClass),
            constructor: constructorInfo,
            testContextProperty: testContextProperty,
            classAttribute: classAttribute,
            parent: testAssemblyInfo);

        _globalTestMethodOptions = new TestMethodOptions()
        {
            Timeout = 3600 * 1000,
            Executor = _testMethodAttribute,
            TestContext = _testContextImplementation,
            ExpectedException = null,
        };
        var globalTestMethodInfo = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _globalTestMethodOptions);
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, null);

        _testMethodOptions = new TestMethodOptions()
        {
            Timeout = 200,
            Executor = _testMethodAttribute,
            TestContext = _testContextImplementation,
            ExpectedException = null,
        };

        // Reset test hooks
        DummyTestClass.TestConstructorMethodBody = () => { };
        DummyTestClass.TestContextSetterBody = value => { };
        DummyTestClass.TestInitializeMethodBody = value => { };
        DummyTestClass.TestMethodBody = instance => { };
        DummyTestClass.TestCleanupMethodBody = value => { };

        // TestInitialize
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void ExecuteForAssemblyInitializeThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        // Arrange.
        var tai = new TestAssemblyInfo(typeof(TestMethodRunnerTests).Assembly)
        {
            AssemblyInitializeMethod = typeof(TestMethodRunnerTests).GetMethod(
            "InitMethodThrowingException",
            BindingFlags.Static | BindingFlags.NonPublic),
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
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, tci, _testMethodOptions, () => { throw new Exception("DummyException"); });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        // Act.
        var results = testMethodRunner.Execute();

        // Assert.
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
        Verify(results[0].ErrorMessage.Contains("System.ArgumentException: Value does not fall within the expected range.. Aborting test execution."));
    }

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
            BindingFlags.Static | BindingFlags.NonPublic),
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, tci, _testMethodOptions, () => { throw new Exception("DummyException"); });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        // Act.
        var results = testMethodRunner.Execute();

        // Assert.
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
        Verify(results[0].ErrorMessage.Contains("System.ArgumentException: Value does not fall within the expected range."));
    }

    public void ExecuteForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => { throw new Exception("DummyException"); });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
        Verify(results[0].ErrorMessage.Contains("Exception thrown while executing test"));
    }

    public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        StringWriter writer = new(new StringBuilder("DummyTrace"));
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        var results = testMethodRunner.Execute();
        Verify(results[0].DebugTrace == string.Empty);
    }

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
                    Outcome = UTF.UnitTestOutcome.Passed,
                };
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, true);
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        var results = testMethodRunner.Execute();

        Verify(results[0].DebugTrace == string.Empty);
    }

    public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => { throw new Exception("Dummy Exception"); });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
        Verify(results[0].ErrorMessage.Contains("Exception thrown while executing test"));
    }

    public void RunTestMethodForMultipleResultsReturnMultipleResults()
    {
        var testMethodAttributeMock = new Mock<UTF.TestMethodAttribute>();
        testMethodAttributeMock.Setup(_ => _.Execute(It.IsAny<UTF.ITestMethod>())).Returns(
        [
            new UTF.TestResult { Outcome = UTF.UnitTestOutcome.Passed },
            new UTF.TestResult { Outcome = UTF.UnitTestOutcome.Failed },
        ]);

        var localTestMethodOptions = new TestMethodOptions
        {
            Timeout = 200,
            Executor = testMethodAttributeMock.Object,
            TestContext = _testContextImplementation,
            ExpectedException = null,
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, localTestMethodOptions, null);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Verify(results.Length == 2);

        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
        Verify(results[1].Outcome == AdapterTestOutcome.Failed);
    }

    public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.Execute();
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
    }

    public void RunTestMethodShouldGiveTestResultAsPassedWhenTestMethodPasses()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void RunTestMethodShouldGiveTestResultAsFailedWhenTestMethodFails()
    {
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        var results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
    }

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
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
        Verify(results[1].Outcome == AdapterTestOutcome.Passed);
        Verify(results[2].Outcome == AdapterTestOutcome.Passed);
    }

    public void RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataRowAttribute()
    {
        UTF.TestResult testResult = new()
        {
            Outcome = UTF.UnitTestOutcome.Inconclusive,
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();
        Verify(results[0].Outcome == AdapterTestOutcome.Inconclusive);
    }

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
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

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
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

    public void RunTestMethodShouldFillInDisplayNameWithDataRowDisplayNameIfProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(dummyIntData, dummyStringData)
        {
            DisplayName = "DataRowTestDisplayName",
        };

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName == "DataRowTestDisplayName");
    }

    public void RunTestMethodShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attribs = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName == "DummyTestMethod (2,DummyString)");
    }

    public void RunTestMethodShouldSetResultFilesIfPresentForDataDrivenTests()
    {
        UTF.TestResult testResult = new()
        {
            ResultFiles = new List<string>() { "C:\\temp.txt" },
        };

        var testMethodInfo = new TestableTestmethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, false);

        int dummyIntData1 = 1;
        int dummyIntData2 = 2;
        UTF.DataRowAttribute dataRowAttribute1 = new(dummyIntData1);
        UTF.DataRowAttribute dataRowAttribute2 = new(dummyIntData2);

        var attribs = new Attribute[] { dataRowAttribute1, dataRowAttribute2 };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attribs);

        var results = testMethodRunner.RunTestMethod();
        Verify(results[0].ResultFiles.ToList().Contains("C:\\temp.txt"));
        Verify(results[1].ResultFiles.ToList().Contains("C:\\temp.txt"));
    }

    #region Test data

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Use through reflection")]
    private static void InitMethodThrowingException(UTFExtension.TestContext tc)
    {
        // TODO: Fix exception type
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
        throw new ArgumentException();
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
    }

    public class TestableTestmethodInfo : TestMethodInfo
    {
        private readonly Func<UTF.TestResult> _invokeTest;

        internal TestableTestmethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<UTF.TestResult> invoke)
            : base(testMethod, parent, testMethodOptions)
        {
            _invokeTest = invoke;
        }

        public override UTF.TestResult Invoke(object[] arguments)
        {
            // Ignore args for now
            return _invokeTest();
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

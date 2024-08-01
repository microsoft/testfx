// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
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
        ConstructorInfo constructorInfo = typeof(DummyTestClass).GetConstructor([])!;
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod", StringComparison.Ordinal));
        var classAttribute = new UTF.TestClassAttribute();
        _testMethodAttribute = new UTF.TestMethodAttribute();
        PropertyInfo testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        var testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);
        _testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(_testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>());
        _testClassInfo = new TestClassInfo(typeof(DummyTestClass), constructorInfo, true, testContextProperty, classAttribute, testAssemblyInfo);

        _globalTestMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(3600 * 1000), null, _testContextImplementation, false, _testMethodAttribute);
        var globalTestMethodInfo = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _globalTestMethodOptions);
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, null);

        _testMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), null, _testContextImplementation, false, _testMethodAttribute);

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

        ReflectHelper.Instance.ClearCache();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void ExecuteForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
        Verify(results[0].ErrorMessage.Contains("Exception thrown while executing test"));
    }

    public void ExecuteForPassingTestShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        StringWriter writer = new(new StringBuilder("DummyTrace"));
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].DebugTrace == string.Empty);
    }

    public void ExecuteShouldNotFillInDebugAndTraceLogsFromRunningTestMethod()
    {
        StringWriter writer = new(new StringBuilder());
        var testMethodInfo = new TestableTestMethodInfo(
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
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);

        Verify(results[0].DebugTrace == string.Empty);
    }

    public void RunTestMethodForTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("Dummy Exception"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();
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

        var localTestMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), null, _testContextImplementation, false, testMethodAttributeMock.Object);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, localTestMethodOptions, null);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results.Length == 2);

        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
        Verify(results[1].Outcome == AdapterTestOutcome.Failed);
    }

    public void RunTestMethodForPassingTestThrowingExceptionShouldReturnUnitTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void RunTestMethodForFailingTestThrowingExceptionShouldReturnUnitTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.Execute(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
    }

    public void RunTestMethodShouldGiveTestResultAsPassedWhenTestMethodPasses()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == AdapterTestOutcome.Passed);
    }

    public void RunTestMethodShouldGiveTestResultAsFailedWhenTestMethodFails()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == AdapterTestOutcome.Failed);
    }

    public void RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attributes = new Attribute[] { dataSourceAttribute };

        TestDataSource testDataSource = new();

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

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

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attributes);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();
        Verify(results[0].Outcome == AdapterTestOutcome.Inconclusive);
    }

    public void RunTestMethodShouldSetDataRowIndexForDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attributes = new Attribute[] { dataSourceAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        // check for datarowIndex
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

    public void RunTestMethodShouldRunOnlyDataSourceTestsWhenBothDataSourceAndDataRowAreProvided()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new UTF.TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        UTF.DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");
        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataSourceAttribute, dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        // check for datarowIndex as only DataSource Tests are Run
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

    public void RunTestMethodShouldFillInDisplayNameWithDataRowDisplayNameIfProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(dummyIntData, dummyStringData)
        {
            DisplayName = "DataRowTestDisplayName",
        };

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName == "DataRowTestDisplayName");
    }

    public void RunTestMethodShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvidedForDataDrivenTests()
    {
        UTF.TestResult testResult = new();
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        UTF.DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName is "dummyTestName (2,\"DummyString\")" or "DummyTestMethod (2,DummyString)", $"Display name: {results[0].DisplayName}");
    }

    public void RunTestMethodShouldSetResultFilesIfPresentForDataDrivenTests()
    {
        UTF.TestResult testResult = new()
        {
            ResultFiles = new List<string>() { "C:\\temp.txt" },
        };

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        int dummyIntData1 = 1;
        int dummyIntData2 = 2;
        UTF.DataRowAttribute dataRowAttribute1 = new(dummyIntData1);
        UTF.DataRowAttribute dataRowAttribute2 = new(dummyIntData2);

        var attributes = new Attribute[] { dataRowAttribute1, dataRowAttribute2 };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        UnitTestResult[] results = testMethodRunner.RunTestMethod();
        Verify(results[0].ResultFiles.ToList().Contains("C:\\temp.txt"));
        Verify(results[1].ResultFiles.ToList().Contains("C:\\temp.txt"));
    }

    #region Test data

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Use through reflection")]
    private static void InitMethodThrowingException(UTFExtension.TestContext tc)
        // TODO: Fix exception type
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
        => throw new ArgumentException();
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

    public class TestableTestMethodInfo : TestMethodInfo
    {
        private readonly Func<UTF.TestResult> _invokeTest;

        internal TestableTestMethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<UTF.TestResult> invoke)
            : base(testMethod, parent, testMethodOptions)
        {
            _invokeTest = invoke;
        }

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

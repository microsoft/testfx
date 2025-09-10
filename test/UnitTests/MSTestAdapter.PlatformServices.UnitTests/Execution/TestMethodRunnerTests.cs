// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting.Resources;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestMethodRunnerTests : TestContainer
{
    private readonly MethodInfo _methodInfo;

    private readonly TestMethodAttribute _testMethodAttribute;

    private readonly TestContextImplementation _testContextImplementation;

    private readonly TestClassInfo _testClassInfo;

    private readonly TestMethod _testMethod;

    private readonly TestMethodOptions _testMethodOptions;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TestMethodRunnerTests()
    {
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod", StringComparison.Ordinal));
        _testMethodAttribute = new TestMethodAttribute();

        _testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(_testMethod, new ThreadSafeStringWriter(null!, "test"), new Dictionary<string, object?>());
        _testClassInfo = GetTestClassInfo<DummyTestClass>();

        _testMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), _testContextImplementation, _testMethodAttribute);

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

    private static TestClassInfo GetTestClassInfo<T>()
    {
        ConstructorInfo constructorInfo = typeof(T).GetConstructor([])!;
        var classAttribute = new TestClassAttribute();
        var testAssemblyInfo = new TestAssemblyInfo(typeof(T).Assembly);
        return new TestClassInfo(typeof(T), constructorInfo, isParameterlessConstructor: true, classAttribute, testAssemblyInfo);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public async Task ExecuteForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Failed);
        Verify(results[0].ExceptionMessage!.StartsWith(
            """
            An unhandled exception was thrown by the 'Execute' method. Please report this error to the author of the attribute 'Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute'.
            System.Exception: DummyException
            """,
            StringComparison.Ordinal));
    }

    public async Task ExecuteForPassingTestShouldReturnTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        StringWriter writer = new(new StringBuilder("DummyTrace"));
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].DebugTrace == string.Empty);
    }

    public async Task ExecuteShouldNotFillInDebugAndTraceLogsFromRunningTestMethod()
    {
        StringWriter writer = new(new StringBuilder());
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () =>
            {
                writer.Write("InTestMethod");
                return new TestResult
                {
                    Outcome = UTF.UnitTestOutcome.Passed,
                };
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);
        _testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        Verify(results[0].DebugTrace == string.Empty);
    }

    public async Task RunTestMethodForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("Dummy Exception"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Failed);
        Verify(results[0].ExceptionMessage!.StartsWith(
            """
            An unhandled exception was thrown by the 'Execute' method. Please report this error to the author of the attribute 'Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute'.
            System.Exception: Dummy Exception
            """,
            StringComparison.Ordinal));
    }

    public async Task RunTestMethodForMultipleResultsReturnMultipleResults()
    {
        var testMethodAttributeMock = new Mock<TestMethodAttribute>();
        testMethodAttributeMock.Setup(_ => _.ExecuteAsync(It.IsAny<ITestMethod>())).Returns(Task.FromResult<TestResult[]>(
        [
            new TestResult { Outcome = UTF.UnitTestOutcome.Passed },
            new TestResult { Outcome = UTF.UnitTestOutcome.Failed },
        ]));

        var localTestMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), _testContextImplementation, testMethodAttributeMock.Object);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, localTestMethodOptions, null!);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results.Length == 2);

        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
        Verify(results[1].Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task RunTestMethodForPassingTestThrowingExceptionShouldReturnTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodForFailingTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task RunTestMethodShouldGiveTestResultAsPassedWhenTestMethodPasses()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodShouldGiveTestResultAsFailedWhenTestMethodFails()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // Since data is not provided, tests run normally giving passed as outcome.
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attributes = new Attribute[] { dataSourceAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UTF.UnitTestOutcome.Passed });
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // check for outcome
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
        Verify(results[1].Outcome == UTF.UnitTestOutcome.Passed);
        Verify(results[2].Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataRowAttribute()
    {
        TestResult testResult = new()
        {
            Outcome = UTF.UnitTestOutcome.Inconclusive,
        };

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<Type>(), It.IsAny<bool>())).Returns(attributes);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task RunTestMethodShouldSetDataRowIndexForDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals(nameof(DummyTestClass.DummyDataSourceTestMethod), StringComparison.Ordinal));
        object[] attributes = methodInfo.GetCustomAttributes(inherit: false);

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // check for datarowIndex
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

    public async Task RunTestMethodShouldRunOnlyDataSourceTestsWhenBothDataSourceAndDataRowAreProvided()
    {
        DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");
        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataSourceAttribute, dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // check for datarowIndex as only DataSource Tests are Run
        Verify(results[0].DatarowIndex == 0);
        Verify(results[1].DatarowIndex == 1);
        Verify(results[2].DatarowIndex == 2);
    }

    public async Task RunTestMethodShouldFillInDisplayNameWithDataRowDisplayNameIfProvidedForDataDrivenTests()
    {
        TestResult testResult = new();

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        DataRowAttribute dataRowAttribute = new(dummyIntData, dummyStringData)
        {
            DisplayName = "DataRowTestDisplayName",
        };

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName == "DataRowTestDisplayName");
    }

    public async Task RunTestMethodShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvidedForDataDrivenTests()
    {
        TestResult testResult = new();

        int dummyIntData = 2;
        string dummyStringData = "DummyString";
        DataRowAttribute dataRowAttribute = new(
            dummyIntData,
            dummyStringData);

        var attributes = new Attribute[] { dataRowAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        Verify(results.Length == 1);
        Verify(results[0].DisplayName is "dummyTestName (2,\"DummyString\")" or "DummyTestMethod (2,DummyString)", $"Display name: {results[0].DisplayName}");
    }

    public async Task RunTestMethodShouldSetResultFilesIfPresentForDataDrivenTests()
    {
        TestResult testResult = new()
        {
            ResultFiles = ["C:\\temp.txt"],
        };

        int dummyIntData1 = 1;
        int dummyIntData2 = 2;
        DataRowAttribute dataRowAttribute1 = new(dummyIntData1);
        DataRowAttribute dataRowAttribute2 = new(dummyIntData2);

        var attributes = new Attribute[] { dataRowAttribute1, dataRowAttribute2 };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo, It.IsAny<bool>())).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        Verify(results[0].ResultFiles!.Contains("C:\\temp.txt"));
        Verify(results[1].ResultFiles!.Contains("C:\\temp.txt"));
    }

    public async Task RunTestMethodWithEmptyDataSourceShouldFailBecauseConsiderEmptyDataSourceAsInconclusiveIsFalse()
        => await RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(false);

    public async Task RunTestMethodWithEmptyDataSourceShouldNotFailBecauseConsiderEmptyDataSourceAsInconclusiveIsTrue()
        => await RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(true);

    private async Task RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(bool considerEmptyAsInconclusive)
    {
        Mock<IReflectionOperations2>? existingMock = _testablePlatformServiceProvider.MockReflectionOperations;
        try
        {
            // We want this test to go through the "real" reflection to hit the product code path relevant for the test.
            _testablePlatformServiceProvider.MockReflectionOperations = null!;

            string xml =
                $$"""
                <RunSettings>
                    <MSTestV2>
                        <ConsiderEmptyDataSourceAsInconclusive>{{considerEmptyAsInconclusive}}</ConsiderEmptyDataSourceAsInconclusive>
                    </MSTestV2>
                </RunSettings>
                """;

            var settings = MSTestSettings.GetSettings(xml, MSTestSettings.SettingsNameAlias, null);
            MSTestSettings.PopulateSettings(settings!);

            var testMethodInfo = new TestableTestMethodInfo(
                typeof(DummyTestClassEmptyDataSource).GetMethod(nameof(DummyTestClassEmptyDataSource.TestMethod))!,
                GetTestClassInfo<DummyTestClassEmptyDataSource>(),
                _testMethodOptions,
                () => throw ApplicationStateGuard.Unreachable());
            var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

            if (considerEmptyAsInconclusive)
            {
                TestResult[] results = await testMethodRunner.RunTestMethodAsync();
                Verify(results[0].Outcome == UTF.UnitTestOutcome.Inconclusive);
            }
            else
            {
                ArgumentException thrownException = await VerifyThrowsAsync<ArgumentException>(testMethodRunner.RunTestMethodAsync);
                Verify(thrownException.Message == string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataIEnumerableEmpty, nameof(DummyTestClassEmptyDataSource.EmptyProperty), typeof(DummyTestClassEmptyDataSource).FullName));
            }
        }
        finally
        {
            _testablePlatformServiceProvider.MockReflectionOperations = existingMock;
        }
    }

    #region Test data

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Use through reflection")]
    private static void InitMethodThrowingException(TestContext tc)
        // TODO: Fix exception type
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
        => throw new ArgumentException();
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

    private sealed class TestMethodOptions
    {
        public TimeoutInfo TimeoutInfo { get; }

        public ITestContext TestContext { get; }

        public TestMethodAttribute TestMethodAttribute { get; }

        public TestMethodOptions(TimeoutInfo timeoutInfo, ITestContext testContextImplementation, TestMethodAttribute testMethodAttribute)
        {
            TimeoutInfo = timeoutInfo;
            TestContext = testContextImplementation;
            TestMethodAttribute = testMethodAttribute;
        }
    }

    private class TestableTestMethodInfo : TestMethodInfo
    {
        private readonly Func<TestResult> _invokeTest;

        internal TestableTestMethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<TestResult> invoke)
            : base(testMethod, parent, testMethodOptions.TestContext)
        {
            TimeoutInfo = testMethodOptions.TimeoutInfo;
            Executor = testMethodOptions.TestMethodAttribute;
            _invokeTest = invoke;
        }

        public override Task<TestResult> InvokeAsync(object?[]? arguments) =>
            // Ignore args for now
            Task.FromResult(_invokeTest());
    }

    public class DummyTestClassBase
    {
        public static Action<DummyTestClassBase> BaseTestClassMethodBody { get; set; } = null!;

        public void DummyBaseTestClassMethod() => BaseTestClassMethodBody!(this);
    }

    public class DummyTestClass : DummyTestClassBase
    {
        public DummyTestClass() => TestConstructorMethodBody!();

        public static Action TestConstructorMethodBody { get; set; } = null!;

        public static Action<object> TestContextSetterBody { get; set; } = null!;

        public static Action<DummyTestClass> TestInitializeMethodBody { get; set; } = null!;

        public static Action<DummyTestClass> TestMethodBody { get; set; } = null!;

        public static Action<DummyTestClass> TestCleanupMethodBody { get; set; } = null!;

        public static Func<Task> DummyAsyncTestMethodBody { get; set; } = null!;

        public static Action<TestContext> AssemblyInitializeMethodBody { get; set; } = null!;

        public static Action<TestContext> ClassInitializeMethodBody { get; set; } = null!;

        public TestContext TestContext
        {
            get => throw new NotImplementedException();
            set => TestContextSetterBody!(value);
        }

        public static void DummyAssemblyInit(TestContext tc) => AssemblyInitializeMethodBody!(tc);

        public static void DummyClassInit(TestContext tc) => ClassInitializeMethodBody!(tc);

        public void DummyTestInitializeMethod() => TestInitializeMethodBody!(this);

        public void DummyTestCleanupMethod() => TestCleanupMethodBody!(this);

        public void DummyTestMethod() => TestMethodBody!(this);

        [DataSource("DummyConnectionString", "DummyTableName")]
        public void DummyDataSourceTestMethod() => TestMethodBody(this);

        public Task DummyAsyncTestMethod() =>
            // We use this method to validate async TestInitialize, TestCleanup, TestMethod
            DummyAsyncTestMethodBody!();
    }

    public class DummyTestClassWithParameterizedCtor
    {
        public DummyTestClassWithParameterizedCtor(int x)
        {
        }
    }

    public class DummyTestClassWithTestContextWithoutSetter
    {
        public TestContext TestContext => null!;
    }

    public class DummyTestClassEmptyDataSource
    {
        public static IEnumerable<object[]> EmptyProperty => [];

        [DynamicData("EmptyProperty")]
        public void TestMethod(int x)
        {
        }
    }

    #endregion
}

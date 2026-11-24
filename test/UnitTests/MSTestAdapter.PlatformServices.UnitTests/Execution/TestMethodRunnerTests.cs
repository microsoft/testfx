// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting.Resources;

using Moq;

using TestFramework.ForTestingMSTest;

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

        _testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", displayName: null);
        _testContextImplementation = new TestContextImplementation(_testMethod, null, new Dictionary<string, object?>(), null, null);
        _testClassInfo = GetTestClassInfo<DummyTestClass>();

        _testMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), _testMethodAttribute);

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
            MSTestInstrumentation.SetActivityFactory(null);
        }
    }

    public async Task ExecuteForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("DummyException"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        results[0].ExceptionMessage!.StartsWith(
            """
            An unhandled exception was thrown by the 'Execute' method. Please report this error to the author of the attribute 'Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute'.
            System.Exception: DummyException
            """,
            StringComparison.Ordinal).Should().BeTrue();
    }

    public async Task ExecuteForPassingTestShouldReturnTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteShouldNotFillInDebugAndTraceLogsIfDebugTraceDisabled()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results[0].DebugTrace.Should().Be(string.Empty);
    }

    public async Task ExecuteShouldNotFillInDebugAndTraceLogsFromRunningTestMethod()
    {
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () =>
            {
                _testContextImplementation.Write("InTestMethod");
                return new TestResult
                {
                    Outcome = UnitTestOutcome.Passed,
                };
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        results[0].DebugTrace.Should().Be(string.Empty);
    }

    public async Task RunTestMethodForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new Exception("Dummy Exception"));
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        results[0].ExceptionMessage!.StartsWith(
            """
            An unhandled exception was thrown by the 'Execute' method. Please report this error to the author of the attribute 'Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute'.
            System.Exception: Dummy Exception
            """,
            StringComparison.Ordinal).Should().BeTrue();
    }

    private sealed class TestMethodWithFailingAndPassingResultsAttribute : TestMethodAttribute
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
            => Task.FromResult<TestResult[]>(
            [
                new TestResult { Outcome = UnitTestOutcome.Passed },
                new TestResult { Outcome = UnitTestOutcome.Failed },
            ]);
    }

    public async Task RunTestMethodForMultipleResultsReturnMultipleResults()
    {
        var localTestMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), new TestMethodWithFailingAndPassingResultsAttribute());

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, localTestMethodOptions, null!);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results.Length.Should().Be(2);

        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[1].Outcome.Should().Be(UnitTestOutcome.Failed);
    }

    public async Task Execute_RunsInsideLeaseAfterRestoringFixtureExecutionContext()
    {
        var fixtureState = new AsyncLocal<string?> { Value = "fixture" };
        _testClassInfo.ExecutionContext = ExecutionContext.Capture();
        fixtureState.Value = null;

        var lease = new FakeExecutionActivityLease();
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () =>
            {
                fixtureState.Value.Should().Be("fixture");
                lease.IsEntered.Should().BeTrue();
                return new TestResult { Outcome = UnitTestOutcome.Passed };
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation, lease);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        lease.EnterCount.Should().Be(1);
        lease.ExitCount.Should().Be(1);
        lease.IsEntered.Should().BeFalse();
        fixtureState.Value.Should().BeNull();
    }

    public async Task ExecuteForFailingTestShouldRecordReturnedFailureExceptionOnActivity()
    {
        var failureException = new InvalidOperationException("failed");
        var activity = new FakeActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => activity);
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () => new TestResult
            {
                Outcome = UnitTestOutcome.Failed,
                TestFailureException = failureException,
            });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        activity.Tags.Should().Contain(new KeyValuePair<string, object?>("test.case.result.status", "fail"));
        activity.RecordedException.Should().BeSameAs(failureException);
        activity.FailureDescription.Should().BeNull();
    }

    public async Task ExecuteForFailingTestWithoutFailureExceptionShouldSetActivityFailureStatus()
    {
        var activity = new FakeActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => activity);
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () => new TestResult { Outcome = UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        activity.Tags.Should().Contain(new KeyValuePair<string, object?>("test.case.result.status", "fail"));
        activity.RecordedException.Should().BeNull();
        activity.FailureDescription.Should().Be(nameof(UnitTestOutcome.Failed));
    }

    public async Task ExecuteForAbortedTestShouldReportFailStatusOnActivity()
    {
        // Aborted, Unknown and Inconclusive used to fall through to "pass" because they are neither in the
        // failing set nor counted as skipped, which contradicted the outcome the adapter reports.
        var activity = new FakeActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => activity);
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () => new TestResult { Outcome = UnitTestOutcome.Aborted });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        activity.Tags.Should().Contain(new KeyValuePair<string, object?>("test.case.result.status", "fail"));
    }

    public async Task ExecuteForInconclusiveTestShouldReportSkippedStatusOnActivityByDefault()
    {
        var activity = new FakeActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => activity);
        var testMethodInfo = new TestableTestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions,
            () => new TestResult { Outcome = UnitTestOutcome.Inconclusive });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);

        // MapInconclusiveToFailed defaults to false, so the adapter reports skipped and the span must agree.
        activity.Tags.Should().Contain(new KeyValuePair<string, object?>("test.case.result.status", "skipped"));
    }

    public async Task RunTestMethodForPassingTestThrowingExceptionShouldReturnTestResultWithPassedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodForFailingTestThrowingExceptionShouldReturnTestResultWithFailedOutcome()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
    }

    public async Task RunTestMethodShouldGiveTestResultAsPassedWhenTestMethodPasses()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Passed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // Since data is not provided, tests run normally giving passed as outcome.
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodShouldGiveTestResultAsFailedWhenTestMethodFails()
    {
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Failed });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // Since data is not provided, tests run normally giving passed as outcome.
        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
    }

    public async Task RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        DataSourceAttribute dataSourceAttribute = new("DummyConnectionString", "DummyTableName");

        var attributes = new Attribute[] { dataSourceAttribute };

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult { Outcome = UnitTestOutcome.Passed });
        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        // check for outcome
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[1].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[2].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodShouldRunDataDrivenTestsWhenDataIsProvidedUsingDataRowAttribute()
    {
        TestResult testResult = new()
        {
            Outcome = UnitTestOutcome.Inconclusive,
        };

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        results[0].Outcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public async Task RunTestMethodShouldSetDisplayNameForDataDrivenTestsWhenDataIsProvidedUsingDataSourceAttribute()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals(nameof(DummyTestClass.DummyDataSourceTestMethod), StringComparison.Ordinal));
        object[] attributes = methodInfo.GetCustomAttributes(inherit: false);

        // Setup mocks
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        results.Should().HaveCount(3);
        results[0].DisplayName.Should().Be("dummyTestName (Data Row 0)");
        results[1].DisplayName.Should().Be("dummyTestName (Data Row 1)");
        results[2].DisplayName.Should().Be("dummyTestName (Data Row 2)");
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
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _testablePlatformServiceProvider.MockTestDataSource.Setup(tds => tds.GetData(testMethodInfo, _testContextImplementation)).Returns([1, 2, 3]);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        results.Should().HaveCount(3);
        results[0].DisplayName.Should().Be("dummyTestName (Data Row 0)");
        results[1].DisplayName.Should().Be("dummyTestName (Data Row 1)");
        results[2].DisplayName.Should().Be("dummyTestName (Data Row 2)");
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
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        results.Length.Should().Be(1);
        results[0].DisplayName.Should().Be("DataRowTestDisplayName");
    }

    public async Task RunTestMethodArgumentsDisplayNameOverridesSourceNameAndKeepsMethodDisplayName()
    {
        var row = new TestDataRow<string>(string.Empty) { ArgumentsDisplayName = "empty input" };
        var source = new DataRowAttribute(row) { DisplayName = "Source name" };
        _testMethod.DisplayName = "Friendly method";
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(new Attribute[] { source });
        var methodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
        var runner = new TestMethodRunner(methodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().ContainSingle().Which.DisplayName.Should().Be("Friendly method (empty input)");
    }

    public async Task RunTestMethodArgumentsDisplayNamePreservesBlankFullRowNames()
    {
        foreach (string fullName in new[] { string.Empty, " \t" })
        {
            var row = new TestDataRow<string>(string.Empty) { DisplayName = fullName, ArgumentsDisplayName = "empty input" };
            var source = new DataRowAttribute(row) { DisplayName = "Source name" };
            _testablePlatformServiceProvider.MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(new Attribute[] { source });
            var methodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
            var runner = new TestMethodRunner(methodInfo, _testMethod, _testContextImplementation);

            TestResult[] results = await runner.RunTestMethodAsync();

            results.Should().ContainSingle().Which.DisplayName.Should().Be(fullName);
        }
    }

    public async Task RunTestMethodBlankArgumentsDisplayNamesKeepSourceCustomization()
    {
        foreach (string? label in new string?[] { null, string.Empty, " \t" })
        {
            var row = new TestDataRow<string>(string.Empty) { ArgumentsDisplayName = label };
            var source = new DataRowAttribute(row) { DisplayName = "Source name" };
            _testablePlatformServiceProvider.MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(new Attribute[] { source });
            var methodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => new TestResult());
            var runner = new TestMethodRunner(methodInfo, _testMethod, _testContextImplementation);

            TestResult[] results = await runner.RunTestMethodAsync();

            results.Should().ContainSingle().Which.DisplayName.Should().Be("Source name");
        }
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
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();

        results.Length.Should().Be(1);
        results[0].DisplayName.Should().BeOneOf("dummyTestName (2,\"DummyString\")", "DummyTestMethod (2,DummyString)");
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
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => testResult);
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.RunTestMethodAsync();
        results[0].ResultFiles!.Should().Contain("C:\\temp.txt");
        results[1].ResultFiles!.Should().Contain("C:\\temp.txt");
    }

    public async Task RunTestMethodWithEmptyDataSourceShouldFailBecauseConsiderEmptyDataSourceAsInconclusiveIsFalse()
        => await RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(false);

    public async Task RunTestMethodWithEmptyDataSourceShouldNotFailBecauseConsiderEmptyDataSourceAsInconclusiveIsTrue()
        => await RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(true);

    private async Task RunTestMethodWithEmptyDataSourceShouldFailIfConsiderEmptyDataSourceAsInconclusiveIsNotTrueHelper(bool considerEmptyAsInconclusive)
    {
        Mock<IReflectionOperations>? existingMock = _testablePlatformServiceProvider.MockReflectionOperations;
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
            settings.Should().NotBeNull();
            MSTestSettings.PopulateSettings(settings);

            var testMethodInfo = new TestableTestMethodInfo(
                typeof(DummyTestClassEmptyDataSource).GetMethod(nameof(DummyTestClassEmptyDataSource.TestMethod))!,
                GetTestClassInfo<DummyTestClassEmptyDataSource>(),
                _testMethodOptions,
                () => throw ApplicationStateGuard.Unreachable());
            var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

            if (considerEmptyAsInconclusive)
            {
                TestResult[] results = await testMethodRunner.RunTestMethodAsync();
                results[0].Outcome.Should().Be(UnitTestOutcome.Inconclusive);
            }
            else
            {
                Func<Task> func = testMethodRunner.RunTestMethodAsync;
                (await func.Should().ThrowAsync<ArgumentException>())
                    .WithMessage(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataIEnumerableEmpty, nameof(DummyTestClassEmptyDataSource.EmptyProperty), typeof(DummyTestClassEmptyDataSource).FullName));
            }
        }
        finally
        {
            _testablePlatformServiceProvider.MockReflectionOperations = existingMock;
        }
    }

    public async Task RunTestMethodShouldPassWhenAttributeInvokesTestMethodOnExecutionContextUnsafeThread()
    {
        _testablePlatformServiceProvider.MockThreadOperations
            .Setup(tho => tho.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(true)
            .Callback((Action a, int timeout, CancellationToken token) => a.Invoke());

        var localTestMethodOptions = new TestMethodOptions(TimeoutInfo.FromTimeout(200), new ExecutionContextUnsafeThreadTestMethodAttribute());
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo)
        {
            TimeoutInfo = localTestMethodOptions.TimeoutInfo,
            Executor = localTestMethodOptions.TestMethodAttribute,
        };
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        TestResult[] results = await testMethodRunner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty);
        results.Should().HaveCount(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodShouldUseFreshTestContextPerIterationForFoldedDataDrivenTests()
    {
        // Capture TestContext.Current per iteration so we can verify each row gets a distinct
        // TestContextImplementation instance — see https://github.com/microsoft/testfx/issues/7933.
#pragma warning disable MSTESTEXP // TestContext.Current is experimental.
        var observedContexts = new List<TestContext?>();
        DataRowAttribute dataRowAttribute1 = new(1);
        DataRowAttribute dataRowAttribute2 = new(2);
        DataRowAttribute dataRowAttribute3 = new(3);
        var attributes = new Attribute[] { dataRowAttribute1, dataRowAttribute2, dataRowAttribute3 };

        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () =>
        {
            observedContexts.Add(TestContext.Current);
            return new TestResult { Outcome = UnitTestOutcome.Passed };
        });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _ = await testMethodRunner.RunTestMethodAsync();

        observedContexts.Should().HaveCount(3);
        observedContexts.Should().AllSatisfy(c => c.Should().NotBeNull());

        // Each iteration must observe its own fresh context instance — none should be the
        // outer shared context, and no two iterations should share an instance.
        observedContexts.Should().OnlyHaveUniqueItems();
        observedContexts.Should().NotContain(_testContextImplementation);
#pragma warning restore MSTESTEXP
    }

    public async Task RunTestMethodShouldNotLeakCapturedOutputAcrossFoldedDataDrivenIterations()
    {
        // Each iteration writes to its own context's console-out buffer. With a fresh
        // per-iteration context, the iteration that fires write N must observe an empty buffer
        // before writing — without the fix, row 2 would observe row 1's content.
        var observedLengthsBeforeWrite = new List<int>();
        DataRowAttribute dataRowAttribute1 = new(1);
        DataRowAttribute dataRowAttribute2 = new(2);
        DataRowAttribute dataRowAttribute3 = new(3);
        var attributes = new Attribute[] { dataRowAttribute1, dataRowAttribute2, dataRowAttribute3 };

        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(attributes);

        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () =>
        {
#pragma warning disable MSTESTEXP // TestContext.Current is experimental.
            var current = (TestContextImplementation?)TestContext.Current;
#pragma warning restore MSTESTEXP
            current.Should().NotBeNull();
            string? existing = current!.GetAndClearOutput();
            observedLengthsBeforeWrite.Add(existing?.Length ?? 0);

            // Write a fairly large chunk of console output. If contexts were shared, the next
            // iteration would observe a non-zero existing length.
            current.StandardOutputBuilder.Append(new string('x', 1024));
            return new TestResult { Outcome = UnitTestOutcome.Passed };
        });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _ = await testMethodRunner.RunTestMethodAsync();

        observedLengthsBeforeWrite.Should().HaveCount(3);
        observedLengthsBeforeWrite.Should().AllSatisfy(len => len.Should().Be(0));
    }

    public async Task RunTestMethodShouldNotLeakPropertyBagMutationsAcrossFoldedDataDrivenIterations()
    {
        // Each iteration adds a unique property key. If contexts were shared, row N would see
        // the keys added by rows 1..N-1. With a fresh per-iteration context, every row starts
        // with the same baseline property set.
        var observedKeyCounts = new List<int>();
        DataRowAttribute dataRowAttribute1 = new(1);
        DataRowAttribute dataRowAttribute2 = new(2);
        DataRowAttribute dataRowAttribute3 = new(3);
        var attributes = new Attribute[] { dataRowAttribute1, dataRowAttribute2, dataRowAttribute3 };

        _testablePlatformServiceProvider.MockReflectionOperations.Setup(ro => ro.GetCustomAttributes(_methodInfo)).Returns(attributes);

        int iteration = 0;
        var testMethodInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () =>
        {
#pragma warning disable MSTESTEXP // TestContext.Current is experimental.
            TestContext? current = TestContext.Current;
#pragma warning restore MSTESTEXP
            current.Should().NotBeNull();
            observedKeyCounts.Add(current!.Properties.Count);
            current.Properties[$"AddedByIteration_{iteration++}"] = "value";
            return new TestResult { Outcome = UnitTestOutcome.Passed };
        });
        var testMethodRunner = new TestMethodRunner(testMethodInfo, _testMethod, _testContextImplementation);

        _ = await testMethodRunner.RunTestMethodAsync();

        // All three iterations observe the same key count (no leak from prior iterations) and
        // the outer context is unchanged.
        observedKeyCounts.Should().HaveCount(3);
        observedKeyCounts.Should().AllSatisfy(c => c.Should().Be(observedKeyCounts[0]));
        _testContextImplementation.Properties.Should().NotContainKey("AddedByIteration_0");
        _testContextImplementation.Properties.Should().NotContainKey("AddedByIteration_1");
        _testContextImplementation.Properties.Should().NotContainKey("AddedByIteration_2");
    }

    #region Test data

    private sealed class FakeActivity : IMSTestActivity
    {
        public List<KeyValuePair<string, object?>> Tags { get; } = [];

        public string? FailureDescription { get; private set; }

        public Exception? RecordedException { get; private set; }

        public void SetTag(string key, object? value)
            => Tags.Add(new KeyValuePair<string, object?>(key, value));

        public void SetFailed(string? description)
            => FailureDescription = description;

        public void RecordException(Exception exception)
            => RecordedException = exception;

        public void Dispose()
        {
        }
    }

    private sealed class FakeExecutionActivityLease : ITestExecutionActivityLease
    {
        private readonly AsyncLocal<bool> _isEntered = new();

        public bool IsEntered => _isEntered.Value;

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public async Task<T> RunAsync<T>(Func<Task<T>> callback)
        {
            EnterCount++;
            _isEntered.Value = true;
            try
            {
                return await callback();
            }
            finally
            {
                ExitCount++;
                _isEntered.Value = false;
            }
        }
    }

    private sealed class ExecutionContextUnsafeThreadTestMethodAttribute : TestMethodAttribute
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            var taskCompletionSource = new TaskCompletionSource<TestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.UnsafeQueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        taskCompletionSource.SetResult(testMethod.InvokeAsync(null).ConfigureAwait(false).GetAwaiter().GetResult());
                    }
                    catch (Exception exception)
                    {
                        taskCompletionSource.SetException(exception);
                    }
                },
                null);

            Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(WaitTimeout)).ConfigureAwait(false);
            return completedTask == taskCompletionSource.Task
                ? [await taskCompletionSource.Task.ConfigureAwait(false)]
                : throw new TimeoutException($"The execution did not complete within {WaitTimeout}.");
        }
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Use through reflection")]
    private static void InitMethodThrowingException(TestContext tc)
        // TODO: Fix exception type (tracked by https://github.com/microsoft/testfx/issues/8086)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
        => throw new ArgumentException();
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

    private sealed class TestMethodOptions
    {
        public TimeoutInfo TimeoutInfo { get; }

        public TestMethodAttribute TestMethodAttribute { get; }

        public TestMethodOptions(TimeoutInfo timeoutInfo, TestMethodAttribute testMethodAttribute)
        {
            TimeoutInfo = timeoutInfo;
            TestMethodAttribute = testMethodAttribute;
        }
    }

    private class TestableTestMethodInfo : TestMethodInfo
    {
        private readonly Func<TestResult> _invokeTest;

        internal TestableTestMethodInfo(MethodInfo testMethod, TestClassInfo parent, TestMethodOptions testMethodOptions, Func<TestResult> invoke)
            : base(testMethod, parent)
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

    public async Task AggregateActivityStatusHonorsMappingsAndSelectsTheFirstFailure()
    {
        (UnitTestOutcome[] Outcomes, bool MapInconclusive, bool MapNotRunnable, string Status, string? FirstFailure)[] cases =
        [
            ([UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive, UnitTestOutcome.Ignored], false, false, "pass", null),
            ([UnitTestOutcome.Ignored, UnitTestOutcome.Inconclusive, UnitTestOutcome.NotRunnable], false, false, "skipped", null),
            ([UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive], true, false, "fail", "Inconclusive"),
            ([UnitTestOutcome.Passed, UnitTestOutcome.NotRunnable], false, true, "fail", "NotRunnable"),
            ([UnitTestOutcome.Passed, UnitTestOutcome.Unknown, UnitTestOutcome.Timeout], false, false, "fail", "Unknown"),
        ];
        foreach ((UnitTestOutcome[] Outcomes, bool MapInconclusive, bool MapNotRunnable, string Status, string? FirstFailure) item in cases)
        {
            MSTestSettings.PopulateSettings(
                $"<RunSettings><MSTest><MapInconclusiveToFailed>{item.MapInconclusive.ToString().ToLowerInvariant()}</MapInconclusiveToFailed><MapNotRunnableToFailed>{item.MapNotRunnable.ToString().ToLowerInvariant()}</MapNotRunnableToFailed></MSTest></RunSettings>", null, null);
            var activity = new ContinuationActivity();
            MSTestInstrumentation.SetActivityFactory((_, _) => activity);
            TestResult[] expected = item.Outcomes.Select(outcome => new TestResult { Outcome = outcome, Duration = TimeSpan.FromSeconds(7) }).ToArray();
            var executor = new ContinuationExecutor(() => expected);
            var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, new TestMethodOptions(TimeoutInfo.FromTimeout(0), executor), null!);

            TestResult[] actual = await new TestMethodRunner(info, _testMethod, _testContextImplementation).ExecuteAsync("prefix", null, null, null);

            actual.Select(result => result.Outcome).Should().Equal(item.Outcomes);
            actual.Select(result => result.Duration).Should().OnlyContain(duration => duration == TimeSpan.FromSeconds(7));
            activity.Tags["test.case.result.status"].Should().Be(item.Status);
            activity.Tags["test.case.data_row.count"].Should().Be(expected.Length);
            activity.Exceptions.Should().BeEmpty();
            activity.FailureDescription.Should().Be(item.FirstFailure);
            executor.Calls.Should().Be(1);
            actual[0].LogOutput.Should().Be("prefix");
        }

        var firstFailure = new InvalidOperationException("first failure");
        var laterFailure = new InvalidOperationException("later failure");
        var failedActivity = new ContinuationActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => failedActivity);
        var failedExecutor = new ContinuationExecutor(() =>
        [
            new() { Outcome = UnitTestOutcome.Passed },
            new() { Outcome = UnitTestOutcome.Failed, TestFailureException = firstFailure },
            new() { Outcome = UnitTestOutcome.Timeout, TestFailureException = laterFailure },
        ]);
        var failedInfo = new TestableTestMethodInfo(_methodInfo, _testClassInfo, new TestMethodOptions(TimeoutInfo.FromTimeout(0), failedExecutor), null!);
        await new TestMethodRunner(failedInfo, _testMethod, _testContextImplementation).ExecuteAsync(null, null, null, null);
        failedActivity.Exceptions.Should().Equal(firstFailure);
        failedActivity.Tags["test.case.result.status"].Should().Be("fail");
    }

    public async Task UnexpectedExecutorStateProducesOneErrorAndOneRecordedException()
    {
        var activity = new ContinuationActivity();
        MSTestInstrumentation.SetActivityFactory((_, _) => activity);
        var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, null!) { Executor = null! };

        TestResult[] results = await new TestMethodRunner(info, _testMethod, _testContextImplementation)
            .ExecuteAsync("out", "error", "trace", "messages");

        results.Should().ContainSingle();
        results[0].Outcome.Should().Be(UnitTestOutcome.Error);
        results[0].ExceptionMessage.Should().Contain("TestMethodRunner.cs");
        results[0].LogOutput.Should().Be("out");
        results[0].LogError.Should().Be("error");
        results[0].DebugTrace.Should().Be("trace");
        results[0].TestContextMessages.Should().Be("messages");
        activity.Exceptions.Should().ContainSingle();
        activity.Tags["test.case.result.status"].Should().Be("fail");
        activity.Tags["test.case.data_row.count"].Should().Be(1);
    }

    public async Task EmptyExecutorResultsBecomeTheDocumentedUnknownSentinel()
    {
        var executor = new ContinuationExecutor(() => []);
        var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, new TestMethodOptions(TimeoutInfo.FromTimeout(0), executor), null!);

        TestResult[] results = await new TestMethodRunner(info, _testMethod, _testContextImplementation).RunTestMethodAsync();

        results.Should().ContainSingle();
        results[0].Outcome.Should().Be(UnitTestOutcome.Unknown);
        results[0].ExceptionMessage.Should().Be(Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource.UTA_NoTestResult);
        executor.Calls.Should().Be(1);
    }

    public async Task DuplicateLegacyDataSourcesFallBackWithoutOpeningDataConnections()
    {
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(_methodInfo))
            .Returns([new DataSourceAttribute("first", "table"), new DataSourceAttribute("second", "table")]);
        var executor = new ContinuationExecutor(() => [new TestResult { Outcome = UnitTestOutcome.Passed }]);
        var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, new TestMethodOptions(TimeoutInfo.FromTimeout(0), executor), null!);

        TestResult[] results = await new TestMethodRunner(info, _testMethod, _testContextImplementation).RunTestMethodAsync();

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        executor.Calls.Should().Be(1);
        _testablePlatformServiceProvider.MockTestDataSource.Verify(value => value.GetData(info, _testContextImplementation), Times.Never);
    }

    public async Task LegacyDataSourceNullAndFailureHaveDistinctOutcomesAndOriginalCause()
    {
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(_methodInfo))
            .Returns([new DataSourceAttribute("connection", "table")]);
        var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions, () => throw new InvalidOperationException("no row should execute"));
        _testablePlatformServiceProvider.MockTestDataSource.Setup(value => value.GetData(info, _testContextImplementation))
            .Returns((IEnumerable<object>?)null);
        TestResult[] absent = await new TestMethodRunner(info, _testMethod, _testContextImplementation).RunTestMethodAsync();
        absent.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Inconclusive);
        var cause = new InvalidOperationException("legacy source failed");
        _testablePlatformServiceProvider.MockTestDataSource.Setup(value => value.GetData(info, _testContextImplementation)).Throws(cause);

        TestResult[] failed = await new TestMethodRunner(info, _testMethod, _testContextImplementation).RunTestMethodAsync();

        failed.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Error);
        failed[0].TestFailureException.Should().BeSameAs(cause);
        failed[0].ExecutionId.Should().NotBe(Guid.Empty);
        failed[0].ParentExecId.Should().NotBe(Guid.Empty);
    }

    public async Task ExecuteExpectedDuration_UsesStrictReportedDurationBoundary()
    {
        (int Budget, string Warning)[] cases =
        [
            (100, "Test 'dummyClassName.dummyTestName (above budget)' took 100.0001 ms, exceeding its expected maximum duration of 100 ms."),
            (1, "Test 'dummyClassName.dummyTestName (above budget)' took 1.0001 ms, exceeding its expected maximum duration of 1 ms."),
        ];
        foreach ((int budget, string warning) in cases)
        {
            long budgetTicks = budget * TimeSpan.TicksPerMillisecond;
            TestResult[] reported =
            [
                new() { Outcome = UnitTestOutcome.Passed, DisplayName = "below budget", Duration = TimeSpan.FromTicks(budgetTicks - 1), TestContextMessages = "below output" },
                new() { Outcome = UnitTestOutcome.Passed, DisplayName = "at budget", Duration = TimeSpan.FromTicks(budgetTicks), TestContextMessages = "equal output" },
                new() { Outcome = UnitTestOutcome.Passed, DisplayName = "above budget", Duration = TimeSpan.FromTicks(budgetTicks + 1) },
            ];

            (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(reported, budget: budget);

            messages.Should().Equal((MessageLevel.Warning, warning));
            results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Passed, UnitTestOutcome.Passed);
            results.Select(result => result.Duration).Should().Equal(TimeSpan.FromTicks(budgetTicks - 1), TimeSpan.FromTicks(budgetTicks), TimeSpan.FromTicks(budgetTicks + 1));
            results.Select(result => result.DisplayName).Should().Equal("below budget", "at budget", "above budget");
            results[0].TestContextMessages.Should().Be("below output");
            results[1].TestContextMessages.Should().Be("equal output");
            NormalizeExpectedDurationOutput(results[2].TestContextMessages!).Should().Be(
                NormalizeExpectedDurationOutput(
                    $"""
                    {warning}

                    """));
        }
    }

    public async Task ExecuteExpectedDuration_WithoutAttributeDoesNotWarnOrChangeOutput()
    {
        TestResult[] reported =
        [
            new() { Outcome = UnitTestOutcome.Passed, Duration = TimeSpan.FromSeconds(1), TestContextMessages = "unannotated output" },
        ];

        (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(reported, optIn: false);

        messages.Should().BeEmpty();
        results.Should().ContainSingle();
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[0].Duration.Should().Be(TimeSpan.FromSeconds(1));
        results[0].TestContextMessages.Should().Be("unannotated output");
    }

    public async Task ExecuteExpectedDuration_PreservesOutcomesExceptionsAndAuthoredOutput()
    {
        var failure = new InvalidOperationException("original failure");
        var passedId = new Guid("11111111-1111-1111-1111-111111111111");
        var failedId = new Guid("22222222-2222-2222-2222-222222222222");
        var parentId = new Guid("33333333-3333-3333-3333-333333333333");
        TestResult[] reported =
        [
            new()
            {
                Outcome = UnitTestOutcome.Passed,
                DisplayName = "dummyTestName",
                Duration = TimeSpan.FromTicks(1_501_250),
                TestContextMessages = "authored pass",
                LogOutput = "passing stdout",
                LogError = "passing stderr",
                DebugTrace = "passing trace",
                ExecutionId = passedId,
                ParentExecId = parentId,
                RetryAttemptNumber = 2,
            },
            new()
            {
                Outcome = UnitTestOutcome.Failed,
                DisplayName = "failure row",
                Duration = TimeSpan.FromTicks(2_005_000),
                TestFailureException = failure,
                TestContextMessages = """
                    authored failure

                    """,
                LogOutput = "failing stdout",
                LogError = "failing stderr",
                DebugTrace = "failing trace",
                ExecutionId = failedId,
                ParentExecId = parentId,
                RetryAttemptNumber = 3,
            },
        ];

        (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(
            reported, initializationTestContextMessages: "fixture:");

        const string PassedWarning = "Test 'dummyClassName.dummyTestName' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";
        const string FailedWarning = "Test 'dummyClassName.dummyTestName (failure row)' took 200.5000 ms, exceeding its expected maximum duration of 100 ms.";
        messages.Should().Equal((MessageLevel.Warning, PassedWarning), (MessageLevel.Warning, FailedWarning));
        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Failed);
        results[0].TestFailureException.Should().BeNull();
        results[1].TestFailureException.Should().BeSameAs(failure);
        results[1].ExceptionMessage.Should().Be("original failure");
        results.Select(result => result.Duration).Should().Equal(TimeSpan.FromTicks(1_501_250), TimeSpan.FromTicks(2_005_000));
        results.Select(result => result.DisplayName).Should().Equal("dummyTestName", "failure row");
        results.Select(result => result.ExecutionId).Should().Equal(passedId, failedId);
        results.Select(result => result.ParentExecId).Should().Equal(parentId, parentId);
        results.Select(result => result.RetryAttemptNumber).Should().Equal(2, 3);
        results.Select(result => result.IsSupersededRetryAttempt).Should().Equal(false, false);
        results.Select(result => result.LogOutput).Should().Equal("passing stdout", "failing stdout");
        results.Select(result => result.LogError).Should().Equal("passing stderr", "failing stderr");
        results.Select(result => result.DebugTrace).Should().Equal("passing trace", "failing trace");
        NormalizeExpectedDurationOutput(results[0].TestContextMessages!).Should().Be(
            NormalizeExpectedDurationOutput(
                $"""
                fixture:authored pass
                {PassedWarning}

                """));
        NormalizeExpectedDurationOutput(results[1].TestContextMessages!).Should().Be(
            NormalizeExpectedDurationOutput(
                $"""
                authored failure

                {FailedWarning}

                """));
    }

    public async Task ExecuteExpectedDuration_DoesNotWarnForSkippedTimeoutOrInfrastructureResults()
    {
        UnitTestOutcome[] outcomes =
        [
            UnitTestOutcome.Inconclusive,
            UnitTestOutcome.InProgress,
            UnitTestOutcome.Error,
            UnitTestOutcome.Timeout,
            UnitTestOutcome.Aborted,
            UnitTestOutcome.Unknown,
            UnitTestOutcome.NotRunnable,
            UnitTestOutcome.NotFound,
            UnitTestOutcome.Ignored,
        ];
        TestResult[] reported = outcomes.Select(outcome => new TestResult
        {
            Outcome = outcome,
            Duration = TimeSpan.FromMilliseconds(250),
            DisplayName = outcome.ToString(),
            TestContextMessages = $"original {outcome}",
        }).ToArray();

        (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(reported);

        messages.Should().BeEmpty();
        results.Select(result => result.Outcome).Should().Equal(outcomes);
        results.Select(result => result.TestContextMessages).Should().Equal(outcomes.Select(outcome => $"original {outcome}"));
        results.Select(result => result.Duration).Should().OnlyContain(duration => duration == TimeSpan.FromMilliseconds(250));
    }

    public async Task ExecuteExpectedDuration_DoesNotWarnForSupersededAttempts()
    {
        foreach (long finalTicks in new[] { 500_000L, 1_501_250L })
        {
            TestResult[] reported =
            [
                new()
                {
                    Outcome = UnitTestOutcome.Failed,
                    Duration = TimeSpan.FromMilliseconds(250),
                    TestContextMessages = "superseded output",
                    RetryAttemptNumber = 1,
                    IsSupersededRetryAttempt = true,
                },
                new()
                {
                    Outcome = UnitTestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(250),
                    TestContextMessages = "superseded passing output",
                    RetryAttemptNumber = 1,
                    IsSupersededRetryAttempt = true,
                },
                new()
                {
                    Outcome = UnitTestOutcome.Passed,
                    Duration = TimeSpan.FromTicks(finalTicks),
                    TestContextMessages = "final output",
                    RetryAttemptNumber = 2,
                },
            ];

            (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(reported);

            results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Failed, UnitTestOutcome.Passed, UnitTestOutcome.Passed);
            results.Select(result => result.Duration).Should().Equal(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromTicks(finalTicks));
            results.Select(result => result.RetryAttemptNumber).Should().Equal(1, 1, 2);
            results.Select(result => result.IsSupersededRetryAttempt).Should().Equal(true, true, false);
            results[0].TestContextMessages.Should().Be("superseded output");
            results[1].TestContextMessages.Should().Be("superseded passing output");
            if (finalTicks == 500_000)
            {
                messages.Should().BeEmpty();
                results[2].TestContextMessages.Should().Be("final output");
            }
            else
            {
                const string Warning = "Test 'dummyClassName.dummyTestName' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";
                messages.Should().Equal((MessageLevel.Warning, Warning));
                NormalizeExpectedDurationOutput(results[2].TestContextMessages!).Should().Be(
                    NormalizeExpectedDurationOutput(
                        $"""
                        final output
                        {Warning}

                        """));
            }
        }
    }

    public async Task ExecuteExpectedDuration_FormatsFractionalMillisecondsWithCurrentCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            TestResult[] reported = [new() { Outcome = UnitTestOutcome.Passed, Duration = TimeSpan.FromTicks(1_234_560) }];

            (TestResult[] results, (MessageLevel Level, string Message)[] messages) = await ExecuteWithExpectedDurationAsync(reported, cultureName: "fr-FR");

            const string Warning = "Test 'dummyClassName.dummyTestName' took 123,4560 ms, exceeding its expected maximum duration of 100 ms.";
            messages.Should().Equal((MessageLevel.Warning, Warning));
            NormalizeExpectedDurationOutput(results[0].TestContextMessages!).Should().Be(
                NormalizeExpectedDurationOutput(
                    """
                    Test 'dummyClassName.dummyTestName' took 123,4560 ms, exceeding its expected maximum duration of 100 ms.

                    """));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private async Task<(TestResult[] Results, (MessageLevel Level, string Message)[] Messages)> ExecuteWithExpectedDurationAsync(
        TestResult[] reported,
        bool optIn = true,
        string? initializationTestContextMessages = null,
        int budget = 100,
        string cultureName = "en-US")
    {
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(operations => operations.GetCustomAttributes(_methodInfo))
            .Returns(optIn ? [new ExpectedDurationAttribute(budget)] : []);
        var executor = new ContinuationExecutor(() => reported);
        var info = new TestableTestMethodInfo(_methodInfo, _testClassInfo, new TestMethodOptions(TimeoutInfo.FromTimeout(0), executor), null!);
        List<(MessageLevel Level, string Message)> messages = [];
        var logger = new Mock<IAdapterMessageLogger>();
        logger.Setup(value => value.SendMessage(It.IsAny<MessageLevel>(), It.IsAny<string>()))
            .Callback<MessageLevel, string>((level, message) => messages.Add((level, message)));
        using var context = new TestContextImplementation(_testMethod, null, new Dictionary<string, object?>(), logger.Object, null);
        string?[] originalContextMessages = reported.Select(result => result.TestContextMessages).ToArray();
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var runner = new TestMethodRunner(info, _testMethod, context);
            TestResult[] results = await runner.ExecuteAsync(null, null, null, initializationTestContextMessages);

            messages.Should().BeEmpty("duration warnings must wait until final retry results are selected");
            results.Select(result => result.TestContextMessages).Should().Equal(
                originalContextMessages.Select((message, index) => index == 0 ? initializationTestContextMessages + message : message));

            // These deterministic results already carry their final-attempt tags.
            // Packaged [Retry] cases protect the real UnitTestRunner orchestration.
            runner.ReportExpectedDurationWarnings(results);
            return (results, messages.ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static string NormalizeExpectedDurationOutput(string output) => output.Replace("\r\n", "\n");

    private sealed class ContinuationExecutor(Func<TestResult[]> execute) : TestMethodAttribute
    {
        internal int Calls { get; private set; }

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            Calls++;
            return Task.FromResult(execute());
        }
    }

    private sealed class ContinuationActivity : IMSTestActivity
    {
        internal Dictionary<string, object?> Tags { get; } = [with(StringComparer.Ordinal)];

        internal List<Exception> Exceptions { get; } = [];

        internal string? FailureDescription { get; private set; }

        public void SetTag(string key, object? value) => Tags[key] = value;

        public void RecordException(Exception exception) => Exceptions.Add(exception);

        public void SetFailed(string? description) => FailureDescription = description;

        public void Dispose()
        {
        }
    }
}

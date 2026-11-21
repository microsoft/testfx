// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestDataSource = Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodRunnerMutationTests : TestContainer
{
#if !WINDOWS_UWP && !WIN_UI
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";

    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
#endif

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly TestContextImplementation _testContextImplementation;
    private readonly TestClassInfo _testClassInfo;

    public TestMethodRunnerMutationTests()
    {
#if !WINDOWS_UWP && !WIN_UI
        Environment.SetEnvironmentVariable(RetainVariable, null);
#endif

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _testContextImplementation = new TestContextImplementation(DefaultTestMethod(nameof(RunnerSubject.NoArgs)), null, new Dictionary<string, object?>(), null, null);
        _testClassInfo = new TestClassInfo(
            typeof(RunnerSubject),
            typeof(RunnerSubject).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(RunnerSubject).Assembly));
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
            MSTestInstrumentation.SetActivityFactory(null);
#if !WINDOWS_UWP && !WIN_UI
            Environment.SetEnvironmentVariable(RetainVariable, _previousRetain);
#endif
        }
    }

#if DEBUG && !NETFRAMEWORK
    public void Constructor_AssertsThatEveryDependencyIsProvided()
    {
        // The observer must be installed before the violating call: a failed Debug.Assert without observer ends the test host.
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new TestMethodRunner(null!, null!, null!);

        assertions.Should().Equal(
            "testMethodInfo should not be null",
            "testMethod should not be null",
            "testContext should not be null");
    }

    public async Task RunTestMethodAsync_AssertsThatTheTestIsProvided()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var runner = new TestMethodRunner(
            CreateTestMethodInfo(nameof(RunnerSubject.NoArgs), [], new DelegateExecutor(_ => Passed())),
            null!,
            _testContextImplementation,
            executionActivityLease: null);
        assertions.Clear();

        Func<Task> run = () => runner.RunTestMethodAsync();
        await run.Should().ThrowAsync<NullReferenceException>();

        assertions.Should().Equal("Test should not be null.");
    }

    public async Task RunTestMethodAsync_AssertsThatTheTestMethodIsProvided()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var testMethodInfo = (TestMethodInfo)RuntimeHelpers.GetUninitializedObject(typeof(TestMethodInfo));
        testMethodInfo.Executor = new DelegateExecutor(_ => Passed());
        var runner = new TestMethodRunner(
            testMethodInfo,
            DataDrivenTestMethod(nameof(RunnerSubject.NoArgs), ignoreMessage: "ignored"),
            _testContextImplementation,
            executionActivityLease: null);

        TestResult[] results = await runner.RunTestMethodAsync();

        assertions.Should().Equal("Test method should not be null.");
        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Ignored);
    }
#endif

    public async Task ExecuteAsync_CountsEveryRunOnTheTestContext()
    {
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), [], _ => Passed());

        await runner.ExecuteAsync(null, null, null, null);
        int countAfterFirstRun = _testContextImplementation.Context.TestRunCount;
        await runner.ExecuteAsync(null, null, null, null);
        int countAfterSecondRun = _testContextImplementation.Context.TestRunCount;

        countAfterFirstRun.Should().Be(1);
        countAfterSecondRun.Should().Be(2);
    }

    public async Task ExecuteAsync_StartsAnActivityNamedAfterTheDisplayNameOfTheTest()
    {
        using var recorder = new ActivityRecorder();
        MSTestInstrumentation.SetActivityFactory(recorder.Start);
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            [],
            _ => Passed(),
            DefaultTestMethod(nameof(RunnerSubject.NoArgs), "custom display"));

        await runner.ExecuteAsync(null, null, null, null);

        recorder.StartedNames.Should().Equal("MSTest.TestMethod");
        recorder.StartedTags.Should().Equal(
            "test.case.name=custom display",
            $"test.suite.name={typeof(RunnerSubject).FullName}",
            "test.assembly.name=dummyAssemblyName");
    }

    public async Task ExecuteAsync_DoesNotStartAnActivityWhenAnExecutionLeaseOwnsTheRun()
    {
        using var recorder = new ActivityRecorder();
        MSTestInstrumentation.SetActivityFactory(recorder.Start);
        var lease = new FakeExecutionActivityLease();
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), [], _ => Passed(), lease: lease);

        await runner.ExecuteAsync(null, null, null, null);

        recorder.StartedNames.Should().BeEmpty();
        lease.EnterCount.Should().Be(1);
    }

    public async Task ExecuteAsync_ReportsAnErrorResultWhenTheRunFailsAndNoActivityIsRunning()
    {
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), [], execute: null);

        TestResult[] results = await runner.ExecuteAsync(null, null, null, null);

        TestResult result = results.Should().ContainSingle().Which;
        result.Outcome.Should().Be(UnitTestOutcome.Error);
        TestFailedException failure = result.TestFailureException.Should().BeAssignableTo<TestFailedException>().Subject;
        failure.Outcome.Should().Be(UnitTestOutcome.Error);
    }

    public async Task ExecuteAsync_RecordsTheOriginalExceptionOnTheActivityWhenTheRunFails()
    {
        using var recorder = new ActivityRecorder();
        MSTestInstrumentation.SetActivityFactory(recorder.Start);
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), [], execute: null);

        TestResult[] results = await runner.ExecuteAsync(null, null, null, null);

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Error);
        recorder.Activity.Exceptions.Should().ContainSingle().Which.GetType().Name.Should().Be("UnreachableException");
        recorder.Activity.Failures.Should().BeEmpty();
        Format(recorder.Activity.Tags).Should().Equal("test.case.data_row.count=1", "test.case.result.status=fail");
    }

    public async Task RunTestMethodAsync_WithoutDataSources_ExposesTheDisplayNameAndAggregatesTheOutcomes()
    {
        string? displayNameSeenByTheTest = null;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            [],
            info =>
            {
                displayNameSeenByTheTest = info.TestContext.Context.TestDisplayName;
                return Results(UnitTestOutcome.Passed, UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive);
            },
            DefaultTestMethod(nameof(RunnerSubject.NoArgs), "custom display"));

        TestResult[] results = await runner.RunTestMethodAsync();

        displayNameSeenByTheTest.Should().Be("custom display");
        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive);
        _testContextImplementation.Context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public async Task RunTestMethodAsync_WithoutDataSources_BackfillsTheMissingDisplayNames()
    {
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            [],
            _ => ResultsWithDisplayNames(null, "   ", "from executor"),
            DefaultTestMethod(nameof(RunnerSubject.NoArgs), "custom display"));

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Select(result => result.DisplayName).Should().Equal("custom display", "custom display", "from executor");
    }

    public async Task RunTestMethodAsync_WithSeveralLegacyDataSources_ExposesTheDisplayNameAndAggregatesTheOutcomes()
    {
        string? displayNameSeenByTheTest = null;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            TwoLegacyDataSources(),
            info =>
            {
                displayNameSeenByTheTest = info.TestContext.Context.TestDisplayName;
                return Results(UnitTestOutcome.Passed, UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive);
            },
            DefaultTestMethod(nameof(RunnerSubject.NoArgs), "custom display"));

        TestResult[] results = await runner.RunTestMethodAsync();

        displayNameSeenByTheTest.Should().Be("custom display");
        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive);
        results.Select(result => result.ExecutionId).Should().OnlyContain(id => id == Guid.Empty);
        results.Select(result => result.ParentExecId).Should().OnlyContain(id => id == Guid.Empty);
        _testContextImplementation.Context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public async Task RunTestMethodAsync_WithSeveralLegacyDataSources_BackfillsTheMissingDisplayNames()
    {
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            TwoLegacyDataSources(),
            _ => ResultsWithDisplayNames(null, "   ", "from executor"),
            DefaultTestMethod(nameof(RunnerSubject.NoArgs), "custom display"));

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Select(result => result.DisplayName).Should().Equal("custom display", "custom display", "from executor");
    }

    public async Task RunTestMethodAsync_WithFoldedDataRows_GivesEveryResultItsOwnExecutionIds()
    {
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.OneInt), [new RowsAttribute([[1], [2]])], _ => Passed());

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().HaveCount(2);
        results.Select(result => result.ExecutionId).Should().OnlyContain(id => id != Guid.Empty).And.OnlyHaveUniqueItems();
        results.Select(result => result.ParentExecId).Should().OnlyContain(id => id != Guid.Empty);
    }

    public async Task RunTestMethodAsync_WithAnIgnoredDataSource_DoesNotRunTheTest()
    {
        int executions = 0;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.OneInt),
            [],
            _ =>
            {
                executions++;
                return Passed();
            },
            DataDrivenTestMethod(nameof(RunnerSubject.OneInt), ignoreMessage: "ignored by source"));

        TestResult[] results = await runner.RunTestMethodAsync();

        executions.Should().Be(0);
        TestResult result = results.Should().ContainSingle().Which;
        result.Outcome.Should().Be(UnitTestOutcome.Ignored);
        result.IgnoreReason.Should().Be("ignored by source");
        _testContextImplementation.Context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Ignored);
    }

    public async Task RunTestMethodAsync_WithDiscoveredData_PrefersTheActualDataOverTheSerializedData()
    {
        object?[]? arguments = null;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.OneInt),
            [],
            info =>
            {
                arguments = info.Arguments;
                return Passed();
            },
            DataDrivenTestMethod(nameof(RunnerSubject.OneInt), actualData: [1], serializedData: DataSerializationHelper.Serialize([2])));

        await runner.RunTestMethodAsync();

        arguments.Should().Equal(1);
    }

    public async Task RunTestMethodAsync_WithDiscoveredSerializedData_DeserializesTheArguments()
    {
        object?[]? arguments = null;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.OneInt),
            [],
            info =>
            {
                arguments = info.Arguments;
                return Passed();
            },
            DataDrivenTestMethod(nameof(RunnerSubject.OneInt), serializedData: DataSerializationHelper.Serialize([2])));

        await runner.RunTestMethodAsync();

        arguments.Should().Equal(2);
    }

    public async Task RunTestMethodAsync_WithDiscoveredData_ReturnsEveryResultOfTheTest()
    {
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.OneInt),
            [],
            _ => Results(UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive),
            DataDrivenTestMethod(nameof(RunnerSubject.OneInt), actualData: [1]));

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Inconclusive);
        results.Select(result => result.ExecutionId).Should().OnlyContain(id => id == Guid.Empty);
        _testContextImplementation.Context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public async Task RunTestMethodAsync_WhenTheTestReturnsNoResult_ReportsAnUnknownResult()
    {
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), TwoLegacyDataSources(), _ => []);

        TestResult[] results = await runner.RunTestMethodAsync();

        TestResult result = results.Should().ContainSingle().Which;
        result.Outcome.Should().Be(UnitTestOutcome.Unknown);
        TestFailedException failure = result.TestFailureException.Should().BeAssignableTo<TestFailedException>().Subject;
        failure.Outcome.Should().Be(UnitTestOutcome.Error);
        failure.Message.Should().Be(Resource.UTA_NoTestResult);
    }

    public async Task RunTestMethodAsync_WithoutDataSources_ScansTheAttributesOnlyOnce()
    {
        MethodInfo method = typeof(RunnerSubject).GetMethod(nameof(RunnerSubject.NoArgs))!;
        TestMethodRunner runner = CreateRunner(nameof(RunnerSubject.NoArgs), [], _ => Passed());
        _testablePlatformServiceProvider.MockReflectionOperations.Invocations.Clear();

        await runner.RunTestMethodAsync();

        _testablePlatformServiceProvider.MockReflectionOperations.Verify(rf => rf.GetCustomAttributes(method), Times.Once());
    }

#if !WINDOWS_UWP && !WIN_UI
    public async Task RunTestMethodAsync_WithSeveralLegacyDataSources_AttachesTheCapturedDiagnosticsToTheFailedResult()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            TwoLegacyDataSources(),
            _ =>
            {
                TestContextImplementation.CaptureAssertionFailureDiagnostics("failure", "expected", "actual");
                return Failed();
            },
            testContext: outerContext);

        TestResult[] results = await runner.RunTestMethodAsync();

        IList<string>? files = results.Should().ContainSingle().Which.ResultFiles;
        files.Should().ContainSingle().Which.Should().EndWith(".json");
        File.Exists(files![0]).Should().BeTrue();
    }

    public async Task RunTestMethodAsync_WithoutDataSources_ResetsTheCaptureBudgetOnceTheResultsAreFinalized()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        int captures = 5;
        TestMethodRunner runner = CreateRunner(
            nameof(RunnerSubject.NoArgs),
            [],
            _ =>
            {
                for (int i = 0; i < captures; i++)
                {
                    TestContextImplementation.CaptureAssertionFailureDiagnostics("failure", "expected", "actual");
                }

                return Failed();
            },
            testContext: outerContext);

        TestResult[] firstRun = await runner.RunTestMethodAsync();
        captures = 1;
        outerContext.Context.TestRunCount = 2;
        TestResult[] secondRun = await runner.RunTestMethodAsync();

        // The budget caps the captures of one attempt, so the first run keeps fewer files than it captured.
        IList<string>? firstFiles = firstRun.Should().ContainSingle().Which.ResultFiles;
        firstFiles.Should().NotBeEmpty().And.HaveCountLessThan(5);
        IList<string>? secondFiles = secondRun.Should().ContainSingle().Which.ResultFiles;
        secondFiles.Should().ContainSingle().Which.Should().EndWith(".json");
        File.Exists(secondFiles![0]).Should().BeTrue();
    }
#endif

    private static TestResult[] Passed() => [new TestResult { Outcome = UnitTestOutcome.Passed }];

#if !WINDOWS_UWP && !WIN_UI
    private static TestResult[] Failed() => [new TestResult { Outcome = UnitTestOutcome.Failed }];
#endif

    private static TestResult[] Results(params UnitTestOutcome[] outcomes)
        => [.. outcomes.Select(outcome => new TestResult { Outcome = outcome })];

    private static TestResult[] ResultsWithDisplayNames(params string?[] displayNames)
        => [.. displayNames.Select(displayName => new TestResult { Outcome = UnitTestOutcome.Passed, DisplayName = displayName })];

    private static IEnumerable<string> Format(IEnumerable<KeyValuePair<string, object?>>? tags)
        => tags?.Select(tag => $"{tag.Key}={tag.Value}") ?? [];

    private static TestMethod DefaultTestMethod(string methodName, string? displayName = null)
        => new(methodName, typeof(RunnerSubject).FullName!, "dummyAssemblyName", displayName);

    private static TestMethod DataDrivenTestMethod(
        string methodName,
        string? displayName = null,
        object?[]? actualData = null,
        string?[]? serializedData = null,
        string? ignoreMessage = null)
        => new(methodName, typeof(RunnerSubject).FullName!, "dummyAssemblyName", displayName)
        {
            DataType = DynamicDataType.ITestDataSource,
            ActualData = actualData,
            SerializedData = serializedData,
            TestDataSourceIgnoreMessage = ignoreMessage,
        };

    private static Attribute[] TwoLegacyDataSources()
        =>
        [
            new DataSourceAttribute("DummyConnectionString", "DummyTableName"),
            new DataSourceAttribute("OtherConnectionString", "OtherTableName"),
        ];

#if !WINDOWS_UWP && !WIN_UI
    private static void EnableCapture()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <CaptureAssertionFailureDiagnostics>true</CaptureAssertionFailureDiagnostics>
              </MSTest>
            </RunSettings>
            """;

        MSTestSettings settings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, logger: null)!;
        MSTestSettings.PopulateSettings(settings);
    }

    private static TestContextImplementation CreateContextWithResultsDirectory(string resultsDirectory)
    {
        var context = new TestContextImplementation(DefaultTestMethod(nameof(RunnerSubject.NoArgs)), null, new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
        context.Context.TestRunCount = 1;

        return context;
    }
#endif

    private TestMethodInfo CreateTestMethodInfo(string methodName, Attribute[] attributes, TestMethodAttribute? executor)
    {
        MethodInfo method = typeof(RunnerSubject).GetMethod(methodName)!;
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(rf => rf.GetCustomAttributes(method))
            .Returns(attributes);

        return new TestMethodInfo(method, _testClassInfo) { Executor = executor! };
    }

    private TestMethodRunner CreateRunner(
        string methodName,
        Attribute[] attributes,
        Func<TestMethodInfo, TestResult[]>? execute,
        TestMethod? testMethod = null,
        ITestContext? testContext = null,
        ITestExecutionActivityLease? lease = null)
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(methodName, attributes, execute is null ? null : new DelegateExecutor(execute));

        return new TestMethodRunner(testMethodInfo, testMethod ?? DefaultTestMethod(methodName), testContext ?? _testContextImplementation, lease);
    }

    public class RunnerSubject
    {
        public void NoArgs()
        {
        }

        public void OneInt(int a)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    private sealed class RowsAttribute(object?[][] rows) : Attribute, ITestDataSource
    {
        public IEnumerable<object?[]> GetData(MethodInfo methodInfo) => rows;

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data) => null;
    }

    private sealed class DelegateExecutor(Func<TestMethodInfo, TestResult[]> execute) : TestMethodAttribute
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
            => Task.FromResult(execute((TestMethodInfo)testMethod));
    }

    private sealed class FakeExecutionActivityLease : ITestExecutionActivityLease
    {
        public bool IsEntered { get; private set; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public async Task<T> RunAsync<T>(Func<Task<T>> callback)
        {
            EnterCount++;
            IsEntered = true;
            try
            {
                return await callback();
            }
            finally
            {
                ExitCount++;
                IsEntered = false;
            }
        }
    }

    private sealed class RecordingActivity : IMSTestActivity
    {
        public List<KeyValuePair<string, object?>> Tags { get; } = [];

        public List<Exception> Exceptions { get; } = [];

        public List<string?> Failures { get; } = [];

        public void SetTag(string key, object? value) => Tags.Add(new(key, value));

        public void SetFailed(string? description) => Failures.Add(description);

        public void RecordException(Exception exception) => Exceptions.Add(exception);

        public void Dispose()
        {
        }
    }

    private sealed class ActivityRecorder : IDisposable
    {
        public RecordingActivity Activity { get; } = new();

        public List<string> StartedNames { get; } = [];

        public List<string> StartedTags { get; } = [];

        public IMSTestActivity? Start(string name, IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            StartedNames.Add(name);
            StartedTags.AddRange(Format(tags));

            return Activity;
        }

        public void Dispose() => Activity.Dispose();
    }

#if !WINDOWS_UWP && !WIN_UI
    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "MSTestRunnerMutationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
#endif
}

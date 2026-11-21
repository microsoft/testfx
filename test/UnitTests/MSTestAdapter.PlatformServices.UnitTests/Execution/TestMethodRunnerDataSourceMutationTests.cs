// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestDataSource = Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodRunnerDataSourceMutationTests : TestContainer
{
#if !WINDOWS_UWP && !WIN_UI
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";

    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
#endif

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly TestContextImplementation _testContextImplementation;
    private readonly TestClassInfo _testClassInfo;

    public TestMethodRunnerDataSourceMutationTests()
    {
#if !WINDOWS_UWP && !WIN_UI
        Environment.SetEnvironmentVariable(RetainVariable, null);
#endif

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        _testContextImplementation = new TestContextImplementation(DefaultTestMethod(nameof(DataSourceSubject.NoArgs)), null, new Dictionary<string, object?>(), null, null);
        _testClassInfo = new TestClassInfo(typeof(DataSourceSubject), typeof(DataSourceSubject).GetConstructor([])!, isParameterlessConstructor: true, new TestClassAttribute(), new TestAssemblyInfo(typeof(DataSourceSubject).Assembly));
    }

    public async Task FoldedRows_SetArgumentsTestDataAndDisplayNameBeforeRunningEachRow()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.TwoInts),
            [new RowsAttribute([[1, 2], [3, 4]], (_, data) => "custom " + data![0])],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            });

        TestResult[] results = await runner.RunTestMethodAsync();

        observations.Should().HaveCount(2);
        observations[0].Arguments.Should().Equal(1, 2);
        observations[0].TestData.Should().Equal(1, 2);
        observations[0].TestDisplayName.Should().Be("custom 1");
        observations[1].Arguments.Should().Equal(3, 4);
        observations[1].TestData.Should().Equal(3, 4);
        observations[1].TestDisplayName.Should().Be("custom 3");
        results.Select(r => r.DisplayName).Should().Equal("custom 1", "custom 3");
    }

    public async Task FoldedTupleRow_IsExpandedIntoTheMethodArguments()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.TwoInts),
            [new RowsAttribute([[(1, 2)]])],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            });

        await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.Arguments.Should().Equal(1, 2);
    }

    public async Task FoldedTupleRow_ForASingleArrayParameter_IsNotExpanded()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.ObjectArray),
            [new RowsAttribute([[(1, 2)]])],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            });

        await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.Arguments.Should().ContainSingle().Which.Should().Be((1, 2));
    }

    public async Task ActualData_WithATupleRow_DoesNotExpandTheTupleAgain()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.TwoInts),
            [],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            },
            ActualDataTestMethod(nameof(DataSourceSubject.TwoInts), displayName: null, actualData: [(1, 2)]));

        await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.Arguments.Should().ContainSingle().Which.Should().Be((1, 2));
    }

    public async Task ActualData_WithATestDataRow_PassesTheRowThroughUnchanged()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            },
            ActualDataTestMethod(nameof(DataSourceSubject.OneInt), displayName: null, actualData: [new TestDataRow<int>(5)]));

        await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.Arguments.Should().ContainSingle().Which.Should().BeOfType<TestDataRow<int>>();
    }

    public async Task FoldedRow_WithAnIgnoreMessage_IsReportedAsIgnoredWithoutRunningTheMethod()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [new RowsAttribute([[new TestDataRow<int>(5) { IgnoreMessage = "why" }], [new TestDataRow<int>(6)]])],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            });

        TestResult[] results = await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.Arguments.Should().Equal(6);
        results.Select(r => r.Outcome).Should().Equal(UnitTestOutcome.Ignored, UnitTestOutcome.Passed);
    }

    public async Task FoldedRows_ReuseTheSameReflectionMethodInfoForEveryDisplayName()
    {
        var source = new RowsAttribute([[1], [new TestDataRow<int>(2) { ArgumentsDisplayName = "args" }], [3]], (_, _) => "named");
        TestMethodRunner runner = CreateRunner(nameof(DataSourceSubject.OneInt), [source], _ => Passed());

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Select(r => r.DisplayName).Should().Equal("named", "OneInt (args)", "named");
        source.DisplayNameMethods.Should().HaveCount(2);
        source.DisplayNameMethods[1].Should().BeSameAs(source.DisplayNameMethods[0]);
    }

    public async Task FoldedRow_WithoutADisplayNameFromTheSource_UsesTheDefaultDisplayNameWithArguments()
    {
        TestMethodRunner runner = CreateRunner(nameof(DataSourceSubject.TwoInts), [new RowsAttribute([[1, 2]])], _ => Passed());

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().ContainSingle().Which.DisplayName.Should().Be("TwoInts (1,2)");
    }

    public async Task ActualData_WithABlankDisplayName_FallsBackToTheMethodName()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            },
            ActualDataTestMethod(nameof(DataSourceSubject.OneInt), displayName: " ", actualData: [1]));

        TestResult[] results = await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.TestDisplayName.Should().Be(nameof(DataSourceSubject.OneInt));
        results.Should().ContainSingle().Which.DisplayName.Should().Be(nameof(DataSourceSubject.OneInt));
    }

    public async Task ActualData_WithADisplayName_KeepsIt()
    {
        var observations = new List<Observation>();
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [],
            info =>
            {
                observations.Add(Observe(info));

                return Passed();
            },
            ActualDataTestMethod(nameof(DataSourceSubject.OneInt), displayName: "Custom name", actualData: [1]));

        TestResult[] results = await runner.RunTestMethodAsync();

        observations.Should().ContainSingle().Which.TestDisplayName.Should().Be("Custom name");
        results.Should().ContainSingle().Which.DisplayName.Should().Be("Custom name");
    }

    public async Task ActualData_FillsOnlyZeroDurationsWithTheElapsedTimeOfTheRun()
    {
        var reported = TimeSpan.FromSeconds(5);
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [],
            _ =>
            {
                SpinUntilStopwatchAdvances();

                return [new TestResult { Outcome = UnitTestOutcome.Passed, Duration = reported }, new TestResult { Outcome = UnitTestOutcome.Passed }];
            },
            ActualDataTestMethod(nameof(DataSourceSubject.OneInt), displayName: null, actualData: [1]));

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().HaveCount(2);
        results[0].Duration.Should().Be(reported);
        results[1].Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    public async Task ActualData_ReportsTheSameDurationForEveryResultOfTheRun()
    {
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.OneInt),
            [],
            _ =>
            {
                SpinUntilStopwatchAdvances();

                return PassedResults(2000);
            },
            ActualDataTestMethod(nameof(DataSourceSubject.OneInt), displayName: null, actualData: [1]));

        TestResult[] results = await runner.RunTestMethodAsync();

        // The runner stops its stopwatch once the method is done, so every result gets the very same duration.
        results.Select(r => r.Duration).Distinct().Should().ContainSingle().Which.Should().BeGreaterThan(TimeSpan.Zero);
    }

#if !WINDOWS_UWP && !WIN_UI
    public async Task LegacyDataSource_ReportsTheOutcomeOfEachRowAndDisposesItsContext()
    {
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        SetupLegacyRows(rowCount: 2);
        var tempDirectories = new List<string?>();
        int call = 0;
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.NoArgs),
            [new DataSourceAttribute("DummyConnectionString", "DummyTableName")],
            info =>
            {
                tempDirectories.Add(info.TestContext.Context.TestTempDirectory);

                return call++ == 0 ? Passed() : Failed();
            },
            testContext: outerContext);

        await runner.RunTestMethodAsync();

        tempDirectories.Should().HaveCount(2);
        tempDirectories[0].Should().NotBeNullOrEmpty();
        tempDirectories[1].Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectories[0]).Should().BeFalse("a passed row disposes its context, which removes the temporary directory");
        Directory.Exists(tempDirectories[1]).Should().BeTrue("a failed row keeps its temporary directory");
    }

    public async Task LegacyDataSource_AttachesTheDiagnosticsCapturedDuringARowToItsFailedResult()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        SetupLegacyRows(rowCount: 1);
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.NoArgs),
            [new DataSourceAttribute("DummyConnectionString", "DummyTableName")],
            _ =>
            {
                TestContextImplementation.CaptureAssertionFailureDiagnostics("legacy row failure", "expected", "actual");

                return Failed();
            },
            testContext: outerContext);

        TestResult[] results = await runner.RunTestMethodAsync();

        IList<string>? resultFiles = results.Should().ContainSingle().Which.ResultFiles;
        resultFiles.Should().ContainSingle().Which.Should().EndWith(".json");
        File.Exists(resultFiles![0]).Should().BeTrue();
    }
#endif

    public async Task LegacyDataSource_ClearsTheDataConnectionAndTheDataRowOfTheTestContextOnceAfterAllRows()
    {
        var testContext = new Mock<ITestContext>();
        testContext.SetupGet(c => c.Context).Returns(_testContextImplementation);
        SetupLegacyRows(rowCount: 2);
        TestMethodRunner runner = CreateRunner(
            nameof(DataSourceSubject.NoArgs),
            [new DataSourceAttribute("DummyConnectionString", "DummyTableName")],
            _ => Passed(),
            testContext: testContext.Object);

        await runner.RunTestMethodAsync();

        testContext.Verify(c => c.SetDataConnection(null), Times.Once);
        testContext.Verify(c => c.SetDataRow(null), Times.Once);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
#if !WINDOWS_UWP && !WIN_UI
            Environment.SetEnvironmentVariable(RetainVariable, _previousRetain);
#endif
        }
    }

    private static TestResult[] Passed()
        => [new TestResult { Outcome = UnitTestOutcome.Passed }];

    private static TestResult[] Failed()
        => [new TestResult { Outcome = UnitTestOutcome.Failed }];

    private static TestResult[] PassedResults(int count)
    {
        var results = new TestResult[count];
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = new TestResult { Outcome = UnitTestOutcome.Passed };
        }

        return results;
    }

    private static Observation Observe(TestMethodInfo info)
    {
        TestContext context = info.TestContext.Context;

        return new Observation(info.Arguments, context.TestData, context.TestDisplayName);
    }

    private static TestMethod DefaultTestMethod(string methodName)
        => new(methodName, typeof(DataSourceSubject).FullName!, "dummyAssemblyName", displayName: null);

    private static TestMethod ActualDataTestMethod(string methodName, string? displayName, object?[] actualData)
        => new(methodName, typeof(DataSourceSubject).FullName!, "dummyAssemblyName", displayName)
        {
            DataType = DynamicDataType.ITestDataSource,
            ActualData = actualData,
        };

    // Makes sure the stopwatch of the runner, which is already running, has measured a non-zero duration whatever the clock resolution.
    private static void SpinUntilStopwatchAdvances()
    {
        var spinner = Stopwatch.StartNew();
        while (spinner.Elapsed == TimeSpan.Zero)
        {
        }
    }

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
        var context = new TestContextImplementation(DefaultTestMethod(nameof(DataSourceSubject.NoArgs)), null, new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
        context.Context.TestRunCount = 1;

        return context;
    }
#endif

    private TestMethodRunner CreateRunner(
        string methodName,
        Attribute[] attributes,
        Func<TestMethodInfo, TestResult[]> execute,
        TestMethod? testMethod = null,
        ITestContext? testContext = null)
    {
        MethodInfo method = typeof(DataSourceSubject).GetMethod(methodName)!;
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(rf => rf.GetCustomAttributes(method)).Returns(attributes);
        var testMethodInfo = new TestMethodInfo(method, _testClassInfo) { Executor = new DelegateExecutor(execute) };

        return new TestMethodRunner(testMethodInfo, testMethod ?? DefaultTestMethod(methodName), testContext ?? _testContextImplementation);
    }

    private void SetupLegacyRows(int rowCount)
        => _testablePlatformServiceProvider.MockTestDataSource
            .Setup(tds => tds.GetData(It.IsAny<ITestMethod>(), It.IsAny<ITestContext>()))
            .Returns(Enumerable.Range(0, rowCount).Cast<object>());

    public sealed class DataSourceSubject
    {
        public void NoArgs()
        {
        }

        public void OneInt(int a)
        {
        }

        public void TwoInts(int a, int b)
        {
        }

        public void ObjectArray(object[] values)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    private sealed class RowsAttribute(object?[][] rows, Func<MethodInfo, object?[]?, string?>? getDisplayName = null) : Attribute, ITestDataSource
    {
        public List<MethodInfo> DisplayNameMethods { get; } = [];

        public IEnumerable<object?[]> GetData(MethodInfo methodInfo) => rows;

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        {
            DisplayNameMethods.Add(methodInfo);

            return getDisplayName?.Invoke(methodInfo, data);
        }
    }

    private sealed class DelegateExecutor(Func<TestMethodInfo, TestResult[]> execute) : TestMethodAttribute
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => Task.FromResult(execute((TestMethodInfo)testMethod));
    }

    private sealed class Observation(object?[]? arguments, object?[]? testData, string? testDisplayName)
    {
        public object?[]? Arguments { get; } = arguments;

        public object?[]? TestData { get; } = testData;

        public string? TestDisplayName { get; } = testDisplayName;
    }

#if !WINDOWS_UWP && !WIN_UI
    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "MSTestRunnerDataSourceMutationTests", Guid.NewGuid().ToString("N"));
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

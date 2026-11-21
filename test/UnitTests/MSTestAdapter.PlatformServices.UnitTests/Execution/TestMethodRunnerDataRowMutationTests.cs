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

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodRunnerDataRowMutationTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly TestContextImplementation _testContextImplementation;
    private readonly TestClassInfo _testClassInfo;

    public TestMethodRunnerDataRowMutationTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        _testContextImplementation = new TestContextImplementation(DefaultTestMethod(), null, new Dictionary<string, object?>(), null, null);
        _testClassInfo = new TestClassInfo(typeof(DataRowSubject), typeof(DataRowSubject).GetConstructor([])!, isParameterlessConstructor: true, new TestClassAttribute(), new TestAssemblyInfo(typeof(DataRowSubject).Assembly));
    }

    public async Task LegacyDataSource_SetsTheDisplayNameOfEachRowOnItsContext()
    {
        SetupLegacyRows(rowCount: 2);
        var displayNames = new List<string?>();
        TestMethodRunner runner = CreateLegacyRunner(info =>
        {
            displayNames.Add(info.TestContext.Context.TestDisplayName);

            return Passed();
        });

        TestResult[] results = await runner.RunTestMethodAsync();

        displayNames.Should().Equal("NoArgs (Data Row 0)", "NoArgs (Data Row 1)");
        results.Select(r => r.DisplayName).Should().Equal("NoArgs (Data Row 0)", "NoArgs (Data Row 1)");
    }

    public async Task LegacyDataSource_ReportsTheSameDurationForEveryResultOfARow()
    {
        SetupLegacyRows(rowCount: 1);
        TestMethodRunner runner = CreateLegacyRunner(_ =>
        {
            SpinUntilStopwatchAdvances();

            return PassedResults(2000);
        });

        TestResult[] results = await runner.RunTestMethodAsync();

        // The runner stops the stopwatch of the row once its method is done, so every result gets the very same duration.
        results.Select(r => r.Duration).Distinct().Should().ContainSingle().Which.Should().BeGreaterThan(TimeSpan.Zero);
    }

    public async Task ExecuteTestWithDataRow_PublishesTheRowToTheContextOnlyWhileTheRowRuns()
    {
        // Only .NET Framework exposes the row through TestContext.DataRow, so the publication is verified on the context itself.
        // The method is private and its context is always a TestContextImplementation when reached through RunTestMethodAsync.
        object row = new();
        var events = new List<string>();
        var context = new Mock<ITestContext>();
        context.Setup(c => c.SetDataRow(It.IsAny<object?>()))
            .Callback<object?>(published => events.Add(published is null ? "clear row" : "set row"));
        TestMethodRunner runner = CreateLegacyRunner(_ =>
        {
            events.Add("run method");

            return Passed();
        });
        MethodInfo executeTestWithDataRow = typeof(TestMethodRunner).GetMethod("ExecuteTestWithDataRowAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        TestResult[] results = await (Task<TestResult[]>)executeTestWithDataRow.Invoke(runner, [context.Object, row, 3])!;

        events.Should().Equal("set row", "run method", "clear row");
        context.Verify(c => c.SetDataRow(row), Times.Once());
        results.Should().ContainSingle().Which.DisplayName.Should().Be("NoArgs (Data Row 3)");
    }

#if !WINDOWS_UWP && !WIN_UI
    public async Task LegacyDataSource_AttachesTheDiagnosticsCapturedByARowWithoutResultToTheResultOfTheRun()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        SetupLegacyRows(rowCount: 1);
        TestMethodRunner runner = CreateLegacyRunner(
            _ =>
            {
                TestContextImplementation.CaptureAssertionFailureDiagnostics("row without result", "expected", "actual");

                return [];
            },
            outerContext);

        TestResult[] results = await runner.RunTestMethodAsync();

        TestResult result = results.Should().ContainSingle().Which;
        result.Outcome.Should().Be(UnitTestOutcome.Unknown);
        IList<string>? resultFiles = result.ResultFiles;
        resultFiles.Should().ContainSingle().Which.Should().EndWith(".json");
        File.Exists(resultFiles![0]).Should().BeTrue();
    }

    public async Task LegacyDataSource_AttachesTheDiagnosticsOfARowToTheResultOfThatRow()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        SetupLegacyRows(rowCount: 2);
        int call = 0;
        TestMethodRunner runner = CreateLegacyRunner(
            _ =>
            {
                if (call++ == 1)
                {
                    TestContextImplementation.CaptureAssertionFailureDiagnostics("second row failure", "expected", "actual");
                }

                return Failed();
            },
            outerContext);

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().HaveCount(2);
        results[0].ResultFiles.Should().BeNullOrEmpty();
        IList<string>? secondRowFiles = results[1].ResultFiles;
        secondRowFiles.Should().ContainSingle().Which.Should().EndWith(".json");
        File.Exists(secondRowFiles![0]).Should().BeTrue();
    }

    public async Task LegacyDataSource_SharesTheCaptureBudgetOfTheTestAcrossItsRows()
    {
        EnableCapture();
        using var scope = new TempDirectoryScope();
        using TestContextImplementation outerContext = CreateContextWithResultsDirectory(scope.Root);
        SetupLegacyRows(rowCount: 2);
        TestMethodRunner runner = CreateLegacyRunner(
            _ =>
            {
                // More captures than the budget allows for one test, so that the first row exhausts it.
                for (int capture = 0; capture < 5; capture++)
                {
                    TestContextImplementation.CaptureAssertionFailureDiagnostics($"failure {capture}", "expected", "actual");
                }

                return Failed();
            },
            outerContext);

        TestResult[] results = await runner.RunTestMethodAsync();

        results.Should().HaveCount(2);
        results[0].ResultFiles.Should().NotBeNullOrEmpty();
        results[1].ResultFiles.Should().BeNullOrEmpty("the budget is only reset once the whole test is done");
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    private static TestResult[] Passed()
        => [new TestResult { Outcome = UnitTestOutcome.Passed }];

    private static TestResult[] PassedResults(int count)
    {
        var results = new TestResult[count];
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = new TestResult { Outcome = UnitTestOutcome.Passed };
        }

        return results;
    }

    private static TestMethod DefaultTestMethod()
        => new(nameof(DataRowSubject.NoArgs), typeof(DataRowSubject).FullName!, "dummyAssemblyName", displayName: null);

    // Makes sure the stopwatch of the runner, which is already running, has measured a non-zero duration whatever the clock resolution.
    private static void SpinUntilStopwatchAdvances()
    {
        var spinner = Stopwatch.StartNew();
        while (spinner.Elapsed == TimeSpan.Zero)
        {
        }
    }

#if !WINDOWS_UWP && !WIN_UI
    private static TestResult[] Failed()
        => [new TestResult { Outcome = UnitTestOutcome.Failed }];

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
        var context = new TestContextImplementation(DefaultTestMethod(), null, new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
        context.Context.TestRunCount = 1;

        return context;
    }
#endif

    private TestMethodRunner CreateLegacyRunner(Func<TestMethodInfo, TestResult[]> execute, TestContextImplementation? testContext = null)
    {
        MethodInfo method = typeof(DataRowSubject).GetMethod(nameof(DataRowSubject.NoArgs))!;
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(rf => rf.GetCustomAttributes(method))
            .Returns([new DataSourceAttribute("DummyConnectionString", "DummyTableName")]);
        var testMethodInfo = new TestMethodInfo(method, _testClassInfo) { Executor = new DelegateExecutor(execute) };

        return new TestMethodRunner(testMethodInfo, DefaultTestMethod(), testContext ?? _testContextImplementation);
    }

    private void SetupLegacyRows(int rowCount)
        => _testablePlatformServiceProvider.MockTestDataSource
            .Setup(tds => tds.GetData(It.IsAny<ITestMethod>(), It.IsAny<ITestContext>()))
            .Returns(Enumerable.Range(0, rowCount).Cast<object>());

    public sealed class DataRowSubject
    {
        public void NoArgs()
        {
        }
    }

    private sealed class DelegateExecutor(Func<TestMethodInfo, TestResult[]> execute) : TestMethodAttribute
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => Task.FromResult(execute((TestMethodInfo)testMethod));
    }

#if !WINDOWS_UWP && !WIN_UI
    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "MSTestRunnerDataRowMutationTests", Guid.NewGuid().ToString("N"));
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

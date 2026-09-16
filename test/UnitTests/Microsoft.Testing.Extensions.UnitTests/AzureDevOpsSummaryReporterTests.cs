// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsSummaryReporterTests
{
    private static readonly string ResultsDirectory = Path.Combine(Path.GetTempPath(), "azdo-summary-reporter-tests");

    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IMessageBus> _messageBusMock = new();
    private readonly Mock<IOutputDevice> _outputDeviceMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ITestApplicationProcessExitCode> _testApplicationProcessExitCodeMock = new();
    private readonly Mock<ITestCoverageResult> _testCoverageResultMock = new();
    private readonly List<IOutputDeviceData> _outputData = [];

    public AzureDevOpsSummaryReporterTests()
    {
        _ = _configurationMock.SetupGet(c => c[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(ResultsDirectory);
        _ = _testApplicationModuleInfoMock.Setup(info => info.TryGetAssemblyName()).Returns("MyAssembly");
        _ = _loggerFactoryMock.Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _ = _testCoverageResultMock.SetupGet(result => result.Scopes).Returns([]);
        _ = _testCoverageResultMock.SetupGet(result => result.Thresholds).Returns([]);
        _ = _outputDeviceMock
            .Setup(outputDevice => outputDevice.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _outputData.Add(data))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenSummaryOptionNotSetAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(options: []);
        Assert.IsFalse(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task SessionFinishing_DoesNothingAndEmitsWarning_WhenTfBuildNotSetAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.Contains(AzureDevOpsResources.SummaryRequiresTfBuildWarning, GetWarnings());

        await reporter.ConsumeAsync(CreateProducer(), CreatePassed("t1"), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetCommandLines());
    }

    [TestMethod]
    public void BuildMarkdown_RendersTotalsAndTopFailingAndSlowest()
    {
        var records = new List<TestRecord>
        {
            new("Test1", "MyCo.Suite.ClassA.Test1", TerminalKind.Passed, TimeSpan.FromMilliseconds(100)),
            new("Test2", "MyCo.Suite.ClassA.Test2", TerminalKind.Failed, TimeSpan.FromMilliseconds(200)),
            new("Test3", "MyCo.Suite.ClassA.Test3", TerminalKind.Failed, TimeSpan.FromMilliseconds(50)),
            new("Slowpoke", "MyCo.Suite.ClassB.Slowpoke", TerminalKind.Passed, TimeSpan.FromSeconds(12)),
            new("Skipper", "MyCo.Suite.ClassC.Skipper", TerminalKind.Skipped, TimeSpan.Zero),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("MyAssembly", md);
        Assert.Contains("net8.0", md);
        Assert.Contains("Total", md);
        Assert.Contains("Passed", md);
        Assert.Contains("Failed", md);
        Assert.Contains("Skipped", md);
        Assert.Contains("MyCo.Suite.ClassA", md);
        Assert.Contains("Slowpoke", md);
        Assert.Contains("MyCo.Suite.ClassA.Test2", md);
    }

    [TestMethod]
    public void BuildMarkdown_FormatsTotalDurationCorrectly_BeyondTwentyFourHours()
    {
        // 25 hours 7 minutes 8 seconds total: TimeSpan custom format `hh` wraps at 24h
        // (would render as 01:07:08), so the implementation must use TotalHours for >= 1h.
        var records = new List<TestRecord>
        {
            new("LongTest", "MyCo.X.LongTest", TerminalKind.Passed, new TimeSpan(25, 7, 8)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("| Total duration | 25:07:08 |", md);
        Assert.DoesNotContain("| Total duration | 01:07:08 |", md);
    }

    [TestMethod]
    public void BuildMarkdown_FormatsTotalDurationAsMinutesAndSeconds_WhenBelowOneHour()
    {
        var records = new List<TestRecord>
        {
            new("MidTest", "MyCo.X.MidTest", TerminalKind.Passed, new TimeSpan(0, 5, 30)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("| Total duration | 05:30 |", md);
    }

    [TestMethod]
    public void BuildMarkdown_EscapesPipesAndNewlinesInCells()
    {
        var records = new List<TestRecord>
        {
            new("Has|Pipe", "MyCo.X.HasPipe", TerminalKind.Failed, TimeSpan.FromMilliseconds(1)),
            new("Has\nNewline", "MyCo.X.HasNewline", TerminalKind.Failed, TimeSpan.FromMilliseconds(1)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("Has\\|Pipe", md);
        Assert.Contains("Has<br>Newline", md);
        Assert.DoesNotContain("Has|Pipe", md);
        Assert.DoesNotContain("Has\nNewline", md);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_HtmlEncodesModuleSummaryLabel()
    {
        var module = new CiRunSummaryModule
        {
            AssemblyName = "<h1>A&B</h1>",
            ModulePath = "A.dll",
            TargetFramework = "net9.0<&>",
            Architecture = "x64&arm64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            Coverage = new CiCoverageSummaryData
            {
                Metrics =
                [
                    new CiCoverageMetric
                    {
                        ScopeLevel = CoverageScopeLevel.Overall,
                        Metric = CoverageMetric.Line,
                        ProducerId = "coverlet",
                        CoveredCount = 82,
                        CoverableCount = 100,
                    },
                ],
                Thresholds =
                [
                    new CiCoverageThreshold
                    {
                        ScopeLevel = CoverageScopeLevel.Overall,
                        Metric = CoverageMetric.Line,
                        ProducerId = "coverlet",
                        ActualPercentage = 82,
                        RequiredPercentage = 85,
                        HasCoverableData = true,
                        Passed = false,
                    },
                ],
                ReportingModuleCount = 1,
                TotalModuleCount = 1,
            },
        };
        var aggregate = new CiRunSummaryAggregate(
            [module],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 0,
            passedTests: 0,
            failedTests: 0,
            skippedTests: 0,
            duration: null,
            exitCode: null,
            hasAuthoritativeRunSummary: false,
            isPartial: false);

        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("<summary>&lt;h1&gt;A&amp;B&lt;/h1&gt; (net9.0&lt;&amp;&gt;, x64&amp;arm64)</summary>", markdown);
        Assert.DoesNotContain("<summary><h1>", markdown);
        Assert.Contains("| Overall | Line | 82 | 100 | 82.0% |", markdown);
        Assert.Contains("| &lt;h1&gt;A&amp;B&lt;/h1&gt; (net9.0&lt;&amp;&gt;) — Overall | Line | 82.0% | 85.0% | ❌ Failed |", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_SameRenderedIdentityDifferentPaths_DisambiguatesSections()
    {
        var first = new CiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "first/Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-1",
            AttemptNumber = 1,
        };
        var second = new CiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "second/Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-2",
            AttemptNumber = 2,
        };
        var aggregate = new CiRunSummaryAggregate(
            [first, second],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 0,
            passedTests: 0,
            failedTests: 0,
            skippedTests: 0,
            duration: null,
            exitCode: null,
            hasAuthoritativeRunSummary: false,
            isPartial: false);

        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("attempt 1, session session-1", markdown);
        Assert.Contains("attempt 2, session session-2", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_RendersFailureFlakinessDurationAndDependencyInsights()
    {
        var module = new CiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            TotalTests = 3,
            PassedTests = 2,
            FailedTests = 1,
            TestDurationTicks = TimeSpan.FromSeconds(3).Ticks,
            Failures =
            [
                new CiRunSummaryTest
                {
                    DisplayName = "Fails",
                    FullyQualifiedName = "Tests.Suite.Fails",
                    DurationTicks = TimeSpan.FromSeconds(1).Ticks,
                    ErrorMessage = "Expected <safe>, got |unsafe|",
                    ErrorType = "System.InvalidOperationException",
                    StackTrace = "at Tests.Suite.Fails()",
                },
            ],
            FlakyTests =
            [
                new CiRunSummaryTest
                {
                    DisplayName = "Recovered",
                    FullyQualifiedName = "Tests.Suite.Recovered",
                },
            ],
            HistoryTests =
            [
                new CiRunSummaryHistoryTest
                {
                    TestId = "history",
                    DisplayName = "Unstable",
                    FullyQualifiedName = "Tests.Suite.Unstable",
                    Outcome = "passed",
                    DurationTicks = TimeSpan.FromSeconds(1).Ticks,
                    HistoricalPassCount = 7,
                    HistoricalFailCount = 3,
                    HistoryWindowInDays = 14,
                    DurationSampleCount = 10,
                    P95DurationMilliseconds = 500,
                    P99DurationMilliseconds = 750,
                },
            ],
            Dependencies =
            [
                new CiRunSummaryDependency
                {
                    DependentFullyQualifiedName = "Tests.Suite.Dependent",
                    Prerequisite = "Tests.Suite.Setup",
                    ProceedOnFailure = true,
                },
            ],
        };
        var aggregate = new CiRunSummaryAggregate(
            [module],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 3,
            passedTests: 2,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(3),
            exitCode: 2,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("| Flaky | 1 |", markdown);
        Assert.Contains("| Pass rate | 66.7% |", markdown);
        Assert.Contains("## Flakiness history", markdown);
        Assert.Contains("30.0% (14d)", markdown);
        Assert.Contains("## Duration history", markdown);
        Assert.Contains("2.00×", markdown);
        Assert.Contains("## Test dependencies", markdown);
        Assert.Contains("Tests.Suite.Setup", markdown);
        Assert.Contains("| Continue |", markdown);
        Assert.Contains("### Failure details", markdown);
        Assert.Contains("System.InvalidOperationException", markdown);
        Assert.Contains("Expected &lt;safe&gt;, got \\|unsafe\\|", markdown);
        Assert.DoesNotContain("at Tests.Suite.Fails()", markdown);
        Assert.DoesNotContain("Stack trace", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_CapsExpandedFailureDetailsAcrossModules()
    {
        static CiRunSummaryModule CreateModule(string assemblyName, string prefix)
            => new()
            {
                AssemblyName = assemblyName,
                ModulePath = assemblyName + ".dll",
                TargetFramework = "net9.0",
                Architecture = "x64",
                ExecutionId = "execution",
                SessionUid = assemblyName,
                AttemptNumber = 1,
                TotalTests = 15,
                FailedTests = 15,
                Failures =
                [
                    .. Enumerable.Range(0, 15).Select(index => new CiRunSummaryTest
                    {
                        DisplayName = $"{prefix}{index}",
                        FullyQualifiedName = $"{prefix}.Test{index}",
                        ErrorMessage = $"message-{prefix}-{index}",
                        StackTrace = $"sensitive-stack-{prefix}-{index}",
                    }),
                ],
            };

        CiRunSummaryModule first = CreateModule("First", "A");
        CiRunSummaryModule second = CreateModule("Second", "B");
        var aggregate = new CiRunSummaryAggregate(
            [first, second],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 30,
            passedTests: 0,
            failedTests: 30,
            skippedTests: 0,
            duration: TimeSpan.Zero,
            exitCode: 2,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("message-A-14", markdown);
        Assert.Contains("message-B-4", markdown);
        Assert.DoesNotContain("message-B-5", markdown);
        Assert.Contains("`B.Test5`", markdown);
        Assert.DoesNotContain("sensitive-stack", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_NameOnlyLegacyFailuresDoNotConsumeDetailBudget()
    {
        var legacyModule = new CiRunSummaryModule
        {
            AssemblyName = "Legacy",
            ModulePath = "Legacy.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "legacy",
            AttemptNumber = 1,
            TotalTests = 20,
            FailedTests = 20,
            Failures =
            [
                .. Enumerable.Range(0, 20).Select(index => new CiRunSummaryTest
                {
                    DisplayName = $"Legacy{index}",
                    FullyQualifiedName = $"Legacy.Test{index}",
                }),
            ],
        };
        var currentModule = new CiRunSummaryModule
        {
            AssemblyName = "Current",
            ModulePath = "Current.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "current",
            AttemptNumber = 1,
            TotalTests = 1,
            FailedTests = 1,
            Failures =
            [
                new CiRunSummaryTest
                {
                    DisplayName = "Current",
                    FullyQualifiedName = "Current.Test",
                    ErrorMessage = "current diagnostic",
                },
            ],
        };
        var aggregate = new CiRunSummaryAggregate(
            [legacyModule, currentModule],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 21,
            passedTests: 0,
            failedTests: 21,
            skippedTests: 0,
            duration: TimeSpan.Zero,
            exitCode: 2,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("current diagnostic", markdown);
        Assert.Contains("`Legacy.Test0`", markdown);
    }

    [TestMethod]
    public async Task SessionFinishing_WritesSummaryFileAndEmitsUploadSummaryCommandAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        // Capture the StreamWriter output.
        using var memoryStream = new MemoryStream();
        IFileStream fakeStream = new FakeFileStream(memoryStream);
        _ = _fileSystemMock.Setup(fs => fs.ExistDirectory(It.IsAny<string>())).Returns(true);
        _ = _fileSystemMock.Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read)).Returns(fakeStream);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreatePassed("t1"), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateFailed("t2"), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string written = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        Assert.Contains("MyAssembly", written);

        string[] lines = GetCommandLines();
        Assert.HasCount(1, lines);
        Assert.StartsWith("##vso[task.uploadsummary]", lines[0]);
        Assert.Contains("azdo-summary-", lines[0]);
        // The assembly name must be part of the default file name so concurrent test assemblies
        // sharing the same TFM and TestResults directory don't race to write the same file.
        Assert.Contains("MyAssembly", lines[0]);
        Assert.EndsWith(".md", lines[0]);
    }

    [TestMethod]
    public async Task SessionFinishing_RendersHistoryDependenciesAndFailureDetailsAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions(), new StubHistoryService());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        using var memoryStream = new MemoryStream();
        IFileStream fakeStream = new FakeFileStream(memoryStream);
        _ = _fileSystemMock.Setup(fs => fs.ExistDirectory(It.IsAny<string>())).Returns(true);
        _ = _fileSystemMock.Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read)).Returns(fakeStream);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var properties = new PropertyBag(
            new FailedTestNodeStateProperty(new InvalidOperationException("boom"), "Expected <safe>, got |unsafe|"),
            new TimingProperty(new TimingInfo(now, now.AddSeconds(1), TimeSpan.FromSeconds(1))),
            new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", "MyCo.Suite.Dependent"),
            new SerializableKeyValuePairStringProperty("mstest.TestCase.Dependency", "SMyCo.Suite.Setup\nInitialize"));
        var update = new TestNodeUpdateMessage(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "dependent",
                DisplayName = "Dependent",
                Properties = properties,
            });

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), update, CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string written = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        Assert.Contains("Flakiness history", written);
        Assert.Contains("20.0% (14d)", written);
        Assert.Contains("Duration history", written);
        Assert.Contains("2.00×", written);
        Assert.Contains("Test dependencies", written);
        Assert.Contains("MyCo.Suite.Setup.Initialize", written);
        Assert.Contains("Failure details", written);
        Assert.Contains("Expected &lt;safe&gt;, got \\|unsafe\\|", written);
        Assert.Contains("System.InvalidOperationException", written);
    }

    [TestMethod]
    public async Task SessionFinishing_ReportsRecoveredRetryAsFlakyAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        using var memoryStream = new MemoryStream();
        IFileStream fakeStream = new FakeFileStream(memoryStream);
        _ = _fileSystemMock.Setup(fs => fs.ExistDirectory(It.IsAny<string>())).Returns(true);
        _ = _fileSystemMock.Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read)).Returns(fakeStream);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await reporter.ConsumeAsync(
            CreateProducer(),
            CreateRetryUpdate(new FailedTestNodeStateProperty("first"), attemptNumber: 1, isSuperseded: true),
            CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(
            CreateProducer(),
            CreateRetryUpdate(PassedTestNodeStateProperty.CachedInstance, attemptNumber: 2, isSuperseded: false),
            CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string written = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        Assert.Contains("| Flaky | 1 |", written);
        Assert.Contains("Flaky tests", written);
        Assert.Contains("MyCo.Suite.Retry", written);
    }

    private static Dictionary<string, string[]> EnabledOptions()
        => new() { [AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [] };

    private AzureDevOpsSummaryReporter CreateReporter(
        Dictionary<string, string[]> options,
        IAzureDevOpsHistoryService? historyService = null)
        => new(
            new TestCommandLineOptions(options),
            _configurationMock.Object,
            _environmentMock.Object,
            _fileSystemMock.Object,
            _messageBusMock.Object,
            _outputDeviceMock.Object,
            _testApplicationModuleInfoMock.Object,
            _testApplicationProcessExitCodeMock.Object,
            _testCoverageResultMock.Object,
            _loggerFactoryMock.Object,
            static () => false,
            historyService);

    private static TestNodeUpdateMessage CreatePassed(string uid)
        => Create(uid, new PassedTestNodeStateProperty());

    private static TestNodeUpdateMessage CreateFailed(string uid)
        => Create(uid, new FailedTestNodeStateProperty());

    private static TestNodeUpdateMessage Create(string uid, TestNodeStateProperty state)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(state),
            });

    private static TestNodeUpdateMessage CreateRetryUpdate(
        TestNodeStateProperty state,
        int attemptNumber,
        bool isSuperseded)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "retry",
                DisplayName = "Retry",
                Properties = new PropertyBag(
                    state,
                    new RetryAttemptProperty(attemptNumber, isSuperseded),
                    new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", "MyCo.Suite.Retry")),
            });

    private static IDataProducer CreateProducer() => new TestProducer();

    private string[] GetCommandLines()
        => [.. _outputData.OfType<AzureDevOpsCommandOutputDeviceData>().Select(data => data.Text)];

    private string[] GetWarnings()
        => [.. _outputData.OfType<WarningMessageOutputDeviceData>().Select(output => output.Message)];

    private sealed class TestProducer : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

        public string Uid => "TestProducer";

        public string Version => "1.0.0";

        public string DisplayName => "TestProducer";

        public string Description => "TestProducer";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class StubHistoryService : IAzureDevOpsHistoryService
    {
        public int HistoryWindowInDays => 14;

        public bool TryGetStats(string testName, out FlakyStats stats)
        {
            stats = new FlakyStats(passCount: 8, failCount: 2);
            return true;
        }

        public bool IsLikelyFlaky(string testName, double threshold) => true;

        public bool TryGetDurationStats(string testName, out DurationHistoryStats stats)
        {
            stats = new DurationHistoryStats(p95Milliseconds: 500, p99Milliseconds: 750, sampleCount: 10);
            return true;
        }
    }

    private sealed class TestSessionContext : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class FakeFileStream(Stream inner) : IFileStream
    {
        public Stream Stream { get; } = inner;

        public string Name { get; } = "azdo-summary.md";

        public void Dispose()
        {
            // The underlying MemoryStream is owned by the test.
        }

        public ValueTask DisposeAsync() => default;
    }
}

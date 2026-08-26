// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using GitHubActionsTerminalKind = ghactions::Microsoft.Testing.Extensions.TerminalKind;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;
using GitHubCiCoverageMetric = ghactions::Microsoft.Testing.Extensions.CiCoverageMetric;
using GitHubCiCoverageSummaryData = ghactions::Microsoft.Testing.Extensions.CiCoverageSummaryData;
using GitHubCiCoverageThreshold = ghactions::Microsoft.Testing.Extensions.CiCoverageThreshold;
using GitHubCiRunSummaryAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubCiRunSummaryAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubCiRunSummaryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;
using GitHubSummaryPostProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsSummaryReporterTests
{
    // ExitCode.Success (0): a normal passing run — no exit-code callout expected.
    private const int SuccessExitCode = 0;

    // ExitCode.AtLeastOneTestFailed (2): failures are conveyed by the table/list, not a callout.
    private const int AtLeastOneTestFailedExitCode = 2;

    // ExitCode.ZeroTests (8) and MinimumExpectedTestsPolicyViolation (9): non-test-result failures.
    private const int ZeroTestsExitCode = 8;
    private const int MinimumExpectedTestsExitCode = 9;

    [TestMethod]
    public void SummaryPostProcessor_SupportsRetryAttempts()
    {
        GitHubSummaryPostProcessor processor = new(
            new TestCommandLineOptions([]),
            Mock.Of<IEnvironment>(),
            new SystemFileSystem());

        Assert.AreSequenceEqual(
            new[] { ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts },
            processor.SupportedModes);
    }

    [TestMethod]
    public async Task SummaryPostProcessor_ForRetryAttempts_ReturnsChainableFragmentAndWritesSummary()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-summary-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var flakyTest = new GitHubCiRunSummaryTest
            {
                DisplayName = "Flaky",
                FullyQualifiedName = "Tests.Flaky",
                DurationTicks = TimeSpan.FromMilliseconds(10).Ticks,
            };
            GitHubCiRunSummaryModule first = CreateRetryModule("session-1", attempt: 1, passed: 2, failed: 1);
            first.Failures = [flakyTest];
            GitHubCiRunSummaryModule retry = CreateRetryModule("session-2", attempt: 2, passed: 1, failed: 0);
            retry.FlakyTests = [flakyTest];
            string firstPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                first);
            string retryPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                retry);
            string stepSummaryPath = Path.Combine(directory, "step-summary.md");
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(stepSummaryPath);
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                environment.Object,
                new SystemFileSystem());
            var context = new ArtifactPostProcessingContext(
                ArtifactPostProcessingTruncationReason.None,
                ArtifactPostProcessingMode.RetryAttempts,
                new ArtifactPostProcessingRunSummary(
                    totalTests: 3,
                    passedTests: 3,
                    failedTests: 0,
                    skippedTests: 0,
                    duration: TimeSpan.FromSeconds(1),
                    exitCode: 0,
                    testModuleCount: 1));

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1"),
                    new InputArtifact(retryPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "2"),
                ],
                directory,
                context,
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(GitHubSummaryPostProcessor.FragmentArtifactKind, output.Kind);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [new InputArtifact(output.Path, output.Kind, null, null, null, null)],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            Assert.AreEqual(3, aggregate.TotalTests);
            Assert.AreEqual(3, aggregate.PassedTests);
            Assert.AreEqual(0, aggregate.FailedTests);
            Assert.AreEqual("Tests.Flaky", aggregate.FlakyTests.Single().FullyQualifiedName);
            string summary = File.ReadAllText(stepSummaryPath);
            Assert.Contains("| 3 | 3 | 0 | 0 | 1 |", summary, summary);
            Assert.Contains("### ⚠️ Flaky tests (1)", summary, summary);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SummaryPostProcessor_ForDotnetTestRetryAttempts_DefersStepSummaryToDownstreamProcessor()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-summary-dotnet-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubCiRunSummaryModule module = CreateRetryModule("session-1", attempt: 1, passed: 1, failed: 0);
            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);
            string stepSummaryPath = Path.Combine(directory, "step-summary.md");
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(stepSummaryPath);
            environment
                .Setup(item => item.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"))
                .Returns("execution");
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                environment.Object,
                new SystemFileSystem());

            ProcessedArtifact? output = await processor.ProcessAsync(
                [new InputArtifact(fragmentPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1")],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(GitHubSummaryPostProcessor.FragmentArtifactKind, output.Kind);
            Assert.IsFalse(File.Exists(stepSummaryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SessionFinishing_WhenDeferred_PersistsOnFailurePolicyInFragmentAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-summary-reporter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new Mock<IConfiguration>();
            configuration.SetupGet(item => item[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(directory);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(Path.Combine(directory, "step-summary.md"));
            var moduleInfo = new Mock<ITestApplicationModuleInfo>();
            moduleInfo.Setup(item => item.TryGetAssemblyName()).Returns("Tests");
            moduleInfo.Setup(item => item.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(directory, "Tests.dll"));
            var coverage = new Mock<ITestCoverageResult>();
            coverage.SetupGet(item => item.Scopes).Returns([]);
            coverage.SetupGet(item => item.Thresholds).Returns([]);
            var messageBus = new Mock<IMessageBus>();
            SessionFileArtifact? artifact = null;
            messageBus
                .Setup(item => item.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, data) => artifact = (SessionFileArtifact)data)
                .Returns(Task.CompletedTask);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            GitHubActionsSummaryReporter reporter = new(
                new TestCommandLineOptions(new()
                {
                    [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
                    [GitHubActionsCommandLineOptions.GitHubActionsStepSummary] = [GitHubActionsCommandLineOptions.StepSummaryOnFailureValue],
                }),
                configuration.Object,
                environment.Object,
                new SystemFileSystem(),
                messageBus.Object,
                Mock.Of<IOutputDevice>(),
                moduleInfo.Object,
                Mock.Of<ITestApplicationProcessExitCode>(),
                coverage.Object,
                loggerFactory.Object,
                static () => true);
            var context = new Mock<ITestSessionContext>();
            context.SetupGet(item => item.SessionUid).Returns(new SessionUid("session"));
            context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

            await reporter.OnTestSessionStartingAsync(context.Object);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(new FailedTestNodeStateProperty("first attempt"), attempt: 1, isSuperseded: true),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(PassedTestNodeStateProperty.CachedInstance, attempt: 2, isSuperseded: false),
                CancellationToken.None);
            await reporter.OnTestSessionFinishingAsync(context.Object);

            Assert.IsNotNull(artifact);
            var input = new InputArtifact(artifact.FileInfo.FullName, artifact.Kind, null, null, null, null);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [input],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            Assert.IsTrue(aggregate.Modules.Single().WriteOnFailureOnly);
            Assert.AreEqual("Tests.Flaky", aggregate.FlakyTests.Single().FullyQualifiedName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SessionFinishing_WhenFoldedUidHasFinalFailure_DoesNotMarkLaterPassAsFlakyAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-summary-folded-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new Mock<IConfiguration>();
            configuration.SetupGet(item => item[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(directory);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(Path.Combine(directory, "step-summary.md"));
            var moduleInfo = new Mock<ITestApplicationModuleInfo>();
            moduleInfo.Setup(item => item.TryGetAssemblyName()).Returns("Tests");
            moduleInfo.Setup(item => item.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(directory, "Tests.dll"));
            var coverage = new Mock<ITestCoverageResult>();
            coverage.SetupGet(item => item.Scopes).Returns([]);
            coverage.SetupGet(item => item.Thresholds).Returns([]);
            var messageBus = new Mock<IMessageBus>();
            SessionFileArtifact? artifact = null;
            messageBus
                .Setup(item => item.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, data) => artifact = (SessionFileArtifact)data)
                .Returns(Task.CompletedTask);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            GitHubActionsSummaryReporter reporter = new(
                new TestCommandLineOptions(new()
                {
                    [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
                }),
                configuration.Object,
                environment.Object,
                new SystemFileSystem(),
                messageBus.Object,
                Mock.Of<IOutputDevice>(),
                moduleInfo.Object,
                Mock.Of<ITestApplicationProcessExitCode>(),
                coverage.Object,
                loggerFactory.Object,
                static () => true);
            var context = new Mock<ITestSessionContext>();
            context.SetupGet(item => item.SessionUid).Returns(new SessionUid("session"));
            context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

            await reporter.OnTestSessionStartingAsync(context.Object);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(new FailedTestNodeStateProperty("superseded row"), attempt: 1, isSuperseded: true),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(new FailedTestNodeStateProperty("final failed row"), attempt: 2, isSuperseded: false),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(PassedTestNodeStateProperty.CachedInstance, attempt: 2, isSuperseded: false),
                CancellationToken.None);
            await reporter.OnTestSessionFinishingAsync(context.Object);

            Assert.IsNotNull(artifact);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [new InputArtifact(artifact.FileInfo.FullName, artifact.Kind, null, null, null, null)],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            Assert.IsEmpty(aggregate.FlakyTests);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void BuildMarkdown_AllPassing_UsesSuccessIconAndTotals()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Add", "CalculatorTests.Add", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Sub", "CalculatorTests.Sub", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(20)),
            new("Skip", "CalculatorTests.Skip", GitHubActionsTerminalKind.Skipped, TimeSpan.Zero),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", SuccessExitCode);

        Assert.Contains("## ✅ Test Run Summary — CalculatorTests (net9.0)", markdown);
        Assert.Contains("| 3 | 2 | 0 | 1 | 0 | 30ms |", markdown);
        Assert.DoesNotContain("### ❌ Failures", markdown);
        Assert.DoesNotContain("### ⚠️ Flaky tests", markdown);
        Assert.DoesNotContain("[!WARNING]", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_FlakyTest_IsCountedAndListed()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Flaky", "CalculatorTests.Flaky", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10), isFlaky: true),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", SuccessExitCode);

        Assert.Contains("| 1 | 1 | 0 | 0 | 1 | 10ms |", markdown);
        Assert.Contains("### ⚠️ Flaky tests (1)", markdown);
        Assert.Contains("- `CalculatorTests.Flaky`", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_ManyFlakyTests_TruncatesList()
    {
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(1, 21).Select(index =>
                new GitHubActionsTestRecord(
                    $"Flaky{index}",
                    $"CalculatorTests.Flaky{index}",
                    GitHubActionsTerminalKind.Passed,
                    TimeSpan.Zero,
                    isFlaky: true)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", SuccessExitCode);

        Assert.Contains("### ⚠️ Flaky tests (21)", markdown);
        Assert.Contains("- … and 1 more", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailures_UsesFailureIconAndListsFailures()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Pass", "StringUtilsTests.Pass", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(5)),
            new("Boom", "StringUtilsTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(7)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "StringUtilsTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("## ❌ Test Run Summary — StringUtilsTests (net9.0)", markdown);
        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("- `StringUtilsTests.Boom`", markdown);

        // A plain "at least one test failed" outcome is conveyed by the failures list, not an exit-code callout.
        Assert.DoesNotContain("[!WARNING]", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_EmitsSlowestTestsSortedByDuration()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Fast", "T.Fast", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Slow", "T.Slow", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(65)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", SuccessExitCode);

        Assert.Contains("### ⏱ Slowest tests", markdown);
        int slowIndex = markdown.IndexOf("- `T.Slow` — 1m 05s", StringComparison.Ordinal);
        int fastIndex = markdown.IndexOf("- `T.Fast` — 10ms", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, slowIndex, markdown);
        Assert.IsGreaterThanOrEqualTo(0, fastIndex, markdown);

        // Slowest-first ordering: the slow test must be listed before the fast one, i.e. at a smaller index.
        // IsLessThan(upperBound, value) asserts value < upperBound, so this asserts slowIndex < fastIndex.
        Assert.IsLessThan(fastIndex, slowIndex, markdown);
    }

    [TestMethod]
    public void BuildMarkdown_TestResultsOnly_OmitsSlowTestsSection()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Pass", "T.Pass", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(70)),
            new("Fail", "T.Fail", GitHubActionsTerminalKind.Failed, TimeSpan.FromSeconds(5)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(
            records,
            "T",
            "net9.0",
            AtLeastOneTestFailedExitCode,
            GitHubActionsStepSummarySections.TestResults);

        Assert.Contains("| 2 | 1 | 1 | 0 | 0 | 1m 15s |", markdown);
        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("`T.Fail`", markdown);
        Assert.DoesNotContain("### ⏱ Slowest tests", markdown);
        Assert.DoesNotContain("`T.Pass`", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_SlowTestsOnly_OmitsTestResultsSection()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Slow", "T.Slow", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(65)),
            new("Fail", "T.Fail", GitHubActionsTerminalKind.Failed, TimeSpan.FromSeconds(5)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(
            records,
            "T",
            "net9.0",
            ZeroTestsExitCode,
            GitHubActionsStepSummarySections.SlowTests);

        Assert.Contains("## ❌ Test Run Summary — T (net9.0)", markdown);
        Assert.Contains("### ⏱ Slowest tests", markdown);
        Assert.Contains("`T.Slow` — 1m 05s", markdown);
        Assert.DoesNotContain("| Total | Passed | Failed | Skipped | Flaky | Duration |", markdown);
        Assert.DoesNotContain("### ❌ Failures", markdown);
        Assert.DoesNotContain("[!WARNING]", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_DefaultSections_IncludeTestResultsAndSlowTests()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Slow", "T.Slow", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(65)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", SuccessExitCode);

        Assert.Contains("| 1 | 1 | 0 | 0 | 0 | 1m 05s |", markdown);
        Assert.Contains("### ⏱ Slowest tests", markdown);
        Assert.Contains("`T.Slow` — 1m 05s", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_NoTests_StillEmitsHeaderAndZeroTotals()
    {
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown([], "Empty", "net9.0", SuccessExitCode);

        Assert.Contains("## ✅ Test Run Summary — Empty (net9.0)", markdown);
        Assert.Contains("| 0 | 0 | 0 | 0 | 0 | 0ms |", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_ZeroTestsExitCode_UsesFailureIconAndEmitsCallout()
    {
        // No failing tests, but the process exit code says the run failed because nothing ran.
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown([], "Empty", "net9.0", ZeroTestsExitCode);

        Assert.Contains("## ❌ Test Run Summary — Empty (net9.0)", markdown);
        Assert.Contains("> [!WARNING]", markdown);
        Assert.Contains("Exit code 8 — ZeroTests:", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_MinimumExpectedTestsExitCode_EmitsCalloutEvenWhenTestsPassed()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Add", "CalculatorTests.Add", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", MinimumExpectedTestsExitCode);

        // The single test passed, yet the run failed the minimum-expected-tests policy: icon and callout reflect it.
        Assert.Contains("## ❌ Test Run Summary — CalculatorTests (net9.0)", markdown);
        Assert.Contains("Exit code 9 — MinimumExpectedTestsPolicyViolation:", markdown);
        Assert.Contains("--minimum-expected-tests", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_HtmlEncodesModuleSummaryLabel()
    {
        var module = new GitHubCiRunSummaryModule
        {
            AssemblyName = "<h1>A&B</h1>",
            ModulePath = "A.dll",
            TargetFramework = "net9.0<&>",
            Architecture = "x64&arm64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            Coverage = new GitHubCiCoverageSummaryData
            {
                Metrics =
                [
                    new GitHubCiCoverageMetric
                    {
                        ScopeLevel = CoverageScopeLevel.Overall,
                        Metric = CoverageMetric.Branch,
                        ProducerId = "coverlet",
                        CoveredCount = 0,
                        CoverableCount = 0,
                    },
                ],
                Thresholds =
                [
                    new GitHubCiCoverageThreshold
                    {
                        ScopeLevel = CoverageScopeLevel.Overall,
                        Metric = CoverageMetric.Line,
                        ProducerId = "coverlet",
                        ActualPercentage = 82,
                        RequiredPercentage = 80,
                        HasCoverableData = true,
                        Passed = true,
                    },
                ],
                ReportingModuleCount = 1,
                TotalModuleCount = 1,
            },
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("<summary>&lt;h1&gt;A&amp;B&lt;/h1&gt; (net9.0&lt;&amp;&gt;, x64&amp;arm64)</summary>", markdown);
        Assert.DoesNotContain("<summary><h1>", markdown);
        Assert.Contains("| Overall | Branch | 0 | 0 | No data |", markdown);
        Assert.Contains("| &lt;h1&gt;A&amp;B&lt;/h1&gt; (net9.0&lt;&amp;&gt;) — Overall | Line | 82.0% | 80.0% | ✅ Passed |", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_PartialFailureUsesFailureIconAndDisambiguatesAttempts()
    {
        var first = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-1",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            FailedTests = 1,
            TotalTests = 1,
        };
        var second = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "other/Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-2",
            AttemptNumber = 2,
            ExitCode = SuccessExitCode,
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
            [first, second],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.MaximumFailedTests),
            totalTests: 1,
            passedTests: 0,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: true);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.StartsWith("## ❌ Overall Test Run Summary", markdown);
        Assert.Contains("attempt 1, session session-1", markdown);
        Assert.Contains("attempt 2, session session-2", markdown);
        Assert.Contains("This summary is partial because the test run was truncated.", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_UnionsSelectionsAcrossModules()
    {
        GitHubCiRunSummaryModule testResultsModule = CreateAggregateModule(
            "TestResults",
            ["test-results"],
            "TestResults.Slow");
        GitHubCiRunSummaryModule slowTestsModule = CreateAggregateModule(
            "SlowTests",
            ["slow-tests"],
            "SlowTests.Slow");
        testResultsModule.FlakyTests =
        [
            new GitHubCiRunSummaryTest
            {
                DisplayName = "Flaky",
                FullyQualifiedName = "TestResults.Flaky",
            },
        ];
        GitHubCiRunSummaryAggregate aggregate = CreateAggregate([testResultsModule, slowTestsModule]);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Duration |", markdown);
        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Test duration |", markdown);
        Assert.Contains("#### ⚠️ Flaky tests (1)", markdown);
        Assert.Contains("#### ⏱ Slowest tests", markdown);
        Assert.Contains("`TestResults.Slow` — 2.00s", markdown);
        Assert.Contains("`SlowTests.Slow` — 2.00s", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_CoverageOnly_OmitsOtherSections()
    {
        GitHubCiRunSummaryModule module = CreateAggregateModule(
            "Coverage",
            ["coverage"],
            "Coverage.Slow");
        module.Coverage = CreateCoverageSummary();
        GitHubCiRunSummaryAggregate aggregate = CreateAggregate([module]);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("### Code coverage", markdown);
        Assert.Contains("#### Code coverage", markdown);
        Assert.Contains("| Overall | Line | 80 | 100 | 80.0% |", markdown);
        Assert.DoesNotContain("| Total | Passed | Failed | Skipped | Flaky | Duration |", markdown);
        Assert.DoesNotContain("| Total | Passed | Failed | Skipped | Flaky | Test duration |", markdown);
        Assert.DoesNotContain("Slowest tests", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_LegacyModuleWithoutSelection_DefaultsToAll()
    {
        GitHubCiRunSummaryModule legacyModule = CreateAggregateModule(
            "Legacy",
            sections: null,
            slowTestName: "Legacy.Slow");
        legacyModule.Coverage = CreateCoverageSummary();
        GitHubCiRunSummaryAggregate aggregate = CreateAggregate([legacyModule]);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Duration |", markdown);
        Assert.Contains("### Code coverage", markdown);
        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Test duration |", markdown);
        Assert.Contains("#### ⏱ Slowest tests", markdown);
        Assert.Contains("`Legacy.Slow` — 2.00s", markdown);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_WritesContent_OnFirstAttempt()
    {
        var buffer = new MemoryStream();
        Mock<IFileSystem> fileSystem = CreateFileSystemWritingTo(buffer);

        await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "hello world", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None);

        // UTF8Encoding(false) is used by the reporter, so there is no BOM to strip.
        Assert.AreEqual("hello world", Encoding.UTF8.GetString(buffer.ToArray()));
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Once);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_RetriesOnSharingViolation_ThenSucceeds()
    {
        var buffer = new MemoryStream();
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(buffer);

        var fileSystem = new Mock<IFileSystem>();
        // First open loses the race against another process (sharing violation), the second one wins.
        fileSystem.SetupSequence(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("The process cannot access the file because it is being used by another process."))
            .Returns(fileStream.Object);

        await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "second-wins", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None);

        Assert.AreEqual("second-wins", Encoding.UTF8.GetString(buffer.ToArray()));
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Exactly(2));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_Rethrows_WhenAllAttemptsFail()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("locked"));

        // After exhausting the bounded attempts the final IOException propagates so the caller can surface its
        // best-effort warning rather than looping forever.
        await Assert.ThrowsExactlyAsync<IOException>(() => GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "never-written", maxAttempts: 3, retryDelay: TimeSpan.Zero, CancellationToken.None));

        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Exactly(3));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_DoesNotRetry_WhenWriteFailsAfterHandleAcquired()
    {
        // The handle is acquired successfully but the write/flush fails (e.g. disk full). Retrying would re-append
        // the full section on top of a partial one, so the failure must propagate after a single attempt.
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(new ThrowOnWriteStream());

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object);

        await Assert.ThrowsExactlyAsync<IOException>(() => GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "partial", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None));

        // Exactly one acquisition: a post-acquire write failure is not contention and must not be retried.
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Once);
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_ReplacesMatchingSection()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new SystemFileSystem();

            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "first", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);
            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "second", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);
            Assert.Contains("existing", summary);
            Assert.Contains("second", summary);
            Assert.DoesNotContain("first", summary);
            const string Marker = "microsoft-testing-platform:github-actions:run-1:start";
            int firstMarker = summary.IndexOf(Marker, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, firstMarker);
            Assert.AreEqual(-1, summary.IndexOf(Marker, firstMarker + Marker.Length, StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Mock<IFileSystem> CreateFileSystemWritingTo(Stream target)
    {
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(target);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object);
        return fileSystem;
    }

    private static TestNodeUpdateMessage CreateRetryUpdate(
        TestNodeStateProperty state,
        int attempt,
        bool isSuperseded)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "flaky",
                DisplayName = "Tests.Flaky",
                Properties = new PropertyBag(
                    state,
                    new RetryAttemptProperty(attempt, isSuperseded)),
            });

    private static GitHubCiRunSummaryModule CreateAggregateModule(
        string assemblyName,
        string[]? sections,
        string slowTestName)
        => new()
        {
            AssemblyName = assemblyName,
            ModulePath = $"{assemblyName}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = $"execution-{assemblyName}",
            SessionUid = $"session-{assemblyName}",
            AttemptNumber = 1,
            ExitCode = SuccessExitCode,
            TotalTests = 1,
            PassedTests = 1,
            TestDurationTicks = TimeSpan.FromSeconds(2).Ticks,
            SlowestTests =
            [
                new()
                {
                    DisplayName = slowTestName,
                    FullyQualifiedName = slowTestName,
                    DurationTicks = TimeSpan.FromSeconds(2).Ticks,
                },
            ],
            GitHubActionsStepSummarySections = sections,
        };

    private static GitHubCiRunSummaryModule CreateRetryModule(
        string sessionUid,
        int attempt,
        long passed,
        long failed)
        => new()
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = sessionUid,
            AttemptNumber = attempt,
            ExitCode = failed > 0 ? AtLeastOneTestFailedExitCode : SuccessExitCode,
            TotalTests = passed + failed,
            PassedTests = passed,
            FailedTests = failed,
        };

    private static GitHubCiCoverageSummaryData CreateCoverageSummary()
        => new()
        {
            Metrics =
            [
                new GitHubCiCoverageMetric
                {
                    ScopeLevel = CoverageScopeLevel.Overall,
                    Metric = CoverageMetric.Line,
                    ProducerId = "coverage",
                    CoveredCount = 80,
                    CoverableCount = 100,
                },
            ],
            ReportingModuleCount = 1,
            TotalModuleCount = 1,
        };

    private static GitHubCiRunSummaryAggregate CreateAggregate(IReadOnlyList<GitHubCiRunSummaryModule> modules)
        => new(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: modules.Sum(module => module.TotalTests),
            passedTests: modules.Sum(module => module.PassedTests),
            failedTests: modules.Sum(module => module.FailedTests),
            skippedTests: modules.Sum(module => module.SkippedTests),
            duration: TimeSpan.FromTicks(modules.Sum(module => module.TestDurationTicks)),
            exitCode: SuccessExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

    // A writable stream that fails on any attempt to write or flush, simulating a mid-write I/O error (e.g. disk full)
    // after the exclusive append handle has already been acquired.
    private sealed class ThrowOnWriteStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;

            // Position is not settable on this write-only, non-seekable test stream. The discard makes the
            // otherwise-ignored assigned value explicit so static analysis doesn't flag it.
            set => _ = value;
        }

        public override void Flush() => throw new IOException("There is not enough space on the disk.");

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("There is not enough space on the disk.");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

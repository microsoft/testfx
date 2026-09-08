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
using GitHubActionsTestFailureDetails = ghactions::Microsoft.Testing.Extensions.TestFailureDetails;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;
using GitHubCiCoverageMetric = ghactions::Microsoft.Testing.Extensions.CiCoverageMetric;
using GitHubCiCoverageSummaryData = ghactions::Microsoft.Testing.Extensions.CiCoverageSummaryData;
using GitHubCiCoverageThreshold = ghactions::Microsoft.Testing.Extensions.CiCoverageThreshold;
using GitHubCiRunSummaryAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubCiRunSummaryAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubCiRunSummaryHistoryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryHistoryTest;
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
            new SystemFileSystem(),
            Mock.Of<ILoggerFactory>(),
            static () => false);

        Assert.AreSequenceEqual(
            new[] { ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts },
            processor.SupportedModes);
    }

    [TestMethod]
    public async Task SummaryPostProcessor_HistoryOnlyWritesHistoryWithoutSummaryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-only-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubCiRunSummaryModule module = CreateRetryModule("session", attempt: 1, passed: 1, failed: 0);
            module.GitHubActionsStepSummaryEnabled = false;
            module.GitHubActionsHistoryPath = Path.Combine(directory, "history.json");
            module.GitHubActionsHistoryWindowInDays = 30;
            module.HistoryTests = [CreateHistoryTest("test", "Tests.Test", "passed")];
            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);
            var history = new CapturingHistoryService();
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                Mock.Of<IEnvironment>(),
                new SystemFileSystem(),
                Mock.Of<ILoggerFactory>(),
                static () => false,
                history);

            ProcessedArtifact? output = await processor.ProcessAsync(
                [new InputArtifact(fragmentPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, null)],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.HasCount(1, history.Writes);
            Assert.AreEqual("Tests.Test", history.Writes[0].Single().HistoryTests.Single().FullyQualifiedName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SummaryPostProcessor_RetryWithoutDownstreamWritesMergedHistoryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubCiRunSummaryModule first = CreateRetryModule("session-1", attempt: 1, passed: 0, failed: 1);
            first.GitHubActionsHistoryPath = Path.Combine(directory, "history.json");
            first.GitHubActionsHistoryWindowInDays = 30;
            first.HistoryTests = [CreateHistoryTest("flaky", "Tests.Flaky", "failed")];
            GitHubCiRunSummaryModule second = CreateRetryModule("session-2", attempt: 2, passed: 1, failed: 0);
            second.GitHubActionsHistoryPath = first.GitHubActionsHistoryPath;
            second.GitHubActionsHistoryWindowInDays = 30;
            second.HistoryTests = [CreateHistoryTest("flaky", "Tests.Flaky", "passed")];
            string firstPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory, GitHubSummaryPostProcessor.Provider, GitHubSummaryPostProcessor.ProviderSlug, first);
            string secondPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory, GitHubSummaryPostProcessor.Provider, GitHubSummaryPostProcessor.ProviderSlug, second);
            var history = new CapturingHistoryService();
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                Mock.Of<IEnvironment>(),
                new SystemFileSystem(),
                Mock.Of<ILoggerFactory>(),
                static () => false,
                history);

            await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1"),
                    new InputArtifact(secondPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts,
                    new ArtifactPostProcessingRunSummary(1, 1, 0, 0, TimeSpan.FromSeconds(1), 0, 1)),
                CancellationToken.None);

            GitHubCiRunSummaryHistoryTest persisted = history.Writes.Single().Single().HistoryTests.Single();
            Assert.AreEqual("passed", persisted.Outcome);
            Assert.IsTrue(persisted.IsFlaky);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_BoundsHistoryAcrossModulesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-fragments-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubCiRunSummaryModule first = CreateRetryModule("session-1", attempt: 1, passed: 1, failed: 0);
            first.ExecutionId = "first";
            first.HistoryTests =
            [
                .. Enumerable.Range(0, 6_000).Select(index =>
                    CreateHistoryTest($"first-{index}", $"Tests.First{index}", "passed")),
            ];
            GitHubCiRunSummaryModule second = CreateRetryModule("session-2", attempt: 1, passed: 1, failed: 0);
            second.ExecutionId = "second";
            second.HistoryTests =
            [
                .. Enumerable.Range(0, 6_000).Select(index =>
                    CreateHistoryTest($"second-{index}", $"Tests.Second{index}", "passed")),
            ];
            string firstPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory, GitHubSummaryPostProcessor.Provider, GitHubSummaryPostProcessor.ProviderSlug, first);
            string secondPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory, GitHubSummaryPostProcessor.Provider, GitHubSummaryPostProcessor.ProviderSlug, second);

            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [
                    new InputArtifact(firstPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "first"),
                    new InputArtifact(secondPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "second"),
                ],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            Assert.AreEqual(10_000, aggregate.Modules.Sum(module => module.HistoryTests.Length));
            Assert.HasCount(6_000, aggregate.Modules[0].HistoryTests);
            Assert.HasCount(4_000, aggregate.Modules[1].HistoryTests);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
            first.HistoryTests =
            [
                CreateHistoryTest("stable", "Tests.Stable", "passed"),
                CreateHistoryTest("flaky", "Tests.Flaky", "failed"),
            ];
            first.TestDurationTicks = TimeSpan.FromMilliseconds(100).Ticks;
            GitHubCiRunSummaryModule retry = CreateRetryModule("session-2", attempt: 2, passed: 1, failed: 0);
            retry.FlakyTests = [flakyTest];
            retry.HistoryTests = [CreateHistoryTest("flaky", "Tests.Flaky", "passed")];
            retry.TestDurationTicks = TimeSpan.FromMilliseconds(200).Ticks;
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
                new SystemFileSystem(),
                Mock.Of<ILoggerFactory>(),
                static () => false);
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
            Assert.HasCount(2, aggregate.Modules.Single().HistoryTests);
            GitHubCiRunSummaryHistoryTest flakyHistory =
                aggregate.Modules.Single().HistoryTests.Single(test => test.FullyQualifiedName == "Tests.Flaky");
            Assert.IsTrue(flakyHistory.IsFlaky);
            GitHubCiRunSummaryHistoryTest stableHistory =
                aggregate.Modules.Single().HistoryTests.Single(test => test.FullyQualifiedName == "Tests.Stable");
            Assert.AreEqual("passed", stableHistory.Outcome);
            Assert.IsFalse(stableHistory.IsFlaky);
            Assert.AreEqual(TimeSpan.FromMilliseconds(300).Ticks, aggregate.Modules.Single().TestDurationTicks);
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
    public async Task SummaryPostProcessor_ForRetryAttemptsWithoutAuthoritativeSummary_FailsClosed()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-summary-unknown-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubCiRunSummaryModule module = CreateRetryModule("session-1", attempt: 1, passed: 1, failed: 1);
            module.GitHubActionsHistoryPath = Path.Combine(directory, "history.json");
            module.GitHubActionsHistoryWindowInDays = 30;
            module.HistoryTests = [CreateHistoryTest("skipped", "Tests.Skipped", "skipped")];
            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);
            string stepSummaryPath = Path.Combine(directory, "step-summary.md");
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(stepSummaryPath);
            var history = new CapturingHistoryService();
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                environment.Object,
                new SystemFileSystem(),
                Mock.Of<ILoggerFactory>(),
                static () => false,
                history);

            ProcessedArtifact? output = await processor.ProcessAsync(
                [new InputArtifact(fragmentPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1")],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.IsFalse(File.Exists(stepSummaryPath));
            GitHubCiRunSummaryModule persisted = history.Writes.Single().Single();
            Assert.AreEqual(module.GitHubActionsHistoryPath, persisted.GitHubActionsHistoryPath);
            Assert.AreEqual("Tests.Skipped", persisted.HistoryTests.Single().FullyQualifiedName);
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
            GitHubSummaryPostProcessor processor = new(
                new TestCommandLineOptions([]),
                environment.Object,
                new SystemFileSystem(),
                Mock.Of<ILoggerFactory>(),
                static () => true);

            ProcessedArtifact? output = await processor.ProcessAsync(
                [new InputArtifact(fragmentPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1")],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts,
                    new ArtifactPostProcessingRunSummary(
                        totalTests: 1,
                        passedTests: 1,
                        failedTests: 0,
                        skippedTests: 0,
                        duration: TimeSpan.FromSeconds(1),
                        exitCode: 0,
                        testModuleCount: 1)),
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
                static () => true,
                new CapturingHistoryService(isEnabled: true));
            var context = new Mock<ITestSessionContext>();
            context.SetupGet(item => item.SessionUid).Returns(new SessionUid("session"));
            context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

            await reporter.OnTestSessionStartingAsync(context.Object);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(new FailedTestNodeStateProperty("superseded row"), attempt: 1, isSuperseded: true, displayName: "Tests.SharedTitle"),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(PassedTestNodeStateProperty.CachedInstance, attempt: 2, isSuperseded: false, displayName: "Tests.SharedTitle"),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(PassedTestNodeStateProperty.CachedInstance, attempt: 2, isSuperseded: false, displayName: "Tests.SharedTitle"),
                CancellationToken.None);
            await reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateRetryUpdate(new FailedTestNodeStateProperty("final failed row"), attempt: 2, isSuperseded: false, displayName: "Tests.SharedTitle"),
                CancellationToken.None);
            await reporter.OnTestSessionFinishingAsync(context.Object);

            Assert.IsNotNull(artifact);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [new InputArtifact(artifact.FileInfo.FullName, artifact.Kind, null, null, null, null)],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            Assert.IsEmpty(aggregate.FlakyTests);
            Assert.AreEqual(3, aggregate.TotalTests);
            Assert.AreEqual(2, aggregate.PassedTests);
            Assert.AreEqual(1, aggregate.FailedTests);
            Assert.AreEqual("Tests.SharedTitle", aggregate.Modules.Single().Failures.Single().FullyQualifiedName);
            GitHubCiRunSummaryHistoryTest history = aggregate.Modules.Single().HistoryTests.Single();
            Assert.AreEqual("failed", history.Outcome);
            Assert.AreEqual("flaky", history.TestId);
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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

        Assert.StartsWith("## ❌ Overall Test Run Summary", markdown);
        Assert.Contains("attempt 1, session session-1", markdown);
        Assert.Contains("attempt 2, session session-2", markdown);
        Assert.Contains("This summary is partial because the test run was truncated.", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailureDetails_RendersCollapsibleSection()
    {
        var failure = new GitHubActionsTestFailureDetails(
            "Expected: 42\nActual:   41",
            "System.Exception",
            "   at Calc.Add() in Calc.cs:line 42",
            "src/Calc.cs",
            42);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(2400), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("<details>\n<summary><code>CalcTests.Boom</code> — 2.40s</summary>", markdown);
        Assert.Contains("**Exception:** `System.Exception`", markdown);
        Assert.Contains("**Location:** `src/Calc.cs:42`", markdown);

        // Assert the whole fenced block, not the message and stack trace separately: they have to land inside one
        // code block. Rendered outside it, an assertion diff's leading spaces and angle brackets would be eaten as
        // markdown, and a stack frame would fold onto the line above.
        Assert.Contains(
            "```text\nExpected: 42\nActual:   41\n\n   at Calc.Add() in Calc.cs:line 42\n```",
            markdown);
        Assert.Contains("</details>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailureDetailsDisabled_KeepsCompactFailureList()
    {
        var failure = new GitHubActionsTestFailureDetails("boom", "System.Exception", "at X()", "src/X.cs", 3);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(120), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: false);

        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("- `CalcTests.Boom` — 120ms", markdown);
        Assert.DoesNotContain("<details>", markdown);
        Assert.DoesNotContain("**Exception:**", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_FailureWithoutDetails_FallsBackToCompactLine()
    {
        // A framework that reports a failure without an explanation, exception or location has nothing to expand.
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(5)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("- `CalcTests.Boom` — 5ms", markdown);
        Assert.DoesNotContain("<details>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_HtmlEncodesFailureNameInSummaryElement()
    {
        // A generic test name would otherwise be parsed as an HTML tag and swallow the rest of the <summary> line.
        var failure = new GitHubActionsTestFailureDetails("boom", null, null, null, 0);
        GitHubActionsTestRecord[] records =
        [
            new("Map", "T.Map<string,int>", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("<code>T.Map&lt;string,int&gt;</code>", markdown);
        Assert.DoesNotContain("<code>T.Map<string,int></code>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_FailureMessageContainingCodeFence_DoesNotBreakOutOfTheBlock()
    {
        // A message that itself contains a ``` fence must not terminate the block we open around it.
        var failure = new GitHubActionsTestFailureDetails("before\n```\ninjected\n```\nafter", null, null, null, 0);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "T.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        // The fence has to survive intact, and so does the collapsible wrapper around it: a body that closed our
        // fence early would leave the block open and swallow everything after it.
        Assert.Contains("<details>\n<summary><code>T.Boom</code> — ", markdown);
        Assert.Contains("</summary>\n\n````text\n", markdown);
        Assert.Contains("injected", markdown);
        Assert.Contains("\n````\n\n</details>\n", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_TruncatesFailureList_AndSaysSo()
    {
        // 25 failures exceed the 20-failure cap, so the summary must state that the list was truncated.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 25).Select(i =>
                new GitHubActionsTestRecord($"T{i}", $"T.Test{i}", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1))),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("### ❌ Failures (25)", markdown);
        Assert.Contains("Showing the first 20 of 25 failed tests", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_DegradesOversizedFailuresToNamedLines_KeepingEveryFailureListed()
    {
        // Every failure carries a stack trace far larger than the supplied budget, so only the first few can be
        // expanded. The rest must degrade to a named line rather than disappearing: which tests failed is the
        // part a reader cannot reconstruct, and it costs a line where the diagnostics cost kilobytes.
        string hugeStackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 100), GitHubActionsFailureDetails.MaxStackTraceRows));
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 20).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", hugeStackTrace, null, 0))),
        ];

        // Room for roughly two of the ~3.2 KB blocks, so the remaining failures must degrade.
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(7000));

        Assert.Contains("<details>", markdown);
        Assert.IsLessThan(records.Length, CountOccurrences(markdown, "<details>"));

        // Every failure is still named, expanded or not.
        foreach (GitHubActionsTestRecord record in records)
        {
            Assert.Contains(record.FullyQualifiedName, markdown);
        }
    }

    [TestMethod]
    public void BuildMarkdown_ExhaustedBudget_ListsFailuresAndSaysTheirDetailsWereOmitted()
    {
        // With no budget at all the section is a plain list. It must still say why the diagnostics are missing,
        // so a bare list is not mistaken for failures that had nothing more to show.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 3).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", "at T.Test()", null, 0))),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(0));

        Assert.DoesNotContain("<details>", markdown);
        Assert.Contains("Failure details for 3 listed test(s) were omitted", markdown);
        Assert.Contains("T.Test0", markdown);
        Assert.Contains("T.Test2", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_SharesTheBudgetAcrossFailures_SoASmallerBudgetExpandsFewer()
    {
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 100), 20));
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 10).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", stackTrace, null, 0))),
        ];

        int generous = CountOccurrences(
            GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(100_000)),
            "<details>");
        int tight = CountOccurrences(
            GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(5_000)),
            "<details>");

        Assert.AreEqual(10, generous);
        Assert.IsLessThan(generous, tight);
    }

    [TestMethod]
    public void Clip_ManyShortRows_IsTruncatedByRowCountEvenWhenUnderTheCharacterLimit()
    {
        // 200 one-word frames are only ~2,600 characters — under the character cap — yet far too long to read.
        // The row limit is what bounds this shape.
        string manyRows = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"at Frame{i}()"));
        Assert.IsLessThan(GitHubActionsFailureDetails.MaxStackTraceLength, manyRows.Length, "The input must be under the character cap for this test to be meaningful.");

        string clipped = GitHubActionsFailureDetails.Clip(manyRows, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows)!;

        Assert.Contains("[... truncated]", clipped);

        // The kept rows plus the truncation marker.
        Assert.HasCount(GitHubActionsFailureDetails.MaxStackTraceRows + 1, clipped.Split('\n'));
        Assert.Contains("at Frame0()", clipped);
        Assert.DoesNotContain("at Frame199()", clipped);
    }

    [TestMethod]
    public void Clip_RowCountUnderLimit_IsNotMarkedTruncated()
    {
        string fewRows = "line 1\nline 2\nline 3";

        string clipped = GitHubActionsFailureDetails.Clip(fewRows, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows)!;

        Assert.AreEqual(fewRows, clipped);
        Assert.DoesNotContain("[... truncated]", clipped);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_EmptyFile_ReturnsFullBudget()
        => Assert.AreEqual(GitHubActionsFailureDetails.MaxTotalDetailsLength, SummaryBudget.ForProject(0).DetailBytesAvailable);

    [TestMethod]
    public void GetRemainingDetailsBudget_ExistingContent_IsSubtracted()
    {
        // A sibling test project already wrote to the shared summary file; this project may only claim the rest.
        const int AlreadyWritten = 100_000;

        int budget = SummaryBudget.ForProject(AlreadyWritten).DetailBytesAvailable;

        Assert.AreEqual(GitHubActionsFailureDetails.MaxTotalDetailsLength - AlreadyWritten, budget);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_FileAlreadyOverBudget_ReturnsZero()
    {
        int budget = SummaryBudget.ForProject(GitHubActionsFailureDetails.MaxTotalDetailsLength + 1).DetailBytesAvailable;

        // Never negative: the caller uses this as a length, and a later project simply renders compact lines.
        Assert.AreEqual(0, budget);
    }

    [TestMethod]
    public void Clip_LoneCarriageReturns_AreCountedAsRows()
    {
        // A \r still renders as a line break, so leaving it unnormalized would let a \r-separated message defeat
        // the row cap entirely: Split('\n') sees one row where the reader sees hundreds.
        string manyRows = string.Join("\r", Enumerable.Range(0, 200).Select(i => $"at Frame{i}()"));

        string clipped = GitHubActionsFailureDetails.Clip(manyRows, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows)!;

        Assert.Contains("[... truncated]", clipped);
        Assert.HasCount(GitHubActionsFailureDetails.MaxStackTraceRows + 1, clipped.Split('\n'));
    }

    [TestMethod]
    public void AppendFailuresSection_ChargesTheBudgetInBytes_NotCharacters()
    {
        // The budget is denominated in UTF-8 bytes because that is what GitHub counts. Charging UTF-16 chars
        // would under-bill a failure carrying Japanese text by roughly threefold, so a project would overshoot
        // its share and have the whole rendering refused rather than degrading gracefully.
        GitHubActionsTestRecord[] records =
        [
            new GitHubActionsTestRecord(
                "失敗",
                "テスト.失敗",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails(new string('あ', 400), "System.Exception", null, null, 0)),
        ];

        // Comfortably over the UTF-16 length of the block, but under its UTF-8 size: a char-charged budget would
        // expand it, a byte-charged one must not.
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(700));

        Assert.DoesNotContain("<details>", markdown);
        Assert.Contains("テスト.失敗", markdown);
    }

    [TestMethod]
    public void TryGetFailureInfo_ReadsTheExplanationAndException_FromEveryFailingStateShape()
    {
        // The summary is built from whatever these arms return, and nothing else drives them, so an arm that
        // silently stopped returning diagnostics would leave every rendering test passing.
        var exception = new InvalidOperationException("boom");

        AssertCarries(new FailedTestNodeStateProperty(exception, "failed explanation"), "failed explanation");
        AssertCarries(new ErrorTestNodeStateProperty(exception, "error explanation"), "error explanation");
        AssertCarries(new TimeoutTestNodeStateProperty(exception, "timeout explanation"), "timeout explanation");
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
        AssertCarries(new CancelledTestNodeStateProperty(exception, "cancelled explanation"), "cancelled explanation");
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete

        // A passing test carries nothing to report.
        Assert.IsNull(GitHubActionsSummaryReporter.TryGetFailureInfo(new PassedTestNodeStateProperty()));
        Assert.IsNull(GitHubActionsSummaryReporter.TryGetFailureInfo(null));

        void AssertCarries(TestNodeStateProperty state, string expectedExplanation)
        {
            (string? Explanation, Exception? Exception)? info = GitHubActionsSummaryReporter.TryGetFailureInfo(state);
            if (info is not { } failureInfo)
            {
                Assert.Fail($"Expected {state.GetType().Name} to carry failure info.");
                return;
            }

            Assert.AreEqual(expectedExplanation, failureInfo.Explanation);
            Assert.AreSame(exception, failureInfo.Exception);
        }
    }

    [TestMethod]
    public async Task AppendRenderedStepSummarySectionAsync_RendersAgainstTheLengthObservedUnderTheLock()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new string('a', 5_000));
            var fileSystem = new SystemFileSystem();
            long observed = -1;

            await NewWriter(fileSystem, path, 1).AppendRenderedStepSummarySectionAsync(
                currentLength =>
                {
                    observed = currentLength;
                    return ("## Section\n", false);
                },
                GitHubActionsSummaryReporter.BuildTruncationNotice,
                CancellationToken.None);

            // The rendering decision has to see the same length the write lands on. Measuring before taking the
            // lock would let every project finishing at the same moment observe an empty file and each render a
            // full section, so the cap would admit the first few and refuse the rest outright.
            Assert.AreEqual(5_000, observed);
            Assert.EndsWith("## Section\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void EffectiveStepSummaryLimit_IsSlightlyBelowTheDocumentedLimit()
    {
        // GitHubStepSummaryLimit is defined as 1024 * 1024, GitHub's documented cap. Measured on ubuntu-latest: a
        // summary of exactly 1 MiB is accepted, and 1,148,551 bytes is rejected outright with
        // "$GITHUB_STEP_SUMMARY upload aborted" — GitHub discards the whole file rather than truncating it. The
        // small margin below the documented limit guards the near-boundary rejection reported in
        // actions/runner#4337, which did not reproduce but costs two bytes to defend against.
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, GitHubActionsFailureDetails.EffectiveStepSummaryLimit);

        // The margin must stay negligible: a large one would silently cost users summary content for no reason.
        Assert.IsLessThan(1024, GitHubActionsFailureDetails.GitHubStepSummaryLimit - GitHubActionsFailureDetails.EffectiveStepSummaryLimit);
    }

    [TestMethod]
    public void DegradationThresholds_AreOrderedWithHeadroom()
    {
        // The two degradation thresholds must be ordered and must both leave headroom below the point of no
        // return, since co-writer output keeps accumulating after this reporter has degraded.
        Assert.IsLessThan(GitHubActionsFailureDetails.CondenseLength, GitHubActionsFailureDetails.DetailBudgetLength);
        Assert.IsLessThan(GitHubActionsFailureDetails.EffectiveStepSummaryLimit, GitHubActionsFailureDetails.CondenseLength);
    }

    [TestMethod]
    public void DegradationThresholds_ShedDiagnosticsBeforeWholeSections()
    {
        // The order of the two thresholds is the whole design: diagnostics are kilobytes per failure and go
        // first, while the list of which tests failed is a line each and survives until whole sections have to
        // go. Reversing them would drop the names of failing tests while still expanding stack traces.
        Assert.IsLessThan(
            GitHubActionsFailureDetails.CondenseLength,
            GitHubActionsFailureDetails.MaxTotalDetailsLength);

        // And that ordering has to hold in the rendering, not just between the constants. At a budget past the
        // detail allowance but short of the condense point, the failing test must still be named while its
        // diagnostics are gone — the reader keeps what tells them which test broke.
        GitHubActionsTestRecord[] records =
        [
            new(
                "Boom",
                "T.Boom",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("kaboom", "System.Exception", "at T.Boom()", null, 0)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, budget: BudgetOf(0));

        Assert.Contains("T.Boom", markdown);
        Assert.DoesNotContain("<details>", markdown);
        Assert.DoesNotContain("kaboom", markdown);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_FileOverGitHubLimit_ReturnsZero()
        // A file this large is already beyond saving: GitHub will discard it. The budget must bottom out at zero
        // rather than going negative, which is what stops this project expanding into a file already lost.
        => Assert.AreEqual(0, SummaryBudget.ForProject(GitHubActionsFailureDetails.EffectiveStepSummaryLimit + 1024).DetailBytesAvailable);

    [TestMethod]
    public void BuildMinimalMarkdown_IsSmallEnoughThatTheProjectedSizeGateIsMeaningful()
    {
        // The overflow gate compares current file length + the rendered markdown against the limit. That is only
        // a useful last line of defence if the condensed form is genuinely small: a multi-kilobyte "minimal"
        // section would be refused so often that projects near the limit would report nothing at all.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 500).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"Some.Very.Long.Namespace.And.Class.Name.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails(new string('x', 5000), "System.Exception", new string('y', 5000), "src/F.cs", 1))),
        ];

        string minimal = GitHubActionsSummaryReporter.BuildMinimalMarkdown(records, "Asm", "net9.0", AtLeastOneTestFailedExitCode);

        // Independent of test count and of how large the individual failures are.
        Assert.IsLessThan(1024, minimal.Length, minimal);
        Assert.Contains("500 total", minimal);
        Assert.Contains("condensed to one line", minimal);
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(200)]
    [DataRow(600)]
    [DataRow(2000)]
    [DataRow(5000)]
    public void BuildAggregateMarkdown_ScalesWithModuleCount_WithoutExceedingTheCap(int moduleCount)
    {
        // The aggregated path divides its detail budget by module count, so expanded diagnostics cannot grow
        // without bound. Each module's heading, totals table and failure list are written regardless, though, and
        // that per-module overhead is what a large run accumulates — the reason to check the rendered size at
        // module counts a big repository would actually reach, not just at the handful a unit test is tempted to
        // use. Exceeding GitHub's cap costs the entire summary, not its tail.
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 120), 25));
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, moduleCount).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 20,
            FailedTests = 20,
            Failures =
            [
                .. Enumerable.Range(0, 20).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"Boom{j}",
                    FullyQualifiedName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}.SomeFixtureName.Boom{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = stackTrace,
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: moduleCount * 20,
            passedTests: 0,
            failedTests: moduleCount * 20,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult result = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
        string markdown = result.Markdown;
        int omitted = result.ModulesWithOmittedDetails;
        string notice = omitted > 0 ? GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(omitted, moduleCount) : string.Empty;

        // Measured in bytes: the cap GitHub enforces is on the file, and a char count understates any summary
        // carrying non-ASCII text — which this one does, if only through its per-module status emoji.
        int byteCount = Encoding.UTF8.GetByteCount(markdown) + Encoding.UTF8.GetByteCount(notice);
        Assert.IsLessThan(
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit,
            byteCount,
            $"A {moduleCount}-module run renders {byteCount} bytes, which GitHub discards in full.");

        if (moduleCount >= 200)
        {
            // Staying under the cap is not on its own evidence that the budget did anything: a regression that
            // stopped degrading but happened to fit would pass the size check alone. At these counts at least one
            // stage has to have engaged.
            Assert.IsGreaterThan(
                0,
                result.ModulesWithOmittedDetails + result.CondensedModules + result.UnlistedModules,
                $"A {moduleCount}-module run must exercise at least one degradation stage.");
        }
    }

    [TestMethod]
    public void BuildAggregateMarkdown_ManyModules_StaysUnderTheLimitAndReportsOmittedModules()
    {
        // 40 modules, each with failures large enough that the shared budget cannot expand them all. The
        // file-level note must say so, because a per-module note is invisible inside a collapsed section.
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 120), 25));
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 40).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Tests{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 20,
            FailedTests = 20,
            Failures =
            [
                .. Enumerable.Range(0, 20).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"Boom{j}",
                    FullyQualifiedName = $"Tests{i}.Boom{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = stackTrace,
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 800,
            passedTests: 0,
            failedTests: 800,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult result = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
        string markdown = result.Markdown;
        int modulesWithOmittedDetails = result.ModulesWithOmittedDetails;

        // The whole point of the shared budget: the file stays under GitHub's cap no matter the module count.
        // Measured in UTF-8 bytes, which is what the budget counts and what GitHub weighs — a char count would
        // understate a summary carrying non-ASCII assertion diffs by up to three times.
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, Encoding.UTF8.GetByteCount(markdown));

        // The shortfall is reported to the caller rather than buried at the end of the block, so it can be stated
        // at the top of the file where the reader will actually see it. The per-module notes inside each module's
        // section stay where they are.
        Assert.IsGreaterThan(0, modulesWithOmittedDetails);
        Assert.DoesNotContain("test project(s) because", markdown);

        string notice = GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(modulesWithOmittedDetails, modules.Length);
        Assert.Contains("take up too much space", notice);
        Assert.DoesNotContain("job summary size limit was reached", notice);
    }

    [TestMethod]
    public void TruncationNotices_KeepTheMarkerOnALineOfItsOwn()
    {
        // Consumers match the marker as a whole line — this repo's own validation workflow greps
        // '^<!-- ...summary-truncated -->$'. Anything appended to that line, such as the strength token, silently
        // stops the notice being recognised, so the token belongs on the next line.
        foreach (string[] lines in new[]
        {
            GitHubActionsSummaryReporter.BuildTruncationNotice(3),
            GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5),
        }.Select(static notice => notice.Split('\n')))
        {
            Assert.AreEqual(GitHubActionsSummaryReporter.TruncationNoticeMarker, lines[0]);

            // The strength token goes on its own line. Appending it to the marker's line would silently break
            // every consumer that greps for the marker as a whole line, which is how the validation workflow
            // and anything else scanning the summary matches it.
            Assert.DoesNotContain("truncation-strength", lines[0]);
            Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeEndMarker, lines);
        }
    }

    [TestMethod]
    public async Task TruncationNotices_ShareOneMarker_SoASummaryCannotCarryTwoWarnings()
    {
        // The per-project and aggregated writing modes describe different losses, and only one of them runs in a
        // given test process — but a workflow can mix them across steps. They share a marker so the reader meets
        // one warning rather than two contradictory ones, which only means anything if writing both really does
        // leave one behind.
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Empty);
            var fileSystem = new SystemFileSystem();

            // The weaker "failure details were omitted" note, then the stronger "whole sections were removed" one.
            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                "first\n",
                static _ => GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5),
                CancellationToken.None);
            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                "second\n",
                static _ => GitHubActionsSummaryReporter.BuildTruncationNotice(3),
                CancellationToken.None);

            string summary = File.ReadAllText(path);

            Assert.AreEqual(1, CountOccurrences(summary, GitHubActionsSummaryReporter.TruncationNoticeMarker));
            Assert.AreEqual(1, CountOccurrences(summary, GitHubActionsSummaryReporter.TruncationNoticeEndMarker));

            // The one left is the stronger, and neither section was lost to the replacement.
            Assert.AreEqual(GitHubActionsSummaryReporter.SectionsRemovedNoticeStrength, StepSummaryWriter.GetLeadingNoticeStrength(summary));
            Assert.Contains("first", summary);
            Assert.Contains("second", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [TestMethod]
    public void Clip_LongValue_TruncatesAndMarksIt()
    {
        string clipped = GitHubActionsFailureDetails.Clip(new string('a', 100), maxLength: 10)!;

        Assert.StartsWith("aaaaaaaaaa", clipped);
        Assert.Contains("[... truncated]", clipped);
    }

    [TestMethod]
    public void Clip_AtSupplementaryCharacterBoundary_DoesNotSplitSurrogatePair()
    {
        string clipped = GitHubActionsFailureDetails.Clip(new string('a', 9) + "\U0001F600tail", maxLength: 10)!;

        Assert.StartsWith(new string('a', 9) + "\n", clipped);
        Assert.DoesNotContain(static character => char.IsSurrogate(character), clipped);
        Assert.Contains("[... truncated]", clipped);
    }

    [TestMethod]
    public void Clip_ShortOrEmptyValue_IsReturnedAsIsOrNull()
    {
        Assert.AreEqual("short", GitHubActionsFailureDetails.Clip("short", maxLength: 10));
        Assert.IsNull(GitHubActionsFailureDetails.Clip("   ", maxLength: 10));
        Assert.IsNull(GitHubActionsFailureDetails.Clip(null, maxLength: 10));
    }

    [TestMethod]
    public void BuildAggregateMarkdown_RendersFailureDetailsFromFragment()
    {
        var module = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 1,
            FailedTests = 1,
            Failures =
            [
                new GitHubCiRunSummaryTest
                {
                    DisplayName = "Boom",
                    FullyQualifiedName = "T.Boom",
                    DurationTicks = TimeSpan.FromSeconds(2).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = "at T.Boom()",
                    FilePath = "src/T.cs",
                    LineNumber = 7,
                },
            ],
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
            [module],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 1,
            passedTests: 0,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(2),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

        Assert.Contains("<summary><code>T.Boom</code> — 2.00s</summary>", markdown);
        Assert.Contains("**Exception:** `System.Exception`", markdown);
        Assert.Contains("**Location:** `src/T.cs:7`", markdown);
        Assert.Contains("assertion failed", markdown);
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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;

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

        await NewWriter(fileSystem.Object, "summary.md", 5).AppendStepSummaryWithRetryAsync("hello world", CancellationToken.None);

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

        await NewWriter(fileSystem.Object, "summary.md", 5).AppendStepSummaryWithRetryAsync("second-wins", CancellationToken.None);

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
        await Assert.ThrowsExactlyAsync<IOException>(() => NewWriter(fileSystem.Object, "summary.md", 3).AppendStepSummaryWithRetryAsync("never-written", CancellationToken.None));

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

        await Assert.ThrowsExactlyAsync<IOException>(() => NewWriter(fileSystem.Object, "summary.md", 5).AppendStepSummaryWithRetryAsync("partial", CancellationToken.None));

        // Exactly one acquisition: a post-acquire write failure is not contention and must not be retried.
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Once);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_RefusesTheWrite_WhenItWouldCrossTheCap()
    {
        // The gate lives here rather than in the caller: two sibling projects that each measured the file before
        // acquiring the lock would both see the same length, both conclude they fit, and both append — landing
        // over GitHub's cap, which costs the whole summary rather than one section.
        var buffer = new MemoryStream();
        buffer.SetLength(90);
        buffer.Seek(0, SeekOrigin.End);
        Mock<IFileSystem> fileSystem = CreateFileSystemWritingTo(buffer);

        bool written = await NewWriter(fileSystem.Object, "summary.md", 5).AppendStepSummaryWithRetryAsync("0123456789", CancellationToken.None, maxTotalBytes: 99);

        Assert.IsFalse(written);
        Assert.HasCount(90, buffer.ToArray(), "Nothing may be appended once the write is refused.");

        // One byte of headroom is enough: the gate refuses only what would actually cross the limit.
        Assert.IsTrue(await NewWriter(fileSystem.Object, "summary.md", 5).AppendStepSummaryWithRetryAsync("0123456789", CancellationToken.None, maxTotalBytes: 100));
        Assert.HasCount(100, buffer.ToArray());
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_LeavesTheFileUntouched_WhenTheResultWouldCrossTheCap()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new SystemFileSystem();

            bool written = await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", "a section that does not fit", CancellationToken.None, leadingNoticeFactory: null, maxTotalBytes: 16);

            Assert.IsFalse(written);

            // Replacing the file with one GitHub would discard in full is worse than writing nothing: everything
            // other steps already wrote survives only if we leave it alone.
            Assert.AreEqual("existing\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void BuildAggregateMarkdown_CondenseAllModules_RendersOnlyVerdictLines()
    {
        // The fallback the post-processor takes when the full rendering is refused: the smallest report that still
        // names every test project.
        var aggregate = new GitHubCiRunSummaryAggregate(
            [
                new GitHubCiRunSummaryModule
                {
                    AssemblyName = "Tests",
                    ModulePath = "Tests.dll",
                    TargetFramework = "net9.0",
                    Architecture = "x64",
                    SessionUid = "session",
                    AttemptNumber = 1,
                    ExitCode = AtLeastOneTestFailedExitCode,
                    TotalTests = 2,
                    FailedTests = 1,
                    PassedTests = 1,
                    Failures =
                    [
                        new GitHubCiRunSummaryTest
                        {
                            DisplayName = "Boom",
                            FullyQualifiedName = "Tests.Boom",
                            DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                            ErrorMessage = "assertion failed",
                            ErrorType = "System.Exception",
                        },
                    ],
                },
            ],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 2,
            passedTests: 1,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult result = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, condenseAllModules: true);
        string markdown = result.Markdown;
        int condensedModules = result.CondensedModules;

        Assert.AreEqual(1, condensedModules);
        Assert.Contains("❌ `Tests` (net9.0): 2 total", markdown);
        Assert.DoesNotContain("assertion failed", markdown);
        Assert.DoesNotContain("Tests.Boom", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_CondenseAllModules_StillStopsListing_WhenEvenVerdictLinesWouldOverflow()
    {
        // This rendering is the fallback the post-processor reaches *after* the full one was refused for size.
        // If it rendered a verdict line per project without bound, a large enough run would have both renderings
        // refused and contribute nothing at all — the outcome the stop-listing bound exists to prevent.
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 6000).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 5,
            FailedTests = 5,
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 30000,
            passedTests: 0,
            failedTests: 30000,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult result = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, condenseAllModules: true);

        int byteCount = Encoding.UTF8.GetByteCount(result.Markdown);
        Assert.IsGreaterThan(0, result.UnlistedModules, "The fallback must stop listing, or it is unbounded.");
        Assert.IsLessThan(
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit,
            byteCount,
            $"The condensed fallback renders {byteCount} bytes, which GitHub discards in full.");
        Assert.Contains("further test project(s) are not listed", result.Markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_AccountsForWhatTheSharedFileAlreadyHolds()
    {
        // The rendering is appended to a file other steps also write to, and it is that file GitHub measures.
        // A budget that only bounded this section's own size would let a job whose earlier steps already wrote
        // several hundred kilobytes produce a section that is refused — including the condensed fallback, which
        // would leave the run contributing nothing at all.
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 400).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 5,
            FailedTests = 5,
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 2000,
            passedTests: 0,
            failedTests: 2000,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult onEmptyFile = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
        GitHubActionsSummaryReporter.AggregateRenderResult onFullFile = GitHubActionsSummaryReporter.BuildAggregateMarkdown(
            aggregate,
            includeFailureDetails: true,
            condenseAllModules: false,
            alreadyWrittenBytes: GitHubActionsFailureDetails.StopListingLength);

        // Same run, but appended to a nearly-full file: the rendering must shrink rather than ignore the file.
        Assert.AreEqual(0, onEmptyFile.UnlistedModules);
        Assert.IsGreaterThan(0, onFullFile.UnlistedModules);

        // Shrinking alone would not say the budget engaged for the right reason, so assert the degradation
        // counters moved too, and that the reader is told the listing stopped.
        Assert.IsGreaterThan(
            onEmptyFile.CondensedModules + onEmptyFile.UnlistedModules,
            onFullFile.CondensedModules + onFullFile.UnlistedModules);
        Assert.Contains("further test project(s) are not listed", onFullFile.Markdown);

        Assert.IsLessThan(
            Encoding.UTF8.GetByteCount(onEmptyFile.Markdown),
            Encoding.UTF8.GetByteCount(onFullFile.Markdown));

        // And the whole file — what is already there plus what we add — stays under the cap.
        Assert.IsLessThan(
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit,
            GitHubActionsFailureDetails.StopListingLength + Encoding.UTF8.GetByteCount(onFullFile.Markdown));
    }

    [TestMethod]
    public void BuildAggregateMarkdown_StaysUnderTheCap_WhenTheContentIsNonAscii()
    {
        // The cap GitHub enforces is on bytes, and this content is the non-ASCII-heavy kind: a UTF-16 char count
        // understates a summary of Japanese test names by threefold, which is enough to render a file GitHub
        // discards while every char-based check reports it as comfortably within budget. The module count is high
        // enough to exhaust the listing bound too, so the tail of the run is reported as a count rather than as
        // one line per project — the only rendering whose size does not grow with the run.
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 3000).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"テストアセンブリの名前{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 5,
            FailedTests = 5,
            Failures =
            [
                .. Enumerable.Range(0, 5).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"失敗したテスト{j}",
                    FullyQualifiedName = $"テストアセンブリの名前{i}.とても長い名前空間.失敗したテスト{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "アサーションが失敗しました。期待値と実際の値が一致しません。",
                    ErrorType = "System.Exception",
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 15000,
            passedTests: 0,
            failedTests: 15000,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubActionsSummaryReporter.AggregateRenderResult result = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
        string markdown = result.Markdown;
        int condensedModules = result.CondensedModules;
        int unlistedModules = result.UnlistedModules;

        int byteCount = Encoding.UTF8.GetByteCount(markdown);
        Assert.IsGreaterThan(0, condensedModules, "This many modules must exhaust the budget, or the test is not exercising the bound.");
        Assert.IsGreaterThan(0, unlistedModules, "This many modules must exhaust the listing bound, or the test is not exercising it.");
        Assert.IsLessThan(
            GitHubActionsFailureDetails.GitHubStepSummaryLimit,
            byteCount,
            $"A 3000-module run renders {byteCount} bytes, which GitHub discards in full.");

        // Silently stopping the listing would leave a reader believing the run had only the projects shown.
        Assert.Contains("further test project(s) are not listed", markdown);

        // And "not listed" has to mean it: the tail of the run must genuinely be absent, not merely condensed,
        // while the head is still rendered in full. Pinning both sides is what makes this a bound rather than
        // just "something was dropped".
        Assert.Contains("失敗したテスト0", markdown, "The first module is inside the bound, so its failures are still listed.");
        Assert.DoesNotContain("テストアセンブリの名前2999", markdown, "The last module falls past the listing bound, so it must not be rendered at all.");
    }

    [TestMethod]
    public async Task CreateModule_ThenFragmentRoundTrip_KeepsFailureDiagnostics_AndOmitsThemFromSlowestTests()
    {
        // The other aggregate tests hand-build CiRunSummaryTest instances, so none of them exercises the
        // TestRecord -> CiRunSummaryTest conversion or the JSON fragment the deferred path actually writes.
        // Without this test, dropping the diagnostics from either would leave every test passing while real
        // multi-project runs silently lost the failure details this PR exists to render.
        string resultsDirectory = Path.Combine(Path.GetTempPath(), "mtp-fragment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDirectory);
        try
        {
            GitHubActionsTestRecord[] records =
            [
                new GitHubActionsTestRecord(
                    "Boom",
                    "T.Boom",
                    GitHubActionsTerminalKind.Failed,
                    TimeSpan.FromSeconds(9),
                    new GitHubActionsTestFailureDetails("assertion failed", "System.InvalidOperationException", "   at T.Boom()", "src/T.cs", 42)),
            ];

            GitHubCiRunSummaryModule module = GitHubCiRunSummaryAggregation.CreateModule(
                records,
                "Tests",
                Path.Combine(resultsDirectory, "Tests.dll"),
                "net9.0",
                "x64",
                executionId: "execution",
                sessionUid: "session",
                attemptNumber: 1,
                exitCode: AtLeastOneTestFailedExitCode);

            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(resultsDirectory, "github-actions", "github-actions", module);

            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [new InputArtifact(fragmentPath, "microsoft.testing.github-actions-summary-fragment", Path.Combine(resultsDirectory, "Tests.dll"), "net9.0", "x64", "execution")],
                "github-actions",
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            GitHubCiRunSummaryTest failure = aggregate.Modules.Single().Failures.Single();
            Assert.AreEqual("assertion failed", failure.ErrorMessage);
            Assert.AreEqual("System.InvalidOperationException", failure.ErrorType);
            Assert.AreEqual("   at T.Boom()", failure.StackTrace);
            Assert.AreEqual("src/T.cs", failure.FilePath);
            Assert.AreEqual(42, failure.LineNumber);

            // The same test also qualifies as a slowest test. Carrying its stack trace there too would duplicate
            // potentially large diagnostics in every fragment for no rendering benefit.
            GitHubCiRunSummaryTest slowest = aggregate.Modules.Single().SlowestTests.Single();
            Assert.AreEqual("T.Boom", slowest.FullyQualifiedName);
            Assert.IsNull(slowest.StackTrace);
            Assert.IsNull(slowest.ErrorMessage);

            // Rendering it end to end is what proves the diagnostics survived in a usable shape.
            string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;
            Assert.Contains("**Exception:** `System.InvalidOperationException`", markdown);
            Assert.Contains("**Location:** `src/T.cs:42`", markdown);
            Assert.Contains("assertion failed", markdown);
        }
        finally
        {
            Directory.Delete(resultsDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void HasLeadingTruncationNotice_IgnoresTheMarker_WhenItAppearsInsideFailureDiagnostics()
    {
        // Failure messages are copied verbatim into the summary, so a test whose output contains the marker text
        // must not be mistaken for the notice — that would suppress the real warning and leave a shortened
        // summary that never says it was shortened.
        Assert.IsTrue(StepSummaryWriter.HasLeadingTruncationNotice(GitHubActionsSummaryReporter.BuildTruncationNotice(3)));
        Assert.IsFalse(StepSummaryWriter.HasLeadingTruncationNotice(
            $"## Tests\n\n```text\nExpected the summary to contain {GitHubActionsSummaryReporter.TruncationNoticeMarker}\n```\n"));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_StillHoistsTheNotice_WhenAFailureMessageContainsTheMarker()
    {
        string path = Path.GetTempFileName();
        try
        {
            // A previously written section whose rendered failure message happens to carry the marker text.
            File.WriteAllText(path, $"## Tests\n\n```text\nexpected {GitHubActionsSummaryReporter.TruncationNoticeMarker}\n```\n");
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync("## More\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);

            string summary = File.ReadAllText(path);
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);

            // The hoist rewrites the file, so the section that merely looked like a marker has to come through it
            // unharmed — both the appended content and the fenced text that provoked the ambiguity. That fenced
            // copy is why this test cannot assert a single occurrence of the marker: two is correct here, and only
            // one of them is a notice.
            Assert.Contains("## More", summary);
            Assert.Contains($"expected {GitHubActionsSummaryReporter.TruncationNoticeMarker}", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_UpgradesAWeakerNotice_WhenSectionsAreRemoved()
    {
        string path = Path.GetTempFileName();
        try
        {
            // An aggregate step already said "diagnostics were dropped". A later project whose whole section is
            // condensed describes a worse loss: results are missing, not merely shortened. The weaker note must
            // not suppress it, or the summary never says anything is gone.
            File.WriteAllText(path, GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5) + "## Earlier\n");
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                "## Later\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);

            string summary = File.ReadAllText(path);
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.AreEqual(
                GitHubActionsSummaryReporter.SectionsRemovedNoticeStrength,
                StepSummaryWriter.GetLeadingNoticeStrength(summary));

            // Exactly one notice survives: upgrading replaces, it does not stack.
            AssertSingleNotice(summary);
            Assert.Contains("## Earlier", summary);
            Assert.Contains("## Later", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_KeepsTheStrongerNotice_WhenAWeakerOneFollows()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, GitHubActionsSummaryReporter.BuildTruncationNotice(3) + "## Earlier\n");
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                "## Later\n", static _ => GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(1, 4), CancellationToken.None);

            string summary = File.ReadAllText(path);
            Assert.AreEqual(
                GitHubActionsSummaryReporter.SectionsRemovedNoticeStrength,
                StepSummaryWriter.GetLeadingNoticeStrength(summary));
            AssertSingleNotice(summary);

            // The replacement must not eat either section, and the weaker note's own wording must be gone rather
            // than left stranded below the stronger one.
            Assert.Contains("## Earlier", summary);
            Assert.Contains("## Later", summary);
            Assert.DoesNotContain("take up too much space", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CountProjectSections_IgnoresTheMarker_WhenAFailureBodyPrintsItOnItsOwnLine()
    {
        // A failure message is copied verbatim into a fenced block, so a test can print the marker on a line of
        // its own. Counting it would inflate the project count the truncation note reports, on user-controlled
        // input.
        string summary =
            GitHubActionsSummaryReporter.ProjectSectionMarker + "\n## Real project\n\n"
            + "```text\n"
            + GitHubActionsSummaryReporter.ProjectSectionMarker + "\n"
            + "   at T.Boom()\n"
            + "```\n";

        Assert.AreEqual(1, StepSummaryWriter.CountProjectSections(summary));
    }

    [TestMethod]
    public void CountProjectSections_CountsMarkersAfterAFenceCloses()
    {
        // The scan must resume after the fence, or a project section following a failure body would be lost.
        string summary =
            GitHubActionsSummaryReporter.ProjectSectionMarker + "\n"
            + "```text\n" + GitHubActionsSummaryReporter.ProjectSectionMarker + "\n```\n"
            + GitHubActionsSummaryReporter.ProjectSectionMarker + "\n";

        Assert.AreEqual(2, StepSummaryWriter.CountProjectSections(summary));
    }

    [TestMethod]
    public void CountProjectSections_HandlesLongerFences()
    {
        // The renderer picks a fence longer than the longest backtick run in the body, so a shorter run inside
        // must not close it early.
        string summary =
            GitHubActionsSummaryReporter.ProjectSectionMarker + "\n"
            + "````text\n```\n" + GitHubActionsSummaryReporter.ProjectSectionMarker + "\n````\n";

        Assert.AreEqual(1, StepSummaryWriter.CountProjectSections(summary));
    }

    [TestMethod]
    public void TrimToRenderedFailures_KeepsOnlyTheFailuresThatWillBeRendered()
    {
        // A run can fail thousands of tests while only MaxFailures are ever expanded. Retention has to be bounded
        // to those, or a high-failure run holds diagnostics for output it discards.
        var pending = new Dictionary<string, GitHubActionsSummaryReporter.PendingFailure>(StringComparer.Ordinal);
        for (int i = 0; i < 500; i++)
        {
            string name = $"T.Test{i:D4}";
            pending[name] = new GitHubActionsSummaryReporter.PendingFailure(name, name, "boom", new InvalidOperationException("boom"), null, 0);
            GitHubActionsSummaryReporter.TrimToRenderedFailures(pending, 20);

            // Bounded at every step, not just at the end — the point is never to hold more than this.
            Assert.IsLessThanOrEqualTo(20, pending.Count);
        }

        // And the ones kept are those that sort first, which is the order the summary renders in.
        Assert.AreSequenceEqual(
            [.. Enumerable.Range(0, 20).Select(static i => $"T.Test{i:D4}")],
            [.. pending.Values.Select(static f => f.FullyQualifiedName).OrderBy(static name => name, StringComparer.Ordinal)]);
    }

    [TestMethod]
    public void TrimToRenderedFailures_BreaksNameTiesByUid()
    {
        // Distinct tests can share both names — duplicate parameterized cases most obviously. The retained set
        // and the rendered set are chosen by two different code paths (this trim, and the snapshot's sort), so
        // unless the order is total they can disagree about which of the tied failures is inside the cap. The
        // loser would then render with no diagnostics and no note saying why.
        var pending = new Dictionary<string, GitHubActionsSummaryReporter.PendingFailure>(StringComparer.Ordinal);
        foreach (string uid in new[] { "uid-c", "uid-a", "uid-d", "uid-b" })
        {
            pending[uid] = new GitHubActionsSummaryReporter.PendingFailure("T.Same", "Same", "boom", new InvalidOperationException("boom"), null, 0);
        }

        GitHubActionsSummaryReporter.TrimToRenderedFailures(pending, 2);

        // Ordinal by uid, so the survivors are decidable rather than whatever the dictionary happened to yield.
        Assert.AreSequenceEqual(["uid-a", "uid-b"], [.. pending.Keys.OrderBy(static key => key, StringComparer.Ordinal)]);
    }

    [TestMethod]
    public void ApplyPendingFailure_ReleasesTheSlotWhenARetryRecovers()
    {
        // In-process retries reuse the UID, so a test that failed and then passed must not keep occupying one of
        // the retained slots — doing so would evict a test that is still failing.
        var pending = new Dictionary<string, GitHubActionsSummaryReporter.PendingFailure>(StringComparer.Ordinal);
        static GitHubActionsSummaryReporter.PendingFailure Failure(string name)
            => new(name, name, "boom", new InvalidOperationException("boom"), null, 0);

        // Two failures, room for exactly two.
        GitHubActionsSummaryReporter.ApplyPendingFailure(pending, "uid-a", Failure("T.A"), 2);
        GitHubActionsSummaryReporter.ApplyPendingFailure(pending, "uid-b", Failure("T.B"), 2);
        Assert.HasCount(2, pending);

        // "T.A" is retried and passes: no failure details this time round.
        GitHubActionsSummaryReporter.ApplyPendingFailure(pending, "uid-a", null, 2);
        Assert.HasCount(1, pending);
        Assert.IsFalse(pending.ContainsKey("uid-a"));

        // So a test that really is still failing keeps its diagnostics instead of being crowded out by the
        // recovered one, which sorts first and would otherwise have won the slot.
        GitHubActionsSummaryReporter.ApplyPendingFailure(pending, "uid-c", Failure("T.C"), 2);
        Assert.AreSequenceEqual(
            ["T.B", "T.C"],
            [.. pending.Values.Select(static f => f.FullyQualifiedName).OrderBy(static name => name, StringComparer.Ordinal)]);
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_ReplacesMatchingSection()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", "first", CancellationToken.None);
            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", "second", CancellationToken.None);

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

    [TestMethod]
    public void BuildTruncationNotice_SaysHowManyProjectsGotTheirResultsIn()
    {
        string notice = GitHubActionsSummaryReporter.BuildTruncationNotice(7);

        Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeMarker, notice);
        Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeEndMarker, notice);
        Assert.Contains("shortened", notice);
        // The count is the point: it tells the reader how much of the report they can trust as complete.
        Assert.Contains("7", notice);
        // The limit is quoted in round units, not to the byte — the exact figure is noise to the reader.
        Assert.Contains("1 MB", notice);
        Assert.DoesNotContain(GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture), notice);
    }

    [TestMethod]
    public void CountProjectSections_CountsOnlyFullSections()
    {
        string full = GitHubActionsSummaryReporter.BuildMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);
        string condensed = GitHubActionsSummaryReporter.BuildMinimalMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);

        Assert.AreEqual(0, StepSummaryWriter.CountProjectSections(condensed));
        Assert.AreEqual(2, StepSummaryWriter.CountProjectSections(full + condensed + full));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_HoistsNoticeToTheTop_AndAppendsContentAfterIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "earlier-project\n");
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync("first-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);
            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync("second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);

            string summary = File.ReadAllText(path);

            // The warning leads the report, so a reader meets it before the sections it is warning about, and the
            // content that was already there keeps its order behind it.
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.IsLessThan(
                summary.IndexOf("first-project", StringComparison.Ordinal),
                summary.IndexOf("earlier-project", StringComparison.Ordinal));
            Assert.IsLessThan(
                summary.IndexOf("second-project", StringComparison.Ordinal),
                summary.IndexOf("first-project", StringComparison.Ordinal));
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_KeepsNoticeFirst_WhenACoWriterAppendsAfterIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Empty);
            var fileSystem = new SystemFileSystem();

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync("first-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);

            // This extension is not the only writer to GITHUB_STEP_SUMMARY: a test framework appends its own block
            // after the reporter runs. Appending cannot dislodge a note that sits at the top, which is the reason
            // it goes there rather than at the end.
            File.AppendAllText(path, "### framework's own section\n");

            await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync("second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None);

            string summary = File.ReadAllText(path);

            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.Contains("framework's own section", summary);
            Assert.Contains("second-project", summary);
            // The note is stated once. Repeating it per project would spend the little headroom that is left on
            // restating the same sentence.
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_PutsTheNoticeFirst_AndNeverAddsASecondOne()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "earlier content\n");
            var fileSystem = new SystemFileSystem();
            string notice = GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5);

            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", "aggregate block", CancellationToken.None, _ => notice);

            string summary = File.ReadAllText(path);
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.Contains("earlier content", summary);
            Assert.Contains("aggregate block", summary);
            AssertSingleNotice(summary);

            // Re-running the same aggregation replaces its block; the warning must not be duplicated with it.
            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", "aggregate block v2", CancellationToken.None, _ => notice);

            summary = File.ReadAllText(path);
            Assert.Contains("aggregate block v2", summary);
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CountProjectSections_IgnoresTheMarkerInsideTestOutput()
    {
        // A failing test's name and message are rendered into the summary verbatim, so a test that mentions the
        // marker would otherwise be counted as a project section and inflate the number the warning quotes.
        string full = GitHubActionsSummaryReporter.BuildMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);

        string withMarkerInProse = full + $"- `SomeTest_Mentioning_{GitHubActionsSummaryReporter.ProjectSectionMarker}_InItsName` — 1ms\n";

        Assert.AreEqual(1, StepSummaryWriter.CountProjectSections(withMarkerInProse));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_LeavesTheSummaryIntact_WhenTheRewriteFailsPartway()
    {
        // Hoisting the notice replaces the whole file. If that were done by truncating in place, a failure partway
        // through — a full disk on a runner, or cancellation during session teardown — would leave the summary
        // empty, losing every section earlier projects wrote. Failing must cost this project's section, never the
        // file, so the write is staged elsewhere and only swapped in once complete.
        string dir = Path.Combine(Path.GetTempPath(), "mtp-summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.md");
        const string Original = "earlier-project-section\n";
        try
        {
            File.WriteAllText(path, Original);
            var real = new SystemFileSystem();

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns<string>(File.Exists);
            fileSystem.Setup(f => f.ReplaceFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>(real.ReplaceFile);
            fileSystem.Setup(f => f.DeleteFile(It.IsAny<string>())).Callback<string>(real.DeleteFile);
            fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                .Returns<string, FileMode, FileAccess, FileShare>((p, mode, access, share) =>
                {
                    // Everything behaves normally except the staged copy, which fails mid-write.
                    if (p.EndsWith(".tmp", StringComparison.Ordinal))
                    {
                        var throwing = new Mock<IFileStream>();
                        throwing.Setup(s => s.Stream).Returns(new ThrowOnWriteStream());
                        return throwing.Object;
                    }

                    return real.NewFileStream(p, mode, access, share);
                });

            await Assert.ThrowsExactlyAsync<IOException>(() =>
                NewWriter(fileSystem.Object, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                    "second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, CancellationToken.None));

            // The pre-existing content is still there: the failed write never touched the summary itself.
            Assert.AreEqual(Original, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void AssertSingleNotice(string summary)
    {
        string marker = GitHubActionsSummaryReporter.TruncationNoticeMarker;
        int first = summary.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, first);
        Assert.AreEqual(-1, summary.IndexOf(marker, first + marker.Length, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_DoesNotOverwriteAForeignAppendWhenAttemptsRunOut()
    {
        // The summary handle has to be released before the staged replacement can be swapped in, and another
        // writer can append in that gap. Once retries are exhausted the swap must be abandoned rather than
        // carried out against a stale snapshot: an understated leading notice is recoverable, a deleted block
        // written by someone else is not.
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new InterferingFileSystem(path, "foreign\n");

            bool written = await NewWriter(fileSystem, path, 2)
                .AppendStepSummaryWithLeadingNoticeAsync("mine\n", static _ => GitHubActionsSummaryReporter.BuildTruncationNotice(3), CancellationToken.None);

            Assert.IsTrue(written);
            Assert.IsGreaterThan(0, fileSystem.Interferences, "The test is vacuous unless the foreign append actually landed.");

            string summary = File.ReadAllText(path);
            Assert.Contains("existing", summary);
            Assert.Contains("mine", summary);
            Assert.AreEqual(
                fileSystem.Interferences,
                summary.Split(["foreign"], StringSplitOptions.None).Length - 1,
                "Every foreign append has to survive.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_DoesNotOverwriteAForeignAppendWhenAttemptsRunOut()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new InterferingFileSystem(path, "foreign\n");

            await Assert.ThrowsExactlyAsync<IOException>(() => NewWriter(fileSystem, path, 2)
                .UpsertStepSummaryWithRetryAsync("run-1", "mine\n", CancellationToken.None));

            Assert.IsGreaterThan(0, fileSystem.Interferences, "The test is vacuous unless the foreign append actually landed.");

            string summary = File.ReadAllText(path);
            Assert.Contains("existing", summary);
            Assert.DoesNotContain("mine", summary);
            Assert.AreEqual(
                fileSystem.Interferences,
                summary.Split(["foreign"], StringSplitOptions.None).Length - 1,
                "Every foreign append has to survive.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_ExcludesForeignWritersThroughReplacement()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var inner = new SystemFileSystem();
            bool appendWasBlocked = false;

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns<string>(inner.ExistFile);
            fileSystem.Setup(f => f.DeleteFile(It.IsAny<string>())).Callback<string>(inner.DeleteFile);
            fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                .Returns<string, FileMode, FileAccess, FileShare>(inner.NewFileStream);
            fileSystem.Setup(f => f.ReplaceFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((source, destination) =>
                {
                    try
                    {
                        File.AppendAllText(path, "foreign\n");
                    }
                    catch (IOException)
                    {
                        appendWasBlocked = true;
                    }

                    inner.ReplaceFile(source, destination);
                });

            bool written = await NewWriter(fileSystem.Object, path, 1)
                .UpsertStepSummaryWithRetryAsync("run-1", "mine\n", CancellationToken.None);

            Assert.IsTrue(written);
            Assert.IsTrue(appendWasBlocked, "A foreign append must be excluded until the replacement completes.");

            // A co-writer that retries after the exclusive validation handle is released appends to the new file.
            File.AppendAllText(path, "foreign\n");
            string summary = File.ReadAllText(path);
            Assert.Contains("existing", summary);
            Assert.Contains("mine", summary);
            Assert.Contains("foreign", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummary_CountsOtherWritersSections_ButNotItsOwn()
    {
        // A job can mix the two writing modes across steps — `dotnet test` aggregating in one, a standalone test
        // executable writing directly in another — into one summary file. The note counts how much of that file is
        // fully reported, so it has to see the sections the direct path wrote. It must not also see this run's own
        // modules, which the caller adds itself: on a re-run the file still holds the previous copy of this
        // section, and counting those markers too would report the run's projects twice.
        string path = Path.GetTempFileName();
        try
        {
            // Two sections as the direct per-project path leaves them.
            File.WriteAllText(path, GitHubActionsSummaryReporter.ProjectSectionMarker + "\nA\n\n" + GitHubActionsSummaryReporter.ProjectSectionMarker + "\nB\n");
            var fileSystem = new SystemFileSystem();

            // Three full modules, marked the way the aggregate rendering marks them.
            string aggregateBlock = string.Concat(Enumerable.Repeat(GitHubActionsSummaryReporter.ProjectSectionMarker + "\nmodule\n\n", 3));

            int observed = -1;
            Func<int, string> factory = count =>
            {
                observed = count;
                return GitHubActionsSummaryReporter.BuildTruncationNotice(count + 3);
            };

            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", aggregateBlock, CancellationToken.None, factory);
            Assert.AreEqual(2, observed, "The two sections already in the file have to be counted.");

            string summary = File.ReadAllText(path);
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);

            // A later direct writer sees this run's modules too, which is why they are marked at all.
            Assert.AreEqual(5, StepSummaryWriter.CountProjectSections(summary));

            // Re-running the same aggregation must not count its own previous modules on top of the ones the
            // caller adds — the file already holds them.
            observed = -1;
            await NewWriter(fileSystem, path, 1).UpsertStepSummaryWithRetryAsync("run-1", aggregateBlock, CancellationToken.None, factory);
            Assert.AreEqual(2, observed, "This run's own section must be excised before counting.");
            Assert.AreEqual(5, StepSummaryWriter.CountProjectSections(File.ReadAllText(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_RefusesBeforeReadingAFileAlreadyOverTheBound()
    {
        // The file is shared with producers this extension does not control, so its size decides how much memory
        // this process would allocate to read it. Nothing this method writes can fit once the existing content
        // alone is over the bound, so it has to refuse without reading — the alternative is letting another
        // producer's output size an allocation big enough to take the test host down.
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new string('x', 4096));
            var fileSystem = new SystemFileSystem();

            bool written = await NewWriter(fileSystem, path, 1).AppendStepSummaryWithLeadingNoticeAsync(
                "## Mine\n",
                static _ => GitHubActionsSummaryReporter.BuildTruncationNotice(3),
                CancellationToken.None,
                maxTotalBytes: 1024);

            Assert.IsFalse(written);
            Assert.AreEqual(4096, new FileInfo(path).Length, "A refused write leaves the file untouched.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void GetSummaryLengthExcludingSection_ReportsTheRawLength_WithoutReadingAnOversizedFile()
    {
        // Discounting our own section requires reading the file, whose size other producers control. Past the
        // ceiling this must report the length rather than read — the caller then sees no room and degrades, which
        // is the outcome that keeps the run alive. The stream throws on read, so a regression fails loudly.
        var stream = new LengthOnlyStream(128L * 1024 * 1024 * 1024);
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(stream);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, It.IsAny<FileShare>()))
            .Returns(fileStream.Object);

        long measured = NewWriter(fileSystem.Object, "summary.md", 1).GetSummaryLengthExcludingSection("run-1");

        Assert.AreEqual(128L * 1024 * 1024 * 1024, measured);
    }

    /// <summary>
    /// Reports a length but refuses to be read, so a test can assert that a size guard runs before any read.
    /// </summary>
    private sealed class LengthOnlyStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; } = length;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("The file must not be read once it is over the size ceiling.");

        public override long Seek(long offset, SeekOrigin origin) => 0;

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static StepSummaryWriter NewWriter(IFileSystem fileSystem, string path, int maxAttempts)
        => new(fileSystem, path, new Mock<ILogger>().Object, maxAttempts, TimeSpan.Zero);

    /// <summary>
    /// A budget whose detail allowance is exactly <paramref name="detailBytes"/>, so a test can pin the one
    /// number it is exercising without depending on the file-size arithmetic.
    /// </summary>
    private static SummaryBudget BudgetOf(int detailBytes)
        => SummaryBudget.ForProject(GitHubActionsFailureDetails.DetailBudgetLength - GitHubActionsFailureDetails.ProjectOverheadReserve - detailBytes);

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
        bool isSuperseded,
        string displayName = "Tests.Flaky")
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "flaky",
                DisplayName = displayName,
                Properties = new PropertyBag(
                    state,
                    new RetryAttemptProperty(attempt, isSuperseded)),
            });

    /// <summary>
    /// The real file system, plus another writer appending to the summary in the window between staging the
    /// replacement and swapping it in — the window this extension's own lock cannot cover, because a file that is
    /// open cannot be replaced.
    /// </summary>
    private sealed class InterferingFileSystem(string summaryPath, string foreignAppend) : IFileSystem
    {
        private readonly SystemFileSystem _inner = new();

        public int Interferences { get; private set; }

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            IFileStream stream = _inner.NewFileStream(path, mode, access, share);
            if (path.EndsWith(".tmp", StringComparison.Ordinal) && mode == FileMode.CreateNew)
            {
                // The replacement is being staged, so the summary handle is already released. Land an append now.
                File.AppendAllText(summaryPath, foreignAppend);
                Interferences++;
            }

            return stream;
        }

        public bool ExistFile(string path) => _inner.ExistFile(path);

        public bool ExistDirectory(string? path) => _inner.ExistDirectory(path);

        public string CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false)
            => _inner.MoveFile(sourceFileName, destFileName, overwrite);

        public void ReplaceFile(string sourceFileName, string destFileName)
            => _inner.ReplaceFile(sourceFileName, destFileName);

        public IFileStream NewFileStream(string path, FileMode mode) => _inner.NewFileStream(path, mode);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access)
            => _inner.NewFileStream(path, mode, access);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public Task<string> ReadAllTextAsync(string path) => _inner.ReadAllTextAsync(path);

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
            => _inner.CopyFile(sourceFileName, destFileName, overwrite);

        public void DeleteFile(string path) => _inner.DeleteFile(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
            => _inner.GetFiles(path, searchPattern, searchOption);
    }

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

    private static GitHubCiRunSummaryHistoryTest CreateHistoryTest(string id, string fullyQualifiedName, string outcome)
        => new()
        {
            TestId = id,
            DisplayName = fullyQualifiedName,
            FullyQualifiedName = fullyQualifiedName,
            Outcome = outcome,
            DurationTicks = TimeSpan.FromMilliseconds(10).Ticks,
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

    private sealed class CapturingHistoryService(bool isEnabled = false) : IGitHubActionsHistoryService
    {
        public List<IReadOnlyList<GitHubCiRunSummaryModule>> Writes { get; } = [];

        public bool IsEnabled { get; } = isEnabled;

        public string? HistoryPath => IsEnabled ? "history.json" : null;

        public int HistoryWindowInDays => IsEnabled ? 30 : 0;

        public bool TryGetStats(
            string testId,
            string fullyQualifiedName,
            string displayName,
            out GitHubActionsHistoryStats stats)
        {
            stats = default;
            return false;
        }

        public Task WriteAsync(IReadOnlyList<GitHubCiRunSummaryModule> modules, CancellationToken cancellationToken)
        {
            Writes.Add(modules);
            return Task.CompletedTask;
        }
    }

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

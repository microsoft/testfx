// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using GitHubActionsStepSummarySections = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsStepSummarySections;
using GitHubActionsStepSummarySectionsParser = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsStepSummarySectionsParser;
using GitHubCiCoverageMetric = ghactions::Microsoft.Testing.Extensions.CiCoverageMetric;
using GitHubCiCoverageSummaryData = ghactions::Microsoft.Testing.Extensions.CiCoverageSummaryData;
using GitHubCiRunSummaryAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubCiRunSummaryAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubCiRunSummaryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;
using GitHubSummaryPostProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class CiRunSummaryAggregationTests
{
    [TestMethod]
    public async Task ReadAndAggregate_UsesAuthoritativeSummaryAndSortsModulesAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule moduleB = CreateModule("B", passed: 1, failed: 0);
            CiRunSummaryModule moduleA = CreateModule("A", passed: 0, failed: 1);
            string pathB = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleB);
            string pathA = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleA);
            InputArtifact[] inputs =
            [
                CreateInput(pathB, moduleB),
                CreateInput(pathA, moduleA),
            ];
            var runSummary = new ArtifactPostProcessingRunSummary(
                totalTests: 9,
                passedTests: 6,
                failedTests: 2,
                skippedTests: 1,
                duration: TimeSpan.FromSeconds(12),
                exitCode: 2,
                testModuleCount: 12);

            CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(
                inputs,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary));

            Assert.IsTrue(aggregate.HasAuthoritativeRunSummary);
            Assert.IsFalse(aggregate.IsPartial);
            Assert.AreEqual(9, aggregate.TotalTests);
            Assert.AreEqual(TimeSpan.FromSeconds(12), aggregate.Duration);
            Assert.AreEqual(2, aggregate.ExitCode);
            Assert.AreEqual("A", aggregate.Modules[0].AssemblyName);
            Assert.AreEqual("B", aggregate.Modules[1].AssemblyName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_WithoutRunSummary_UsesObservedCountsAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("A", passed: 2, failed: 1);
            string path = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);

            CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            Assert.IsFalse(aggregate.HasAuthoritativeRunSummary);
            Assert.AreEqual(3, aggregate.TotalTests);
            Assert.AreEqual(2, aggregate.PassedTests);
            Assert.AreEqual(1, aggregate.FailedTests);
            Assert.IsNull(aggregate.Duration);
            Assert.IsNull(aggregate.ExitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadAndAggregate_MalformedFragment_ThrowsFormatException()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{ "schemaVersion": 1, "provider": "azure-devops" }""");
            var input = new InputArtifact(path, AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind, null, null, null, null);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [input],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_DuplicateFragmentIdentity_ThrowsFormatExceptionAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("A", passed: 1, failed: 0);
            string path = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);
            InputArtifact input = CreateInput(path, module);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [input, input],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GitHubFragment_RoundTripsStepSummarySectionsAsync()
    {
        string directory = CreateDirectory();
        try
        {
            GitHubCiRunSummaryModule module = CreateGitHubModule("Selected");
            module.GitHubActionsStepSummarySections = ["test-results"];
            module.FlakyTests =
            [
                new GitHubCiRunSummaryTest
                {
                    DisplayName = "Flaky",
                    FullyQualifiedName = "Tests.Flaky",
                    DurationTicks = TimeSpan.FromMilliseconds(10).Ticks,
                },
            ];
            string path = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);

            string json = File.ReadAllText(path);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [CreateGitHubInput(path, module)],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            string[] persistedSections = aggregate.Modules.Single().GitHubActionsStepSummarySections!;

            Assert.Contains("\"gitHubActionsStepSummarySections\"", json);
            Assert.Contains("\"flakyTests\"", json);
            Assert.AreSequenceEqual(["test-results"], persistedSections);
            Assert.AreEqual("Tests.Flaky", aggregate.FlakyTests.Single().FullyQualifiedName);
            Assert.AreEqual(
                GitHubActionsStepSummarySections.TestResults,
                GitHubActionsStepSummarySectionsParser.GetAggregateSections(aggregate.Modules));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GitHubAggregate_ForRetryAttempts_UsesInitialAttemptCoverage()
    {
        GitHubCiRunSummaryModule first = CreateGitHubModule("Tests");
        first.AttemptNumber = 1;
        first.Coverage = CreateGitHubCoverage(covered: 50, coverable: 100);
        GitHubCiRunSummaryModule retry = CreateGitHubModule("Tests");
        retry.AttemptNumber = 2;
        retry.Coverage = CreateGitHubCoverage(covered: 10, coverable: 20);

        var aggregate = new GitHubCiRunSummaryAggregate(
            [retry, first],
            new ArtifactPostProcessingContext(
                ArtifactPostProcessingTruncationReason.None,
                ArtifactPostProcessingMode.RetryAttempts),
            totalTests: 1,
            passedTests: 1,
            failedTests: 0,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: 0,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        GitHubCiCoverageMetric metric = aggregate.Coverage.Metrics.Single();
        Assert.AreEqual(50, metric.CoveredCount);
        Assert.AreEqual(100, metric.CoverableCount);
    }

    [TestMethod]
    public async Task ReadAndAggregate_AggregatesCoverageAndReportsMissingModulesAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule moduleA = CreateModule("A", passed: 1, failed: 0);
            moduleA.Coverage = CreateCoverage(80, 100);
            CiRunSummaryModule moduleB = CreateModule("B", passed: 1, failed: 0);
            moduleB.Coverage = CreateCoverage(10, 20);
            CiRunSummaryModule moduleWithoutCoverage = CreateModule("C", passed: 1, failed: 0);
            CiRunSummaryModule thresholdOnlyModule = CreateModule("D", passed: 1, failed: 0);
            thresholdOnlyModule.Coverage = CreateThresholdOnlyCoverage();
            string pathA = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleA);
            string pathB = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleB);
            string pathC = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleWithoutCoverage);
            string pathD = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                thresholdOnlyModule);

            CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(
                [
                    CreateInput(pathA, moduleA),
                    CreateInput(pathB, moduleB),
                    CreateInput(pathC, moduleWithoutCoverage),
                    CreateInput(pathD, thresholdOnlyModule),
                ],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    new ArtifactPostProcessingRunSummary(
                        totalTests: 4,
                        passedTests: 4,
                        failedTests: 0,
                        skippedTests: 0,
                        duration: TimeSpan.FromSeconds(1),
                        exitCode: 0,
                        testModuleCount: 4)));
            string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);

            Assert.HasCount(1, aggregate.Coverage.Metrics);
            Assert.AreEqual(90, aggregate.Coverage.Metrics[0].CoveredCount);
            Assert.AreEqual(120, aggregate.Coverage.Metrics[0].CoverableCount);
            Assert.AreEqual(3, aggregate.Coverage.ReportingModuleCount);
            Assert.AreEqual(4, aggregate.Coverage.TotalModuleCount);
            Assert.Contains("| Overall | Line | 90 | 120 | 75.0% |", markdown);
            Assert.Contains("Coverage data was reported by 3 of 4 test modules.", markdown);
            Assert.Contains("| D (net9.0) — Overall | Branch (Average) | No data | 80.0% | ❌ Failed |", markdown);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GitHubFragment_LegacyPayloadWithoutStepSummarySections_DefaultsToAllAsync()
    {
        string directory = CreateDirectory();
        try
        {
            GitHubCiRunSummaryModule module = CreateGitHubModule("Legacy");
            string path = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);

            string json = File.ReadAllText(path);
            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [CreateGitHubInput(path, module)],
                GitHubSummaryPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            Assert.DoesNotContain("githubActionsStepSummarySections", json);
            Assert.IsNull(aggregate.Modules.Single().GitHubActionsStepSummarySections);
            Assert.AreEqual(
                GitHubActionsStepSummarySections.All,
                GitHubActionsStepSummarySectionsParser.GetAggregateSections(aggregate.Modules));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CreateCoverageSummary_PrefersOverallScopePerProducer()
    {
        var coverageResult = new Mock<ITestCoverageResult>();
        var sessionUid = new SessionUid("session");
        coverageResult.SetupGet(result => result.Scopes).Returns(
        [
            new CoverageScopeSummary(
                sessionUid,
                CoverageScope.Overall,
                [new CoverageMetricResult(CoverageMetric.Line, 80, 100, "producer-with-overall")]),
            new CoverageScopeSummary(
                sessionUid,
                new CoverageScope(CoverageScopeLevel.Module, "A.dll"),
                [
                    new CoverageMetricResult(CoverageMetric.Line, 40, 50, "producer-with-overall"),
                    new CoverageMetricResult(CoverageMetric.Branch, 20, 25, "producer-with-overall"),
                    new CoverageMetricResult(CoverageMetric.Statement, 30, 40, "module-only-producer"),
                ]),
        ]);
        coverageResult.SetupGet(result => result.Thresholds).Returns([]);

        CiCoverageSummaryData summary = CiCoverageSummary.Create(coverageResult.Object, sessionUid);

        Assert.HasCount(3, summary.Metrics);
        Assert.AreEqual(CoverageScopeLevel.Overall, summary.Metrics[0].ScopeLevel);
        Assert.AreEqual("producer-with-overall", summary.Metrics[0].ProducerId);
        Assert.AreEqual(CoverageScopeLevel.Module, summary.Metrics[1].ScopeLevel);
        Assert.AreEqual(CoverageMetric.Branch, summary.Metrics[1].Metric);
        Assert.AreEqual("producer-with-overall", summary.Metrics[1].ProducerId);
        Assert.AreEqual(CoverageScopeLevel.Module, summary.Metrics[2].ScopeLevel);
        Assert.AreEqual("module-only-producer", summary.Metrics[2].ProducerId);
    }

    [TestMethod]
    public void CreateCoverageSummary_ThresholdOnlySessionCountsAsReporting()
    {
        var coverageResult = new Mock<ITestCoverageResult>();
        var sessionUid = new SessionUid("session");
        coverageResult.SetupGet(result => result.Scopes).Returns([]);
        coverageResult.SetupGet(result => result.Thresholds).Returns(
        [
            new TestCoverageThresholdMessage(
                sessionUid,
                CoverageScope.Overall,
                CoverageMetric.Line,
                CoverageAggregation.None,
                actualPercentage: 0,
                requiredPercentage: 80,
                hasCoverableData: false,
                producerId: "threshold-only"),
        ]);

        CiCoverageSummaryData summary = CiCoverageSummary.Create(coverageResult.Object, sessionUid);

        Assert.AreEqual(1, summary.ReportingModuleCount);
        Assert.IsEmpty(summary.Metrics);
        Assert.HasCount(1, summary.Thresholds);
        Assert.AreEqual(CoverageMetric.Line, summary.Thresholds[0].Metric);
        Assert.AreEqual(0, summary.Thresholds[0].ActualPercentage);
        Assert.AreEqual(80, summary.Thresholds[0].RequiredPercentage);
        Assert.IsFalse(summary.Thresholds[0].HasCoverableData);
        Assert.AreEqual("threshold-only", summary.Thresholds[0].ProducerId);
    }

    [TestMethod]
    public void ProvidersUseDistinctFragmentKinds()
    {
        string[] kinds =
        [
            AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            GitHubSummaryPostProcessor.FragmentArtifactKind,
        ];

        Assert.AreNotEqual(kinds[0], kinds[1]);
    }

    [TestMethod]
    public async Task AzureDevOpsPostProcessor_WritesAggregateAndUploadsOnceAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("A", passed: 2, failed: 1);
            module.RequestedOutputPath = Path.Combine(directory, "requested-summary.md");
            string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);
            InputArtifact[] inputs = [CreateInput(fragmentPath, module)];
            var runSummary = new ArtifactPostProcessingRunSummary(
                3,
                2,
                1,
                0,
                TimeSpan.FromSeconds(2),
                exitCode: 2,
                testModuleCount: 1);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("TF_BUILD")).Returns("true");
            var output = new List<IOutputDeviceData>();
            var outputDevice = new Mock<IOutputDevice>();
            outputDevice
                .Setup(item => item.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.IsAny<IOutputDeviceData>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => output.Add(data))
                .Returns(Task.CompletedTask);
            var processor = new AzureDevOpsSummaryArtifactPostProcessor(
                new TestCommandLineOptions(new()
                {
                    [AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [],
                }),
                environment.Object,
                outputDevice.Object);

            ProcessedArtifact? first = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary),
                CancellationToken.None);
            ProcessedArtifact? second = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary),
                CancellationToken.None);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(AzureDevOpsSummaryArtifactPostProcessor.SummaryArtifactKind, first.Kind);
            Assert.IsTrue(File.Exists(first.Path));
            Assert.IsTrue(File.Exists(module.RequestedOutputPath));
            Assert.Contains("# Overall test summary", File.ReadAllText(first.Path));
            string[] commands =
            [
                .. output
                    .OfType<AzureDevOpsCommandOutputDeviceData>()
                    .Select(item => item.Text),
            ];
            Assert.HasCount(1, commands);
            Assert.StartsWith("##vso[task.uploadsummary]", commands[0]);
            Assert.Contains(module.RequestedOutputPath, commands[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task AzureDevOpsPostProcessor_IsEnabledForDispatcherRelaunchAsync()
    {
        var processor = new AzureDevOpsSummaryArtifactPostProcessor(
            new TestCommandLineOptions(new()
            {
                ["manifest"] = ["manifest.json"],
            }),
            Mock.Of<IEnvironment>(),
            Mock.Of<IOutputDevice>());

        Assert.IsTrue(await processor.IsEnabledAsync());
    }

    [TestMethod]
    public async Task AzureDevOpsPostProcessor_ConflictingOutputPaths_UsesCanonicalOutputAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule firstModule = CreateModule("A", passed: 1, failed: 0);
            firstModule.RequestedOutputPath = Path.Combine(directory, "first.md");
            CiRunSummaryModule secondModule = CreateModule("B", passed: 1, failed: 0);
            secondModule.RequestedOutputPath = Path.Combine(directory, "second.md");
            string firstPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                firstModule);
            string secondPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                secondModule);
            var outputDevice = new Mock<IOutputDevice>();
            var processor = new AzureDevOpsSummaryArtifactPostProcessor(
                new TestCommandLineOptions(new()
                {
                    ["manifest"] = ["manifest.json"],
                }),
                Mock.Of<IEnvironment>(),
                outputDevice.Object);

            ProcessedArtifact? result = await processor.ProcessAsync(
                [CreateInput(firstPath, firstModule), CreateInput(secondPath, secondModule)],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(File.Exists(result.Path));
            Assert.IsFalse(File.Exists(firstModule.RequestedOutputPath));
            Assert.IsFalse(File.Exists(secondModule.RequestedOutputPath));
            outputDevice.Verify(
                item => item.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.Is<AzureDevOpsCommandOutputDeviceData>(data => data.Text.Contains(result.Path, StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(0, 0, false)]
    [DataRow(2, 1, true)]
    [DataRow(8, 0, true)]
    public async Task GitHubPostProcessor_OnFailureOnly_WritesStepSummaryOnlyForFailureAsync(int exitCode, int failedTests, bool shouldWriteSummary)
    {
        var runSummary = new ArtifactPostProcessingRunSummary(
            totalTests: 1,
            passedTests: 1 - failedTests,
            failedTests: failedTests,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: exitCode,
            testModuleCount: 1);

        string? summary = await RunGitHubPostProcessorAsync(
            moduleExitCode: exitCode,
            failedTests: failedTests,
            writeOnFailureOnly: true,
            context: new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary));

        Assert.AreEqual(shouldWriteSummary, summary is not null);
        if (shouldWriteSummary)
        {
            Assert.IsNotNull(summary);
            Assert.Contains("❌ Overall Test Run Summary", summary);
        }
    }

    [TestMethod]
    public async Task GitHubPostProcessor_OnFailureOnly_UsesModuleExitCodeWhenRunSummaryIsUnavailableAsync()
    {
        string? summary = await RunGitHubPostProcessorAsync(
            moduleExitCode: 8,
            failedTests: 0,
            writeOnFailureOnly: true,
            context: new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

        Assert.IsNotNull(summary);
        Assert.Contains("⚠️ Overall Test Run Summary", summary);
    }

    [TestMethod]
    public async Task GitHubPostProcessor_OnFailureOnly_WritesPartialSummaryForTruncatedRunAsync()
    {
        string? summary = await RunGitHubPostProcessorAsync(
            moduleExitCode: 0,
            failedTests: 0,
            writeOnFailureOnly: true,
            context: new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.Timeout));

        Assert.IsNotNull(summary);
        Assert.Contains("This summary is partial because the test run was truncated.", summary);
    }

    [TestMethod]
    public async Task GitHubPostProcessor_AlwaysMode_WritesSuccessfulSummaryAsync()
    {
        string? summary = await RunGitHubPostProcessorAsync(
            moduleExitCode: 0,
            failedTests: 0,
            writeOnFailureOnly: false,
            context: new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

        Assert.IsNotNull(summary);
        Assert.Contains("⚠️ Overall Test Run Summary", summary);
    }

    [TestMethod]
    public async Task WriteFragmentAsync_BoundsLongFileNameAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule(new string('a', 300), passed: 1, failed: 0);

            string path = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);

            Assert.IsTrue(File.Exists(path));
            Assert.IsLessThanOrEqualTo(64, Path.GetFileName(path).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CiRunSummaryModule CreateModule(string assemblyName, int passed, int failed)
    {
        var records = new List<TestRecord>();
        for (int i = 0; i < passed; i++)
        {
            records.Add(new TestRecord($"pass-{i}", $"{assemblyName}.Pass{i}", TerminalKind.Passed, TimeSpan.FromMilliseconds(i + 1)));
        }

        for (int i = 0; i < failed; i++)
        {
            records.Add(new TestRecord($"fail-{i}", $"{assemblyName}.Fail{i}", TerminalKind.Failed, TimeSpan.FromMilliseconds(i + 1)));
        }

        return CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            Path.Combine(Path.GetTempPath(), assemblyName + ".dll"),
            "net9.0",
            "x64",
            "execution-" + assemblyName,
            "session-" + assemblyName,
            attemptNumber: 1,
            exitCode: failed > 0 ? 2 : 0);
    }

    private static CiCoverageSummaryData CreateCoverage(long covered, long coverable)
        => new()
        {
            Metrics =
            [
                new CiCoverageMetric
                {
                    ScopeLevel = CoverageScopeLevel.Overall,
                    Metric = CoverageMetric.Line,
                    ProducerId = "coverlet",
                    CoveredCount = covered,
                    CoverableCount = coverable,
                },
            ],
            ReportingModuleCount = 1,
            TotalModuleCount = 1,
        };

    private static CiCoverageSummaryData CreateThresholdOnlyCoverage()
        => new()
        {
            Thresholds =
            [
                new CiCoverageThreshold
                {
                    ScopeLevel = CoverageScopeLevel.Overall,
                    Metric = CoverageMetric.Branch,
                    ProducerId = "threshold-only",
                    Aggregation = CoverageAggregation.Average,
                    RequiredPercentage = 80,
                    HasCoverableData = false,
                    Passed = false,
                },
            ],
            ReportingModuleCount = 1,
            TotalModuleCount = 1,
        };

    private static InputArtifact CreateInput(string path, CiRunSummaryModule module)
        => new(
            path,
            AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            module.ModulePath,
            module.TargetFramework,
            module.Architecture,
            module.ExecutionId);

    private static async Task<string?> RunGitHubPostProcessorAsync(
        int moduleExitCode,
        int failedTests,
        bool writeOnFailureOnly,
        ArtifactPostProcessingContext context)
    {
        string directory = CreateDirectory();
        try
        {
            string stepSummaryPath = Path.Combine(directory, "step-summary.md");
            var module = new GitHubCiRunSummaryModule
            {
                AssemblyName = "Tests",
                ModulePath = Path.Combine(directory, "Tests.dll"),
                TargetFramework = "net9.0",
                Architecture = "x64",
                ExecutionId = "execution",
                SessionUid = "session",
                AttemptNumber = 1,
                ExitCode = moduleExitCode,
                TotalTests = 1,
                PassedTests = 1 - failedTests,
                FailedTests = failedTests,
                WriteOnFailureOnly = writeOnFailureOnly,
            };
            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                GitHubSummaryPostProcessor.Provider,
                GitHubSummaryPostProcessor.ProviderSlug,
                module);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(stepSummaryPath);
            var processor = new GitHubSummaryPostProcessor(
                new TestCommandLineOptions(new()
                {
                    ["manifest"] = ["manifest.json"],
                }),
                environment.Object,
                new SystemFileSystem(),
                new Mock<ILoggerFactory>().Object,
                static () => false);

            ProcessedArtifact? result = await processor.ProcessAsync(
                [
                    new InputArtifact(
                        fragmentPath,
                        GitHubSummaryPostProcessor.FragmentArtifactKind,
                        module.ModulePath,
                        module.TargetFramework,
                        module.Architecture,
                        module.ExecutionId),
                ],
                directory,
                context,
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(File.Exists(result.Path));
            return File.Exists(stepSummaryPath) ? File.ReadAllText(stepSummaryPath) : null;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static GitHubCiRunSummaryModule CreateGitHubModule(string assemblyName)
        => new()
        {
            AssemblyName = assemblyName,
            ModulePath = Path.Combine(Path.GetTempPath(), assemblyName + ".dll"),
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution-" + assemblyName,
            SessionUid = "session-" + assemblyName,
            AttemptNumber = 1,
            ExitCode = 0,
        };

    private static GitHubCiCoverageSummaryData CreateGitHubCoverage(long covered, long coverable)
        => new()
        {
            Metrics =
            [
                new GitHubCiCoverageMetric
                {
                    ScopeLevel = CoverageScopeLevel.Overall,
                    Metric = CoverageMetric.Line,
                    ProducerId = "coverage",
                    CoveredCount = covered,
                    CoverableCount = coverable,
                },
            ],
            ReportingModuleCount = 1,
            TotalModuleCount = 1,
        };

    private static InputArtifact CreateGitHubInput(string path, GitHubCiRunSummaryModule module)
        => new(
            path,
            GitHubSummaryPostProcessor.FragmentArtifactKind,
            module.ModulePath,
            module.TargetFramework,
            module.Architecture,
            module.ExecutionId);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ci-summary-aggregation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

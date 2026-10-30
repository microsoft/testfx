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
    public async Task ReadAndAggregate_ProducingTestModuleMismatch_ThrowsFormatExceptionAsync()
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

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module, producingTestModule: Path.Combine(directory, "Other.dll"))],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));

            Assert.AreEqual($"CI summary module provenance does not match '{path}'.", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_TargetFrameworkMismatch_ThrowsFormatExceptionAsync()
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

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module, targetFramework: "net8.0")],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));

            Assert.AreEqual($"CI summary target framework provenance does not match '{path}'.", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_ArchitectureMismatch_ThrowsFormatExceptionAsync()
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

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module, architecture: "arm64")],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));

            Assert.AreEqual($"CI summary architecture provenance does not match '{path}'.", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_ExecutionIdMismatch_ThrowsFormatExceptionAsync()
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

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module, executionId: "other-execution")],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));

            Assert.AreEqual($"CI summary execution provenance does not match '{path}'.", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("2")]
    [DataRow("not-an-attempt")]
    public async Task ReadAndAggregate_ForRetryAttempts_ExecutionIdMismatch_ThrowsFormatExceptionAsync(string executionId)
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

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module, executionId: executionId)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts)));

            Assert.AreEqual($"CI summary execution provenance does not match '{path}'.", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_NullDependencyEntry_ThrowsFormatExceptionAsync()
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
            string json = File.ReadAllText(path);
            string malformedJson = json.Replace("\"dependencies\": []", "\"dependencies\": [null]");
            Assert.AreNotEqual(json, malformedJson);
            File.WriteAllText(path, malformedJson);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("historyTests")]
    [DataRow("dependencies")]
    public async Task ReadAndAggregate_NullBoundedCollection_ThrowsFormatExceptionAsync(string propertyName)
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
            string json = File.ReadAllText(path);
            string malformedJson = json.Replace($"\"{propertyName}\": []", $"\"{propertyName}\": null");
            Assert.AreNotEqual(json, malformedJson);
            File.WriteAllText(path, malformedJson);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_NullHistoryEntry_ThrowsFormatExceptionAsync()
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
            string json = File.ReadAllText(path);
            string malformedJson = json.Replace("\"historyTests\": []", "\"historyTests\": [null]");
            Assert.AreNotEqual(json, malformedJson);
            File.WriteAllText(path, malformedJson);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_AppliesDependencyBoundAfterDeterministicModuleSortAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule moduleA = CreateModule("A", passed: 1, failed: 0);
            moduleA.Dependencies =
            [
                .. Enumerable.Range(0, 600).Select(static index => new CiRunSummaryDependency
                {
                    DependentFullyQualifiedName = $"A.Test{index}",
                    Prerequisite = "A.Setup",
                }),
            ];
            CiRunSummaryModule moduleB = CreateModule("B", passed: 1, failed: 0);
            moduleB.Dependencies =
            [
                .. Enumerable.Range(0, 600).Select(static index => new CiRunSummaryDependency
                {
                    DependentFullyQualifiedName = $"B.Test{index}",
                    Prerequisite = "B.Setup",
                }),
            ];
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

            CiRunSummaryAggregate reverseInput = CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(pathB, moduleB), CreateInput(pathA, moduleA)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
            CiRunSummaryAggregate sortedInput = CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(pathA, moduleA), CreateInput(pathB, moduleB)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            Assert.HasCount(600, reverseInput.Modules[0].Dependencies);
            Assert.HasCount(400, reverseInput.Modules[1].Dependencies);
            Assert.HasCount(600, sortedInput.Modules[0].Dependencies);
            Assert.HasCount(400, sortedInput.Modules[1].Dependencies);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
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
            Assert.Contains("## ❌ Overall test results", File.ReadAllText(first.Path));
            string[] commands =
            [
                .. output
                    .OfType<AzureDevOpsCommandOutputDeviceData>()
                    .Select(item => item.Text),
            ];
            Assert.HasCount(1, commands);
            string aggregationId = CiRunSummaryAggregation.CreateAggregationId(inputs);
            Assert.StartsWith($"##vso[task.addattachment type=Distributedtask.Core.Summary;name=Overall test results - {aggregationId};]", commands[0]);
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

    [TestMethod]
    public void TestRecord_LegacyConstructorsKeepSkipReasonOptional()
    {
        var failure = new TestFailureDetails("failure", "Exception", null, null, 0);
        TestRecord[] records =
        [
            new("Four", "Tests.Four", TerminalKind.Skipped, TimeSpan.Zero),
            new("FiveBool", "Tests.FiveBool", TerminalKind.Passed, TimeSpan.Zero, isFlaky: true),
            new("FiveFailure", "Tests.FiveFailure", TerminalKind.Failed, TimeSpan.Zero, failure),
            new("Six", "Tests.Six", TerminalKind.Failed, TimeSpan.Zero, isFlaky: true, failure),
            new("Seven", "Tests.Seven", TerminalKind.Skipped, TimeSpan.Zero, isFlaky: false, failure: null, skipReason: "reason"),
        ];

        Assert.AreSequenceEqual(new string?[] { null, null, null, null, "reason" }, records.Select(record => record.SkipReason));
        Assert.IsTrue(records[1].IsFlaky);
        Assert.AreSame(failure, records[2].Failure);
        Assert.AreSame(failure, records[3].Failure);
        Assert.IsTrue(records[3].IsFlaky);
    }

    [TestMethod]
    public void CreateModule_PreservesTerminalOutcomesAndNormalizedSkipReasons()
    {
        var failure = new TestFailureDetails("failure message", "FailureType", "stack", "Tests.cs", 7);
        TestRecord[] records =
        [
            new("Passed", "Tests.Passed", TerminalKind.Passed, TimeSpan.FromSeconds(1), isFlaky: true),
            new("Failed", "Tests.Failed", TerminalKind.Failed, TimeSpan.FromSeconds(2), failure),
            new("TimedSkip", "Tests.TimedSkip", TerminalKind.Skipped, TimeSpan.FromSeconds(3),
                isFlaky: false, failure: null, skipReason: "  First\nreason  "),
            new("SameReason", "Tests.SameReason", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "First reason"),
            new("NullReason", "Tests.NullReason", TerminalKind.Skipped, TimeSpan.Zero),
            new("EmptyReason", "Tests.EmptyReason", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: string.Empty),
            new("BlankReason", "Tests.BlankReason", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: " \r\n "),
            new("NotSkipped", "Tests.NotSkipped", TerminalKind.Passed, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "Not a skip reason"),
        ];

        CiRunSummaryModule module = CreateDiagnosticModule(records);

        Assert.AreEqual(8L, module.TotalTests);
        Assert.AreEqual(2L, module.PassedTests);
        Assert.AreEqual(1L, module.FailedTests);
        Assert.AreEqual(5L, module.SkippedTests);
        Assert.AreEqual(TimeSpan.FromSeconds(6).Ticks, module.TestDurationTicks);
        Assert.AreSequenceEqual(new[] { "TimedSkip", "Failed", "Passed" }, module.SlowestTests.Select(test => test.DisplayName));
        Assert.AreSequenceEqual(new[] { "skipped", "failed", "passed" }, module.SlowestTests.Select(test => test.Outcome));
        Assert.AreSequenceEqual(new[] { 3L, 2L, 1L }, module.SlowestTests.Select(test => TimeSpan.FromTicks(test.DurationTicks).Ticks / TimeSpan.TicksPerSecond));
        Assert.IsNotNull(module.SkipReasons);
        Assert.AreSequenceEqual(
            new[] { ("Reason not provided", 3L), ("First reason", 2L) },
            module.SkipReasons.Select(reason => (reason.Reason, reason.Count)));
        CiRunSummaryTest failed = Assert.ContainsSingle(module.Failures);
        Assert.AreEqual("failed", failed.Outcome);
        Assert.AreEqual("failure message", failed.ErrorMessage);
        Assert.AreEqual(7, failed.LineNumber);
        Assert.IsTrue(module.SlowestTests.All(test => test.ErrorMessage is null));
        Assert.AreEqual("passed", Assert.ContainsSingle(module.FlakyTests).Outcome);
        string expectedOperatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "Linux"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";
        Assert.AreEqual(expectedOperatingSystem, module.OperatingSystem);
    }

    [TestMethod]
    public void CreateModule_BoundsSkipReasonsAfterGroupingAndOrdinalSorting()
    {
        TestRecord[] records =
        [
            .. Enumerable.Range(0, 12).Reverse().Select(index => new TestRecord(
                $"Skipped{index}", $"Tests.Skipped{index}", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: index == 0 ? "alpha" : $"Reason{index:D2}")),
            new("Extra1", "Tests.Extra1", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "Reason11"),
            new("Extra2", "Tests.Extra2", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: " Reason11 "),
        ];

        CiRunSummaryModule module = CreateDiagnosticModule(records);

        Assert.AreEqual(14L, module.SkippedTests);
        Assert.IsNotNull(module.SkipReasons);
        Assert.AreSequenceEqual(
            new[] { "Reason11", "Reason01", "Reason02", "Reason03", "Reason04", "Reason05", "Reason06", "Reason07", "Reason08", "Reason09" },
            module.SkipReasons.Select(reason => reason.Reason));
        Assert.AreSequenceEqual(new[] { 3L, 1L, 1L, 1L, 1L, 1L, 1L, 1L, 1L, 1L }, module.SkipReasons.Select(reason => reason.Count));
    }

    [TestMethod]
    [DataRow(511, DisplayName = "SkipReasonBelowLengthLimit")]
    [DataRow(512, DisplayName = "SkipReasonAtLengthLimit")]
    [DataRow(513, DisplayName = "SkipReasonAboveLengthLimit")]
    public void CreateModule_TruncatesSkipReasonsOnlyAfterTheLengthLimit(int length)
    {
        CiRunSummaryModule module = CreateDiagnosticModule(
        [
            new("Skipped", "Tests.Skipped", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: " \r\n" + new string('x', length) + "\n "),
        ]);

        Assert.IsNotNull(module.SkipReasons);
        CiRunSummarySkipReason reason = Assert.ContainsSingle(module.SkipReasons);
        Assert.AreEqual(length > 512 ? new string('x', 509) + "..." : new string('x', length), reason.Reason);
        Assert.AreEqual(1L, reason.Count);
    }

    [TestMethod]
    public void CreateModule_GroupsReasonsAfterFlatteningAndTruncation()
    {
        TestRecord[] records =
        [
            new("First", "Tests.First", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: new string('x', 509) + "First suffix"),
            new("Second", "Tests.Second", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: new string('x', 509) + "Second suffix"),
            new("Third", "Tests.Third", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "  Issue\r\nlink  "),
            new("Fourth", "Tests.Fourth", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "Issue  link"),
        ];

        CiRunSummaryModule module = CreateDiagnosticModule(records);

        Assert.IsNotNull(module.SkipReasons);
        Assert.AreSequenceEqual(
            new[] { ("Issue  link", 2L), (new string('x', 509) + "...", 2L) },
            module.SkipReasons.Select(reason => (reason.Reason, reason.Count)));
    }

    [TestMethod]
    [DataRow("current", DisplayName = "CurrentDiagnosticMetadata")]
    [DataRow("null", DisplayName = "ExplicitNullDiagnosticMetadata")]
    [DataRow("absent", DisplayName = "LegacyAbsentDiagnosticMetadata")]
    public async Task ReadAndAggregate_DiagnosticMetadataRoundTripsForBothProvidersAsync(string metadata)
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateDiagnosticFragmentModule();
            foreach (bool github in new[] { false, true })
            {
                string provider = github ? "github-actions" : "azure-devops";
                string path = await CiRunSummaryAggregation.WriteFragmentAsync(directory, provider, provider, module);
                if (metadata != "current")
                {
                    JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                    JsonObject jsonModule = root["module"]!.AsObject();
                    foreach (string field in new[] { "operatingSystem", "skipReasons" })
                    {
                        if (metadata == "absent")
                        {
                            jsonModule.Remove(field);
                        }
                        else
                        {
                            jsonModule[field] = null;
                        }
                    }

                    foreach (string list in new[] { "failures", "flakyTests", "slowestTests" })
                    {
                        foreach (JsonNode? item in jsonModule[list]!.AsArray())
                        {
                            if (metadata == "absent")
                            {
                                item!.AsObject().Remove("outcome");
                            }
                            else
                            {
                                item!["outcome"] = null;
                            }
                        }
                    }

                    File.WriteAllText(path, root.ToJsonString());
                }

                InputArtifact input = CreateDiagnosticInput(path, module, github);
                var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);
                if (github)
                {
                    GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    GitHubCiRunSummaryModule restored = Assert.ContainsSingle(aggregate.Modules);
                    Assert.AreEqual(4L, aggregate.TotalTests);
                    Assert.AreEqual(2L, aggregate.SkippedTests);
                    Assert.IsNull(aggregate.Duration);
                    AssertDiagnosticMetadata(metadata, restored.OperatingSystem,
                        restored.SlowestTests.Select(test => test.Outcome).ToArray(),
                        restored.SkipReasons?.Select(reason => (reason.Reason, reason.Count)).ToArray());
                }
                else
                {
                    CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    CiRunSummaryModule restored = Assert.ContainsSingle(aggregate.Modules);
                    Assert.AreEqual(4L, aggregate.TotalTests);
                    Assert.AreEqual(2L, aggregate.SkippedTests);
                    Assert.IsNull(aggregate.Duration);
                    AssertDiagnosticMetadata(metadata, restored.OperatingSystem,
                        restored.SlowestTests.Select(test => test.Outcome).ToArray(),
                        restored.SkipReasons?.Select(reason => (reason.Reason, reason.Count)).ToArray());
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("emptyOs")]
    [DataRow("blankOs")]
    [DataRow("longOs")]
    [DataRow("emptyOutcome")]
    [DataRow("blankOutcome")]
    [DataRow("unknownOutcome")]
    [DataRow("caseSensitiveOutcome")]
    [DataRow("failureOutcome")]
    [DataRow("flakyOutcome")]
    [DataRow("tooManyReasons")]
    [DataRow("nullReasonEntry")]
    [DataRow("nullReasonText")]
    [DataRow("emptyReasonText")]
    [DataRow("blankReasonText")]
    [DataRow("longReasonText")]
    [DataRow("zeroReasonCount")]
    [DataRow("negativeReasonCount")]
    [DataRow("excessReasonCount")]
    [DataRow("excessReasonSum")]
    [DataRow("overflowReasonSum")]
    public async Task ReadAndAggregate_InvalidDiagnosticMetadataIsRejectedByBothProvidersAsync(string partition)
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateDiagnosticFragmentModule();
            foreach (bool github in new[] { false, true })
            {
                string provider = github ? "github-actions" : "azure-devops";
                string path = await CiRunSummaryAggregation.WriteFragmentAsync(directory, provider, provider, module);
                JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                JsonObject json = root["module"]!.AsObject();
                JsonNode firstReason = json["skipReasons"]![0]!;
                switch (partition)
                {
                    case "emptyOs": json["operatingSystem"] = string.Empty; break;
                    case "blankOs": json["operatingSystem"] = " \r\n "; break;
                    case "longOs": json["operatingSystem"] = new string('x', 65); break;
                    case "emptyOutcome": json["slowestTests"]![0]!["outcome"] = string.Empty; break;
                    case "blankOutcome": json["slowestTests"]![0]!["outcome"] = " "; break;
                    case "unknownOutcome": json["slowestTests"]![0]!["outcome"] = "error"; break;
                    case "caseSensitiveOutcome": json["slowestTests"]![0]!["outcome"] = "Skipped"; break;
                    case "failureOutcome": json["failures"]![0]!["outcome"] = "error"; break;
                    case "flakyOutcome": json["flakyTests"]![0]!["outcome"] = "error"; break;
                    case "tooManyReasons":
                        json["totalTests"] = 13;
                        json["skippedTests"] = 11;
                        json["skipReasons"] = new JsonArray([.. Enumerable.Range(0, 11)
                            .Select(index => (JsonNode)new JsonObject { ["reason"] = $"Reason{index}", ["count"] = 1 })]);
                        break;
                    case "nullReasonEntry": json["skipReasons"]![0] = null; break;
                    case "nullReasonText": firstReason["reason"] = null; break;
                    case "emptyReasonText": firstReason["reason"] = string.Empty; break;
                    case "blankReasonText": firstReason["reason"] = " \r\n "; break;
                    case "longReasonText": firstReason["reason"] = new string('x', 513); break;
                    case "zeroReasonCount": firstReason["count"] = 0; break;
                    case "negativeReasonCount": firstReason["count"] = -1; break;
                    case "excessReasonCount": firstReason["count"] = 3; break;
                    case "excessReasonSum":
                        json["skipReasons"]!.AsArray().Add(new JsonObject { ["reason"] = "Other", ["count"] = 1 });
                        break;
                    case "overflowReasonSum":
                        json["totalTests"] = long.MaxValue;
                        json["passedTests"] = 0;
                        json["failedTests"] = 0;
                        json["skippedTests"] = long.MaxValue;
                        firstReason["count"] = long.MaxValue;
                        json["skipReasons"]!.AsArray().Add(new JsonObject { ["reason"] = "Other", ["count"] = 1 });
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(partition));
                }

                File.WriteAllText(path, root.ToJsonString());
                InputArtifact input = CreateDiagnosticInput(path, module, github);
                var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);
                FormatException error = Assert.ThrowsExactly<FormatException>(() =>
                {
                    if (github)
                    {
                        GitHubCiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    }
                    else
                    {
                        CiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    }
                });
                Assert.AreEqual($"Invalid CI summary module in '{path}'.", error.Message);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(0, 0L, DisplayName = "EmptyCurrentReasons")]
    [DataRow(0, 1L, DisplayName = "EmptyCurrentReasonsAllowOmittedSkips")]
    [DataRow(1, 1L, DisplayName = "SingleReasonExactCount")]
    [DataRow(10, 10L, DisplayName = "TenReasonsExactCount")]
    [DataRow(10, 11L, DisplayName = "TenReasonsAllowOmittedSkips")]
    [DataRow(1, long.MaxValue, DisplayName = "LargestValidSkipCount")]
    public async Task ReadAndAggregate_DiagnosticMetadataAtLimitsIsAcceptedByBothProvidersAsync(int reasonCount, long skipped)
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("Limits", passed: 0, failed: 0);
            module.OperatingSystem = new string('x', 64);
            module.TotalTests = skipped;
            module.SkippedTests = skipped;
            module.SkipReasons = [.. Enumerable.Range(0, reasonCount).Select(index => new CiRunSummarySkipReason
            {
                Reason = new string('x', 511) + (char)('A' + index),
                Count = reasonCount == 1 ? skipped : 1,
            })];
            foreach (bool github in new[] { false, true })
            {
                string provider = github ? "github-actions" : "azure-devops";
                string path = await CiRunSummaryAggregation.WriteFragmentAsync(directory, provider, provider, module);
                InputArtifact input = CreateDiagnosticInput(path, module, github);
                var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);
                if (github)
                {
                    GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    GitHubCiRunSummaryModule restored = Assert.ContainsSingle(aggregate.Modules);
                    Assert.AreEqual(skipped, aggregate.SkippedTests);
                    Assert.AreEqual(new string('x', 64), restored.OperatingSystem);
                    Assert.IsNotNull(restored.SkipReasons);
                    Assert.HasCount(reasonCount, restored.SkipReasons);
                    Assert.AreEqual(reasonCount == 1 ? skipped : reasonCount, restored.SkipReasons.Sum(reason => reason.Count));
                }
                else
                {
                    CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate([input], provider, context);
                    CiRunSummaryModule restored = Assert.ContainsSingle(aggregate.Modules);
                    Assert.AreEqual(skipped, aggregate.SkippedTests);
                    Assert.AreEqual(new string('x', 64), restored.OperatingSystem);
                    Assert.IsNotNull(restored.SkipReasons);
                    Assert.HasCount(reasonCount, restored.SkipReasons);
                    Assert.AreEqual(reasonCount == 1 ? skipped : reasonCount, restored.SkipReasons.Sum(reason => reason.Count));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CiRunSummaryModule CreateDiagnosticModule(IReadOnlyList<TestRecord> records)
        => CiRunSummaryAggregation.CreateModule(
            records, "Metadata", Path.Combine(Path.GetTempPath(), "Metadata.dll"), "net8.0", "x64",
            "execution", "session", attemptNumber: 1, exitCode: 0);

    private static CiRunSummaryModule CreateDiagnosticFragmentModule()
    {
        CiRunSummaryModule module = CreateDiagnosticModule(
        [
            new("Passed", "Tests.Passed", TerminalKind.Passed, TimeSpan.FromSeconds(1), isFlaky: true),
            new("Failed", "Tests.Failed", TerminalKind.Failed, TimeSpan.FromSeconds(2)),
            new("Skipped", "Tests.Skipped", TerminalKind.Skipped, TimeSpan.FromSeconds(3),
                isFlaky: false, failure: null, skipReason: "Known issue"),
            new("AlsoSkipped", "Tests.AlsoSkipped", TerminalKind.Skipped, TimeSpan.Zero,
                isFlaky: false, failure: null, skipReason: "Known issue"),
        ]);
        module.OperatingSystem = "Linux";
        return module;
    }

    private static InputArtifact CreateDiagnosticInput(string path, CiRunSummaryModule module, bool github)
        => new(path, github ? GitHubSummaryPostProcessor.FragmentArtifactKind : AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            module.ModulePath, module.TargetFramework, module.Architecture, module.ExecutionId);

    private static void AssertDiagnosticMetadata(
        string metadata, string? operatingSystem, string?[] outcomes, (string Reason, long Count)[]? reasons)
    {
        if (metadata == "current")
        {
            Assert.AreEqual("Linux", operatingSystem);
            Assert.AreSequenceEqual(new[] { "skipped", "failed", "passed" }, outcomes);
            Assert.IsNotNull(reasons);
            Assert.AreSequenceEqual(new[] { ("Known issue", 2L) }, reasons);
        }
        else
        {
            Assert.IsNull(operatingSystem);
            Assert.AreSequenceEqual(new string?[] { null, null, null }, outcomes);
            Assert.IsNull(reasons);
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

    private static InputArtifact CreateInput(
        string path,
        CiRunSummaryModule module,
        string? producingTestModule = null,
        string? targetFramework = null,
        string? architecture = null,
        string? executionId = null)
        => new(
            path,
            AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            producingTestModule ?? module.ModulePath,
            targetFramework ?? module.TargetFramework,
            architecture ?? module.Architecture,
            executionId ?? module.ExecutionId);

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

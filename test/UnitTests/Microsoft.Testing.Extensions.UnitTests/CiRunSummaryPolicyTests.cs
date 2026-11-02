// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

using GitHubAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;
using GitHubReporter = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryReporter;
using GitHubTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class CiRunSummaryPolicyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0, false, "✅", "**Runner policy:** ✅ accepted · exit code **0** (Success — The test run succeeded.)", DisplayName = "AcceptedPolicyRetainsFailedTests")]
    [DataRow(2, false, "❌", "**Runner policy:** ❌ failed · exit code **2** (AtLeastOneTestFailed — At least one test failed.)", DisplayName = "RejectedPolicyRetainsFailedTests")]
    [DataRow(0, true, "⚠️", "**Runner policy:** ✅ accepted · exit code **0** (Success — The test run succeeded.)", DisplayName = "AcceptedPartialPolicyRetainsWarning")]
    public void AggregateRenderers_SeparateAuthoritativePolicyFromTestResults(int exitCode, bool partial, string gitHubIcon, string policyLine)
    {
        (string azure, string github) = Render(exitCode, authoritative: true, partial);

        Assert.StartsWith("## ❌ Overall test results", azure);
        Assert.StartsWith($"## {gitHubIcon} Overall Test Run Summary", github);
        Assert.Contains(policyLine, azure);
        Assert.Contains(policyLine, github);
        Assert.Contains("**20 tests** · ✅ **17 passed** · ❌ **1 failed** · ⏭ **2 skipped**", azure);
        Assert.Contains("| 20 | 17 | 1 | 2 |", github);
        Assert.Contains("retained failure", azure);
        Assert.Contains("retained failure", github);
        Assert.AreEqual(partial, azure.Contains("**Partial summary:**", StringComparison.Ordinal));
        Assert.AreEqual(partial, github.Contains("This summary is partial", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false, null, false, DisplayName = "LegacyPolicyIsUnavailable")]
    [DataRow(false, 0, true, DisplayName = "UntrustedSuccessfulExitIsNotPolicy")]
    [DataRow(true, null, true, DisplayName = "MissingAuthoritativeExitIsNotInvented")]
    public void AggregateRenderers_WithoutAuthoritativeExitDoNotInventPolicy(bool authoritative, int? exitCode, bool partial)
    {
        (string azure, string github) = Render(exitCode, authoritative, partial);

        Assert.DoesNotContain("**Runner policy:**", azure);
        Assert.DoesNotContain("**Runner policy:**", github);
        Assert.Contains("**1 failed**", azure);
        Assert.Contains("| 20 | 17 | 1 | 2 |", github);
        Assert.Contains("retained failure", azure);
        Assert.Contains("retained failure", github);
        Assert.AreEqual(partial, azure.Contains("**Partial summary:**", StringComparison.Ordinal));
        Assert.AreEqual(partial, github.Contains("This summary is partial", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(0, false, true, DisplayName = "AcceptedPolicyRetainsFailureVisibility")]
    [DataRow(0, true, true, DisplayName = "AcceptedPartialPolicyPreservesWarning")]
    [DataRow(2, false, true, DisplayName = "RejectedPolicyWritesStepAndArtifact")]
    [DataRow(2, true, true, DisplayName = "RejectedPartialPolicyPreservesWarning")]
    [DataRow(null, false, true, DisplayName = "LegacyFailureStillWritesStep")]
    [DataRow(null, true, true, DisplayName = "LegacyPartialFailureStillWritesStep")]
    public async Task GitHubOnFailure_RetainsFailuresAndArtifactsWithPolicyContext(int? finalExitCode, bool partial, bool writesStep)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ci-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string stepPath = Path.Combine(directory, "step.md");
            GitHubModule module = CreateGitHubModule(Path.Combine(directory, "Tests.dll"));
            module.WriteOnFailureOnly = true;
            string fragment = await GitHubAggregation.WriteFragmentAsync(directory, GitHubProcessor.Provider, GitHubProcessor.ProviderSlug, module);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")).Returns(stepPath);
            var processor = new GitHubProcessor(
                new TestCommandLineOptions(new() { ["manifest"] = ["manifest.json"] }),
                environment.Object,
                new SystemFileSystem(),
                new Mock<ILoggerFactory>().Object,
                static () => false);
            ArtifactPostProcessingContext context = finalExitCode is int exitCode
                ? new ArtifactPostProcessingContext(
                    partial ? ArtifactPostProcessingTruncationReason.Timeout : ArtifactPostProcessingTruncationReason.None,
                    new ArtifactPostProcessingRunSummary(20, 17, 1, 2, TimeSpan.FromSeconds(1), exitCode, testModuleCount: 1))
                : new ArtifactPostProcessingContext(partial ? ArtifactPostProcessingTruncationReason.Timeout : ArtifactPostProcessingTruncationReason.None);

            ProcessedArtifact? result = await processor.ProcessAsync(
                [new InputArtifact(fragment, GitHubProcessor.FragmentArtifactKind, module.ModulePath, module.TargetFramework, module.Architecture, module.ExecutionId)],
                directory,
                context,
                TestContext.CancellationToken);

            Assert.IsNotNull(result);
            string artifact = File.ReadAllText(result.Path);
            Assert.Contains("| 20 | 17 | 1 | 2 |", artifact);
            Assert.Contains("retained failure", artifact);
            Assert.AreEqual(partial, artifact.Contains("This summary is partial", StringComparison.Ordinal));
            Assert.AreEqual(finalExitCode is not null, artifact.Contains("**Runner policy:**", StringComparison.Ordinal));
            Assert.AreEqual(writesStep, File.Exists(stepPath));
            if (writesStep)
            {
                string step = File.ReadAllText(stepPath);
                Assert.Contains("| 20 | 17 | 1 | 2 |", step);
                Assert.Contains("retained failure", step);
                Assert.AreEqual(partial, step.Contains("This summary is partial", StringComparison.Ordinal));
                Assert.AreEqual(finalExitCode is not null, step.Contains("**Runner policy:**", StringComparison.Ordinal));
            }

            Assert.IsTrue(File.Exists(fragment), "An on-failure preference must not remove producer fragments.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (string Azure, string GitHub) Render(int? exitCode, bool authoritative, bool partial)
    {
        var context = new ArtifactPostProcessingContext(partial ? ArtifactPostProcessingTruncationReason.Timeout : ArtifactPostProcessingTruncationReason.None);
        var module = new CiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net8.0",
            Architecture = "x64",
            ExitCode = 2,
            TotalTests = 20,
            PassedTests = 17,
            FailedTests = 1,
            SkippedTests = 2,
            Failures = [new CiRunSummaryTest { DisplayName = "Failed", FullyQualifiedName = "Tests.Failed", ErrorMessage = "retained failure" }],
        };
        var azure = new CiRunSummaryAggregate([module], context, 20, 17, 1, 2, TimeSpan.FromSeconds(1), exitCode, authoritative, partial);
        var github = new GitHubAggregate([CreateGitHubModule("Tests.dll")], context, 20, 17, 1, 2, TimeSpan.FromSeconds(1), exitCode, authoritative, partial);
        return (AzureDevOpsSummaryReporter.BuildAggregateMarkdown(azure), GitHubReporter.BuildAggregateMarkdown(github).Markdown);
    }

    private static GitHubModule CreateGitHubModule(string path)
        => new()
        {
            AssemblyName = "Tests",
            ModulePath = path,
            TargetFramework = "net8.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            ExitCode = 2,
            TotalTests = 20,
            PassedTests = 17,
            FailedTests = 1,
            SkippedTests = 2,
            Failures = [new GitHubTest { DisplayName = "Failed", FullyQualifiedName = "Tests.Failed", ErrorMessage = "retained failure" }],
        };
}

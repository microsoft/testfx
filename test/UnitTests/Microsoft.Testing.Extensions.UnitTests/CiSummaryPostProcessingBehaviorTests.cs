// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

using GitHub = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;
using GitHubAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubHistoryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryHistoryTest;
using GitHubModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubSummaryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiSummaryPostProcessingBehaviorTests
{
    [TestMethod]
    public async Task AzureDevOps_ExplicitDestinationIsWrittenAndUploadedOnlyOnce()
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        fixture.Options[ArtifactPostProcessingDispatcherToolCommandLine.ManifestOptionName] = ["owned-manifest.json"];
        string requested = Path.Combine(fixture.Root, "requested;summary.md");
        var module = new CiRunSummaryModule
        {
            AssemblyName = "Reporter.Tests",
            ModulePath = Path.Combine(fixture.Root, "tests.dll"),
            TargetFramework = "net8.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session",
            RequestedOutputPath = requested,
            AttemptNumber = 1,
            TotalTests = 3,
            PassedTests = 1,
            FailedTests = 1,
            SkippedTests = 1,
            ExitCode = 2,
        };
        string fragment = await CiRunSummaryAggregation.WriteFragmentAsync(fixture.Root, "azure-devops", "azure-devops", module);
        InputArtifact[] inputs = [new(fragment, "microsoft.testing.azure-devops-summary-fragment", module.ModulePath, "net8.0", "x64", "execution")];
        var processor = new AzureDevOpsSummaryArtifactPostProcessor(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.Output.Object);
        var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);

        Assert.IsTrue(await processor.IsEnabledAsync());
        ProcessedArtifact? first = await processor.ProcessAsync(inputs, fixture.Root, context, CancellationToken.None);
        ProcessedArtifact? second = await processor.ProcessAsync(inputs, fixture.Root, context, CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(first.Path, second.Path);
        Assert.AreEqual("microsoft.testing.azure-devops-summary", first.Kind);
        string markdown = File.ReadAllText(first.Path);
        Assert.Contains("**3 tests**", markdown);
        Assert.Contains("Reporter.Tests", markdown);
        Assert.Contains("| 3 | 1 | 1 | 1 |", markdown);
        Assert.AreEqual(markdown, File.ReadAllText(requested));
        string aggregationId = CiRunSummaryAggregation.CreateAggregationId(inputs);
        Assert.AreEqual(aggregationId, File.ReadAllText(first.Path + ".uploaded"));
        (IOutputDeviceDataProducer owner, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(fixture.DisplayAttempts);
        Assert.AreSame(processor, owner);

        Assert.AreEqual("Microsoft.Testing.Extensions.AzureDevOpsReport.SummaryPostProcessor", owner.Uid);
        Assert.IsNotEmpty(owner.Version);
        Assert.AreEqual(first.DisplayName, owner.DisplayName);
        Assert.AreEqual(first.Description, owner.Description);
        Assert.AreEqual(
            $"##vso[task.addattachment type=Distributedtask.Core.Summary;name=Overall test results - {aggregationId};]{requested.Replace(";", "%3B")}",
            Assert.IsInstanceOfType<AzureDevOpsCommandOutputDeviceData>(data).Text);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryHistory_ConflictingDestinations")]
    [DataRow(true, DisplayName = "RetryHistory_ConsistentDestination")]
    public async Task GitHubActions_RetryWithoutVerdictPersistsOnlyMergeableHistory(bool consistent)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        string historyPath = Path.Combine(fixture.Root, "history.json");
        GitHubModule first = Module(fixture, 1, passed: 0, failed: 1, skipped: 1);
        first.GitHubActionsHistoryPath = historyPath;
        first.GitHubActionsHistoryWindowInDays = 7;
        first.HistoryTests = [History("Tests.A", "failed", 11), History("Tests.B", "skipped", 17)];
        GitHubModule last = Module(fixture, 2, passed: 1);
        last.GitHubActionsHistoryPath = consistent ? historyPath : Path.Combine(fixture.Root, "other-history.json");
        last.GitHubActionsHistoryWindowInDays = 30;
        last.HistoryTests = [History("Tests.A", "passed", 29)];
        InputArtifact[] inputs = [await FragmentAsync(fixture, last), await FragmentAsync(fixture, first)];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, Mock<GitHub.IGitHubActionsHistoryService> history, List<GitHubModule[]> writes) = Processor(fixture);

        ProcessedArtifact? output = await processor.ProcessAsync(inputs, fixture.Root, RetryContext(authoritative: false), CancellationToken.None);

        Assert.IsNull(output);
        GitHubModule written = Assert.ContainsSingle(Assert.ContainsSingle(writes));
        Assert.AreEqual("Reporter.Tests", written.AssemblyName);
        Assert.AreEqual("session-2", written.SessionUid);
        Assert.AreEqual(consistent ? historyPath : null, written.GitHubActionsHistoryPath);
        Assert.AreEqual(30, written.GitHubActionsHistoryWindowInDays);
        Assert.AreSequenceEqual(["Tests.A", "Tests.B"], written.HistoryTests.Select(test => test.TestId));
        Assert.AreSequenceEqual(["passed", "skipped"], written.HistoryTests.Select(test => test.Outcome));
        Assert.AreSequenceEqual([29L, 17L], written.HistoryTests.Select(test => test.DurationTicks));
        Assert.IsTrue(written.HistoryTests[0].IsFlaky);
        Assert.IsFalse(written.HistoryTests[1].IsFlaky);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, "merged")));
        Assert.IsEmpty(fixture.Opens);
        history.Verify(value => value.WriteAsync(It.IsAny<IReadOnlyList<GitHubModule>>(), CancellationToken.None), Times.Once);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetrySummary_DisabledWithoutDownstreamWritesHistoryOnly")]
    [DataRow(true, DisplayName = "RetrySummary_DisabledWithDownstreamReturnsFragment")]
    public async Task GitHubActions_DownstreamDeferralReturnsFragmentWithoutPublishingHistory(bool downstream)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        GitHubModule first = Module(fixture, 1, passed: 0, failed: 1, skipped: 1);
        first.GitHubActionsStepSummaryEnabled = false;
        first.GitHubActionsHistoryPath = Path.Combine(fixture.Root, "history.json");
        first.GitHubActionsHistoryWindowInDays = 7;
        first.HistoryTests = [History("Tests.A", "failed", 10), History("Tests.B", "skipped", 30)];
        first.TestDurationTicks = 40;
        first.SlowestTests = [Slow("Tests.A", 10), Slow("Tests.B", 30)];
        GitHubModule last = Module(fixture, 2, passed: 1);
        last.GitHubActionsStepSummaryEnabled = false;
        last.GitHubActionsHistoryPath = first.GitHubActionsHistoryPath;
        last.GitHubActionsHistoryWindowInDays = 30;
        last.HistoryTests = [History("Tests.A", "passed", 20)];
        last.TestDurationTicks = 20;
        last.SlowestTests = [Slow("Tests.A", 20)];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, Mock<GitHub.IGitHubActionsHistoryService> history, List<GitHubModule[]> writes) = Processor(fixture, downstream);

        ProcessedArtifact? output = await processor.ProcessAsync(
            [await FragmentAsync(fixture, first), await FragmentAsync(fixture, last)], fixture.Root, RetryContext(authoritative: true), CancellationToken.None);

        if (!downstream)
        {
            Assert.IsNull(output);
            GitHubModule historyOnly = Assert.ContainsSingle(Assert.ContainsSingle(writes));
            Assert.AreSequenceEqual(["Tests.A", "Tests.B"], historyOnly.HistoryTests.Select(test => test.TestId));
            Assert.IsTrue(historyOnly.HistoryTests[0].IsFlaky);
            Assert.IsEmpty(fixture.Opens);
            return;
        }

        Assert.IsNotNull(output);
        Assert.AreEqual("microsoft.testing.github-actions-summary-fragment", output.Kind);
        using var document = JsonDocument.Parse(File.ReadAllText(output.Path));
        JsonElement module = document.RootElement.GetProperty("module");
        Assert.AreEqual(2, module.GetProperty("totalTests").GetInt32());
        Assert.AreEqual(1, module.GetProperty("passedTests").GetInt32());
        Assert.AreEqual(1, module.GetProperty("skippedTests").GetInt32());
        Assert.AreEqual(0, module.GetProperty("failedTests").GetInt32());
        Assert.AreEqual(1, module.GetProperty("attemptNumber").GetInt32());
        Assert.AreEqual("session-2", module.GetProperty("sessionUid").GetString());
        Assert.AreEqual(60L, module.GetProperty("testDurationTicks").GetInt64());
        Assert.IsFalse(module.GetProperty("gitHubActionsStepSummaryEnabled").GetBoolean());
        Assert.AreEqual(first.GitHubActionsHistoryPath, module.GetProperty("gitHubActionsHistoryPath").GetString());
        Assert.AreEqual(30, module.GetProperty("gitHubActionsHistoryWindowInDays").GetInt32());
        Assert.AreSequenceEqual(["Tests.B", "Tests.A"], module.GetProperty("slowestTests").EnumerateArray().Select(test => test.GetProperty("fullyQualifiedName").GetString()));
        Assert.AreSequenceEqual([30L, 20L], module.GetProperty("slowestTests").EnumerateArray().Select(test => test.GetProperty("durationTicks").GetInt64()));
        Assert.IsTrue(module.GetProperty("historyTests")[0].GetProperty("isFlaky").GetBoolean());
        Assert.IsEmpty(writes);
        history.Verify(value => value.WriteAsync(It.IsAny<IReadOnlyList<GitHubModule>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsEmpty(fixture.Opens);
    }

    [TestMethod]
    public async Task GitHubActions_HistoryIdentityLimitDoesNotPreventExistingIdentityUpdate()
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        GitHubModule first = Module(fixture, 1, passed: 9998, failed: 1);
        first.HistoryTests = [.. Enumerable.Range(0, 9999).Select(index => History($"Tests.Case{index:D5}", index == 0 ? "failed" : "passed", index))];
        GitHubModule last = Module(fixture, 2, passed: 2);
        last.HistoryTests = [History("Tests.Case00000", "passed", 20000), History("Tests.Excess", "passed", 30000)];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, _, List<GitHubModule[]> writes) = Processor(fixture);

        _ = await processor.ProcessAsync(
            [await FragmentAsync(fixture, first), await FragmentAsync(fixture, last)], fixture.Root, RetryContext(authoritative: false), CancellationToken.None);

        GitHubModule written = Assert.ContainsSingle(Assert.ContainsSingle(writes));
        Assert.HasCount(9999, written.HistoryTests);
        Assert.AreEqual("Tests.Case00000", written.HistoryTests[0].TestId);
        Assert.AreEqual("passed", written.HistoryTests[0].Outcome);
        Assert.AreEqual(20000L, written.HistoryTests[0].DurationTicks);
        Assert.IsTrue(written.HistoryTests[0].IsFlaky);
        Assert.AreEqual("Tests.Case09998", written.HistoryTests[^1].TestId);
        Assert.DoesNotContain(test => test.TestId == "Tests.Excess", written.HistoryTests);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "GitHubSummary_WriteFailureKeepsArtifact")]
    [DataRow(true, DisplayName = "GitHubSummary_SizeRefusalKeepsArtifact")]
    public async Task GitHubActions_WriteFailureOrRefusalKeepsStandaloneArtifact(bool refused)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        string path = Path.Combine(fixture.Root, "step.md");
        fixture.Variables["GITHUB_STEP_SUMMARY"] = path;
        string original = refused ? new string('x', GitHub.GitHubActionsFailureDetails.EffectiveStepSummaryLimit + 1) : "foreign content";
        fixture.PutText(path, original);
        if (!refused)
        {
            fixture.BeforeOpen = (opened, _, _, _) =>
            {
                if (opened == path + ".microsoft-testing-platform.lock")
                {
                    throw new NotSupportedException("writer unavailable");
                }
            };
        }

        GitHubModule module = Module(fixture, 1, passed: 1, failed: 1);
        module.Failures = [new GitHubSummaryTest { DisplayName = "Failed row", FullyQualifiedName = "Tests.Failed", ErrorMessage = "literal failure" }];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, _, List<GitHubModule[]> writes) = Processor(fixture);
        var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);

        ProcessedArtifact? output = await processor.ProcessAsync([await FragmentAsync(fixture, module, retry: false)], fixture.Root, context, CancellationToken.None);

        Assert.IsNotNull(output);
        Assert.AreEqual("microsoft.testing.github-actions-summary", output.Kind);
        Assert.Contains("| 2 | 1 | 1 | 0 |", File.ReadAllText(output.Path));
        Assert.Contains("literal failure", File.ReadAllText(output.Path));
        Assert.AreEqual(original, fixture.ReadText(path));
        Assert.ContainsSingle(writes);
        Assert.HasCount(1, fixture.Logs.Where(entry => entry.Level == Microsoft.Testing.Platform.Logging.LogLevel.Warning));
        Assert.Contains(refused ? "limit" : "writer unavailable", fixture.LogText, StringComparison.OrdinalIgnoreCase);
        Assert.IsEmpty(fixture.Replacements);
        Assert.HasCount(refused ? 2 : 1, fixture.Opens.Where(open => open.Path == path + ".microsoft-testing-platform.lock"));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "GitHubSummary_PreCanceledProcessing")]
    [DataRow(true, DisplayName = "GitHubSummary_WriterCancellation")]
    public async Task GitHubActions_CanceledWriterDoesNotBecomeWarning(bool duringWrite)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        string path = Path.Combine(fixture.Root, "step.md");
        fixture.Variables["GITHUB_STEP_SUMMARY"] = path;
        using var cancellation = new CancellationTokenSource();
        var original = new OperationCanceledException(cancellation.Token);
        if (duringWrite)
        {
            fixture.BeforeOpen = (opened, _, _, _) =>
            {
                if (opened == path + ".microsoft-testing-platform.lock")
                {
                    throw original;
                }
            };
        }
        else
        {
            cancellation.Cancel();
        }

        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, _, List<GitHubModule[]> writes) = Processor(fixture);
        InputArtifact input = await FragmentAsync(fixture, Module(fixture, 1, passed: 1), retry: false);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ProcessAsync(
            [input], fixture.Root, new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        if (duringWrite)
        {
            Assert.AreSame(original, error);
            Assert.ContainsSingle(writes);
            Assert.ContainsSingle(Directory.GetFiles(Path.Combine(fixture.Root, "merged"), "*.md"));
        }
        else
        {
            Assert.IsEmpty(writes);
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, "merged")));
        }

        Assert.IsEmpty(fixture.Logs.Where(entry => entry.Level == Microsoft.Testing.Platform.Logging.LogLevel.Warning));
        Assert.IsFalse(fixture.Files.ContainsKey(path));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "GitHubSummary_DetailBudgetKeepsNamedFailures")]
    [DataRow(true, DisplayName = "GitHubSummary_CondenseBudgetDropsWholeModuleSection")]
    public async Task GitHubActions_ExistingSummaryBudgetSeparatesArtifactFromCondensedStepOutput(bool wholeSections)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        string path = Path.Combine(fixture.Root, "step.md");
        fixture.Variables["GITHUB_STEP_SUMMARY"] = path;
        int occupied = wholeSections ? GitHub.GitHubActionsFailureDetails.CondenseLength : GitHub.GitHubActionsFailureDetails.DetailBudgetLength;
        string original = GitHub.GitHubActionsSummaryReporter.ProjectSectionMarker + "\nforeign project\n" + new string('x', occupied + 1024);
        fixture.PutText(path, original);
        GitHubModule module = Module(fixture, 1, passed: 0, failed: 1);
        module.Failures =
        [
            new GitHubSummaryTest { DisplayName = "failed row", FullyQualifiedName = "Tests.Failed", ErrorMessage = "literal diagnostic retained in standalone artifact" },
        ];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, _, _) = Processor(fixture);

        ProcessedArtifact? output = await processor.ProcessAsync(
            [await FragmentAsync(fixture, module, retry: false)], fixture.Root,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None), CancellationToken.None);

        Assert.IsNotNull(output);
        Assert.Contains("literal diagnostic retained in standalone artifact", File.ReadAllText(output.Path));
        string step = fixture.ReadText(path);
        Assert.Contains(original, step);
        Assert.DoesNotContain("literal diagnostic retained in standalone artifact", step);
        Assert.AreEqual(wholeSections ? 2 : 1, GitHub.StepSummaryWriter.GetLeadingNoticeStrength(step));
        Assert.AreEqual(wholeSections ? 1 : 2, GitHub.StepSummaryWriter.CountProjectSections(step));
        if (wholeSections)
        {
            Assert.DoesNotContain("Tests.Failed", step);
            Assert.StartsWith(GitHub.GitHubActionsSummaryReporter.BuildTruncationNotice(1), step);
            Assert.Contains("1 total, 0 passed, 1 failed, 0 skipped", step);
        }
        else
        {
            Assert.Contains("Tests.Failed", step);
            Assert.StartsWith(GitHub.GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(1, 1), step);
        }

        Assert.IsLessThanOrEqualTo(GitHub.GitHubActionsFailureDetails.EffectiveStepSummaryLimit, System.Text.Encoding.UTF8.GetByteCount(step));
        Assert.IsEmpty(fixture.Logs.Where(entry => entry.Level == Microsoft.Testing.Platform.Logging.LogLevel.Warning));
    }

    [TestMethod]
    public async Task GitHubActions_CondensedFallbackFitsAfterFullWriteIsRefused()
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        string path = Path.Combine(fixture.Root, "step.md");
        fixture.Variables["GITHUB_STEP_SUMMARY"] = path;
        string prefix = GitHub.GitHubActionsSummaryReporter.ProjectSectionMarker + "\nforeign project\n";
        string original = prefix + new string('x', GitHub.GitHubActionsFailureDetails.EffectiveStepSummaryLimit - 2048 - prefix.Length);
        fixture.PutText(path, original);
        // Only the pre-render measurement fails. Both actual writes still see the complete foreign content.
        fixture.FileSystem.Setup(value => value.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Throws(new NotSupportedException("pre-render measurement unavailable"));
        GitHubModule module = Module(fixture, 1, passed: 0, failed: 2);
        module.Failures =
        [
            new GitHubSummaryTest { DisplayName = "first failed row", FullyQualifiedName = "Tests.FailedA", ErrorMessage = new string('a', 2000) },
            new GitHubSummaryTest { DisplayName = "second failed row", FullyQualifiedName = "Tests.FailedB", ErrorMessage = new string('b', 2000) },
        ];
        (GitHub.GitHubActionsSummaryArtifactPostProcessor processor, _, _) = Processor(fixture);

        ProcessedArtifact? output = await processor.ProcessAsync(
            [await FragmentAsync(fixture, module, retry: false)], fixture.Root,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None), CancellationToken.None);

        Assert.IsNotNull(output);
        Assert.Contains(new string('a', 2000), File.ReadAllText(output.Path));
        Assert.Contains(new string('b', 2000), File.ReadAllText(output.Path));
        string step = fixture.ReadText(path);
        Assert.StartsWith(GitHub.GitHubActionsSummaryReporter.BuildTruncationNotice(1), step);
        Assert.Contains(original, step);
        Assert.Contains("2 total, 0 passed, 2 failed, 0 skipped", step);
        Assert.DoesNotContain("Tests.FailedA", step);
        Assert.DoesNotContain("Tests.FailedB", step);
        Assert.HasCount(2, fixture.Opens.Where(open => open.Path == path + ".microsoft-testing-platform.lock"));
        Assert.AreSequenceEqual([path], fixture.Replacements);
        Assert.IsLessThanOrEqualTo(GitHub.GitHubActionsFailureDetails.EffectiveStepSummaryLimit, System.Text.Encoding.UTF8.GetByteCount(step));
        Assert.IsEmpty(fixture.Logs.Where(entry => entry.Level == Microsoft.Testing.Platform.Logging.LogLevel.Warning));
    }

    private static (GitHub.GitHubActionsSummaryArtifactPostProcessor Processor, Mock<GitHub.IGitHubActionsHistoryService> History, List<GitHubModule[]> Writes) Processor(CiReporterFixture fixture, bool downstream = false)
    {
        var history = new Mock<GitHub.IGitHubActionsHistoryService>();
        history.SetupGet(value => value.IsEnabled).Returns(true);
        List<GitHubModule[]> writes = [];
        history.Setup(value => value.WriteAsync(It.IsAny<IReadOnlyList<GitHubModule>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<GitHubModule>, CancellationToken>((modules, _) => writes.Add([.. modules]))
            .Returns(Task.CompletedTask);
        return (new(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.FileSystem.Object, fixture.LoggerFactory.Object, () => downstream, history.Object), history, writes);
    }

    private static GitHubModule Module(CiReporterFixture fixture, int attempt, long passed, long failed = 0, long skipped = 0)
        => new()
        {
            AssemblyName = "Reporter.Tests",
            ModulePath = Path.Combine(fixture.Root, "tests.dll"),
            TargetFramework = "net8.0",
            Architecture = "x64",
            ExecutionId = "owned-execution",
            SessionUid = $"session-{attempt}",
            AttemptNumber = attempt,
            ExitCode = failed > 0 ? 2 : 0,
            TotalTests = passed + failed + skipped,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
        };

    private static GitHubHistoryTest History(string id, string outcome, long duration)
        => new() { TestId = id, FullyQualifiedName = id, DisplayName = id, Outcome = outcome, DurationTicks = duration };

    private static GitHubSummaryTest Slow(string id, long duration)
        => new() { FullyQualifiedName = id, DisplayName = id, DurationTicks = duration };

    private static async Task<InputArtifact> FragmentAsync(CiReporterFixture fixture, GitHubModule module, bool retry = true)
    {
        string path = await GitHubAggregation.WriteFragmentAsync(fixture.Root, "github-actions", "github-actions", module);
        return new(path, "microsoft.testing.github-actions-summary-fragment", module.ModulePath, module.TargetFramework, module.Architecture,
            retry ? module.AttemptNumber.ToString(CultureInfo.InvariantCulture) : module.ExecutionId);
    }

    private static ArtifactPostProcessingContext RetryContext(bool authoritative)
        => new(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts,
            authoritative ? new ArtifactPostProcessingRunSummary(2, 1, 0, 1, TimeSpan.FromSeconds(1), 0, 1) : null);
}

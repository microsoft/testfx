// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using GitHub = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;
using GitHubModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiSummaryLifecycleBehaviorTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "AzureSummary_StartupWarningOutputFailure")]
    [DataRow(1, DisplayName = "AzureSummary_DuplicateTiming")]
    [DataRow(2, DisplayName = "AzureSummary_HistoryLookupFailure")]
    [DataRow(3, DisplayName = "AzureSummary_AttachmentOutputFailure")]
    public async Task AzureDevOps_CallbackFailuresDoNotInventSummary(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [path];
        var history = new Mock<IAzureDevOpsHistoryService>();
        FlakyStats unused = default;
        history.Setup(value => value.TryGetStats("Tests.Malformed", out unused)).Throws(new IOException("history lookup unavailable"));
        AzureDevOpsSummaryReporter reporter = failure == 2 ? Azure(fixture, history: history.Object) : Azure(fixture, legacyConstructor: true);
        if (failure == 0)
        {
            fixture.Variables["TF_BUILD"] = null;
            fixture.DisplayError = _ => new IOException("startup output unavailable");
            await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
            Assert.IsFalse(fixture.Files.ContainsKey(path));
            fixture.Variables["TF_BUILD"] = "true";
            fixture.DisplayError = null;
        }

        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        if (failure is 1 or 2)
        {
            TestNodeUpdateMessage malformed = failure == 1
                ? CiReporterFixture.Update("Tests.Malformed", new FailedTestNodeStateProperty("bad row"), Timing(1), Timing(2))
                : CiReporterFixture.Update("Tests.Malformed", new FailedTestNodeStateProperty("history unavailable"));
            await reporter.ConsumeAsync(null!, malformed, CancellationToken.None);
        }

        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Healthy", new PassedTestNodeStateProperty(), Timing(3)), CancellationToken.None);
        if (failure == 3)
        {
            fixture.DisplayError = _ => new IOException("attachment output unavailable");
        }

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        Assert.Contains("**1 test**", fixture.ReadText(path));
        Assert.Contains("**1 passed**", fixture.ReadText(path));
        Assert.Contains("**0 failed**", fixture.ReadText(path));
        Assert.DoesNotContain("Tests.Malformed", fixture.ReadText(path));
        Assert.IsEmpty(fixture.Publications);
        string callback = failure switch { 0 => "OnTestSessionStartingAsync", 3 => "OnTestSessionFinishingAsync", _ => "ConsumeAsync" };
        Assert.HasCount(1, fixture.Logs.Where(entry => entry.Message.StartsWith("Unexpected exception in " + callback, StringComparison.Ordinal)));
        Assert.HasCount(failure == 3 ? 0 : 1, fixture.Displays.OfType<AzureDevOpsCommandOutputDeviceData>());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "AzureSummary_CanceledStart")]
    [DataRow(1, DisplayName = "AzureSummary_CanceledConsume")]
    [DataRow(2, DisplayName = "AzureSummary_CanceledFinish")]
    public async Task AzureDevOps_CancellationIsNotConvertedToWarning(int callback)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [Path.Combine(fixture.Root, "summary.md")];
        AzureDevOpsSummaryReporter reporter = Azure(fixture);
        if (callback == 2)
        {
            await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new CiReporterFixture.Session(cancellation.Token);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => callback switch
        {
            0 => reporter.OnTestSessionStartingAsync(session),
            1 => reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Canceled", new PassedTestNodeStateProperty()), cancellation.Token),
            _ => reporter.OnTestSessionFinishingAsync(session),
        });

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsEmpty(fixture.Files);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Publications);
    }

    [TestMethod]
    public async Task AzureDevOps_DeferredFragmentPreservesHistoryDependenciesAndBoundedFailures()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.CreateRealDirectory();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [];
        fixture.Variables["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "2";
        var history = new Mock<IAzureDevOpsHistoryService>();
        FlakyStats counts = new(7, 3);
        DurationHistoryStats durations = new(150, 250, 8);
        history.Setup(value => value.TryGetStats(It.IsAny<string>(), out counts)).Returns(true);
        history.Setup(value => value.TryGetDurationStats(It.IsAny<string>(), out durations)).Returns(true);
        history.SetupGet(value => value.HistoryWindowInDays).Returns(14);
        AzureDevOpsSummaryReporter reporter = Azure(fixture, defer: true, history: history.Object);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Retry", new FailedTestNodeStateProperty("superseded"), new RetryAttemptProperty(1, true)), CancellationToken.None);
        string[] dependencies = ["PClass\nMethod", "SClass\n", "BareMethod", "P\nMethod", "SClass\nMethod", "PClass\nMethod", "P", string.Empty, "\t", new string('x', 1025)];
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update(
            "Tests.Retry",
        [
            new PassedTestNodeStateProperty(), new RetryAttemptProperty(2, false), Timing(1),
            .. dependencies.Select(Dependency),
        ]), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Retry", new PassedTestNodeStateProperty(), Timing(1)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Skipped", new SkippedTestNodeStateProperty("intentional"), Timing(1)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Timeout", new TimeoutTestNodeStateProperty(new IOException("exception fallback"), " "), Timing(1)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Error", new ErrorTestNodeStateProperty(new InvalidOperationException("ignored"), new string('e', 4097)), Timing(1)), CancellationToken.None);
#pragma warning disable CS0618, MTP0001
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Cancel", new CancelledTestNodeStateProperty(new IOException("cancel detail")), Timing(1)), CancellationToken.None);
#pragma warning restore CS0618, MTP0001
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.EmptyFailed", new FailedTestNodeStateProperty(), Timing(1)), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Publications));
        Assert.AreEqual("microsoft.testing.azure-devops-summary-fragment", artifact.Kind);
        Assert.AreEqual(new SessionUid("ci-session"), artifact.SessionUid);
        using var document = JsonDocument.Parse(File.ReadAllText(artifact.FileInfo.FullName));
        JsonElement module = document.RootElement.GetProperty("module");
        Assert.AreEqual(7, module.GetProperty("totalTests").GetInt32());
        Assert.AreEqual(2, module.GetProperty("attemptNumber").GetInt32());
        Assert.AreEqual(2, module.GetProperty("passedTests").GetInt32());
        Assert.AreEqual(4, module.GetProperty("failedTests").GetInt32());
        Assert.AreEqual(1, module.GetProperty("skippedTests").GetInt32());
        Assert.AreEqual(TimeSpan.FromSeconds(7).Ticks, module.GetProperty("testDurationTicks").GetInt64());
        Assert.IsEmpty(module.GetProperty("flakyTests").EnumerateArray());
        JsonElement[] histories = [.. module.GetProperty("historyTests").EnumerateArray()];
        Assert.AreSequenceEqual(["failed", "failed", "failed", "passed", "passed", "skipped", "failed"], histories.Select(item => item.GetProperty("outcome").GetString()));
        Assert.IsTrue(histories.All(item => !item.GetProperty("isFlaky").GetBoolean()
            && item.GetProperty("historicalPassCount").GetInt32() == 7
            && item.GetProperty("historicalFailCount").GetInt32() == 3
            && item.GetProperty("durationSampleCount").GetInt32() == 8
            && item.GetProperty("p95DurationMilliseconds").GetDouble() == 150
            && item.GetProperty("p99DurationMilliseconds").GetDouble() == 250
            && item.GetProperty("historyWindowInDays").GetInt32() == 14));
        JsonElement[] edges = [.. module.GetProperty("dependencies").EnumerateArray()];
        Assert.AreSequenceEqual(["BareMethod", "Class.*", "Class.Method", "Class.Method", "Method"], edges.Select(edge => edge.GetProperty("prerequisite").GetString()));
        Assert.AreSequenceEqual([false, false, false, true, true], edges.Select(edge => edge.GetProperty("proceedOnFailure").GetBoolean()));
        Assert.IsTrue(edges.All(edge => edge.GetProperty("dependentFullyQualifiedName").GetString() == "Tests.Retry"));
        JsonElement[] failures = [.. module.GetProperty("failures").EnumerateArray()];
        Assert.AreEqual("cancel detail", failures[0].GetProperty("errorMessage").GetString());
        Assert.IsFalse(failures[1].TryGetProperty("errorMessage", out _));
        Assert.AreEqual(new string('e', 4096) + "\n…[truncated, original length: 4097]", failures[2].GetProperty("errorMessage").GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, failures[2].GetProperty("errorType").GetString());
        Assert.AreEqual("exception fallback", failures[3].GetProperty("errorMessage").GetString());
        Assert.DoesNotContain(failure => failure.TryGetProperty("stackTrace", out _), failures);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AzureSummary_ThirtyTwoDependencies")]
    [DataRow(true, DisplayName = "AzureSummary_SixtyFourCandidateLimit")]
    public async Task AzureDevOps_DependencyBoundsRejectExcessCandidates(bool candidates)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.CreateRealDirectory();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [];
        var emptyHistory = new Mock<IAzureDevOpsHistoryService>();
        FlakyStats emptyCounts = default;
        DurationHistoryStats emptyDurations = default;
        emptyHistory.Setup(value => value.TryGetStats(It.IsAny<string>(), out emptyCounts)).Returns(true);
        emptyHistory.Setup(value => value.TryGetDurationStats(It.IsAny<string>(), out emptyDurations)).Returns(true);
        AzureDevOpsSummaryReporter reporter = Azure(fixture, defer: true, history: emptyHistory.Object);
        string[] encoded = candidates
            ? [.. Enumerable.Repeat("Existing\nBefore", 64), "Beyond\nIgnored"]
            : [new string('x', 1025), .. Enumerable.Range(0, 33).Select(index => $"C{index:D2}\nInit")];
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update(
            "Tests.Dependent",
            [new PassedTestNodeStateProperty(), .. encoded.Select(Dependency)]), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Publications));
        using var document = JsonDocument.Parse(File.ReadAllText(artifact.FileInfo.FullName));
        Assert.IsEmpty(document.RootElement.GetProperty("module").GetProperty("historyTests").EnumerateArray());
        string?[] actual = [.. document.RootElement.GetProperty("module").GetProperty("dependencies").EnumerateArray().Select(edge => edge.GetProperty("prerequisite").GetString())];
        IEnumerable<string> expected = candidates ? ["Existing.Before"] : Enumerable.Range(0, 32).Select(index => $"C{index:D2}.Init");
        Assert.AreSequenceEqual(expected, actual);
        Assert.DoesNotContain("Beyond.Ignored", actual);
        Assert.DoesNotContain("C32.Init", actual);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "AzureSummary_WhitespaceResultsDirectory")]
    [DataRow(1, DisplayName = "AzureSummary_WriteIoFailure")]
    [DataRow(2, DisplayName = "AzureSummary_InvalidExplicitPath")]
    [DataRow(3, DisplayName = "AzureSummary_DisabledCallbacks")]
    public async Task AzureDevOps_MissingPathAndWriteFailureNeverEmitAttachment(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = failure switch { 0 => [], 2 => ["invalid\0path"], _ => [path] };
        if (failure == 3)
        {
            fixture.Options.Remove(AzureDevOpsCommandLineOptions.AzureDevOpsSummary);
        }

        if (failure == 0)
        {
            fixture.ResultsDirectory = " ";
        }
        else if (failure == 1)
        {
            fixture.BeforeOpen = (_, _, _, _) => throw new IOException("summary write denied");
        }

        AzureDevOpsSummaryReporter reporter = Azure(fixture);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Passed", new PassedTestNodeStateProperty()), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.IsEmpty(fixture.Publications);
        Assert.IsEmpty(fixture.Displays.OfType<AzureDevOpsCommandOutputDeviceData>());
        Assert.IsFalse(fixture.Files.ContainsKey(path));
        if (failure == 1)
        {
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.SummaryWriteFailedWarning, path, "summary write denied"),
                Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Displays)).Message);
        }
        else if (failure != 3)
        {
            Assert.IsEmpty(fixture.Displays);
            Assert.Contains(failure == 0 ? "Could not resolve Azure DevOps summary path." : "Unexpected exception in OnTestSessionFinishingAsync", fixture.LogText);
        }
        else
        {
            Assert.IsEmpty(fixture.Displays);
            Assert.IsEmpty(fixture.Logs);
        }
    }

    [TestMethod]
    public async Task GitHubActions_MalformedUpdateDoesNotPoisonFollowingHistory()
    {
        using var fixture = new CiReporterFixture();
        var (reporter, writes) = GitHubHistoryOnly(fixture);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Malformed", new PassedTestNodeStateProperty(), new RetryAttemptProperty(1, false), new RetryAttemptProperty(2, false)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Nonterminal", new InProgressTestNodeStateProperty()), CancellationToken.None);
        await reporter.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Healthy", new PassedTestNodeStateProperty(), Timing(1)), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        GitHubModule module = Assert.ContainsSingle(Assert.ContainsSingle(writes));
        Assert.AreEqual(1L, module.TotalTests);
        Assert.AreEqual(1L, module.PassedTests);
        Assert.AreEqual("Tests.Healthy", Assert.ContainsSingle(module.HistoryTests).FullyQualifiedName);
        Assert.AreEqual("passed", module.HistoryTests[0].Outcome);
        Assert.AreEqual(TimeSpan.FromSeconds(1).Ticks, module.HistoryTests[0].DurationTicks);
        Assert.Contains("Unexpected exception in ConsumeAsync", Assert.ContainsSingle(fixture.Logs).Message);
        Assert.Contains("RetryAttemptProperty", fixture.LogText);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Files);
    }

    [TestMethod]
    public async Task GitHubActions_ResetAndMixedUidOutcomesDoNotInventRecovery()
    {
        using var fixture = new CiReporterFixture();
        var (reporter, writes) = GitHubHistoryOnly(fixture);
        fixture.Variables["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "2";
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.PreviousSession", new FailedTestNodeStateProperty("previous")), CancellationToken.None);
        // This callback deliberately resets even when its context is canceled.
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session(new CancellationToken(canceled: true)));
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Mixed", new FailedTestNodeStateProperty("retry"), new RetryAttemptProperty(1, true)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Mixed", new PassedTestNodeStateProperty(), new RetryAttemptProperty(2, false), Timing(2)), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Mixed", new SkippedTestNodeStateProperty("folded row"), Timing(3)), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        GitHubModule module = Assert.ContainsSingle(Assert.ContainsSingle(writes));
        Assert.AreEqual(2L, module.TotalTests);
        Assert.AreEqual(2, module.AttemptNumber);
        Assert.AreEqual(1L, module.PassedTests);
        Assert.AreEqual(1L, module.SkippedTests);
        Assert.AreEqual(0L, module.FailedTests);
        Assert.IsEmpty(module.Failures);
        Assert.IsEmpty(module.FlakyTests);
        var history = Assert.ContainsSingle(module.HistoryTests);
        Assert.AreEqual("Tests.Mixed", history.FullyQualifiedName);
        Assert.AreEqual("passed", history.Outcome);
        Assert.AreEqual(TimeSpan.FromSeconds(3).Ticks, history.DurationTicks);
        Assert.IsFalse(history.IsFlaky);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task GitHubActions_CanceledConsumePreservesToken()
    {
        using var fixture = new CiReporterFixture();
        var (reporter, writes) = GitHubHistoryOnly(fixture);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Canceled", new PassedTestNodeStateProperty()), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsEmpty(writes);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task GitHubActions_DisabledSummaryIgnoresUpdatesAndFinish()
    {
        using var fixture = new CiReporterFixture();
        var (reporter, writes) = GitHubHistoryOnly(fixture, enabled: false);
        Assert.IsFalse(await reporter.IsEnabledAsync());
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Ignored", new FailedTestNodeStateProperty("ignored"),
            new RetryAttemptProperty(1, false), new RetryAttemptProperty(2, false)), CancellationToken.None);
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.IsEmpty(writes);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Opens);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Publications);
    }

    private static AzureDevOpsSummaryReporter Azure(CiReporterFixture fixture, bool defer = false, IAzureDevOpsHistoryService? history = null, bool legacyConstructor = false)
        => legacyConstructor
            ? new(new TestCommandLineOptions(fixture.Options), fixture.Configuration.Object, fixture.Environment.Object, fixture.FileSystem.Object, fixture.Bus.Object,
                fixture.Output.Object, fixture.Module.Object, fixture.ExitCode.Object, fixture.Coverage.Object, fixture.LoggerFactory.Object, () => defer)
            : new(new TestCommandLineOptions(fixture.Options), fixture.Configuration.Object, fixture.Environment.Object, fixture.FileSystem.Object, fixture.Bus.Object,
                fixture.Output.Object, fixture.Module.Object, fixture.ExitCode.Object, fixture.Coverage.Object, fixture.LoggerFactory.Object, () => defer, history);

    private static (GitHub.GitHubActionsSummaryReporter Reporter, List<GitHubModule[]> Writes) GitHubHistoryOnly(CiReporterFixture fixture, bool enabled = true)
    {
        fixture.ResultsDirectory = fixture.Root;
        fixture.Variables["GITHUB_ACTIONS"] = "true";
        fixture.Options[GitHub.GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];
        fixture.Options[GitHub.GitHubActionsCommandLineOptions.GitHubActionsStepSummary] = ["off"];
        var history = new Mock<GitHub.IGitHubActionsHistoryService>();
        history.SetupGet(value => value.IsEnabled).Returns(enabled);
        history.SetupGet(value => value.HistoryPath).Returns(Path.Combine(fixture.Root, "history.json"));
        history.SetupGet(value => value.HistoryWindowInDays).Returns(30);
        List<GitHubModule[]> writes = [];
        history.Setup(value => value.WriteAsync(It.IsAny<IReadOnlyList<GitHubModule>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<GitHubModule>, CancellationToken>((modules, _) => writes.Add([.. modules]))
            .Returns(Task.CompletedTask);
        return (new(new TestCommandLineOptions(fixture.Options), fixture.Configuration.Object, fixture.Environment.Object, fixture.FileSystem.Object, fixture.Bus.Object,
            fixture.Output.Object, fixture.Module.Object, fixture.ExitCode.Object, fixture.Coverage.Object, fixture.LoggerFactory.Object, () => false, history.Object), writes);
    }

    private static TimingProperty Timing(int seconds)
        => new(new TimingInfo(CiReporterFixture.Epoch, CiReporterFixture.Epoch.AddSeconds(seconds), TimeSpan.FromSeconds(seconds)));

    private static SerializableKeyValuePairStringProperty Dependency(string value)
        => new("mstest.TestCase.Dependency", value);
}

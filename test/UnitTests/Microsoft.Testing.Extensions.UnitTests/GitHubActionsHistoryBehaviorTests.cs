// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

using HistoryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using HistoryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryHistoryTest;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsHistoryBehaviorTests
{
    [TestMethod]
    public async Task Start_ExcludesOnlyCurrentRunAttemptAndClearsFailedReload()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.CreateRealDirectory(), "history.json");
        GitHubActionsHistorySample firstAttempt = Sample("current", 1, "failed");
        GitHubActionsHistorySample currentAttempt = Sample("current", 2, "failed");
        GitHubActionsHistorySample previousRun = Sample("previous", 2, "passed");
        GitHubActionsHistorySample expired = Sample("old", 1, "failed");
        expired.TimestampUtc = CiReporterFixture.Epoch.AddDays(-31);
        WriteSnapshot(path, firstAttempt, currentAttempt, previousRun, expired);
        fixture.Variables["GITHUB_RUN_ID"] = "current";
        fixture.Variables["GITHUB_RUN_ATTEMPT"] = "2";
        GitHubActionsHistoryService service = Service(fixture, path);

        Assert.IsTrue(await service.IsEnabledAsync());
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        Assert.IsTrue(service.TryGetStats("row", "Tests.Flaky", "Flaky row", out GitHubActionsHistoryStats stats));
        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(1, stats.FailCount);
        Assert.AreEqual(2, stats.DurationSampleCount);
        Assert.AreEqual(TimeSpan.FromTicks(1234), stats.P95Duration);
        foreach ((string Id, string Fqn, string Display) identity in new[]
        {
            (null!, "Tests.Flaky", "Flaky row"), ("row", " ", "Flaky row"), ("row", "Tests.Flaky", string.Empty),
        })
        {
            Assert.IsFalse(service.TryGetStats(identity.Id, identity.Fqn, identity.Display, out GitHubActionsHistoryStats empty));
            Assert.AreEqual(0, empty.TotalCount);
        }

        File.WriteAllText(path, "{ malformed", new UTF8Encoding(false));
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await service.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.IsFalse(service.TryGetStats("row", "Tests.Flaky", "Flaky row", out _));
        Assert.HasCount(1, fixture.Logs);
        Assert.AreEqual(LogLevel.Warning, fixture.Logs[0].Level);
        Assert.Contains(path, fixture.LogText);
        Assert.Contains("malformed", File.ReadAllText(path));
        Assert.IsTrue(File.Exists(path + ".lock"));
    }

    [TestMethod]
    public async Task Start_CanceledExistingSnapshotDoesNotReplacePublishedStats()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.CreateRealDirectory(), "history.json");
        WriteSnapshot(path, Sample("prior", 1, "passed"));
        GitHubActionsHistoryService service = Service(fixture, path);
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.OnTestSessionStartingAsync(new CiReporterFixture.Session(cancellation.Token)));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsTrue(service.TryGetStats("row", "Tests.Flaky", "Flaky row", out GitHubActionsHistoryStats stats));
        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(0, stats.FailCount);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Write_GroupsDestinationsAndPreservesScopeAndMaximumWindow()
    {
        using var fixture = new CiReporterFixture();
        string firstPath = Path.Combine(fixture.CreateRealDirectory(), "history.json");
        string secondPath = Path.Combine(fixture.Root, "nested", "second.json");
        GitHubActionsHistorySample old = Sample("older", 1, "passed");
        old.TimestampUtc = CiReporterFixture.Epoch.AddDays(-20);
        WriteSnapshot(firstPath, old);
        fixture.Variables["GITHUB_RUN_ID"] = "current";
        fixture.Variables["GITHUB_RUN_ATTEMPT"] = "3";
        fixture.Variables["GITHUB_SHA"] = "owned-commit";
        fixture.Variables["GITHUB_REF_NAME"] = "feature/owned";
        fixture.Variables["RUNNER_OS"] = "Linux";
        GitHubActionsHistoryService service = Service(fixture, firstPath);
        HistoryModule shortWindow = Module(firstPath, 7, "Tests.New", "passed");
        HistoryModule longWindow = Module(firstPath, 30, "Tests.Other", "failed");
        longWindow.AssemblyName = "Other.Tests";
        longWindow.HistoryTests[0].IsFlaky = true;
        HistoryModule second = Module(secondPath, 14, "Tests.Second", "skipped");

        await service.WriteAsync([shortWindow, longWindow, second, new HistoryModule()], CancellationToken.None);

        using var first = JsonDocument.Parse(File.ReadAllText(firstPath));
        JsonElement[] samples = [.. first.RootElement.GetProperty("samples").EnumerateArray()];
        Assert.HasCount(3, samples);
        Assert.HasCount(1, samples.Where(sample => sample.GetProperty("runId").GetString() == "older"));
        JsonElement newSample = samples.Single(sample => sample.GetProperty("fullyQualifiedName").GetString() == "Tests.New");
        Assert.AreEqual("Reporter.Tests", newSample.GetProperty("assemblyName").GetString());
        Assert.AreEqual("net8.0", newSample.GetProperty("targetFramework").GetString());
        Assert.AreEqual("x64", newSample.GetProperty("architecture").GetString());
        Assert.AreEqual("Linux", newSample.GetProperty("runnerOs").GetString());
        Assert.AreEqual("current", newSample.GetProperty("runId").GetString());
        Assert.AreEqual(3, newSample.GetProperty("runAttempt").GetInt32());
        Assert.AreEqual("owned-commit", newSample.GetProperty("commitSha").GetString());
        Assert.AreEqual("feature/owned", newSample.GetProperty("refName").GetString());
        Assert.AreEqual(CiReporterFixture.Epoch, newSample.GetProperty("timestampUtc").GetDateTimeOffset());
        Assert.AreEqual(5678L, newSample.GetProperty("durationTicks").GetInt64());
        JsonElement other = samples.Single(sample => sample.GetProperty("fullyQualifiedName").GetString() == "Tests.Other");
        Assert.AreEqual("Other.Tests", other.GetProperty("assemblyName").GetString());
        Assert.AreEqual("failed", other.GetProperty("outcome").GetString());
        Assert.IsTrue(other.GetProperty("isFlaky").GetBoolean());
        using var secondSnapshot = JsonDocument.Parse(File.ReadAllText(secondPath));
        JsonElement savedSecond = Assert.ContainsSingle(secondSnapshot.RootElement.GetProperty("samples").EnumerateArray());
        Assert.AreEqual("Tests.Second", savedSecond.GetProperty("fullyQualifiedName").GetString());
        Assert.AreEqual("skipped", savedSecond.GetProperty("outcome").GetString());
        Assert.IsEmpty(Directory.GetFiles(fixture.Root, "*.tmp", SearchOption.AllDirectories));
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "HistoryAttempt_NonnumericDefaultsToOne")]
    [DataRow(1, DisplayName = "HistoryAttempt_ZeroDefaultsToOne")]
    [DataRow(2, DisplayName = "HistoryAttempt_NegativeDefaultsToOne")]
    [DataRow(3, DisplayName = "HistoryAttempt_OverflowDefaultsToOne")]
    [DataRow(4, DisplayName = "HistoryAttempt_WhitespaceDefaultsToOne")]
    [DataRow(5, DisplayName = "HistoryAttempt_SignDefaultsToOne")]
    public async Task Write_UnsupportedSchemaWarnsAndContinuesNextDestination(int attempt)
    {
        using var fixture = new CiReporterFixture();
        string firstPath = Path.Combine(fixture.CreateRealDirectory(), "future.json");
        string secondPath = Path.Combine(fixture.Root, "valid.json");
        File.WriteAllText(firstPath, "{\"schemaVersion\":2,\"samples\":[]}", new UTF8Encoding(false));
        fixture.Variables["GITHUB_RUN_ATTEMPT"] = attempt switch { 0 => "invalid", 1 => "0", 2 => "-1", 3 => "2147483648", 4 => " 2 ", _ => "+2" };
        fixture.Variables["RUNNER_OS"] = "Linux";
        GitHubActionsHistoryService service = Service(fixture, firstPath);

        await service.WriteAsync([Module(firstPath, 30, "Tests.Future", "failed"), Module(secondPath, 30, "Tests.Valid", "passed")], CancellationToken.None);

        Assert.AreEqual("{\"schemaVersion\":2,\"samples\":[]}", File.ReadAllText(firstPath));
        Assert.HasCount(1, fixture.Logs);
        Assert.Contains(firstPath, fixture.LogText);
        Assert.Contains("newer schema version 2", fixture.LogText);
        using var document = JsonDocument.Parse(File.ReadAllText(secondPath));
        JsonElement sample = Assert.ContainsSingle(document.RootElement.GetProperty("samples").EnumerateArray());
        Assert.AreEqual("Tests.Valid", sample.GetProperty("fullyQualifiedName").GetString());
        Assert.AreEqual(string.Empty, sample.GetProperty("runId").GetString());
        Assert.AreEqual(1, sample.GetProperty("runAttempt").GetInt32());
        Assert.AreEqual(string.Empty, sample.GetProperty("commitSha").GetString());
        Assert.AreEqual(string.Empty, sample.GetProperty("refName").GetString());
    }

    [TestMethod]
    public async Task Write_NoConfiguredModulesDoesNotTouchArtifacts()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "unused.json");
        GitHubActionsHistoryService service = Service(fixture, path);

        await service.WriteAsync([new HistoryModule { HistoryTests = [new HistoryTest { FullyQualifiedName = "Tests.Ignored" }] }], CancellationToken.None);
        await DisabledGitHubActionsHistoryService.Instance.WriteAsync([Module(path, 30, "Tests.Disabled", "passed")], CancellationToken.None);

        Assert.IsFalse(Directory.Exists(fixture.Root));
        fixture.Clock.Verify(value => value.UtcNow, Times.Never);
        Assert.IsEmpty(fixture.Logs);
    }

    private static GitHubActionsHistoryService Service(CiReporterFixture fixture, string path)
    {
        fixture.Variables["GITHUB_ACTIONS"] = "true";
        fixture.Options[GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];
        fixture.Options[GitHubActionsCommandLineOptions.GitHubActionsHistory] = [path];
        fixture.Options[GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow] = ["30"];
        return new(
            new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.Clock.Object, fixture.LoggerFactory.Object,
            new GitHubActionsHistoryScope("Reporter.Tests", "net8.0", "x64", "Linux"));
    }

    private static GitHubActionsHistorySample Sample(string runId, int attempt, string outcome)
        => new()
        {
            RunId = runId,
            RunAttempt = attempt,
            TimestampUtc = CiReporterFixture.Epoch.AddDays(-1),
            AssemblyName = "Reporter.Tests",
            TargetFramework = "net8.0",
            Architecture = "x64",
            RunnerOs = "Linux",
            TestId = "row",
            FullyQualifiedName = "Tests.Flaky",
            DisplayName = "Flaky row",
            Outcome = outcome,
            DurationTicks = 1234,
        };

    private static HistoryModule Module(string path, int window, string name, string outcome)
        => new()
        {
            AssemblyName = "Reporter.Tests",
            TargetFramework = "net8.0",
            Architecture = "x64",
            GitHubActionsHistoryPath = path,
            GitHubActionsHistoryWindowInDays = window,
            HistoryTests =
            [
                new HistoryTest { TestId = name, FullyQualifiedName = name, DisplayName = name, Outcome = outcome, DurationTicks = 5678 },
            ],
        };

    private static void WriteSnapshot(string path, params GitHubActionsHistorySample[] samples)
        => File.WriteAllText(path, JsonSerializer.Serialize(
            new GitHubActionsHistorySnapshot { GeneratedAtUtc = CiReporterFixture.Epoch, Samples = samples },
            GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot), new UTF8Encoding(false));
}

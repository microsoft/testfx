// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsHistoryBehaviorTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "History_MasterDisabled")]
    [DataRow(1, DisplayName = "History_NoWindows")]
    [DataRow(2, DisplayName = "History_MalformedWindow")]
    [DataRow(3, DisplayName = "History_OutsideAzureDevOps")]
    [DataRow(4, DisplayName = "History_MissingCollection")]
    [DataRow(5, DisplayName = "History_MissingProject")]
    [DataRow(6, DisplayName = "History_MissingToken")]
    [DataRow(7, DisplayName = "History_MissingDefinition")]
    [DataRow(8, DisplayName = "History_MultipleWindowArguments")]
    public async Task Start_InvalidConfigurationDoesNotQuery(int partition)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"];
        switch (partition)
        {
            case 0: fixture.Options.Remove(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName); break;
            case 1: fixture.Options.Remove(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory); break;
            case 2: fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["not-an-integer"]; break;
            case 3: fixture.Variables["TF_BUILD"] = null; break;
            case 4: fixture.Variables["SYSTEM_COLLECTIONURI"] = " "; break;
            case 5: fixture.Variables["SYSTEM_TEAMPROJECT"] = null; break;
            case 6: fixture.Variables["SYSTEM_ACCESSTOKEN"] = string.Empty; break;
            case 7: fixture.Variables["BUILD_DEFINITIONID"] = null; break;
            case 8: fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["7", "30"]; break;
        }

        var client = new Mock<IAzureDevOpsHistoryClient>(MockBehavior.Strict);
        AzureDevOpsHistoryService service = Create(fixture, client);
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await service.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        client.VerifyNoOtherCalls();
        Assert.AreEqual(0, service.HistoryWindowInDays);
        Assert.IsFalse(service.TryGetStats("Tests.Absent", out _));
        if (partition is >= 4 and <= 7)
        {
            Assert.AreEqual((LogLevel.Warning, AzureDevOpsResources.FlakyHistoryMissingEnvironmentWarning), Assert.ContainsSingle(fixture.Logs));
        }
        else
        {
            Assert.IsEmpty(fixture.Logs);
        }
    }

    [TestMethod]
    public async Task Load_EffectiveWindowAndOffsetPagingUseOneSharedQuery()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["7"];
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory] = ["30"];
        var client = new Mock<IAzureDevOpsHistoryClient>();
        AzureDevOpsHistoryQuery? query = null;
        client.Setup(value => value.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), 501, It.IsAny<CancellationToken>()))
            .Callback<AzureDevOpsHistoryQuery, int, CancellationToken>((value, _, _) => query = value)
            .ReturnsAsync([new AzureDevOpsTestRun("owned-run")]);
        List<(int Skip, string? Continuation)> pages = [];
        client.Setup(value => value.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "owned-run", It.IsAny<int>(), 1000, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsHistoryQuery, string, int, int, string?, CancellationToken>((value, _, skip, _, continuation, _) =>
            {
                Assert.AreSame(query, value);
                pages.Add((skip, continuation));
                IReadOnlyList<AzureDevOpsTestResult> results = skip == 0
                    ? [.. Enumerable.Range(1, 1000).Select(index => new AzureDevOpsTestResult("Tests.Paged", "Passed", index))]
                    : [new AzureDevOpsTestResult("tests.paged", "Failed", 9000), new AzureDevOpsTestResult("Tests.Ignored", "NotExecuted", -1)];
                return Task.FromResult(new AzureDevOpsTestResultsPage(results, null));
            });
        AzureDevOpsHistoryService service = Create(fixture, client);

        Assert.IsTrue(await service.IsEnabledAsync());
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        Assert.IsNotNull(query);
        Assert.AreEqual("https://ci.invalid/org/", query.CollectionUri);
        Assert.AreEqual("project", query.TeamProject);
        Assert.AreEqual("owned-token", query.AccessToken);
        Assert.AreEqual("17", query.BuildDefinitionId);
        Assert.AreEqual(CiReporterFixture.Epoch.AddDays(-30), query.MinLastUpdatedDate);
        Assert.AreEqual(CiReporterFixture.Epoch, query.MaxLastUpdatedDate);
        Assert.AreSequenceEqual([(0, null), (1000, null)], pages);
        Assert.AreEqual(30, service.HistoryWindowInDays);
        Assert.IsTrue(service.TryGetStats("TESTS.PAGED", out FlakyStats counts));
        Assert.AreEqual(1000, counts.PassCount);
        Assert.AreEqual(1, counts.FailCount);
        Assert.IsTrue(service.TryGetDurationStats("tests.paged", out DurationHistoryStats durations));
        Assert.AreEqual(1000, durations.SampleCount);
        Assert.AreEqual(950d, durations.P95Milliseconds);
        Assert.AreEqual(990d, durations.P99Milliseconds);
        Assert.IsTrue(service.TryGetStats("Tests.Ignored", out FlakyStats ignored));
        Assert.AreEqual(0, ignored.TotalCount);
        Assert.IsFalse(service.TryGetDurationStats("Tests.Ignored", out _));
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "History_RepeatedContinuation")]
    [DataRow(true, DisplayName = "History_FiftyPageLimit")]
    public async Task Load_RepeatedContinuationAndPageCapStopWithExactWarning(bool cap)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"];
        var client = new Mock<IAzureDevOpsHistoryClient>();
        client.Setup(value => value.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), 501, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsTestRun("owned-run")]);
        int calls = 0;
        client.Setup(value => value.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "owned-run", 0, 1000, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsHistoryQuery, string, int, int, string?, CancellationToken>((_, _, _, _, continuation, _) =>
            {
                Assert.AreEqual(calls == 0 ? null : cap ? $"page-{calls}" : "same-page", continuation);
                calls++;
                return Task.FromResult(new AzureDevOpsTestResultsPage(
                    [new AzureDevOpsTestResult("Tests.Paged", "Failed")], cap ? $"page-{calls}" : "same-page"));
            });
        AzureDevOpsHistoryService service = Create(fixture, client);

        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        Assert.AreEqual(cap ? 50 : 2, calls);
        Assert.IsTrue(service.TryGetStats("Tests.Paged", out FlakyStats stats));
        Assert.AreEqual(0, stats.PassCount);
        Assert.AreEqual(cap ? 50 : 2, stats.FailCount);
        Assert.AreEqual(
            (LogLevel.Warning, string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryResultsPagingStoppedWarning, "owned-run", 50)),
            Assert.ContainsSingle(fixture.Logs));
    }

    [TestMethod]
    public async Task Load_FailureClearsPreviouslyPublishedSnapshots()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory] = ["30"];
        var client = new Mock<IAzureDevOpsHistoryClient>();
        client.SetupSequence(value => value.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), 501, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsTestRun("owned-run")])
            .ThrowsAsync(new IOException("history unavailable"));
        client.Setup(value => value.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "owned-run", 0, 1000, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage([new AzureDevOpsTestResult("Tests.BeforeFailure", "Passed", 50)], null));
        AzureDevOpsHistoryService service = Create(fixture, client);
        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        Assert.IsTrue(service.TryGetStats("Tests.BeforeFailure", out _));
        Assert.IsTrue(service.TryGetDurationStats("Tests.BeforeFailure", out _));

        await service.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        Assert.AreEqual(0, service.HistoryWindowInDays);
        Assert.IsFalse(service.TryGetStats("Tests.BeforeFailure", out _));
        Assert.IsFalse(service.TryGetDurationStats("Tests.BeforeFailure", out _));
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryLoadFailedWarning, "history unavailable"),
            Assert.ContainsSingle(fixture.Logs).Message);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "History_CallerCanceledLoad")]
    [DataRow(true, DisplayName = "History_CallerCanceledWhenBudgetWins")]
    public async Task Load_CallerCancellationPropagates(bool budgetWins)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"];
        using var cancellation = new CancellationTokenSource();
        if (!budgetWins)
        {
            cancellation.Cancel();
        }

        var original = new OperationCanceledException(cancellation.Token);
        var client = new Mock<IAzureDevOpsHistoryClient>();
        var pendingRuns = new TaskCompletionSource<IReadOnlyList<AzureDevOpsTestRun>>();
        if (budgetWins)
        {
            client.Setup(value => value.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), 501, It.IsAny<CancellationToken>())).Returns(pendingRuns.Task);
            fixture.Delay = (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            };
        }
        else
        {
            client.Setup(value => value.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), 501, It.IsAny<CancellationToken>())).ThrowsAsync(original);
        }

        AzureDevOpsHistoryService service = Create(fixture, client);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.OnTestSessionStartingAsync(new CiReporterFixture.Session(cancellation.Token)));

        pendingRuns.TrySetCanceled(TestContext.CancellationToken);
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        if (!budgetWins)
        {
            Assert.AreSame(original, error);
        }

        Assert.IsEmpty(fixture.Logs);
        Assert.AreEqual(0, service.HistoryWindowInDays);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "HistoryLookup_NullIdentity")]
    [DataRow(1, DisplayName = "HistoryLookup_EmptyIdentity")]
    [DataRow(2, DisplayName = "HistoryLookup_WhitespaceIdentity")]
    public void Lookup_BlankIdentityNeverFindsHistory(int partition)
    {
        using var fixture = new CiReporterFixture();
        AzureDevOpsHistoryService service = Create(fixture, new Mock<IAzureDevOpsHistoryClient>(MockBehavior.Strict));
        string identity = partition switch { 0 => null!, 1 => string.Empty, _ => " \t " };

        Assert.IsFalse(service.TryGetStats(identity, out FlakyStats counts));
        Assert.AreEqual(0, counts.TotalCount);
        Assert.IsFalse(service.TryGetDurationStats(identity, out DurationHistoryStats duration));
        Assert.AreEqual(0, duration.SampleCount);
        Assert.IsFalse(service.IsLikelyFlaky(identity, 0));
    }

    private static AzureDevOpsHistoryService Create(CiReporterFixture fixture, Mock<IAzureDevOpsHistoryClient> client)
        => new(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.Clock.Object, client.Object, fixture.TaskService.Object, fixture.LoggerFactory.Object);

    public TestContext TestContext { get; set; }
}

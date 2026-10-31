// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsRunCoordinationBehaviorTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "RunId_TransientIo")]
    [DataRow(1, DisplayName = "RunId_TransientAccess")]
    [DataRow(2, DisplayName = "RunId_TransientJson")]
    [DataRow(3, DisplayName = "RunId_TransientNull")]
    public async Task Acquire_TransientRunIdReadRetriesThenJoins(int failure)
    {
        using var fixture = new CiReporterFixture();
        AzureDevOpsPublishConfiguration configuration = fixture.PublishConfiguration();
        string ownerPath = Path.Combine(fixture.Root, "azdo-runid.123.owner");
        string runPath = Path.Combine(fixture.Root, "azdo-runid.123.json");
        fixture.PutText(ownerPath, Lease(-1, CiReporterFixture.Epoch.AddHours(1)));
        fixture.PutText(runPath, Run(88, 123, CiReporterFixture.Epoch.AddHours(1)));
        int reads = 0;
        fixture.FileSystem.Setup(value => value.ReadAllTextAsync(runPath)).Returns(() => ++reads == 1
                ? failure switch
                {
                    0 => Task.FromException<string>(new IOException("partial IO")),
                    1 => Task.FromException<string>(new UnauthorizedAccessException("partial access")),
                    2 => Task.FromResult("{ partial JSON"),
                    _ => Task.FromResult("null"),
                }
                : Task.FromResult(fixture.ReadText(runPath)));
        fixture.Delay = (_, _) => Task.CompletedTask;
        int creations = 0;

        AzureDevOpsCoordinatedRun run = await Coordinator(fixture).AcquireRunAsync(configuration, _ => Task.FromResult(++creations), CancellationToken.None);

        Assert.AreEqual(0, creations);
        Assert.AreEqual(88, run.RunId);
        Assert.IsFalse(run.IsOwner);
        Assert.AreEqual(2, reads);
        Assert.AreSequenceEqual([TimeSpan.Zero], fixture.Delays);
        Assert.IsEmpty(fixture.DeletedPaths);
        using var participant = JsonDocument.Parse(fixture.ReadText(run.ParticipantFilePath));
        Assert.AreEqual(42, participant.RootElement.GetProperty("processId").GetInt32());
        Assert.AreEqual(123, participant.RootElement.GetProperty("buildId").GetInt32());
        Assert.AreEqual(CiReporterFixture.Epoch.AddHours(1), participant.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "OwnerLease_NullContentIsNotStolen")]
    [DataRow(1, DisplayName = "OwnerLease_PartialJsonIsNotStolen")]
    [DataRow(2, DisplayName = "OwnerLease_IoFailureIsNotStolen")]
    [DataRow(3, DisplayName = "OwnerLease_AccessFailureIsNotStolen")]
    public async Task Acquire_NullOwnerIsNotStolen(int failure)
    {
        using var fixture = new CiReporterFixture();
        string owner = Path.Combine(fixture.Root, "azdo-runid.123.owner");
        string content = failure == 1 ? "{ partial JSON" : "null";
        fixture.PutText(owner, content);
        if (failure >= 2)
        {
            fixture.FileSystem.Setup(value => value.ReadAllText(owner))
                .Throws(failure == 2 ? new IOException("owner read denied") : new UnauthorizedAccessException("owner read denied"));
        }

        fixture.PutText(Path.Combine(fixture.Root, "azdo-runid.123.json"), Run(91, 123, CiReporterFixture.Epoch.AddHours(1)));
        int creations = 0;

        AzureDevOpsCoordinatedRun run = await Coordinator(fixture).AcquireRunAsync(fixture.PublishConfiguration(), _ => Task.FromResult(++creations), CancellationToken.None);

        Assert.AreEqual(91, run.RunId);
        Assert.IsFalse(run.IsOwner);
        Assert.AreEqual(0, creations);
        Assert.AreEqual(content, fixture.ReadText(owner));
        Assert.IsEmpty(fixture.DeletedPaths);
        Assert.IsEmpty(fixture.Delays);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RunId_WrongBuild")]
    [DataRow(true, DisplayName = "RunId_ExpiredWithoutLiveOwner")]
    public async Task Acquire_StaleOrWrongBuildRunIdIsReplacedBeforeJoining(bool expired)
    {
        using var fixture = new CiReporterFixture();
        string owner = Path.Combine(fixture.Root, "azdo-runid.123.owner");
        string path = Path.Combine(fixture.Root, "azdo-runid.123.json");
        fixture.PutText(owner, Lease(-1, CiReporterFixture.Epoch.AddHours(1)));
        fixture.PutText(path, Run(19, expired ? 123 : 456, expired ? CiReporterFixture.Epoch : CiReporterFixture.Epoch.AddHours(1)));
        fixture.Delay = (_, _) =>
        {
            Assert.IsFalse(fixture.Files.ContainsKey(path), "A stale run must be removed before waiting for its replacement.");
            fixture.PutText(path, Run(92, 123, CiReporterFixture.Epoch.AddHours(1)));
            return Task.CompletedTask;
        };
        int creations = 0;

        AzureDevOpsCoordinatedRun run = await Coordinator(fixture).AcquireRunAsync(fixture.PublishConfiguration(), _ => Task.FromResult(++creations), CancellationToken.None);

        Assert.AreEqual(92, run.RunId);
        Assert.IsFalse(run.IsOwner);
        Assert.AreEqual(0, creations);
        Assert.AreSequenceEqual([path], fixture.DeletedPaths);
        Assert.HasCount(1, fixture.Delays);
    }

    [TestMethod]
    public async Task Acquire_RunIdWriteAndDiagnosticFailureDoNotLoseOwnedRun()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "azdo-runid.123.json");
        fixture.BeforeOpen = (opened, _, _, _) =>
        {
            if (opened == path)
            {
                throw new IOException("run id cannot be published");
            }
        };
        fixture.LogError = new IOException("diagnostic unavailable");
        AzureDevOpsRunIdCoordinator coordinator = Coordinator(fixture);

        AzureDevOpsCoordinatedRun run = await coordinator.AcquireRunAsync(fixture.PublishConfiguration(), _ => Task.FromResult(66), CancellationToken.None);

        Assert.AreEqual(66, run.RunId);
        Assert.IsTrue(run.IsOwner);
        Assert.IsTrue(fixture.Files.ContainsKey(run.OwnerFilePath));
        Assert.IsFalse(fixture.Files.ContainsKey(path));
        Assert.Contains("run id cannot be published", Assert.ContainsSingle(fixture.Logs).Message);
        int finalized = 0;
        await coordinator.FinalizeRunAsync(run, _ =>
        {
            finalized++;
            return Task.CompletedTask;
        }, CancellationToken.None);
        Assert.AreEqual(1, finalized);
        Assert.IsFalse(fixture.Files.ContainsKey(run.OwnerFilePath));
        Assert.IsFalse(fixture.Files.ContainsKey(run.ParticipantFilePath));
    }

    [TestMethod]
    public async Task Acquire_ParticipantCreateFailureFallsBackToOverwrite()
    {
        using var fixture = new CiReporterFixture();
        string participant = Path.Combine(fixture.Root, "azdo-runid.123.participant.42.json");
        fixture.PutText(participant, "stale participant");
        AzureDevOpsRunIdCoordinator coordinator = Coordinator(fixture);

        AzureDevOpsCoordinatedRun run = await coordinator.AcquireRunAsync(fixture.PublishConfiguration(), _ => Task.FromResult(44), CancellationToken.None);

        Assert.IsTrue(run.IsOwner);
        Assert.AreSequenceEqual(
            [FileMode.CreateNew, FileMode.Create],
            fixture.Opens.Where(open => open.Path == participant).Select(open => open.Mode));
        using var replacement = JsonDocument.Parse(fixture.ReadText(participant));
        Assert.AreEqual(42, replacement.RootElement.GetProperty("processId").GetInt32());
        Assert.AreEqual(123, replacement.RootElement.GetProperty("buildId").GetInt32());
    }

    [TestMethod]
    [DataRow(0, false, DisplayName = "Participant_ActiveDeadProcess")]
    [DataRow(1, false, DisplayName = "Participant_ExpiredDeadProcess")]
    [DataRow(2, true, DisplayName = "Participant_ExpiredLiveProcess")]
    [DataRow(3, true, DisplayName = "Participant_TransientRead")]
    [DataRow(4, false, DisplayName = "Participant_Disappeared")]
    [DataRow(5, true, DisplayName = "Participant_LiveFilenameFallback")]
    [DataRow(6, false, DisplayName = "Participant_FilenameWithoutSeparator")]
    [DataRow(7, false, DisplayName = "Participant_NonnumericFilenamePid")]
    [DataRow(8, true, DisplayName = "Participant_ActiveLiveProcess")]
    public async Task Finalize_ParticipantPartitionsAreClassifiedBeforeCompletion(int partition, bool retained)
    {
        using var fixture = new CiReporterFixture();
        // Observe only this test host's liveness. No child process is launched or waited for.
        using var current = Process.GetCurrentProcess();
        int livePid = current.Id;
        fixture.Environment.SetupGet(value => value.ProcessId).Returns(-100);
        AzureDevOpsRunIdCoordinator coordinator = Coordinator(fixture);
        AzureDevOpsCoordinatedRun run = await coordinator.AcquireRunAsync(fixture.PublishConfiguration(), _ => Task.FromResult(55), CancellationToken.None);
        string peer = Path.Combine(fixture.Root, partition switch
        {
            6 => "orphan",
            7 => "azdo-runid.123.participant.unknown.json",
            2 or 5 or 8 => $"azdo-runid.123.participant.{livePid}.json",
            _ => "azdo-runid.123.participant.-1.json",
        });
        if (partition != 4)
        {
            fixture.PutText(peer, partition == 3 ? "{ mid-write" : Lease(
                partition is 2 or 8 ? livePid : -1,
                partition is 0 or 8 ? CiReporterFixture.Epoch.AddHours(1) : CiReporterFixture.Epoch.AddSeconds(-1)));
        }

        fixture.FileSystem.Setup(value => value.GetFiles(fixture.Root, "azdo-runid.123.participant.*.json", SearchOption.TopDirectoryOnly)).Returns([peer]);
        int finalized = 0;

        await coordinator.FinalizeRunAsync(run, token =>
        {
            Assert.AreEqual(CancellationToken.None, token);
            finalized++;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(1, finalized);
        Assert.AreEqual(retained, fixture.Files.ContainsKey(peer));
        Assert.IsFalse(fixture.Files.ContainsKey(run.OwnerFilePath));
        Assert.IsFalse(fixture.Files.ContainsKey(run.RunIdFilePath));
        Assert.IsFalse(fixture.Files.ContainsKey(run.ParticipantFilePath));
        Assert.IsEmpty(fixture.Delays);
        if (retained)
        {
            Assert.AreEqual(LogLevel.Warning, Assert.ContainsSingle(fixture.Logs).Level);
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingFinalizeWaitTimedOut, TimeSpan.Zero, 1),
                fixture.Logs[0].Message);
        }
        else
        {
            Assert.IsEmpty(fixture.Logs);
        }
    }

    private static AzureDevOpsRunIdCoordinator Coordinator(CiReporterFixture fixture)
        => new(fixture.FileSystem.Object, fixture.TaskService.Object, fixture.Clock.Object, fixture.Environment.Object, fixture.Logger.Object, CiReporterFixture.PublisherOptions());

    private static string Lease(int processId, DateTimeOffset expires)
        => JsonSerializer.Serialize(new AzureDevOpsLeaseFile(processId, 123, expires));

    private static string Run(int id, int buildId, DateTimeOffset expires)
        => JsonSerializer.Serialize(new AzureDevOpsRunIdFile(id, buildId, "https://ci.invalid/org/", "project", expires));
}

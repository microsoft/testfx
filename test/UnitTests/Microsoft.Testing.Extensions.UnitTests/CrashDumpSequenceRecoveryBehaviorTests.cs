// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
[ResourceLock("Microsoft.Testing.Extensions.CrashDump.ProcessKilledByHangDumpAppDomainData", Mode = ResourceAccessMode.Read)]
public sealed class CrashDumpSequenceRecoveryBehaviorTests
{
    [TestMethod]
    public async Task Exit_ReconstructsOnlyUnfinishedTestsUsingLatestValidTimestamp()
    {
        using var fixture = new DumpBehaviorFixture();
        string[] lines =
        [
            DumpBehaviorFixture.JournalHeader,
            string.Empty,
            "STARTED\tnot-a-time\tmalformed\tIgnored invalid timestamp",
            "STARTED\t2099-12-31T23:59:59Z\tincomplete",
            "STARTED\t2026-10-30T12:00:10Z\tlate\tLate",
            "STARTED\t2026-10-30T12:00:05Z\tearly\tEarly\twith\textra",
            "STARTED\t2026-10-30T12:00:08Z\t\t",
            "STARTED\t2026-10-30T12:00:00Z\tretried\tOld attempt",
            "STARTED\t2026-10-30T12:00:20Z\tretried\tCurrent attempt",
            "STARTED\t2026-10-30T12:00:03Z\tCase\tCase-sensitive",
            "ENDED\t2026-10-30T12:00:30Z\tcase\tPassed",
            "STARTED\t2026-10-30T12:00:06Z\tfinished\tFinished test",
            "ENDED\t2026-10-30T12:00:25Z\tfinished\tCompleted",
            "started\t2026-10-30T12:00:50Z\tlower\tNot a start",
            "CHECKPOINT\t2026-10-30T12:01:00Z\tignored\tLatest observation",
            "ENDED\t2026-10-30T12:00:12Z\tmissing\tPassed",
            "STARTED\tpartial",
        ];
        string path = fixture.WriteJournal(lines);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);
        using var cancellation = new CancellationTokenSource();

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), cancellation.Token);

        Assert.AreSequenceEqual(
            BaselineMessages(fixture).Concat(
            [
                CrashDumpResources.CrashDumpTestsRunningAtCrash,
                "[00:00:57] Case-sensitive",
                "[00:00:55] Early\twith\textra",
                "[00:00:52] ",
                "[00:00:50] Late",
                "[00:00:40] Current attempt",
            ]),
            ErrorMessages(fixture));
        foreach ((IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) in fixture.Displays)
        {
            Assert.AreSame(handler, producer);
            Assert.AreEqual(cancellation.Token, token);
        }

        AssertSequenceArtifact(fixture, handler, path);
        Assert.AreSequenceEqual(lines, File.ReadAllLines(path), "Recovery must not rewrite or truncate the original diagnostic.");
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SequenceRecovery_HeaderAndCommentsOnly")]
    [DataRow(1, DisplayName = "SequenceRecovery_AllStartedTestsEnded")]
    [DataRow(2, DisplayName = "SequenceRecovery_UnknownAndLowercaseEventsAreNotStarts")]
    [DataRow(3, DisplayName = "SequenceRecovery_MalformedAndTruncatedRecordsAreIgnored")]
    [DataRow(4, DisplayName = "SequenceRecovery_EmptyJournal")]
    public async Task Exit_NoRunningTestsStillPublishesOriginalJournal(int contents)
    {
        using var fixture = new DumpBehaviorFixture();
        string[] lines = contents switch
        {
            0 => [DumpBehaviorFixture.JournalHeader, string.Empty, "# additional diagnostic"],
            1 =>
            [
                DumpBehaviorFixture.JournalHeader,
                "STARTED\t2026-10-30T12:00:00Z\tfinished\tFinished test",
                "ENDED\t2026-10-30T12:00:05Z\tfinished\tCompleted",
            ],
            2 =>
            [
                DumpBehaviorFixture.JournalHeader,
                "started\t2026-10-30T12:00:00Z\tlower\tLowercase event",
                "CHECKPOINT\t2026-10-30T12:00:01Z\tcheckpoint\tCheckpoint",
                "ENDED\t2026-10-30T12:00:02Z\tunknown\tPassed",
            ],
            3 =>
            [
                DumpBehaviorFixture.JournalHeader,
                "invalid",
                "STARTED\tnot-a-timestamp\tinvalid\tIgnored",
                "STARTED\t2099-12-31T23:59:59Z\ttruncated",
            ],
            _ => [],
        };
        string path = fixture.WriteJournal(lines);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), CancellationToken.None);

        Assert.AreSequenceEqual(BaselineMessages(fixture), ErrorMessages(fixture));
        AssertSequenceArtifact(fixture, handler, path);
        Assert.AreSequenceEqual(lines, File.ReadAllLines(path));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SequenceRecovery_NullPath")]
    [DataRow(1, DisplayName = "SequenceRecovery_EmptyPath")]
    [DataRow(2, DisplayName = "SequenceRecovery_MissingFile")]
    [DataRow(3, DisplayName = "SequenceRecovery_DirectoryIsNotAFile")]
    public async Task Exit_AbsentSequenceDoesNotInventArtifact(int pathKind)
    {
        using var fixture = new DumpBehaviorFixture();
        string? path = GetAbsentPath(fixture, pathKind);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), CancellationToken.None);

        Assert.IsEmpty(fixture.Publications);
        Assert.AreSequenceEqual(BaselineMessages(fixture), ErrorMessages(fixture));
        Assert.AreEqual(pathKind == 3, Directory.Exists(fixture.RootDirectory));
        if (pathKind == 3)
        {
            Assert.IsEmpty(Directory.GetFiles(fixture.RootDirectory));
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Exclusive FileShare denial of the reader is deterministic on Windows.")]
    public async Task Exit_ReadFailureReportsAndPublishesOriginalJournal()
    {
        using var fixture = new DumpBehaviorFixture();
        string[] lines = [DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T12:00:00Z\trunning\tRunning test"];
        string path = fixture.WriteJournal(lines);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), CancellationToken.None);
        }

        string[] messages = ErrorMessages(fixture);
        Assert.HasCount(3, messages);
        Assert.AreSequenceEqual(BaselineMessages(fixture), messages.Take(2));
        string errorPrefix = string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileReadError, path, string.Empty);
        Assert.IsTrue(messages[2].StartsWith(errorPrefix, StringComparison.Ordinal));
        Assert.AreNotEqual(errorPrefix, messages[2], "The filesystem error, not just its path, must be reported.");
        AssertSequenceArtifact(fixture, handler, path);
        Assert.AreSequenceEqual(lines, File.ReadAllLines(path));
    }

    [TestMethod]
    public async Task Exit_RunningTestOutputFailureDoesNotPretendToPublishJournal()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = fixture.WriteJournal(DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T12:00:00Z\trunning\tRunning test");
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);
        var failure = new IOException("The recovered-test output transport failed.");
        fixture.Output
            .Setup(x => x.DisplayAsync(
                handler,
                It.Is<IOutputDeviceData>(data => data is ErrorMessageOutputDeviceData && ((ErrorMessageOutputDeviceData)data).Message == CrashDumpResources.CrashDumpTestsRunningAtCrash),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
            () => handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), CancellationToken.None));

        Assert.AreSame(failure, actual);
        Assert.IsEmpty(fixture.Publications);
        Assert.IsTrue(File.Exists(path));
        Assert.AreSequenceEqual(BaselineMessages(fixture), ErrorMessages(fixture));
        fixture.MessageBus.Verify(x => x.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()), Times.Never);
    }

    [TestMethod]
    public async Task Exit_ArtifactFailurePropagatesAndRetainsJournal()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = fixture.WriteJournal(DumpBehaviorFixture.JournalHeader);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);
        var failure = new IOException("The artifact bus is unavailable.");
        fixture.MessageBus
            .Setup(x => x.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .ThrowsAsync(failure);

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
            () => handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 1, false), CancellationToken.None));

        Assert.AreSame(failure, actual);
        Assert.IsTrue(File.Exists(path));
        Assert.AreSequenceEqual(BaselineMessages(fixture), ErrorMessages(fixture));
        fixture.MessageBus.Verify(
            x => x.PublishAsync(
                handler,
                It.Is<IData>(data => data is FileArtifact && ((FileArtifact)data).FileInfo.FullName == path
                    && ((FileArtifact)data).DisplayName == CrashDumpResources.CrashDumpSequenceArtifactDisplayName)),
            Times.Once);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SequenceCleanup_FileShareDeniesDelete")]
    [DataRow(true, DisplayName = "SequenceCleanup_ReadonlyFileDeniesDelete")]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Windows delete-sharing and readonly attributes deterministically produce the two cleanup failures.")]
    public async Task GracefulExit_CleanupFailureKeepsJournalWithoutFailingRun(bool readOnly)
    {
        using var fixture = new DumpBehaviorFixture();
        string path = fixture.WriteJournal(DumpBehaviorFixture.JournalHeader);
        FileAttributes originalAttributes = File.GetAttributes(path);
        FileStream? heldFile = null;
        try
        {
            if (readOnly)
            {
                File.SetAttributes(path, originalAttributes | FileAttributes.ReadOnly);
            }
            else
            {
                heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);
            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 0, true), CancellationToken.None);

            Assert.IsTrue(File.Exists(path), "Best-effort cleanup must leave the denied file rather than fail the run.");
            Assert.IsEmpty(fixture.Displays);
            Assert.IsEmpty(fixture.Publications);
        }
        finally
        {
            heldFile?.Dispose();
            if (File.Exists(path))
            {
                File.SetAttributes(path, originalAttributes);
            }
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SequenceCleanup_NullPath")]
    [DataRow(1, DisplayName = "SequenceCleanup_EmptyPath")]
    [DataRow(2, DisplayName = "SequenceCleanup_MissingFile")]
    [DataRow(3, DisplayName = "SequenceCleanup_DirectoryIsNotAFile")]
    public async Task GracefulExit_AbsentSequenceDoesNotPublishOrCreateFiles(int pathKind)
    {
        using var fixture = new DumpBehaviorFixture();
        string? path = GetAbsentPath(fixture, pathKind);
        CrashDumpProcessLifetimeHandler handler = fixture.CreateCrashHandler(path);

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 0, true), CancellationToken.None);

        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Publications);
        Assert.AreEqual(pathKind == 3, Directory.Exists(fixture.RootDirectory));
    }

    private static string? GetAbsentPath(DumpBehaviorFixture fixture, int pathKind)
    {
        if (pathKind == 3)
        {
            Directory.CreateDirectory(fixture.RootDirectory);
            return fixture.RootDirectory;
        }

        return pathKind switch
        {
            0 => null,
            1 => string.Empty,
            _ => Path.Combine(fixture.RootDirectory, "missing.sequence.log"),
        };
    }

    private static string[] BaselineMessages(DumpBehaviorFixture fixture)
        =>
        [
            string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, 4321),
            string.Format(
                CultureInfo.InvariantCulture,
                CrashDumpResources.CannotFindExpectedCrashDumpFile,
                Path.Combine(fixture.RootDirectory, "missing-dumps", "host_4321.dmp")),
        ];

    private static string[] ErrorMessages(DumpBehaviorFixture fixture)
    {
        foreach ((IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) in fixture.Displays)
        {
            Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(data);
        }

        return fixture.Displays.Select(display => ((ErrorMessageOutputDeviceData)display.Data).Message).ToArray();
    }

    private static void AssertSequenceArtifact(DumpBehaviorFixture fixture, CrashDumpProcessLifetimeHandler owner, string path)
    {
        Assert.HasCount(1, fixture.Publications);
        Assert.AreSame(owner, fixture.Publications[0].Producer);
        Assert.IsInstanceOfType<FileArtifact>(fixture.Publications[0].Data);
        var artifact = (FileArtifact)fixture.Publications[0].Data;
        Assert.AreEqual(Path.GetFullPath(path), artifact.FileInfo.FullName);
        Assert.AreEqual(CrashDumpResources.CrashDumpSequenceArtifactDisplayName, artifact.DisplayName);
        Assert.AreEqual(CrashDumpResources.CrashDumpSequenceArtifactDescription, artifact.Description);
    }
}

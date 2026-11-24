// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpSequenceWritingBehaviorTests
{
    [TestMethod]
    public async Task Session_OverwritesStaleJournalAndFlushesNormalizedTransitions()
    {
        using var fixture = new DumpBehaviorFixture();
        string directory = Path.Combine(fixture.RootDirectory, "nested");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "tests.sequence.log");
        File.WriteAllText(path, "A stale journal must not survive initialization.");
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = string.Empty;
        fixture.Now = new DateTimeOffset(2026, 10, 30, 14, 0, 0, 123, TimeSpan.FromHours(2)).AddTicks(4567);
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        var context = new TestSessionContext(CancellationToken.None);

        await logger.OnTestSessionStartingAsync(context);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("uid\tone\r\nnode", "Suite\tname\r\nUnicode Ω", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T14:00:00.1234567+02:00\tuid one  node\tSuite name  Unicode Ω"],
            ReadJournal(path),
            "AutoFlush must make the record readable while the test host is still running.");

        await logger.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("stateless", "No state"), CancellationToken.None);
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("discovered", "Only discovered", DiscoveredTestNodeStateProperty.CachedInstance), CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(30);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("uid\tone\r\nnode", "A later display name is not an end-state field", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(context);

        Assert.IsTrue(File.Exists(path), "Finishing must retain the journal for controller-side crash/cleanup handling.");
        Assert.AreSequenceEqual(
        [
            DumpBehaviorFixture.JournalHeader,
            "STARTED\t2026-10-30T14:00:00.1234567+02:00\tuid one  node\tSuite name  Unicode Ω",
            "ENDED\t2026-10-30T14:00:30.1234567+02:00\tuid one  node\tPassed",
        ],
        ReadJournal(path));
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Start_PreCanceledSessionStillInitializesJournal()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "nested", "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await logger.OnTestSessionStartingAsync(new TestSessionContext(cancellation.Token));

        Assert.AreSequenceEqual([DumpBehaviorFixture.JournalHeader], ReadJournal(path));
        Assert.IsEmpty(fixture.Displays);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }

    [TestMethod]
    public async Task Session_SupersededAttemptAndSubsequentRetryRemainDistinctJournalRecords()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        var context = new TestSessionContext(CancellationToken.None);
        await logger.OnTestSessionStartingAsync(context);
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("retried", "First attempt", InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(2);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("retried", "First attempt", new FailedTestNodeStateProperty(), new RetryAttemptProperty(1, isSuperseded: true)),
            CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(1);
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("retried", "Second attempt", InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(4);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("retried", "Second attempt", TestNodeExecutionCompletedProperty.CachedInstance),
            CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(context);

        Assert.AreSequenceEqual(
        [
            DumpBehaviorFixture.JournalHeader,
            "STARTED\t2026-10-30T12:00:00.0000000+00:00\tretried\tFirst attempt",
            "ENDED\t2026-10-30T12:00:02.0000000+00:00\tretried\tFailed",
            "STARTED\t2026-10-30T12:00:03.0000000+00:00\tretried\tSecond attempt",
            "ENDED\t2026-10-30T12:00:07.0000000+00:00\tretried\tCompleted",
        ],
        ReadJournal(path));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SequenceCompletion_InProgressStateDoesNotWriteStarted")]
    [DataRow(true, DisplayName = "SequenceCompletion_FailedStateDoesNotOverrideCompleted")]
    public async Task Consume_CompletionMarkerTakesPrecedenceOverState(bool failedState)
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        IProperty state = failedState ? new FailedTestNodeStateProperty() : InProgressTestNodeStateProperty.CachedInstance;

        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("completed", "Completed test", state, TestNodeExecutionCompletedProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "ENDED\t2026-10-30T12:00:00.0000000+00:00\tcompleted\tCompleted"],
            ReadJournal(path));
    }

    [TestMethod]
    public async Task Consume_ConcurrentUpdatesProduceWholeDistinctRecords()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        var context = new TestSessionContext(CancellationToken.None);
        await logger.OnTestSessionStartingAsync(context);
        Task[] writes = Enumerable.Range(0, 24)
            .Select(index => logger.ConsumeAsync(
                null!,
                DumpBehaviorFixture.Update($"uid-{index:00}", $"Concurrent test {index:00}", InProgressTestNodeStateProperty.CachedInstance),
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(writes);
        await logger.OnTestSessionFinishingAsync(context);

        string[] lines = ReadJournal(path);
        Assert.HasCount(25, lines);
        Assert.AreEqual(DumpBehaviorFixture.JournalHeader, lines[0]);
        Assert.AreSequenceEqual(
            Enumerable.Range(0, 24).Select(index => $"STARTED\t2026-10-30T12:00:00.0000000+00:00\tuid-{index:00}\tConcurrent test {index:00}"),
            lines.Skip(1).OrderBy(line => line, StringComparer.Ordinal));
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Consume_CanceledWriteLeavesJournalUnchangedAndNextWriteSucceeds()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("canceled", "Must not be written", InProgressTestNodeStateProperty.CachedInstance),
            cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSequenceEqual([DumpBehaviorFixture.JournalHeader], ReadJournal(path));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("next", "Next test", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "ENDED\t2026-10-30T12:00:00.0000000+00:00\tnext\tPassed"],
            ReadJournal(path));
    }

    [TestMethod]
    public async Task Finish_CanceledTokenLeavesWriterUsable()
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => logger.OnTestSessionFinishingAsync(new TestSessionContext(cancellation.Token)));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("after-canceled-finish", "Still running", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));
        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T12:00:00.0000000+00:00\tafter-canceled-finish\tStill running"],
            ReadJournal(path));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    [DataRow(null, DisplayName = "SequenceDisabled_MissingEnvironmentVariable")]
    [DataRow("", DisplayName = "SequenceDisabled_EmptyEnvironmentVariable")]
    public async Task DisabledLogger_IgnoresMessagesWithoutClockOrFileWork(string? path)
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = path;
        using CrashDumpSequenceLogger logger = fixture.CreateSequenceLogger();
        Assert.IsFalse(await logger.IsEnabledAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await logger.ConsumeAsync(null!, Mock.Of<IData>(), cancellation.Token);
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("disabled", "Not journaled", InProgressTestNodeStateProperty.CachedInstance),
            cancellation.Token);
        await logger.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));

        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
        Assert.IsFalse(Directory.Exists(fixture.RootDirectory));
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Enablement_ReReadsEnvironmentAndUsesLatestSelectedPath()
    {
        using var fixture = new DumpBehaviorFixture();
        string first = Path.Combine(fixture.RootDirectory, "first", "tests.sequence.log");
        string selected = Path.Combine(fixture.RootDirectory, "selected", "nested", "tests.sequence.log");
        using CrashDumpSequenceLogger logger = fixture.CreateSequenceLogger();
        Assert.IsFalse(await logger.IsEnabledAsync());
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = first;
        Assert.IsTrue(await logger.IsEnabledAsync());
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = " \t ";
        Assert.IsTrue(await logger.IsEnabledAsync(), "Presence, not non-whitespace content, is the enablement gate.");
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = string.Empty;
        Assert.IsFalse(await logger.IsEnabledAsync());
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = selected;
        Assert.IsTrue(await logger.IsEnabledAsync());

        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("selected", "Selected path", InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);

        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T12:00:00.0000000+00:00\tselected\tSelected path"],
            ReadJournal(selected));
        Assert.IsFalse(File.Exists(first));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(first)));
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Start_DirectoryPathReportsFailureAndDisablesWriting()
    {
        using var fixture = new DumpBehaviorFixture();
        string directory = Path.Combine(fixture.RootDirectory, "not-a-file");
        Directory.CreateDirectory(directory);
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, directory);
        Assert.IsTrue(await logger.IsEnabledAsync());
        using var cancellation = new CancellationTokenSource();

        await logger.OnTestSessionStartingAsync(new TestSessionContext(cancellation.Token));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("ignored", "No writer", InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, fixture.Displays);
        Assert.AreSame(logger, fixture.Displays[0].Producer);
        Assert.AreEqual(cancellation.Token, fixture.Displays[0].Token);
        Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(fixture.Displays[0].Data);
        string message = ((WarningMessageOutputDeviceData)fixture.Displays[0].Data).Message;
        AssertWarningIncludesPathExceptionAndLocalizedContract(message, directory);
        Assert.IsEmpty(Directory.GetFiles(directory));
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Start_InvalidPathReportsFailureWithoutUnsafeCaseMetadata()
    {
        using var fixture = new DumpBehaviorFixture();
        // Keep the NUL in data, not in a DataRow/display name consumed by TRX/JUnit/CTRF reporters.
        string path = fixture.RootDirectory + Path.DirectorySeparatorChar + "invalid\0journal.log";
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());

        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("ignored", "No writer", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, fixture.Displays);
        Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(fixture.Displays[0].Data);
        string message = ((WarningMessageOutputDeviceData)fixture.Displays[0].Data).Message;
        Assert.Contains(path, message);
        Assert.Contains(nameof(ArgumentException), message);
        Assert.Contains(nameof(CrashDumpSequenceLogger.OnTestSessionStartingAsync), message);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SequenceStartup_OutputFailureFallsBackToLogger")]
    [DataRow(true, DisplayName = "SequenceStartup_OutputAndLoggerFailuresAreBestEffort")]
    public async Task Start_OutputFailureFallsBackWithoutFailingSession(bool loggerFails)
    {
        using var fixture = new DumpBehaviorFixture();
        string directory = Path.Combine(fixture.RootDirectory, "not-a-file");
        Directory.CreateDirectory(directory);
        var outputFailure = new InvalidOperationException("The startup warning transport is unavailable.");
        fixture.Output
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (producer, data, token) => fixture.Displays.Add((producer, data, token)))
            .ThrowsAsync(outputFailure);
        if (loggerFails)
        {
            fixture.Logger
                .Setup(x => x.LogAsync(LogLevel.Warning, It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()))
                .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                    (level, message, exception, _) => fixture.Logs.Add((level, message, exception)))
                .ThrowsAsync(new IOException("The fallback log sink is unavailable."));
        }

        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, directory);
        Assert.IsTrue(await logger.IsEnabledAsync());
        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("ignored", "No writer", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await logger.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, fixture.Displays);
        Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(fixture.Displays[0].Data);
        Assert.HasCount(1, fixture.Logs);
        Assert.AreEqual(LogLevel.Warning, fixture.Logs[0].Level);
        Assert.Contains($"Failed to initialize crash sequence file '{directory}':", fixture.Logs[0].Message);
        Assert.Contains(nameof(CrashDumpSequenceLogger.OnTestSessionStartingAsync), fixture.Logs[0].Message);
        Assert.Contains($"Additionally, displaying this warning failed: {outputFailure}", fixture.Logs[0].Message);
        Assert.IsEmpty(Directory.GetFiles(directory));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SequenceDisposal_SynchronousAndRepeatedCalls")]
#if NETCOREAPP
    [DataRow(true, DisplayName = "SequenceDisposal_AsynchronousAndMixedCalls")]
#endif
    public async Task Dispose_ReleasesJournalAndIsIdempotent(bool asynchronous)
    {
        using var fixture = new DumpBehaviorFixture();
        string path = Path.Combine(fixture.RootDirectory, "tests.sequence.log");
        using CrashDumpSequenceLogger logger = CreateEnabledLogger(fixture, path);
        Assert.IsTrue(await logger.IsEnabledAsync());
        await logger.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await logger.ConsumeAsync(null!, DumpBehaviorFixture.Update("finished", "Finished test", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
#if NETCOREAPP
        if (asynchronous)
        {
            await logger.DisposeAsync();
            await logger.DisposeAsync();
        }
        else
#endif
        {
            logger.Dispose();
            logger.Dispose();
        }

#if NETCOREAPP
        await logger.DisposeAsync();
#else
        Assert.IsFalse(asynchronous);
#endif
        logger.Dispose();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await logger.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("after-disposal", "Not written", InProgressTestNodeStateProperty.CachedInstance),
            cancellation.Token);

        using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(reopened, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        Assert.AreEqual(
            DumpBehaviorFixture.JournalHeader + Environment.NewLine
                + "ENDED\t2026-10-30T12:00:00.0000000+00:00\tfinished\tPassed" + Environment.NewLine,
            reader.ReadToEnd());
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
    }

    private static CrashDumpSequenceLogger CreateEnabledLogger(DumpBehaviorFixture fixture, string path)
    {
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = path;
        return fixture.CreateSequenceLogger();
    }

    private static string[] ReadJournal(string path)
    {
        // Include Write sharing when opening a reader alongside the still-running test-host writer.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    private static void AssertWarningIncludesPathExceptionAndLocalizedContract(string message, string path)
    {
        const string DetailMarker = "EXCEPTION_DETAIL_MARKER";
        string[] contract = string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileOpenError, path, DetailMarker)
            .Split([DetailMarker], StringSplitOptions.None);
        Assert.IsTrue(message.StartsWith(contract[0], StringComparison.Ordinal));
        Assert.IsTrue(message.EndsWith(contract[1], StringComparison.Ordinal));
        Assert.Contains(nameof(CrashDumpSequenceLogger.OnTestSessionStartingAsync), message);
        Assert.IsGreaterThan(contract[0].Length + contract[1].Length, message.Length);
    }
}

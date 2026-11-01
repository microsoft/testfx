// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpActivityBehaviorTests
{
    [TestMethod]
    public void Constructor_EnabledWithoutPipeNameReportsRequiredVariable()
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => fixture.CreateActivityIndicator());

        Assert.AreEqual("Expected TESTINGPLATFORM_HANGDUMP_PIPENAME environment variable set.", exception.Message);
        fixture.EnvironmentMock.Verify(
            x => x.GetEnvironmentVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName),
            Times.Once);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Start_PreCanceledSessionStopsBeforeConnection()
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];
        fixture.EnvironmentValues[HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName] = $"unused-{Guid.NewGuid():N}";
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => indicator.OnTestSessionStartingAsync(new TestSessionContext(cancellation.Token)));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "ActivityStart_MatchingSessionCancellationIsSwallowed")]
    [DataRow(1, DisplayName = "ActivityStart_ForeignCancellationPropagates")]
    [DataRow(2, DisplayName = "ActivityStart_OrdinaryFailurePropagates")]
    public async Task Start_OnlyCancellationForTheSessionTokenIsSwallowed(int scenario)
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];
        string pipeName = $"unused-{Guid.NewGuid():N}";
        fixture.EnvironmentValues[HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName] = pipeName;
        using var sessionCancellation = new CancellationTokenSource();
        using var foreignCancellation = new CancellationTokenSource();
        Exception failure = scenario == 2
            ? new IOException("The activity trace sink is unavailable.")
            : new OperationCanceledException("Trace logging was canceled.", scenario == 0 ? sessionCancellation.Token : foreignCancellation.Token);
        fixture.Logger
            .Setup(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, exception, _) => fixture.Logs.Add((level, message, exception)))
            .ThrowsAsync(failure);
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        var context = new TestSessionContext(sessionCancellation.Token);

        // The first logger is an injected boundary before ConnectAsync, so no pipe server or
        // connection is required to distinguish the exception-filter branches.
        if (scenario == 0)
        {
            await indicator.OnTestSessionStartingAsync(context);
        }
        else if (scenario == 1)
        {
            OperationCanceledException actual = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => indicator.OnTestSessionStartingAsync(context));
            Assert.AreSame(failure, actual);
        }
        else
        {
            IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
                () => indicator.OnTestSessionStartingAsync(context));
            Assert.AreSame(failure, actual);
        }

        Assert.AreSequenceEqual(
            [$"Connecting to the process lifetime handler {pipeName}"],
            fixture.Logs.Select(log => log.Message));
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "ActivityTerminal_Passed")]
    [DataRow(1, DisplayName = "ActivityTerminal_Failed")]
    [DataRow(2, DisplayName = "ActivityTerminal_Error")]
    [DataRow(3, DisplayName = "ActivityTerminal_Skipped")]
    [DataRow(4, DisplayName = "ActivityTerminal_Cancelled")]
    [DataRow(5, DisplayName = "ActivityTerminal_Timeout")]
    [DataRow(6, DisplayName = "ActivityTerminal_ExecutionCompleted")]
    public async Task Consume_TerminalUpdateReportsOriginalStartAndRemainingTestBeforeMissingChannelFails(int terminalState)
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("selected", "Original selected", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(2);
        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("other", "Other test", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(2);
        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("selected", "Renamed selected", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(4);
        TestNodeUpdateMessage terminal = DumpBehaviorFixture.Update("selected", "Terminal selected", GetTerminalProperty(terminalState));

        // A disabled indicator has no activity channel. Its public invariant fails after the
        // bookkeeping/trace diagnostics, providing a deterministic boundary without IPC or reflection.
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => indicator.ConsumeAsync(null!, terminal, CancellationToken.None));
        Assert.Contains("Unexpected state", exception.Message);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => indicator.ConsumeAsync(null!, terminal, CancellationToken.None));

        Assert.AreSequenceEqual(
            ["Test removed from in-progress list 'Original selected' after '00:00:08', total in-progress '1'"],
            fixture.Logs.Select(log => log.Message).Where(message => message.StartsWith("Test removed from in-progress list", StringComparison.Ordinal)));
        Assert.HasCount(
            2,
            fixture.Logs.Where(log => log.Message.StartsWith("Signal for action node Terminal selected - ", StringComparison.Ordinal)));
        Assert.IsEmpty(fixture.Scheduler.Invocations);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "ActivityRetry_SupersededFailedAttempt")]
    [DataRow(true, DisplayName = "ActivityRetry_SupersededCompletionMarker")]
    public async Task Consume_SupersededAttemptRemainsTrackedUntilFinalCompletion(bool completionMarker)
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("retried", "Original retry", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        fixture.Now = fixture.Now.AddSeconds(3);
        IProperty attemptState = completionMarker ? TestNodeExecutionCompletedProperty.CachedInstance : new FailedTestNodeStateProperty();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("retried", "Superseded attempt", attemptState, new RetryAttemptProperty(1, isSuperseded: true)),
            CancellationToken.None));
        Assert.IsEmpty(fixture.Logs.Where(log => log.Message.StartsWith("Test removed from in-progress list", StringComparison.Ordinal)));

        fixture.Now = fixture.Now.AddSeconds(8);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("retried", "Final attempt", PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)),
            CancellationToken.None));

        Assert.AreSequenceEqual(
            ["Test removed from in-progress list 'Original retry' after '00:00:11', total in-progress '0'"],
            fixture.Logs.Select(log => log.Message).Where(message => message.StartsWith("Test removed from in-progress list", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Consume_UnrelatedDataDoesNotCreateActivityOrReadClock()
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        await indicator.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        Assert.IsEmpty(fixture.Logs);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);

        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("running", "Running test", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await indicator.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);

        Assert.AreSequenceEqual(["New in-progress test 'Running test'"], fixture.Logs.Select(log => log.Message));
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Once);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
    }

    [TestMethod]
    public async Task Consume_MissingStateDoesNotInventRunningTest()
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("unknown", "Untracked test"),
            CancellationToken.None));

        Assert.AreSequenceEqual(
            ["Signal for action node Untracked test - ''. _exitSignalActivityIndicatorAsync: False"],
            fixture.Logs.Select(log => log.Message));
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }

    [TestMethod]
    public async Task Start_DisabledIndicatorRejectsBeforeCancellation()
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => indicator.OnTestSessionStartingAsync(new TestSessionContext(cancellation.Token)));

        Assert.Contains("Unexpected state", exception.Message);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }

    [TestMethod]
    public async Task Consume_TraceDisabledDoesNotReadElapsedTimeOrEmitDiagnostics()
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Logger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(false);
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();
        await indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("running", "Running test", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Once);
        fixture.Clock.SetupGet(x => x.UtcNow).Throws(new IOException("Trace-disabled terminal handling must not read elapsed time."));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => indicator.ConsumeAsync(
            null!,
            DumpBehaviorFixture.Update("running", "Completed test", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None));

        Assert.Contains("Unexpected state", exception.Message);
        Assert.IsEmpty(fixture.Logs);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Once);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_RepeatedCallsCloseUnconnectedClientBeforeLaterStartup()
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];
        string pipeName = $"unused-{Guid.NewGuid():N}";
        fixture.EnvironmentValues[HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName] = pipeName;
        using HangDumpActivityIndicator indicator = fixture.CreateActivityIndicator();

        await indicator.DisposeAsync();
        await indicator.DisposeAsync();
        // NamedPipeClientStream rejects operations on a disposed stream before doing any OS
        // connection work. This observes disposal through the public lifecycle, not private fields.
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => indicator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None)));

        Assert.AreSequenceEqual(
            [$"Connecting to the process lifetime handler {pipeName}"],
            fixture.Logs.Select(log => log.Message));
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        fixture.Clock.VerifyGet(x => x.UtcNow, Times.Never);
    }
#endif

    private static IProperty GetTerminalProperty(int terminalState) =>
#pragma warning disable CS0618, MTP0001 // Exercise the terminal states supported by the production consumer.
        terminalState switch
        {
            0 => PassedTestNodeStateProperty.CachedInstance,
            1 => new FailedTestNodeStateProperty(),
            2 => new ErrorTestNodeStateProperty(),
            3 => SkippedTestNodeStateProperty.CachedInstance,
            4 => new CancelledTestNodeStateProperty(),
            5 => new TimeoutTestNodeStateProperty(),
            6 => TestNodeExecutionCompletedProperty.CachedInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(terminalState)),
        };
#pragma warning restore CS0618, MTP0001

}

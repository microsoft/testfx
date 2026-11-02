// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpInProcessLifecycleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(true, true, 0, DisplayName = "HangProtocol_TraceGraceful")]
    [DataRow(true, false, 27, DisplayName = "HangProtocol_TraceUngraceful")]
    [DataRow(false, true, 0, DisplayName = "HangProtocol_QuietGraceful")]
    public async Task RegisteredLifecycle_HandshakeActivityAndExitUseOwnedRoles(bool traceEnabled, bool graceful, int exitCode)
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken, traceEnabled)
        {
            GracefulExit = graceful,
            ExitCode = exitCode,
        };
        try
        {
            await fixture.BindAsync();
            await fixture.ConnectAsync();
            fixture.AssertReadyHandshake();
            await fixture.AwaitAsync(fixture.Consumer.ConsumeAsync(null!, Mock.Of<IData>(MockBehavior.Strict), fixture.Token));
            await ConsumeAsync(fixture, "selected", "Original sélection 東京", InProgressTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "selected", "Renamed sélection", InProgressTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(0);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Exactly(2));

            await ConsumeAsync(fixture, "discovery", "Découverte Ω", DiscoveredTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "selected", "Final sélection", PassedTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "not-running", "Ignoré é", SkippedTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "metadata", "No state 東京");

            fixture.AssertSignalCount(4);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Exactly(traceEnabled ? 4 : 0));
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Exactly(traceEnabled ? 3 : 2));
            string[] removals = RemovalMessages(fixture);
            if (traceEnabled)
            {
                AssertRemoval(Assert.ContainsSingle(removals), "Original sélection 東京", remaining: 0);
            }
            else
            {
                Assert.IsEmpty(removals);
                Assert.IsEmpty(fixture.ChildMessages.Where(message => message.StartsWith("Signal for action node ", StringComparison.Ordinal)
                    || message.StartsWith("New in-progress test '", StringComparison.Ordinal)));
            }

            // Finishing has no transport or cancellation side effects, even with a canceled token.
            using var rejectedFinish = new CancellationTokenSource();
            rejectedFinish.Cancel();
            int logCount = fixture.Logs.Count;
            await fixture.SessionLifetime.OnTestSessionFinishingAsync(new TestSessionContext(rejectedFinish.Token));
            Assert.HasCount(logCount, fixture.Logs);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Once);
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, graceful ? Times.Never() : Times.Once());
            Assert.AreSequenceEqual(
                graceful ? [] : new[] { "Testhost didn't exit gracefully '27')" },
                fixture.ControllerMessages.Where(message => message.StartsWith("Testhost didn't exit gracefully ", StringComparison.Ordinal)));
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_ActivityBeforeReverseConnectionIsAcknowledged()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            Task child = fixture.StartChildAsync();
            await fixture.AwaitAsync(fixture.ChildReverseAccepting.Task);
            Assert.IsNotNull(fixture.ReversePipeName);
            Assert.Contains($"Consumer pipe name received '{fixture.ReversePipeName}'", fixture.ControllerMessages);
            Assert.DoesNotContain("Test host controller connected", fixture.ChildMessages);

            // This boundary is after the primary request lock was released and before the
            // controller ever connects back or creates an inactivity timer.
            await ConsumeAsync(fixture, "before-ready", "Discovery before ready 東京", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            fixture.ProcessInformation.VerifyNoOtherCalls();
            Task controller = fixture.StartControllerAsync();
            await fixture.AwaitAsync(Task.WhenAll(child, controller));
            fixture.AssertReadyHandshake();
            await ConsumeAsync(fixture, "after-ready", "Discovery after ready Ω", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(2);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Exactly(2));
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Never);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_PreCanceledChildCallsCanRetry()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            using var rejected = new CancellationTokenSource();
            rejected.Cancel();
            OperationCanceledException start = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => fixture.SessionLifetime.OnTestSessionStartingAsync(new TestSessionContext(rejected.Token)));
            OperationCanceledException consume = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => fixture.Consumer.ConsumeAsync(null!, Update("rejected", "Rejected 東京", PassedTestNodeStateProperty.CachedInstance), rejected.Token));
            Assert.AreEqual(rejected.Token, start.CancellationToken);
            Assert.AreEqual(rejected.Token, consume.CancellationToken);
            Assert.IsEmpty(fixture.ChildMessages);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ProcessInformation.VerifyNoOtherCalls();

            string? originalEndpoint = fixture.PipeName;
            await fixture.ConnectAsync();
            fixture.AssertReadyHandshake();
            Assert.AreEqual(originalEndpoint, fixture.PipeName);
            await ConsumeAsync(fixture, "accepted", "Accepted after cancellation Ω", PassedTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            Assert.IsEmpty(RemovalMessages(fixture), "Rejected terminal input must not invent a running test.");
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_PreCanceledControllerStartCanRetry()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            using var rejected = new CancellationTokenSource();
            rejected.Cancel();
            Task rejectedStart = fixture.StartControllerAsync(rejected.Token);
            OperationCanceledException cancellation = await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.AwaitAsync(rejectedStart));
            Assert.IsTrue(cancellation.CancellationToken.IsCancellationRequested);
            Assert.AreNotEqual(rejected.Token, cancellation.CancellationToken, "Started passes its linked handshake token to the wait.");
            Assert.IsNull(fixture.ReversePipeName);
            Assert.IsEmpty(fixture.ControllerMessages.Where(message => message.StartsWith("Connected to the test host server pipe '", StringComparison.Ordinal)));
            fixture.ProcessInformation.VerifyNoOtherCalls();

            string? originalEndpoint = fixture.PipeName;
            await fixture.ConnectAsync();
            fixture.AssertReadyHandshake();
            Assert.AreEqual(originalEndpoint, fixture.PipeName);
            Assert.HasCount(2, fixture.ControllerMessages.Where(message => message.StartsWith("Wait for test host connection to the server pipe '", StringComparison.Ordinal)));
            await ConsumeAsync(fixture, "retry", "Controller retry 東京", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_CanceledControllerWaitCanRetryAfterPrimaryConnection()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        var primaryConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseChild = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ChildLogAsync = message =>
        {
            if (message == "Connected to the process lifetime handler")
            {
                primaryConnected.TrySetResult(true);
                return releaseChild.Task;
            }

            return Task.CompletedTask;
        };
        try
        {
            await fixture.BindAsync();
            Task child = fixture.StartChildAsync();
            await fixture.AwaitAsync(primaryConnected.Task);
            await fixture.AwaitAsync(fixture.ControllerAccepted.Task);
            using var rejected = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
            Task firstController = fixture.StartControllerAsync(rejected.Token);
            await fixture.AwaitAsync(fixture.ControllerWaitEntered.Task);
            rejected.Cancel();
            OperationCanceledException cancellation = await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.AwaitAsync(firstController));
            Assert.IsTrue(cancellation.CancellationToken.IsCancellationRequested);
            Assert.AreNotEqual(rejected.Token, cancellation.CancellationToken);
            Assert.IsNull(fixture.ReversePipeName);
            Assert.IsEmpty(fixture.ControllerMessages.Where(message => message.StartsWith("Consumer pipe name received '", StringComparison.Ordinal)
                || message.StartsWith("Connected to the test host server pipe '", StringComparison.Ordinal)));
            fixture.ProcessInformation.VerifyNoOtherCalls();

            fixture.ChildLogAsync = null;
            releaseChild.TrySetResult(true);
            Task retry = fixture.StartControllerAsync();
            await fixture.AwaitAsync(Task.WhenAll(child, retry));
            fixture.AssertReadyHandshake();
            Assert.HasCount(1, fixture.ControllerMessages.Where(message => message.StartsWith("Client connected to ", StringComparison.Ordinal)));
            Assert.HasCount(2, fixture.ControllerMessages.Where(message => message.StartsWith("Wait for test host connection to the server pipe '", StringComparison.Ordinal)));
            await ConsumeAsync(fixture, "after-wait-cancel", "Ready after controller cancellation Ω", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
        }
        finally
        {
            releaseChild.TrySetResult(true);
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "HangProtocol_ControllerWaitIoFailure")]
    [DataRow(true, DisplayName = "HangProtocol_ControllerWaitForeignCancellation")]
    public async Task RegisteredLifecycle_ControllerWaitFailureCanRetry(bool foreignCancellation)
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        using var foreign = new CancellationTokenSource();
        Exception failure = foreignCancellation
            ? new OperationCanceledException("The controller wait diagnostic was canceled by another caller.", foreign.Token)
            : new IOException("The controller wait diagnostic failed.");
        fixture.ControllerLogAsync = message => message.StartsWith("Wait for test host connection to the server pipe '", StringComparison.Ordinal)
            ? Task.FromException(failure)
            : Task.CompletedTask;
        try
        {
            await fixture.BindAsync();
            Task start = fixture.StartControllerAsync();
            Exception actual = foreignCancellation
                ? await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fixture.AwaitAsync(start))
                : await Assert.ThrowsExactlyAsync<IOException>(() => fixture.AwaitAsync(start));
            Assert.AreSame(failure, actual);
            if (foreignCancellation)
            {
                Assert.AreEqual(foreign.Token, ((OperationCanceledException)actual).CancellationToken);
            }

            Assert.IsNull(fixture.ReversePipeName);
            Assert.IsEmpty(fixture.ChildMessages);
            fixture.ProcessInformation.VerifyNoOtherCalls();
            fixture.ControllerLogAsync = null;
            await fixture.ConnectAsync();
            fixture.AssertReadyHandshake();
            Assert.HasCount(2, fixture.ControllerMessages.Where(message => message.StartsWith("Wait for test host connection to the server pipe '", StringComparison.Ordinal)));
            await ConsumeAsync(fixture, "after-wait-failure", "Ready after diagnostic failure 東京", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "HangProtocol_ChildConnectedIoFailure")]
    [DataRow(1, DisplayName = "HangProtocol_ChildConnectedSessionCancellation")]
    [DataRow(2, DisplayName = "HangProtocol_ChildConnectedForeignCancellation")]
    public async Task RegisteredLifecycle_ChildFailureAfterPrimaryConnectionHonorsCancellationFilter(int scenario)
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        using var foreign = new CancellationTokenSource();
        Exception failure = scenario == 0
            ? new IOException("The post-connection child diagnostic failed.")
            : new OperationCanceledException("The post-connection child diagnostic was canceled.", scenario == 1 ? fixture.Token : foreign.Token);
        fixture.ChildLogAsync = message => message == "Connected to the process lifetime handler"
            ? Task.FromException(failure)
            : Task.CompletedTask;
        try
        {
            await fixture.BindAsync();
            Task start = fixture.StartChildAsync();
            if (scenario == 1)
            {
                await fixture.AwaitAsync(start);
            }
            else
            {
                Exception actual = scenario == 0
                    ? await Assert.ThrowsExactlyAsync<IOException>(() => fixture.AwaitAsync(start))
                    : await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fixture.AwaitAsync(start));
                Assert.AreSame(failure, actual);
                if (scenario == 2)
                {
                    Assert.AreEqual(foreign.Token, ((OperationCanceledException)actual).CancellationToken);
                }
            }

            await fixture.AwaitAsync(fixture.ControllerAccepted.Task);
            Assert.AreSequenceEqual(
                new[]
            {
                $"Connecting to the process lifetime handler {fixture.PipeName}",
                "Connected to the process lifetime handler",
            }, fixture.ChildMessages);
            Assert.IsNull(fixture.ReversePipeName);
            Assert.IsEmpty(fixture.ControllerMessages.Where(message => message.StartsWith("Consumer pipe name received '", StringComparison.Ordinal)));
            fixture.AssertSignalCount(0);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ProcessInformation.VerifyNoOtherCalls();
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_ControllerConnectedFailureStillCleansOwnedTransport()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        var failure = new IOException("The reverse-connected controller diagnostic failed before timer creation.");
        fixture.ControllerLogAsync = message => message.StartsWith("Connected to the test host server pipe '", StringComparison.Ordinal)
            ? Task.FromException(failure)
            : Task.CompletedTask;
        try
        {
            await fixture.BindAsync();
            Task controller = fixture.StartControllerAsync();
            Task child = fixture.StartChildAsync();
            IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() => fixture.AwaitAsync(controller));
            Assert.AreSame(failure, actual);
            await fixture.AwaitAsync(child);
            fixture.AssertReadyHandshake();
            await ConsumeAsync(fixture, "connected-without-timer", "Discovery after connected diagnostic failure Ω", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Once);
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, Times.Never);
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "HangProtocol_SupersededFailure")]
    [DataRow(true, DisplayName = "HangProtocol_SupersededCompletion")]
    public async Task RegisteredLifecycle_SupersededRetrySignalsWithoutLosingTrackedTest(bool completionMarker)
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            await fixture.ConnectAsync();
            await ConsumeAsync(fixture, "retried", "Original retry 東京", InProgressTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "other", "Other tracked Ω", InProgressTestNodeStateProperty.CachedInstance);
            await ConsumeAsync(fixture, "retried", "Renamed retry", InProgressTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(0);
            IProperty supersededState = completionMarker
                ? TestNodeExecutionCompletedProperty.CachedInstance
                : new FailedTestNodeStateProperty();
            await ConsumeAsync(fixture, "retried", "Superseded retry", supersededState, new RetryAttemptProperty(1, isSuperseded: true));
            Assert.IsEmpty(RemovalMessages(fixture));
            fixture.AssertSignalCount(1);
            await ConsumeAsync(fixture, "retried", "Final retry", PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false));
            AssertRemoval(Assert.ContainsSingle(RemovalMessages(fixture)), "Original retry 東京", remaining: 1);
            await ConsumeAsync(fixture, "other", "Other final", TestNodeExecutionCompletedProperty.CachedInstance);
            string[] removals = RemovalMessages(fixture);
            Assert.HasCount(2, removals);
            AssertRemoval(removals[1], "Other tracked Ω", remaining: 0);
            fixture.AssertSignalCount(3);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_PreCanceledExitLeavesChannelUsableForRetry()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            await fixture.ConnectAsync();
            using var rejected = new CancellationTokenSource();
            rejected.Cancel();
            OperationCanceledException cancellation = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, rejected.Token));
            Assert.AreEqual(rejected.Token, cancellation.CancellationToken);
            fixture.ProcessInformation.VerifyNoOtherCalls();
            await ConsumeAsync(fixture, "after-rejected-exit", "Activity after rejected exit 東京", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Once);
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, Times.Never);
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task RegisteredLifecycle_ExitDiagnosticFailureCanRetryAfterTimerCleanup()
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken)
        {
            GracefulExit = false,
            ExitCode = 27,
        };
        var failure = new IOException("The nongraceful-exit diagnostic failed.");
        fixture.ControllerLogAsync = message => message == "Testhost didn't exit gracefully '27')"
            ? Task.FromException(failure)
            : Task.CompletedTask;
        try
        {
            await fixture.BindAsync();
            await fixture.ConnectAsync();
            IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
                () => fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            Assert.AreSame(failure, actual);
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Once);
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, Times.Once);
            fixture.AssertNoDumpWork();
            fixture.ControllerLogAsync = null;
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Exactly(2));
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, Times.Exactly(2));
            Assert.AreSequenceEqual(
                new[] { "Testhost didn't exit gracefully '27')", "Testhost didn't exit gracefully '27')" },
                fixture.ControllerMessages.Where(message => message.StartsWith("Testhost didn't exit gracefully ", StringComparison.Ordinal)));
            fixture.AssertSignalCount(0);
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "HangProtocol_SynchronousDisposal")]
#if NETCOREAPP
    [DataRow(true, DisplayName = "HangProtocol_AsynchronousDisposal")]
#endif
    public async Task RegisteredLifecycle_DisposeWithoutExitClosesClientsAndDrainsTasks(bool useAsyncDisposal)
    {
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        try
        {
            await fixture.BindAsync();
            await fixture.ConnectAsync();
            await ConsumeAsync(fixture, "before-disposal", "Activity before disposal Ω", DiscoveredTestNodeStateProperty.CachedInstance);
            fixture.AssertSignalCount(1);
            await fixture.ShutdownAsync(useAsyncDisposal);
            await fixture.ShutdownAsync(useAsyncDisposal);
            Assert.IsTrue(fixture.AllTrackedTasksCompleted);
            Assert.AreEqual(3, fixture.TrackedTransportTaskCount, "One primary accept and one loop for each registered direction must be drained.");
            fixture.ProcessInformation.VerifyNoOtherCalls();

            // CancellationToken.None makes the disposed client, not the canceled session token,
            // the rejection boundary. The semaphore rejects before writing to a closed pipe.
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => fixture.Consumer.ConsumeAsync(null!, Update("after-disposal", "No request after disposal", DiscoveredTestNodeStateProperty.CachedInstance), CancellationToken.None));
            fixture.AssertSignalCount(1);
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "HangProtocol_ExitGateCancellation")]
    [DataRow(true, DisplayName = "HangProtocol_ExitGateDiagnosticFailure")]
    public async Task RegisteredLifecycle_ExitClaimedGateCompletesCanceledPendingHandshake(bool diagnosticFails)
    {
        const string RecoveryPrefix = "Test host handshake failed after the dump started; continuing so the dump can be published. ";
        var fixture = new HangDumpInProcessProtocolFixture(TestContext.CancellationToken);
        var diagnosticFailure = new IOException("The exit-gate recovery diagnostic failed.");
        fixture.ControllerLogAsync = message => diagnosticFails && message.StartsWith(RecoveryPrefix, StringComparison.Ordinal)
            ? Task.FromException(diagnosticFailure)
            : Task.CompletedTask;
        try
        {
            await fixture.BindAsync();
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
            Task pendingStart = fixture.StartControllerAsync(caller.Token);
            await fixture.AwaitAsync(fixture.ControllerWaitEntered.Task);

            // Public exit claims the same gate observed by the handshake filter, without
            // triggering a dump. Cancel only after that callback has returned.
            await fixture.AwaitAsync(fixture.Controller.OnTestHostProcessExitedAsync(fixture.ProcessInformation.Object, fixture.Token));
            caller.Cancel();
            await fixture.AwaitAsync(pendingStart);

            string recovery = Assert.ContainsSingle(fixture.ControllerMessages.Where(
                message => message.StartsWith(RecoveryPrefix, StringComparison.Ordinal)));
            Assert.StartsWith(RecoveryPrefix, recovery);
            Assert.Contains("CanceledException:", recovery);
            Assert.IsNull(fixture.ReversePipeName);
            Assert.IsEmpty(fixture.ChildMessages);
            Assert.IsEmpty(fixture.ControllerMessages.Where(message => message.StartsWith("Consumer pipe name received '", StringComparison.Ordinal)
                || message.StartsWith("Connected to the test host server pipe '", StringComparison.Ordinal)));
            fixture.ProcessInformation.VerifyGet(value => value.HasExitedGracefully, Times.Once);
            fixture.ProcessInformation.VerifyGet(value => value.ExitCode, Times.Never);
            fixture.ControllerClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.ChildClock.VerifyGet(value => value.UtcNow, Times.Never);
            fixture.AssertSignalCount(0);
            fixture.AssertNoDumpWork();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    private static Task ConsumeAsync(HangDumpInProcessProtocolFixture fixture, string uid, string name, params IProperty[] properties)
        => fixture.AwaitAsync(fixture.Consumer.ConsumeAsync(null!, Update(uid, name, properties), fixture.Token));

    private static TestNodeUpdateMessage Update(string uid, string name, params IProperty[] properties)
        => new(new SessionUid("owned-hang-protocol-session"), new TestNode { Uid = uid, DisplayName = name, Properties = new PropertyBag(properties) });

    private static string[] RemovalMessages(HangDumpInProcessProtocolFixture fixture)
        => [.. fixture.ChildMessages.Where(message => message.StartsWith("Test removed from in-progress list '", StringComparison.Ordinal))];

    private static void AssertRemoval(string message, string originalName, int remaining)
    {
        Assert.StartsWith($"Test removed from in-progress list '{originalName}' after '", message);
        Assert.EndsWith($"', total in-progress '{remaining}'", message);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class SlowTestReporterStateBehaviorTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "SlowReporter_Disabled")]
    [DataRow(1, DisplayName = "SlowReporter_ActivationRejected")]
    [DataRow(2, DisplayName = "SlowReporter_ActivationFailed")]
    [DataRow(3, DisplayName = "SlowReporter_SchedulingFailed")]
    public async Task Start_GatesAndFailuresDoNotScheduleUnusableWork(int partition)
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture)
        {
            Enabled = partition != 0,
            Activate = partition != 1,
            ActivationError = partition == 2 ? new IOException("activation failed") : null,
        };
        if (partition == 3)
        {
            fixture.TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("scheduler failed"));
        }

        Assert.AreEqual(partition != 0, await reporter.IsEnabledAsync());
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.AfterFinish", new InProgressTestNodeStateProperty()), CancellationToken.None);
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddMinutes(1), CancellationToken.None);

        Assert.IsEmpty(reporter.Emissions);
        fixture.TaskService.Verify(
            value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            partition == 3 ? Times.Once() : Times.Never());
        if (partition >= 2)
        {
            Assert.HasCount(1, fixture.Logs);
            Assert.Contains("Unexpected exception in OnTestSessionStartingAsync", fixture.LogText);
            Assert.Contains(partition == 2 ? "activation failed" : "scheduler failed", fixture.LogText);
        }
        else
        {
            Assert.IsEmpty(fixture.Logs);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SlowReporter_CanceledStart")]
    [DataRow(true, DisplayName = "SlowReporter_CanceledConsume")]
    public async Task Callbacks_CancellationPropagatesWithoutDiagnostics(bool consume)
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => consume
            ? reporter.ConsumeAsync(null!, Mock.Of<IData>(), cancellation.Token)
            : reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session(cancellation.Token)));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsEmpty(fixture.Logs);
        fixture.TaskService.VerifyNoOtherCalls();
        Assert.IsEmpty(reporter.Emissions);
    }

    [TestMethod]
    public async Task Consume_ThresholdFailureDoesNotPoisonOtherTests()
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture) { ThresholdErrorFor = "Tests.Broken" };
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Broken", new InProgressTestNodeStateProperty()), CancellationToken.None);
        await reporter.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Healthy", new InProgressTestNodeStateProperty()), CancellationToken.None);
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(5), CancellationToken.None);

        Assert.AreEqual(("Tests.Healthy", "Tests.Healthy", TimeSpan.FromSeconds(5)), Assert.ContainsSingle(reporter.Emissions));
        Assert.Contains("Unexpected exception in ConsumeAsync", Assert.ContainsSingle(fixture.Logs).Message);
        Assert.Contains("threshold unavailable", fixture.LogText);
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
    }

    [TestMethod]
    public async Task Loop_InjectedDelayAndClockDriveOneScanWithoutTimers()
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture);
        using var sessionCancellation = new CancellationTokenSource();
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session(sessionCancellation.Token));
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Scanned", new InProgressTestNodeStateProperty()), CancellationToken.None);
        fixture.Now = CiReporterFixture.Epoch.AddSeconds(5);
        int delays = 0;
        fixture.Delay = (_, token) => ++delays == 1
            ? Task.CompletedTask
            : Task.FromException(new OperationCanceledException(token));

        Assert.IsNotNull(fixture.ScheduledAction);
        await fixture.ScheduledAction();

        Assert.AreEqual("ControlledSlowReporter", fixture.ScheduledName);
        Assert.IsTrue(fixture.ScheduledToken.CanBeCanceled);
        Assert.AreSequenceEqual([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)], fixture.Delays);
        Assert.AreEqual(("Tests.Scanned", "Tests.Scanned", TimeSpan.FromSeconds(5)), Assert.ContainsSingle(reporter.Emissions));
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        Assert.IsTrue(fixture.ScheduledToken.IsCancellationRequested);
        Assert.IsFalse(sessionCancellation.IsCancellationRequested);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SlowReporter_FaultedLoop")]
    [DataRow(true, DisplayName = "SlowReporter_CanceledLoop")]
    public async Task Finish_FailedOrCanceledLoopClearsState(bool canceled)
    {
        using var fixture = new CiReporterFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        fixture.ScheduledTask = canceled
            ? Task.FromCanceled(cancellation.Token)
            : Task.FromException(new IOException("loop unavailable"));
        var reporter = new Reporter(fixture);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Running", new InProgressTestNodeStateProperty()), CancellationToken.None);

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session(cancellation.Token));
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddMinutes(1), CancellationToken.None);

        Assert.IsEmpty(reporter.Emissions);
        Assert.HasCount(canceled ? 0 : 1, fixture.Logs);
        if (!canceled)
        {
            Assert.Contains("Unexpected exception in OnTestSessionFinishingAsync", fixture.LogText);
            Assert.Contains("loop unavailable", fixture.LogText);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SlowReporter_EmissionError")]
    [DataRow(true, DisplayName = "SlowReporter_EmissionCancellation")]
    public async Task Scan_EmissionFailurePreservesBackoffAndCancellation(bool canceled)
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Emission", new InProgressTestNodeStateProperty()), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        reporter.EmissionError = canceled ? new OperationCanceledException(cancellation.Token) : new IOException("emit failed");

        if (canceled)
        {
            OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(5), cancellation.Token));
            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            Assert.IsEmpty(fixture.Logs);
        }
        else
        {
            await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(5), CancellationToken.None);
            Assert.Contains("Unexpected exception in ScanOnceAsync", Assert.ContainsSingle(fixture.Logs).Message);
            Assert.Contains("emit failed", fixture.LogText);
        }

        reporter.EmissionError = null;
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(9), CancellationToken.None);
        Assert.HasCount(1, reporter.Emissions);
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(10), CancellationToken.None);
        Assert.AreSequenceEqual([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], reporter.Emissions.Select(entry => entry.Elapsed));
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SlowReporter_SupersededFailure")]
    [DataRow(true, DisplayName = "SlowReporter_SupersededCompletion")]
    public async Task HeartbeatAndRetry_PreserveOriginalStartUntilFinalCompletion(bool completion)
    {
        using var fixture = new CiReporterFixture();
        var reporter = new Reporter(fixture);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        TestNodeUpdateMessage first = CiReporterFixture.UpdateNamed("Tests.Heartbeat", "original row", new InProgressTestNodeStateProperty());
        await reporter.ConsumeAsync(null!, first, CancellationToken.None);
        fixture.Now = CiReporterFixture.Epoch.AddSeconds(3);
        TestNodeUpdateMessage heartbeat = CiReporterFixture.UpdateNamed("Tests.Heartbeat", "renamed heartbeat", new InProgressTestNodeStateProperty());
        await reporter.ConsumeAsync(null!, heartbeat, CancellationToken.None);
        IProperty state = completion ? TestNodeExecutionCompletedProperty.CachedInstance : new FailedTestNodeStateProperty();
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Heartbeat", state, new RetryAttemptProperty(1, true)), CancellationToken.None);

        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddSeconds(5), CancellationToken.None);

        Assert.AreEqual(("Tests.Heartbeat", "original row", TimeSpan.FromSeconds(5)), Assert.ContainsSingle(reporter.Emissions));
        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Heartbeat", TestNodeExecutionCompletedProperty.CachedInstance, new RetryAttemptProperty(2, false)), CancellationToken.None);
        await reporter.ScanOnceAsync(CiReporterFixture.Epoch.AddMinutes(1), CancellationToken.None);
        Assert.HasCount(1, reporter.Emissions);
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
    }

    private sealed class Reporter(CiReporterFixture fixture)
        : SlowTestReporterBase(fixture.Output.Object, fixture.TaskService.Object, fixture.Clock.Object, fixture.Logger.Object)
    {
        public bool Enabled { get; set; } = true;

        public bool Activate { get; set; } = true;

        public Exception? ActivationError { get; set; }

        public string? ThresholdErrorFor { get; set; }

        public Exception? EmissionError { get; set; }

        public List<(string TestName, string DisplayLabel, TimeSpan Elapsed)> Emissions { get; } = [];

        public override string Uid => "ControlledSlowReporter";

        public override string DisplayName => "Controlled slow reporter";

        public override string Description => "Deterministic scan state";

        protected override bool IsEnabled => Enabled;

        protected override bool OnActivating() => ActivationError is null ? Activate : throw ActivationError;

        protected override string GetTestName(TestNode testNode) => testNode.Uid;

        protected override string GetDisplayLabel(TestNode testNode) => testNode.DisplayName;

        protected override TimeSpan ResolveThreshold(string testName)
            => testName == ThresholdErrorFor ? throw new IOException("threshold unavailable") : TimeSpan.FromSeconds(5);

        protected override Task EmitSlowTestAsync(string testName, string displayLabel, TimeSpan elapsed, CancellationToken cancellationToken)
        {
            Emissions.Add((testName, displayLabel, elapsed));
            return EmissionError is null ? Task.CompletedTask : Task.FromException(EmissionError);
        }
    }
}

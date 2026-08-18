// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

#pragma warning disable TPEXP // IGracefulStopTestExecutionCapability is for evaluation purposes only.

[TestClass]
public sealed class AbortAtDeadlineExtensionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly CancellationTokenSource _cts = new();
    private readonly Mock<IStopPoliciesService> _policiesService = new();
    private readonly Mock<IGracefulStopTestExecutionCapability> _capability = new();

    public TestContext TestContext { get; set; } = null!;

    public void Dispose() => _cts.Dispose();

    [TestMethod]
    public async Task WhenGracefulStopFails_TheDeadlineVerdictIsReverted()
    {
        TaskCompletionSource<bool> reverted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _policiesService.Setup(x => x.RevertDeadlineTrigger()).Callback(() => reverted.TrySetResult(true));
        _capability
            .Setup(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("This framework refuses to stop."));

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        // The stop was never accepted, so the run carries on and executes every test. Reporting exit code 15
        // for it would be a lie, so the verdict committed before the stop request has to be taken back.
        await WaitForAsync(reverted.Task);
        _policiesService.Verify(x => x.RevertDeadlineTrigger(), Times.Once);
    }

    [TestMethod]
    public async Task WhenGracefulStopSucceeds_TheDeadlineVerdictIsKept()
    {
        TaskCompletionSource<bool> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopped.TrySetResult(true);
                return Task.CompletedTask;
            });

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        await WaitForAsync(stopped.Task);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
        _policiesService.Verify(x => x.RevertDeadlineTrigger(), Times.Never);
    }

    [TestMethod]
    public async Task WhenTestExecutionCompleted_TheDeadlineDoesNotTrigger()
    {
        TaskCompletionSource<bool> triggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _policiesService.Setup(x => x.ExecuteDeadlineCallbacksAsync()).Callback(() => triggered.TrySetResult(true)).Returns(Task.CompletedTask);

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.FromMilliseconds(300));

        // The host signals this the moment the test framework invoker returns, which is well before the timer
        // below fires. Everything after it -- draining the message bus, the reporters -- happens on a run that
        // already executed every test, so a deadline reached during it must not truncate the verdict.
        extension.NotifyTestExecutionCompleted();

        Task completed = await Task.WhenAny(triggered.Task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
        Assert.AreNotSame(triggered.Task, completed, "The deadline fired even though test execution had already completed.");
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
        _capability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task WhenTestExecutionIsStillRunning_TheDeadlineTriggers()
    {
        // Control for the test above: same timer, no completion signal, so the deadline must fire. Without
        // this, that test would still pass if the timer were simply broken.
        TaskCompletionSource<bool> triggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _policiesService.Setup(x => x.ExecuteDeadlineCallbacksAsync()).Callback(() => triggered.TrySetResult(true)).Returns(Task.CompletedTask);

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.FromMilliseconds(300));

        await WaitForAsync(triggered.Task);
        _capability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenTestExecutionCompletesWhileTheDeadlineIsBeingReported_TheVerdictIsNotCommitted()
    {
        // The overlap the checks above cannot catch. The handler does not run on the timer callback: it yields
        // and then reports, and the test framework invoker can return during exactly that window. Checking
        // completion only before starting the handler would therefore still let the deadline commit its
        // verdict and request a stop for a run in which every test had already finished (exit code 15 on a
        // complete run). Gate the handler inside its reporting to make that window deterministic.
        TaskCompletionSource<bool> reporting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> abandoned = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onLog: async logLevel =>
            {
                switch (logLevel)
                {
                    // "Deadline approaching ...", written before the verdict is committed.
                    case LogLevel.Information:
                        reporting.TrySetResult(true);
                        await release.Task;
                        break;

                    // "... abandoning the graceful stop.", the only thing the handler does once it sees that
                    // test execution won the race.
                    case LogLevel.Debug:
                        abandoned.TrySetResult(true);
                        break;
                }
            });

        // Park the handler mid-report, then let the invoker return underneath it.
        await WaitForAsync(reporting.Task);
        extension.NotifyTestExecutionCompleted();
        release.SetResult(true);

        await WaitForAsync(abandoned.Task);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
        _policiesService.Verify(x => x.RevertDeadlineTrigger(), Times.Never);
        _capability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task WhenTestExecutionCompletesAfterTheDeadlineClaimedTheRun_TheVerdictStands()
    {
        // The mirror image of the test above, and the reason completion cannot simply revert the verdict: the
        // graceful stop is what makes execution finish, so the completion that follows a claimed deadline is
        // the stop taking effect, not a run that got there on its own.
        TaskCompletionSource<bool> stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                stopping.TrySetResult(true);
                await release.Task;
            });

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        await WaitForAsync(stopping.Task);
        extension.NotifyTestExecutionCompleted();
        release.SetResult(true);

        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
        _policiesService.Verify(x => x.RevertDeadlineTrigger(), Times.Never);
    }

    private async Task WaitForAsync(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(task, completed, "Timed out waiting for the deadline handler.");
        await task;
    }

    private AbortAtDeadlineExtension CreateExtension(TimeSpan deadlineIn, Func<LogLevel, Task>? onLog = null)
    {
        Mock<IEnvironment> environment = new();
        _ = environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE))
            .Returns((Now + deadlineIn).ToString("o", CultureInfo.InvariantCulture));

        // A zero stop margin makes the stop instant the deadline itself, so deadlineIn is exactly how long the
        // timer waits (measured against the fixed clock below, not wall-clock time when the test starts).
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN))
            .Returns("0");

        Mock<IClock> clock = new();
        _ = clock.SetupGet(x => x.UtcNow).Returns(Now);

        Mock<ILogger> logger = new();
        _ = logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns((LogLevel logLevel, string _, Exception? _, Func<string, Exception?, string> _)
                => onLog is null ? Task.CompletedTask : onLog(logLevel));
        Mock<ILoggerFactory> loggerFactory = new();
        _ = loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        Mock<IOutputDevice> outputDevice = new();
        _ = outputDevice
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        _ = cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);

        return new AbortAtDeadlineExtension(
            environment.Object,
            clock.Object,
            _capability.Object,
            _policiesService.Object,
            cancellationTokenSource.Object,
            outputDevice.Object,
            loggerFactory.Object);
    }
}

#pragma warning restore TPEXP

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
    private readonly Mock<IGracefulStopTestExecutionResultCapability> _capability = new();

    public AbortAtDeadlineExtensionTests()
        => _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    public TestContext TestContext { get; set; } = null!;

    public void Dispose() => _cts.Dispose();

    [TestMethod]
    public async Task WhenGracefulStopFails_TheDeadlineVerdictIsNeverCommitted()
    {
        TaskCompletionSource<bool> stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                stopping.TrySetResult(true);
                await release.Task;
                throw new InvalidOperationException("This framework refuses to stop.");
            });

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        await WaitForAsync(stopping.Task);
        Task resolution = extension.WaitForDeadlineHandlingAsync();
        Assert.IsFalse(resolution.IsCompleted, "The deadline outcome resolved before the asynchronous stop request completed.");

        release.SetResult(true);
        await WaitForAsync(resolution);

        // The host awaits this resolution before reporters run. A rejected stop therefore leaves no transient
        // deadline verdict for an exit-code consumer to observe.
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
    }

    [TestMethod]
    public async Task WhenGracefulStopSucceeds_TheDeadlineVerdictIsKept()
    {
        TaskCompletionSource<bool> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IOutputDeviceData? displayedData = null;
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopped.TrySetResult(true);
                return Task.FromResult(true);
            });

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onDisplayData: data => displayedData = data);

        await WaitForAsync(stopped.Task);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
        Assert.IsInstanceOfType<SessionMessageOutputDeviceData>(displayedData);
    }

    [TestMethod]
    public async Task WhenGracefulStopReportsExecutionAlreadyCompleted_TheDeadlineVerdictIsNotCommitted()
    {
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
    }

    [TestMethod]
    public async Task WhenFrameworkUsesLegacyGracefulStopCapability_TheDeadlineVerdictIsKept()
    {
        TaskCompletionSource<bool> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IGracefulStopTestExecutionCapability> legacyCapability = new();
        legacyCapability
            .Setup(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => stopped.TrySetResult(true))
            .Returns(Task.CompletedTask);

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            capability: legacyCapability.Object);

        await WaitForAsync(stopped.Task);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        legacyCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
    }

    [TestMethod]
    public async Task WhenFrameworkStopIsRejected_FallbackStopCommitsDeadlineVerdict()
    {
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        TaskCompletionSource<bool> fallbackStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> deadlineCommitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _policiesService
            .Setup(x => x.ExecuteDeadlineCallbacksAsync())
            .Callback(() => deadlineCommitted.TrySetResult(true))
            .Returns(Task.CompletedTask);

        _policiesService
            .Setup(x => x.TryExecuteDeadlineStopFallbackAsync())
            .Returns(() =>
            {
                fallbackStopped.TrySetResult(true);
                return Task.FromResult(true);
            });
        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.FromMilliseconds(300));

        await WaitForAsync(deadlineCommitted.Task);

        Assert.IsTrue(fallbackStopped.Task.IsCompleted);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
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
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task WhenPoliciesReportTestExecutionCompleted_TheDeadlineDoesNotTrigger()
    {
        TaskCompletionSource<bool> triggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _policiesService.SetupGet(x => x.IsTestExecutionCompleted).Returns(true);
        _policiesService.Setup(x => x.ExecuteDeadlineCallbacksAsync()).Callback(() => triggered.TrySetResult(true)).Returns(Task.CompletedTask);

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        Task completed = await Task.WhenAny(triggered.Task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
        Assert.AreNotSame(triggered.Task, completed, "The deadline fired even though the shared policy state reported completed execution.");
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InvalidDeadlineIsVisibleWithoutDiagnosticLogging()
    {
        List<IOutputDeviceData> displayedData = [];
        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onDisplayData: displayedData.Add,
            deadlineValue: "not-a-deadline");

        Assert.IsFalse(await extension.IsEnabledAsync());
        Assert.HasCount(1, displayedData);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(displayedData[0]);
        Assert.Contains(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, warning.Message);
    }

    [TestMethod]
    public async Task InvertedMarginsAreVisibleWithoutDiagnosticLogging()
    {
        List<IOutputDeviceData> displayedData = [];
        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.FromMinutes(1),
            onDisplayData: displayedData.Add,
            stopMargin: "10",
            dumpMargin: "30",
            isHangDumpEnabled: true);

        Assert.IsTrue(await extension.IsEnabledAsync());
        Assert.HasCount(1, displayedData);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(displayedData[0]);
        Assert.Contains("00:00:30", warning.Message);
        Assert.Contains("00:00:10", warning.Message);
    }

    [TestMethod]
    public async Task InvertedMarginsAreVisibleWhenDiagnosticLoggingThrows()
    {
        List<IOutputDeviceData> displayedData = [];
        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.FromMinutes(1),
            onDisplayData: displayedData.Add,
            stopMargin: "10",
            dumpMargin: "30",
            isHangDumpEnabled: true,
            throwOnSynchronousLog: true);

        Assert.IsTrue(await extension.IsEnabledAsync());
        Assert.HasCount(1, displayedData);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(displayedData[0]);
        Assert.Contains("00:00:30", warning.Message);
        Assert.Contains("00:00:10", warning.Message);
    }

    [TestMethod]
    public async Task InvertedMarginsWithoutHangDumpDoNotWarn()
    {
        List<IOutputDeviceData> displayedData = [];
        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.FromMinutes(1),
            onDisplayData: displayedData.Add,
            stopMargin: "10",
            dumpMargin: "30");

        Assert.IsTrue(await extension.IsEnabledAsync());
        Assert.IsEmpty(displayedData);
    }

    [TestMethod]
    public async Task MissingGracefulStopCapabilityIsVisibleWithoutDiagnosticLogging()
    {
        List<IOutputDeviceData> displayedData = [];
        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.FromMinutes(1),
            onDisplayData: displayedData.Add,
            stopMargin: "60",
            hasCapability: false);

        Assert.IsFalse(await extension.IsEnabledAsync());
        Assert.HasCount(1, displayedData);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(displayedData[0]);
        Assert.Contains(nameof(IGracefulStopTestExecutionCapability), warning.Message);
    }

    [TestMethod]
    public async Task WhenTheApproachingDeadlineLogNeverCompletes_TheGracefulStopIsRequestedFirst()
    {
        TaskCompletionSource<bool> reporting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopped.TrySetResult(true);
                return Task.FromResult(true);
            });

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onLog: async logLevel =>
            {
                if (logLevel == LogLevel.Information)
                {
                    reporting.TrySetResult(true);
                    await release.Task;
                }
            });

        try
        {
            await WaitForAsync(reporting.Task);
            await WaitForAsync(stopped.Task);
            _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task WhenTestExecutionCompletesAfterTheDeadlineClaimedTheRun_TheVerdictStands()
    {
        // Completion cannot simply revert the verdict: the graceful stop is what makes execution finish, so
        // completion after a claimed deadline is the stop taking effect, not a run that got there on its own.
        TaskCompletionSource<bool> stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                stopping.TrySetResult(true);
                await release.Task;
                return true;
            });

        using AbortAtDeadlineExtension extension = CreateExtension(deadlineIn: TimeSpan.Zero);

        await WaitForAsync(stopping.Task);
        extension.NotifyTestExecutionCompleted();
        release.SetResult(true);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
    }

    [TestMethod]
    public async Task WhenFrameworkWaitsSynchronouslyForExecutionCompletion_DeadlineHandlingDoesNotDeadlock()
    {
        AbortAtDeadlineExtension? extension = null;
        TaskCompletionSource<bool> stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopping.TrySetResult(true);
#pragma warning disable VSTHRD103 // Intentionally simulate a capability that synchronously waits for teardown.
                Task.Run(() => extension!.NotifyTestExecutionCompleted(), TestContext.CancellationToken).GetAwaiter().GetResult();
#pragma warning restore VSTHRD103
                return Task.FromResult(true);
            });

        using (extension = CreateExtension(deadlineIn: TimeSpan.FromMilliseconds(300)))
        {
            await WaitForAsync(stopping.Task);
            await WaitForAsync(extension.WaitForDeadlineHandlingAsync());
        }

        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
    }

    [TestMethod]
    public async Task TheVerdictAndTheGracefulStopBothPrecedeTheUserFacingMessage()
    {
        // The capability accepts the stop before the verdict is committed, and the verdict is committed before
        // the user-facing message. A delayed message therefore cannot delay either the stop or its outcome.
        TaskCompletionSource<bool> displaying = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onDisplay: async () =>
            {
                displaying.TrySetResult(true);
                await release.Task;
            });

        // Park the handler inside the message. Anything not done by now sits inside the window.
        await WaitForAsync(displaying.Task);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Once);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);

        release.SetResult(true);
    }

    [TestMethod]
    public async Task WhenTheUserFacingMessageNeverCompletes_TheGracefulStopIsStillRequested()
    {
        // A wedged output device hands back a task that never completes, and a task that never completes never
        // faults, so swallowing exceptions does not cover it -- only the bound does. The message is written
        // after the stop, so what this pins is that the bound is still there: without it a wedged device would
        // keep the handler task alive past the end of the run, and it would once again be able to swallow the
        // stop entirely if the message ever moved back ahead of it.
        TaskCompletionSource<bool> displaying = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopped.TrySetResult(true);
                return Task.FromResult(true);
            });

        using AbortAtDeadlineExtension extension = CreateExtension(
            deadlineIn: TimeSpan.Zero,
            onDisplay: () =>
            {
                displaying.TrySetResult(true);
                return neverCompletes.Task;
            },
            reportTimeout: TimeSpan.FromMilliseconds(200));

        await WaitForAsync(displaying.Task);

        // Already requested: the message runs after the stop, so a wedged device cannot swallow it. The bound
        // is what then lets the handler finish rather than sitting on a task that never completes.
        await WaitForAsync(stopped.Task);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);

        Task handling = extension.WaitForDeadlineHandlingAsync();
        try
        {
            await WaitForAsync(handling);
            Assert.IsFalse(neverCompletes.Task.IsCompleted);
        }
        finally
        {
            neverCompletes.TrySetResult(true);
        }
    }

    private async Task WaitForAsync(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(task, completed, "Timed out waiting for the deadline handler.");
        await task;
    }

    private AbortAtDeadlineExtension CreateExtension(
        TimeSpan deadlineIn,
        Func<LogLevel, Task>? onLog = null,
        Func<Task>? onDisplay = null,
        Action<IOutputDeviceData>? onDisplayData = null,
        TimeSpan? reportTimeout = null,
        IGracefulStopTestExecutionCapability? capability = null,
        string? deadlineValue = null,
        string stopMargin = "0",
        string? dumpMargin = null,
        bool hasCapability = true,
        bool isHangDumpEnabled = false,
        bool throwOnSynchronousLog = false)
    {
        Mock<IEnvironment> environment = new();
        _ = environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE))
            .Returns(deadlineValue ?? (Now + deadlineIn).ToString("o", CultureInfo.InvariantCulture));

        // A zero stop margin makes the stop instant the deadline itself, so deadlineIn is exactly how long the
        // timer waits (measured against the fixed clock below, not wall-clock time when the test starts).
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN))
            .Returns(stopMargin);
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN))
            .Returns(dumpMargin);

        var stopwatch = Stopwatch.StartNew();
        Mock<IClock> clock = new();
        _ = clock.SetupGet(x => x.UtcNow).Returns(() => Now + stopwatch.Elapsed);

        Mock<ILogger> logger = new();
        _ = logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns((LogLevel logLevel, string _, Exception? _, Func<string, Exception?, string> _)
                => onLog is null ? Task.CompletedTask : onLog(logLevel));
        if (throwOnSynchronousLog)
        {
            logger
                .Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
                .Throws<InvalidOperationException>();
        }

        Mock<ILoggerFactory> loggerFactory = new();
        _ = loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        Mock<IOutputDevice> outputDevice = new();
        _ = outputDevice
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns((IOutputDeviceDataProducer _, IOutputDeviceData data, CancellationToken _) =>
            {
                onDisplayData?.Invoke(data);
                return onDisplay is null ? Task.CompletedTask : onDisplay();
            });

        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        _ = cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);

        return new AbortAtDeadlineExtension(
            environment.Object,
            clock.Object,
            hasCapability ? capability ?? _capability.Object : null,
            _policiesService.Object,
            cancellationTokenSource.Object,
            outputDevice.Object,
            loggerFactory.Object,
            reportTimeout,
            isHangDumpEnabled);
    }
}

#pragma warning restore TPEXP

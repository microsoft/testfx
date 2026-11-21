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
#pragma warning disable CA1416 // Blocking waits are not supported on browser; these tests do not run there.

/// <summary>
/// Tests driving the timer re-arm, the deadline claim and the disposal paths of <see cref="AbortAtDeadlineExtension"/>
/// through a clock whose answer depends on how many times it has been read.
/// </summary>
[TestClass]
public sealed class AbortAtDeadlineExtensionDeadlineHandlingTests
{
    // With a zero stop margin the stop instant is the deadline itself.
    private static readonly DateTimeOffset StopAt = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FirstDueTime = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DisposeDueTime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DisposeObservationWindow = TimeSpan.FromMilliseconds(1200);
    // Observation window for verifying that no later timer callback occurs.
    private static readonly TimeSpan NegativeWindow = TimeSpan.FromMilliseconds(600);

    private readonly Mock<IStopPoliciesService> _policiesService = new();
    private readonly Mock<IGracefulStopTestExecutionResultCapability> _capability = new();
    private readonly TaskCompletionSource<bool> _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _clockCalls;

    public AbortAtDeadlineExtensionDeadlineHandlingTests()
        => _capability
            .Setup(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _stopRequested.TrySetResult(true);
                return Task.FromResult(true);
            });

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TimerFiringExactlyAtTheStopInstant_RequestsTheStop()
    {
        // The re-check sees a zero remaining time: that must stop, not re-arm the timer for another zero wait.
        using AbortAtDeadlineExtension extension = CreateExtension(n => n == 1 ? StopAt - FirstDueTime : StopAt);

        await WaitForAsync(_stopRequested.Task);

        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TimerFiringEarly_ReArmsAndStopsOnceTheRecheckedInstantIsReached()
    {
        using AbortAtDeadlineExtension extension = CreateExtension(n => n switch
        {
            1 => StopAt - FirstDueTime,
            2 => StopAt - TimeSpan.FromMilliseconds(30),
            _ => StopAt + TimeSpan.FromSeconds(1),
        });

        await WaitForAsync(_stopRequested.Task);

        Assert.IsGreaterThanOrEqualTo(3, _clockCalls);
    }

    [TestMethod]
    public async Task TimerFiringFarBeforeTheStopInstant_ReArmsWithoutStopping()
    {
        TaskCompletionSource<bool> timerFired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using AbortAtDeadlineExtension extension = CreateExtension(n =>
        {
            if (n == 2)
            {
                timerFired.TrySetResult(true);
            }

            return n == 1 ? StopAt - FirstDueTime : StopAt - TimeSpan.FromMinutes(10);
        });

        await WaitForAsync(timerFired.Task);
        await Task.Delay(NegativeWindow, TestContext.CancellationToken);

        Assert.AreEqual(2, _clockCalls);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecutionCompletingWhileTheTimerIsRechecking_DoesNotReArmTheTimer()
    {
        AbortAtDeadlineExtension? extension = null;
        TaskCompletionSource<bool> timerFired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using AbortAtDeadlineExtension created = extension = CreateExtension(n =>
        {
            if (n == 2)
            {
                extension!.NotifyTestExecutionCompleted();
                timerFired.TrySetResult(true);
            }

            return n == 1 ? StopAt - FirstDueTime : StopAt - TimeSpan.FromMilliseconds(20);
        });

        await WaitForAsync(timerFired.Task);
        await Task.Delay(NegativeWindow, TestContext.CancellationToken);

        Assert.AreEqual(2, _clockCalls);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecutionCompletedBeforeTheDeadline_TheStopIsNeverRequested(bool completedByTheSessionFinishing)
    {
        TaskCompletionSource<bool> timerFired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using AbortAtDeadlineExtension extension = CreateExtension(n =>
        {
            if (n == 2)
            {
                timerFired.TrySetResult(true);
            }

            return n == 1 ? StopAt - FirstDueTime : StopAt + TimeSpan.FromSeconds(1);
        });

        if (completedByTheSessionFinishing)
        {
            await extension.OnTestSessionFinishingAsync(null!);
        }
        else
        {
            extension.NotifyTestExecutionCompleted();
        }

        await WaitForAsync(timerFired.Task);
        await Task.Delay(NegativeWindow, TestContext.CancellationToken);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecutionCompletingBetweenTheLockedCheckAndTheClaim_AbandonsTheStop()
    {
        // The completion flag is read three times on the way to a stop: before the lock, inside the lock and when
        // claiming. Completing the run while the second read is answered (still "not completed") must be caught by
        // the claim, which looks at the run state first.
        AbortAtDeadlineExtension? extension = null;
        TaskCompletionSource<bool> lockedCheckReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        _policiesService.SetupGet(x => x.IsTestExecutionCompleted).Returns(() =>
        {
            if (Interlocked.Increment(ref reads) == 2)
            {
                extension!.NotifyTestExecutionCompleted();
                lockedCheckReached.TrySetResult(true);
            }

            return false;
        });
        List<LogLevel> asyncLogs = [];
        using AbortAtDeadlineExtension created = extension = CreateExtension(
            n => n == 1 ? StopAt - FirstDueTime : StopAt + TimeSpan.FromSeconds(1),
            asyncLogs: asyncLogs);

        await WaitForAsync(lockedCheckReached.Task);

        // This takes the extension's lock, so the handler task is already published when it returns.
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        Assert.AreSequenceEqual([LogLevel.Debug], asyncLogs);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _policiesService.Verify(x => x.ExecuteDeadlineCallbacksAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecutionCompletingDuringTheFastPathCheck_NeverStartsTheHandler()
    {
        // The first read of the completion flag happens before the lock is taken. Completing the run at that
        // moment must be caught by the check made under the lock, so no handler (and no abandoned-stop log) exists.
        AbortAtDeadlineExtension? extension = null;
        TaskCompletionSource<bool> fastPathReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        _policiesService.SetupGet(x => x.IsTestExecutionCompleted).Returns(() =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                extension!.NotifyTestExecutionCompleted();
                fastPathReached.TrySetResult(true);
            }

            return false;
        });
        List<LogLevel> asyncLogs = [];
        using AbortAtDeadlineExtension created = extension = CreateExtension(
            n => n == 1 ? StopAt - FirstDueTime : StopAt + TimeSpan.FromSeconds(1),
            asyncLogs: asyncLogs);

        await WaitForAsync(fastPathReached.Task);
        await Task.Delay(NegativeWindow, TestContext.CancellationToken);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        Assert.IsEmpty(asyncLogs);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task WithoutTheGracefulStopCapability_NoTimerIsArmed()
    {
        using AbortAtDeadlineExtension extension = CreateExtension(n => n == 1 ? StopAt - FirstDueTime : StopAt + TimeSpan.FromSeconds(1), hasCapability: false);

        await Task.Delay(NegativeWindow, TestContext.CancellationToken);

        Assert.AreEqual(0, _clockCalls);
        Assert.IsFalse(await extension.IsEnabledAsync());
    }

    [TestMethod]
    public async Task Dispose_DisarmsTheTimer()
    {
        // Keep the timer due within the observation window so a missing Dispose remains observable.
        AbortAtDeadlineExtension extension = CreateExtension(n => n == 1 ? StopAt - DisposeDueTime : StopAt + TimeSpan.FromSeconds(1));
        extension.Dispose();

        await Task.Delay(DisposeObservationWindow, TestContext.CancellationToken);

        Assert.AreEqual(1, _clockCalls);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Dispose_WhileTheTimerCallbackIsInFlight_PreventsTheStop()
    {
        using ManualResetEventSlim release = new(false);
        TaskCompletionSource<bool> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AbortAtDeadlineExtension extension = CreateExtension(n =>
        {
            if (n == 1)
            {
                return StopAt - FirstDueTime;
            }

            callbackEntered.TrySetResult(true);
            release.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            return StopAt + TimeSpan.FromSeconds(1);
        });

        await WaitForAsync(callbackEntered.Task);
        extension.Dispose();
        release.Set();

        await Task.Delay(NegativeWindow, TestContext.CancellationToken);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_DisarmsTheTimer()
    {
        // Keep the timer due within the observation window so a missing Dispose remains observable.
        AbortAtDeadlineExtension extension = CreateExtension(n => n == 1 ? StopAt - DisposeDueTime : StopAt + TimeSpan.FromSeconds(1));
        await extension.DisposeAsync();

        await Task.Delay(DisposeObservationWindow, TestContext.CancellationToken);

        Assert.AreEqual(1, _clockCalls);
        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DisposeAsync_WhileTheTimerCallbackIsInFlight_PreventsTheStop()
    {
        using ManualResetEventSlim release = new(false);
        TaskCompletionSource<bool> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AbortAtDeadlineExtension extension = CreateExtension(n =>
        {
            if (n == 1)
            {
                return StopAt - FirstDueTime;
            }

            callbackEntered.TrySetResult(true);
            release.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            return StopAt + TimeSpan.FromSeconds(1);
        });

        await WaitForAsync(callbackEntered.Task);
        await extension.DisposeAsync();
        release.Set();

        await Task.Delay(NegativeWindow, TestContext.CancellationToken);
        await WaitForAsync(extension.WaitForDeadlineHandlingAsync());

        _capability.Verify(x => x.TryStopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
#endif

    [TestMethod]
    [DataRow("20", "20", true)]
    [DataRow("20", "10", false)]
    [DataRow("10", "20", true)]
    public async Task HangDumpMarginNotSmallerThanTheStopMargin_WarnsAtStartup(string stopMargin, string dumpMargin, bool expectWarning)
    {
        List<IOutputDeviceData> displayed = [];
        using AbortAtDeadlineExtension extension = CreateExtension(_ => StopAt - TimeSpan.FromHours(1), stopMargin, dumpMargin, isHangDumpEnabled: true, displayed: displayed);

        Assert.IsTrue(await extension.IsEnabledAsync());

        if (expectWarning)
        {
            Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(displayed));
        }
        else
        {
            Assert.IsEmpty(displayed);
        }
    }

    [TestMethod]
    [DataRow("20", "20", true)]
    [DataRow("20", "10", false)]
    public void ConstructionDiagnostics_LogTheConfigurationAndWarnOnlyWhenTheMarginsAreInverted(string stopMargin, string dumpMargin, bool inverted)
    {
        List<LogLevel> syncLogs = [];
        using AbortAtDeadlineExtension extension = CreateExtension(_ => StopAt - TimeSpan.FromHours(1), stopMargin, dumpMargin, isHangDumpEnabled: true, syncLogs: syncLogs);

        Assert.AreSequenceEqual(inverted ? [LogLevel.Information, LogLevel.Warning] : [LogLevel.Information], syncLogs);
    }

    private async Task WaitForAsync(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(task, completed, "Timed out waiting for the deadline handler.");
        await task;
    }

    private AbortAtDeadlineExtension CreateExtension(
        Func<int, DateTimeOffset> utcNow,
        string stopMargin = "0",
        string? dumpMargin = null,
        bool hasCapability = true,
        bool isHangDumpEnabled = false,
        List<LogLevel>? syncLogs = null,
        List<LogLevel>? asyncLogs = null,
        List<IOutputDeviceData>? displayed = null)
    {
        // The deadline is StopAt itself when the margin is zero; otherwise it is moved so the stop instant stays StopAt.
        var margin = TimeSpan.FromSeconds(double.Parse(stopMargin, CultureInfo.InvariantCulture));
        Mock<IEnvironment> environment = new();
        _ = environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE))
            .Returns((StopAt + margin).ToString("o", CultureInfo.InvariantCulture));
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN))
            .Returns(stopMargin);
        _ = environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN))
            .Returns(dumpMargin);

        Mock<IClock> clock = new();
        _ = clock.SetupGet(x => x.UtcNow).Returns(() => utcNow(Interlocked.Increment(ref _clockCalls)));

        Mock<ILogger> logger = new();
        _ = logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns((LogLevel logLevel, string _, Exception? _, Func<string, Exception?, string> _) =>
            {
                asyncLogs?.Add(logLevel);
                return Task.CompletedTask;
            });
        _ = logger
            .Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback((LogLevel logLevel, string _, Exception? _, Func<string, Exception?, string> _) => syncLogs?.Add(logLevel));

        Mock<ILoggerFactory> loggerFactory = new();
        _ = loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        Mock<IOutputDevice> outputDevice = new();
        _ = outputDevice
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns((IOutputDeviceDataProducer _, IOutputDeviceData data, CancellationToken _) =>
            {
                displayed?.Add(data);
                return Task.CompletedTask;
            });

        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        _ = cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        return new AbortAtDeadlineExtension(
            environment.Object,
            clock.Object,
            hasCapability ? _capability.Object : null,
            _policiesService.Object,
            cancellationTokenSource.Object,
            outputDevice.Object,
            loggerFactory.Object,
            reportTimeout: null,
            isHangDumpEnabled);
    }
}

#pragma warning restore TPEXP

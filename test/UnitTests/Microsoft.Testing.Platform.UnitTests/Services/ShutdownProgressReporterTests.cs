// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ShutdownProgressReporterTests : IAsyncDisposable
{
    private static readonly TimeSpan TestWaitTimeout = TimeSpan.FromSeconds(30);

    private readonly Mock<ITestApplicationCancellationTokenSource> _cancellationTokenSource = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly CapturingOutputDevice _outputDevice = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<ITask> _task = new();
    private readonly DateTimeOffset _now = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private TaskCompletionSource<PendingDelay> _nextDelay = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _watchdogTask = Task.CompletedTask;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);
        _task.Setup(task => task.Run(It.IsAny<Func<Task>>(), CancellationToken.None))
            .Returns<Func<Task>, CancellationToken>((run, _) => _watchdogTask = run());
        _task.Setup(task => task.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(DelayAsync);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _watchdogTask.WaitAsync(TestWaitTimeout, CancellationToken.None);
        }
        finally
        {
            _cts.Dispose();
        }
    }

    [TestMethod]
    public void Track_AddsEntry_Snapshot_ReturnsIt()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        IReadOnlyList<ShutdownProgressReporter.TrackedWork> snapshot = reporter.Snapshot();
        Assert.HasCount(1, snapshot);
        Assert.AreEqual("uid-1", snapshot[0].Uid);
        Assert.AreEqual("Display 1", snapshot[0].DisplayName);
        Assert.AreEqual("Phase 1", snapshot[0].Phase);

        handle.Dispose();
        Assert.IsEmpty(reporter.Snapshot());
    }

    [TestMethod]
    public void Track_Dispose_IsIdempotent()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        handle.Dispose();
        handle.Dispose();

        Assert.IsEmpty(reporter.Snapshot());
    }

    [TestMethod]
    public void Watchdog_DoesNotEmit_BeforeCancellation()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        using IDisposable tracker = reporter.Track("uid-1", "Display 1", "Phase 1");

        Assert.IsEmpty(_outputDevice.Messages);
        _task.Verify(task => task.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        _task.Verify(task => task.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Watchdog_EmitsAfterQuietWindow_WhenStillTracking()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        using IDisposable tracker = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();
        Assert.IsEmpty(_outputDevice.Messages);

        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);

        IReadOnlyList<string> messages = _outputDevice.Messages;
        Assert.HasCount(1, messages);
        Assert.Contains("Display 1", messages[0]);
        Assert.Contains("Phase 1", messages[0]);

        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);
        Assert.HasCount(2, _outputDevice.Messages);
    }

    [TestMethod]
    public async Task Watchdog_DoesNotEmit_IfAllTrackersDisposedBeforeQuietWindow()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();
        handle.Dispose();

        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);

        Assert.IsEmpty(_outputDevice.Messages);
    }

    [TestMethod]
    public async Task Watchdog_StopsEmitting_OnceAllTrackersDisposed()
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);
        Assert.HasCount(1, _outputDevice.Messages);

        handle.Dispose();

        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);

        Assert.HasCount(1, _outputDevice.Messages);
    }

    [TestMethod]
    public void Track_AfterDispose_ReturnsNoop()
    {
        ShutdownProgressReporter reporter = CreateReporter();
        reporter.Dispose();

        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");
        handle.Dispose();

        Assert.IsEmpty(reporter.Snapshot());
    }

    [TestMethod]
    public async Task Watchdog_EmitsForLateTracking_WhenQuietWindowElapsedWithNoWork()
    {
        using ShutdownProgressReporter reporter = CreateReporter();

        CancelTokenSource();

        // Reaching the poll delay proves that the watchdog processed an empty snapshot.
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);
        Assert.IsEmpty(_outputDevice.Messages);

        using IDisposable tracker = reporter.Track("uid-late", "Display Late", "Phase Late");

        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);

        IReadOnlyList<string> messages = _outputDevice.Messages;
        Assert.HasCount(1, messages);
        Assert.Contains("Display Late", messages[0]);
        Assert.Contains("Phase Late", messages[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Watchdog_Dispose_StopsPolling(bool quietWindowElapsed)
    {
        using ShutdownProgressReporter reporter = CreateReporter();
        CancelTokenSource();
        await WaitForDelayAsync();

        if (quietWindowElapsed)
        {
            await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);
        }

        reporter.Dispose();
        await _watchdogTask.WaitAsync(TestWaitTimeout, TestContext.CancellationToken);

        Assert.AreEqual(TaskStatus.RanToCompletion, _watchdogTask.Status);
        Assert.IsEmpty(_outputDevice.Messages);
        _task.Verify(
            task => task.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(quietWindowElapsed ? 2 : 1));
    }

    private ShutdownProgressReporter CreateReporter()
        => new(
            _cancellationTokenSource.Object,
            _outputDevice,
            new NullLoggerFactory(),
            _clock.Object,
            ShutdownProgressReporter.DefaultQuietWindow,
            ShutdownProgressReporter.DefaultPollInterval,
            _task.Object);

#pragma warning disable VSTHRD103 // Call async methods when in an async method - CancellationTokenSource.CancelAsync is unavailable on net462.
    private void CancelTokenSource() => _cts.Cancel();
#pragma warning restore VSTHRD103

    private async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        _nextDelay.SetResult(new PendingDelay(duration, completion));
        await completion.Task.ConfigureAwait(false);
    }

    private async Task AdvanceDelayAsync(TimeSpan expectedDuration)
    {
        PendingDelay delay = await WaitForDelayAsync();
        Assert.AreEqual(expectedDuration, delay.Duration);
        _nextDelay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        delay.Completion.SetResult(true);
        await WaitForDelayAsync();
    }

    private async Task<PendingDelay> WaitForDelayAsync()
    {
        Task completed = await Task.WhenAny(_nextDelay.Task, _watchdogTask)
            .WaitAsync(TestWaitTimeout, TestContext.CancellationToken);
        Assert.AreSame(_nextDelay.Task, completed, "The watchdog exited before requesting the next delay.");
        return await _nextDelay.Task;
    }

    private sealed record PendingDelay(TimeSpan Duration, TaskCompletionSource<bool> Completion);

    private sealed class CapturingOutputDevice : IOutputDevice
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToArray();
                }
            }
        }

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            string? text = data switch
            {
                WarningMessageOutputDeviceData w => w.Message,
                FormattedTextOutputDeviceData f => f.Text,
                _ => data.ToString(),
            };

            lock (_messages)
            {
                _messages.Add(text ?? string.Empty);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NullLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Task.CompletedTask;
    }

    [TestMethod]
    public async Task Watchdog_MultipleEntriesUseNewestFirstOrderAndIndependentRoundedDurations()
    {
        _clock.SetupSequence(c => c.UtcNow).Returns(_now).Returns(_now.AddSeconds(7)).Returns(_now.AddMilliseconds(10_500));
        using ShutdownProgressReporter reporter = CreateReporter();
        using IDisposable first = reporter.Track("first", "First extension", "flush");
        using IDisposable second = reporter.Track("second", "Second extension", "close");

        CancelTokenSource();
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);

        Assert.AreEqual(
            "Still waiting for: Second extension (close, 4s); First extension (flush, 10s)",
            Assert.ContainsSingle(_outputDevice.Messages));
        Assert.AreSequenceEqual(["second", "first"], reporter.Snapshot().Select(work => work.Uid));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Watchdog_LoggerAndOutputFailuresDoNotStopPolling(bool cancellationFailure)
    {
        Mock<ILogger> logger = new();
        logger.Setup(l => l.LogAsync(LogLevel.Information, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .ThrowsAsync(new IOException("logging failed"));
        Mock<ILoggerFactory> factory = new();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        Mock<IOutputDevice> output = new();
        List<string> messages = [];
        output.Setup(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((producer, data, _) =>
            {
                Assert.AreEqual("ShutdownProgressReporter", producer.Uid);
                Assert.AreEqual("ShutdownProgressReporter", producer.DisplayName);
                Assert.AreEqual("Reports extensions still running during shutdown.", producer.Description);
                messages.Add(Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data).Message);
            })
            .Returns(cancellationFailure ? Task.FromException(new OperationCanceledException()) : Task.FromException(new IOException("output failed")));
        using ShutdownProgressReporter reporter = new(
            _cancellationTokenSource.Object, output.Object, factory.Object, _clock.Object,
            ShutdownProgressReporter.DefaultQuietWindow, ShutdownProgressReporter.DefaultPollInterval, _task.Object);
        using IDisposable tracking = reporter.Track("uid", "Extension", "finalizing");
        Assert.IsTrue(await reporter.IsEnabledAsync());
        Assert.AreEqual(PlatformTestMetadata.Version, reporter.Version);

        CancelTokenSource();
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultQuietWindow);
        await AdvanceDelayAsync(ShutdownProgressReporter.DefaultPollInterval);

        Assert.AreSequenceEqual(["Still waiting for: Extension (finalizing, 1s)", "Still waiting for: Extension (finalizing, 1s)"], messages);
        logger.Verify(l => l.LogAsync(LogLevel.Information, "Still waiting for: Extension (finalizing, 1s)", null, LoggingExtensions.Formatter), Times.Exactly(2));
    }

    [TestMethod]
    public async Task Watchdog_DelayFailureAndThrowingWarningLoggerAreBestEffort()
    {
        IOException expected = new("quiet-window failure");
        _task.Setup(t => t.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).Returns(Task.FromException(expected));
        Mock<ILogger> logger = new();
        logger.Setup(l => l.LogAsync(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .ThrowsAsync(new InvalidOperationException("warning sink failed"));
        Mock<ILoggerFactory> factory = new();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        using ShutdownProgressReporter reporter = new(
            _cancellationTokenSource.Object, _outputDevice, factory.Object, _clock.Object,
            ShutdownProgressReporter.DefaultQuietWindow, ShutdownProgressReporter.DefaultPollInterval, _task.Object);

        CancelTokenSource();
        await _watchdogTask;

        Assert.AreEqual(TaskStatus.RanToCompletion, _watchdogTask.Status);
        Assert.IsEmpty(_outputDevice.Messages);
        logger.Verify(
            l => l.LogAsync(LogLevel.Warning, It.Is<string>(message => message.StartsWith("Shutdown progress watchdog failed: System.IO.IOException: quiet-window failure", StringComparison.Ordinal)), null, LoggingExtensions.Formatter),
            Times.Once);
    }
}

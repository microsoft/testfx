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
public sealed class ShutdownProgressReporterTests : IDisposable
{
    private readonly Mock<ITestApplicationCancellationTokenSource> _cancellationTokenSource = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly CapturingOutputDevice _outputDevice = new();
    private readonly Mock<IClock> _clock = new();
    private readonly DateTimeOffset _now = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);
    }

    public void Dispose() => _cts.Dispose();

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
    public async Task Watchdog_DoesNotEmit_BeforeCancellation()
    {
        using ShutdownProgressReporter reporter = CreateReporter(quietWindow: TimeSpan.FromMilliseconds(20), pollInterval: TimeSpan.FromMilliseconds(20));
        using IDisposable tracker = reporter.Track("uid-1", "Display 1", "Phase 1");

        await Task.Delay(120, TestContext.CancellationToken);

        Assert.IsEmpty(_outputDevice.Messages);
    }

    [TestMethod]
    public async Task Watchdog_EmitsAfterQuietWindow_WhenStillTracking()
    {
        using ShutdownProgressReporter reporter = CreateReporter(quietWindow: TimeSpan.FromMilliseconds(40), pollInterval: TimeSpan.FromMilliseconds(30));
        using IDisposable tracker = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();

        await WaitForMessageAsync(TimeSpan.FromSeconds(5));

        IReadOnlyList<string> messages = _outputDevice.Messages;
        Assert.IsGreaterThanOrEqualTo(1, messages.Count, $"Expected at least one message, got {messages.Count}");
        Assert.Contains("Display 1", messages[0]);
        Assert.Contains("Phase 1", messages[0]);
    }

    [TestMethod]
    public async Task Watchdog_DoesNotEmit_IfAllTrackersDisposedBeforeQuietWindow()
    {
        using ShutdownProgressReporter reporter = CreateReporter(quietWindow: TimeSpan.FromMilliseconds(100), pollInterval: TimeSpan.FromMilliseconds(30));
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();
        handle.Dispose();

        await Task.Delay(300, TestContext.CancellationToken);

        Assert.IsEmpty(_outputDevice.Messages);
    }

    [TestMethod]
    public async Task Watchdog_StopsEmitting_OnceAllTrackersDisposed()
    {
        using ShutdownProgressReporter reporter = CreateReporter(quietWindow: TimeSpan.FromMilliseconds(20), pollInterval: TimeSpan.FromMilliseconds(20));
        IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        CancelTokenSource();
        await WaitForMessageAsync(TimeSpan.FromSeconds(5));

        handle.Dispose();
        int countAfterDispose = _outputDevice.Messages.Count;

        await Task.Delay(200, TestContext.CancellationToken);

        // Allow at most one extra in-flight emission that was already running when we disposed.
        Assert.IsLessThanOrEqualTo(countAfterDispose + 1, _outputDevice.Messages.Count);
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

    private ShutdownProgressReporter CreateReporter(TimeSpan? quietWindow = null, TimeSpan? pollInterval = null)
        => new(
            _cancellationTokenSource.Object,
            _outputDevice,
            new NullLoggerFactory(),
            _clock.Object,
            quietWindow ?? ShutdownProgressReporter.DefaultQuietWindow,
            pollInterval ?? ShutdownProgressReporter.DefaultPollInterval);

#pragma warning disable VSTHRD103 // Call async methods when in an async method - CancellationTokenSource.CancelAsync is unavailable on net462.
    private void CancelTokenSource() => _cts.Cancel();
#pragma warning restore VSTHRD103

    private async Task WaitForMessageAsync(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && _outputDevice.Messages.Count == 0)
        {
            await Task.Delay(20, TestContext.CancellationToken);
        }
    }

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
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Default <see cref="IShutdownProgressReporter"/>. Once the test-application
/// cancellation token is signalled, a single background watchdog periodically
/// reports the set of extensions/consumers that have not yet completed.
/// </summary>
internal sealed class ShutdownProgressReporter : IShutdownProgressReporter, IOutputDeviceDataProducer, IDisposable
{
    internal static readonly TimeSpan DefaultQuietWindow = TimeSpan.FromSeconds(3);
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<long, TrackedWork> _inFlight = new();
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly IOutputDevice? _outputDevice;
    private readonly ILogger _logger;
    private readonly IClock _clock;
    private readonly TimeSpan _quietWindow;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenRegistration _registration;
    private readonly CancellationTokenSource _watchdogStopSource = new();
    private long _nextId;
    private int _watchdogStarted;
    private bool _disposed;

    public ShutdownProgressReporter(
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        IOutputDevice? outputDevice,
        ILoggerFactory loggerFactory,
        IClock clock)
        : this(testApplicationCancellationTokenSource, outputDevice, loggerFactory, clock, DefaultQuietWindow, DefaultPollInterval)
    {
    }

    internal ShutdownProgressReporter(
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        IOutputDevice? outputDevice,
        ILoggerFactory loggerFactory,
        IClock clock,
        TimeSpan quietWindow,
        TimeSpan pollInterval)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger<ShutdownProgressReporter>();
        _clock = clock;
        _quietWindow = quietWindow;
        _pollInterval = pollInterval;
        _registration = _testApplicationCancellationTokenSource.CancellationToken.Register(OnCancellationRequested);
    }

    public string Uid => nameof(ShutdownProgressReporter);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(ShutdownProgressReporter);

    public string Description => "Reports extensions still running during shutdown.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IDisposable Track(string uid, string displayName, string phase)
    {
        if (_disposed)
        {
            return NoopDisposable.Instance;
        }

        long id = Interlocked.Increment(ref _nextId);
        var work = new TrackedWork(uid, displayName, phase, _clock.UtcNow);
        _inFlight[id] = work;
        return new Releaser(this, id);
    }

    internal IReadOnlyList<TrackedWork> Snapshot()
        => _inFlight.Values.OrderByDescending(w => w.StartedAt).ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration.Dispose();
        try
        {
            _watchdogStopSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _watchdogStopSource.Dispose();
    }

    private void OnCancellationRequested()
    {
        if (Interlocked.CompareExchange(ref _watchdogStarted, 1, 0) != 0)
        {
            return;
        }

        // Fire-and-forget watchdog. The process is shutting down, so we accept the unobserved task.
        _ = Task.Run(() => RunWatchdogAsync(_watchdogStopSource.Token));
    }

    private async Task RunWatchdogAsync(CancellationToken stopToken)
    {
        try
        {
            // Quiet window: give extensions a chance to drain before reporting anything.
            await Task.Delay(_quietWindow, stopToken).ConfigureAwait(false);

            while (!stopToken.IsCancellationRequested)
            {
                IReadOnlyList<TrackedWork> snapshot = Snapshot();
                if (snapshot.Count == 0)
                {
                    return;
                }

                await ReportAsync(snapshot).ConfigureAwait(false);
                await Task.Delay(_pollInterval, stopToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose / process exit.
        }
        catch (Exception ex)
        {
            // Never let the watchdog crash the process; just log if possible.
            try
            {
                await _logger.LogWarningAsync($"Shutdown progress watchdog failed: {ex}").ConfigureAwait(false);
            }
            catch
            {
                // Swallow - we are during shutdown.
            }
        }
    }

    private async Task ReportAsync(IReadOnlyList<TrackedWork> snapshot)
    {
        DateTimeOffset now = _clock.UtcNow;
        StringBuilder builder = new();
        builder.Append(PlatformResources.ShutdownProgressStillWaitingPrefix);
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            TrackedWork work = snapshot[i];
            int elapsedSeconds = Math.Max(1, (int)Math.Round((now - work.StartedAt).TotalSeconds));
            builder.Append(CultureInfo.CurrentCulture, $"{work.DisplayName} ({work.Phase}, {elapsedSeconds}s)");
        }

        string message = builder.ToString();

        try
        {
            await _logger.LogInformationAsync(message).ConfigureAwait(false);
        }
        catch
        {
            // Logging during shutdown is best-effort.
        }

        if (_outputDevice is not null)
        {
            try
            {
                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(message), _watchdogStopSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose.
            }
            catch
            {
                // Output during shutdown is best-effort.
            }
        }
    }

    internal readonly record struct TrackedWork(string Uid, string DisplayName, string Phase, DateTimeOffset StartedAt);

    private sealed class Releaser(ShutdownProgressReporter owner, long id) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner._inFlight.TryRemove(id, out _);
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

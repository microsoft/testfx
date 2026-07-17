// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal that updates the progress in place when progress reporting is enabled.
/// </summary>
[UnsupportedOSPlatform("browser")]
[Embedded]
internal sealed partial class TestProgressStateAwareTerminal : IDisposable
{
    internal const int MaximumVisibleProgressMessages = 5;

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly ITerminal _terminal;
    private readonly Func<bool?> _showProgress;
    private readonly IProgressRenderer _renderer;
    private readonly Dictionary<ProgressMessageIdentity, TerminalProgressMessageState> _progressMessages = [];
    private readonly ILogger _logger;

    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private CancellationTokenSource? _cts;
    private TestProgressState?[] _progressItems = [];
    private TerminalProgressMessageState[] _visibleProgressMessages = [];
    private bool? _showProgressCached;
    private int _progressErased;
    private long _messageId;
    private long _messageVersion;

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;
    private long _counter;
    private int _stopped;
    private int _disposed;

    public event EventHandler? OnProgressStartUpdate;

    public event EventHandler? OnProgressStopUpdate;

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            // The renderer chooses the cadence: cursor renderers want a responsive sub-second redraw,
            // while the silence-driven heartbeat only needs second-level granularity.
            int updateCadenceInMs = (int)_renderer.TickInterval.TotalMilliseconds;
            while (!cancellationTokenSource.Token.WaitHandle.WaitOne(updateCadenceInMs))
            {
                // Note: OnProgressStartUpdate is invoked outside the lock to avoid a deadlock where
                // a test subscriber blocks the event handler (e.g. with WaitOne) while the lock is held,
                // preventing other callers (e.g. WriteToTerminal) from acquiring the lock.
                OnProgressStartUpdate?.Invoke(this, EventArgs.Empty);
                lock (_lock)
                {
                    _terminal.StartUpdate();
                    try
                    {
                        UpdateVisibleProgressMessagesUnderLock();
                        _renderer.OnTick(_terminal, _progressItems, _visibleProgressMessages);
                    }
                    finally
                    {
                        _terminal.StopUpdate();
                        OnProgressStopUpdate?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // When we dispose the cancellation token source too early this will throw. This is an expected
            // shutdown race, so it stays silent.
        }
        catch (Exception ex)
        {
            // Swallow so that the unconditional EraseProgress() below still runs.
            // There is no other thread to surface this to; the test run itself should
            // still be allowed to complete. This is unexpected (not a known shutdown/disposal race), so
            // surface it at Debug for diagnosability without being noisy by default.
            QueueLogDebug($"Unexpected exception while rendering test progress; erasing progress and stopping the refresher thread: {ex}");
        }

        try
        {
            _terminal.EraseProgress();
            Interlocked.Exchange(ref _progressErased, 1);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or System.IO.IOException)
        {
            // Best-effort cleanup; we are already in teardown. These are expected disposal/shutdown races, so
            // they stay silent.
        }
        catch (Exception ex)
        {
            // Unexpected failure while erasing progress during teardown; surface it at Debug for diagnosability.
            QueueLogDebug($"Unexpected exception while erasing test progress during teardown: {ex}");
        }
    }

    private void QueueLogDebug(string message)
        => _ = Task.Run(() =>
        {
            try
            {
                _logger.LogDebug(message);
            }
            catch (Exception)
            {
                // Refresher-thread diagnostics must not escape as unhandled exceptions.
            }
        });

    public TestProgressStateAwareTerminal(ITerminal terminal, Func<bool?> showProgress, IProgressRenderer renderer)
        : this(terminal, showProgress, renderer, new NopLogger())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestProgressStateAwareTerminal"/> class with a logger for
    /// low-noise diagnostics of unexpected progress rendering/erase failures.
    /// </summary>
    public TestProgressStateAwareTerminal(ITerminal terminal, Func<bool?> showProgress, IProgressRenderer renderer, ILogger logger)
    {
        _terminal = terminal;
        _showProgress = showProgress;
        _renderer = renderer;
        _logger = logger;
    }

    public int AddWorker(TestProgressState testWorker)
    {
        if (GetShowProgress())
        {
            for (int i = 0; i < _progressItems.Length; i++)
            {
                if (_progressItems[i] == null)
                {
                    _progressItems[i] = testWorker;
                    return i;
                }
            }

            throw new InvalidOperationException("No empty slot found");
        }

        return 0;
    }

    public void StartShowingProgress(int workerCount)
    {
        Interlocked.Exchange(ref _stopped, 0);
        if (!GetShowProgress())
        {
            return;
        }

        _progressItems = new TestProgressState[workerCount];
        Interlocked.Exchange(ref _progressErased, 0);

        var cancellationTokenSource = new CancellationTokenSource();
        _cts = cancellationTokenSource;

        _terminal.StartBusyIndicator();
        _renderer.OnStart();
        // If we crash unexpectedly without completing this thread we don't want it to keep the process running.
        _refresher = new Thread(() => ThreadProc(cancellationTokenSource)) { IsBackground = true };
        _refresher.Start();
    }

    internal void StopShowingProgress()
    {
        if (Interlocked.CompareExchange(ref _stopped, 1, 0) != 0)
        {
            return;
        }

        if (!GetShowProgress())
        {
            ClearProgressMessages();
            return;
        }

        CancellationTokenSource? cancellationTokenSource = _cts;
        Thread? refresher = _refresher;
        _cts = null;
        _refresher = null;

        cancellationTokenSource?.Cancel();
        refresher?.Join();
        cancellationTokenSource?.Dispose();

        lock (_lock)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _progressErased, 1, 0) == 0)
                {
                    _terminal.EraseProgress();
                }

                _terminal.StopBusyIndicator();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or System.IO.IOException)
            {
                // Best-effort cleanup; we are already in teardown.
            }

            ClearProgressMessagesUnderLock();
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        // Ensure that even when callers forget to call StopShowingProgress (e.g. because the process
        // is being torn down by an unhandled exception), we still bring the refresher thread down,
        // erase any in-flight progress, restore the busy-indicator and show the cursor again so the
        // user's terminal is left in a sane state.
        StopShowingProgress();
    }

    internal void WriteToTerminal(Action<ITerminal> write)
    {
        if (GetShowProgress())
        {
            lock (_lock)
            {
                try
                {
                    _terminal.StartUpdate();
                    UpdateVisibleProgressMessagesUnderLock();
                    _renderer.OnWrite(_terminal, _progressItems, write, _visibleProgressMessages);
                }
                finally
                {
                    _terminal.StopUpdate();
                }
            }
        }
        else
        {
            lock (_lock)
            {
                try
                {
                    _terminal.StartUpdate();
                    write(_terminal);
                }
                finally
                {
                    _terminal.StopUpdate();
                }
            }
        }
    }

    internal void RemoveWorker(int slotIndex)
    {
        if (GetShowProgress())
        {
            _progressItems[slotIndex] = null;
        }
    }

    internal void UpdateProgressMessage(
        string executionId,
        string instanceId,
        string producerUid,
        string key,
        string? message)
    {
        var identity = new ProgressMessageIdentity(executionId, instanceId, producerUid, key);
        lock (_lock)
        {
            if (!GetShowProgress() || _renderer is SilenceDrivenHeartbeatRenderer)
            {
                if (message is null)
                {
                    _progressMessages.Remove(identity);
                }
                else
                {
                    bool hasExisting = _progressMessages.TryGetValue(identity, out TerminalProgressMessageState? existing);
                    if (!hasExisting || existing is null || existing.Text != message)
                    {
                        try
                        {
                            _terminal.StartUpdate();
                            _terminal.AppendLine(message);
                        }
                        finally
                        {
                            _terminal.StopUpdate();
                        }

                        long version = ++_messageVersion;
                        _progressMessages[identity] = existing is null
                            ? new TerminalProgressMessageState(--_messageId, version, message)
                            : new TerminalProgressMessageState(existing.Id, version, message);
                    }
                }

                return;
            }

            if (message is null)
            {
                _progressMessages.Remove(identity);
            }
            else if (_progressMessages.TryGetValue(identity, out TerminalProgressMessageState? existing))
            {
                _progressMessages[identity] = new TerminalProgressMessageState(existing.Id, ++_messageVersion, message);
            }
            else
            {
                _progressMessages.Add(identity, new TerminalProgressMessageState(--_messageId, ++_messageVersion, message));
            }

            UpdateVisibleProgressMessagesUnderLock();
        }
    }

    private void UpdateVisibleProgressMessagesUnderLock()
    {
        if (_progressMessages.Count == 0)
        {
            _visibleProgressMessages = [];
            return;
        }

        int activeWorkerCount = _progressItems.Count(static progress => progress is not null);
        int visibleCount = Math.Min(
            MaximumVisibleProgressMessages,
            Math.Max(0, _terminal.Height - 2 - activeWorkerCount));
        _visibleProgressMessages = _progressMessages.Values
            .OrderByDescending(static state => state.Version)
            .Take(visibleCount)
            .OrderBy(static state => state.Version)
            .ToArray();
    }

    private void ClearProgressMessages()
    {
        lock (_lock)
        {
            ClearProgressMessagesUnderLock();
        }
    }

    private void ClearProgressMessagesUnderLock()
    {
        _progressMessages.Clear();
        _visibleProgressMessages = [];
    }

    internal void UpdateWorker(int slotIndex)
    {
        if (GetShowProgress())
        {
            // We increase the counter to say that this version of data is newer than what we had before and
            // it should be completely re-rendered. Another approach would be to use timestamps, or to replace the
            // instance and compare that, but that means more objects floating around.
            _counter++;

            TestProgressState? progress = _progressItems[slotIndex];
            progress?.Version = _counter;
        }
    }

    internal void NotifyTestCompleted()
    {
        if (GetShowProgress())
        {
            _renderer.OnTestCompleted();
        }
    }

    private bool GetShowProgress()
    {
        if (_showProgressCached != null)
        {
            return _showProgressCached.Value;
        }

        // Get the value from the func until we get the first non-null value.
        bool? showProgress = _showProgress();
        if (showProgress != null)
        {
            _showProgressCached = showProgress;
        }

        return showProgress == true;
    }

    private readonly record struct ProgressMessageIdentity(
        string ExecutionId,
        string InstanceId,
        string ProducerUid,
        string Key);
}

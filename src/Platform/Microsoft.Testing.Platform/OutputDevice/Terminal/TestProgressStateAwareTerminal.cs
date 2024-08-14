// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal that updates the progress in place when progress reporting is enabled.
/// </summary>
internal sealed partial class TestProgressStateAwareTerminal : IDisposable
{
    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly object _lock = new();

    private readonly ITerminal _terminal;
    private readonly Func<bool?> _showProgress;
    private readonly int _updateEvery;
    private TestProgressState?[] _progressItems = Array.Empty<TestProgressState>();
    private bool? _showProgressCached;

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        try
        {
            while (!_cts.Token.WaitHandle.WaitOne(_updateEvery))
            {
                lock (_lock)
                {
                    _terminal.RenderProgress(_progressItems);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // When we dispose _cts too early this will throw.
        }

        _terminal.EraseProgress();
    }

    public TestProgressStateAwareTerminal(ITerminal terminal, Func<bool?> showProgress, int updateEvery)
    {
        _terminal = terminal;
        _showProgress = showProgress;
        _updateEvery = updateEvery;
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
        if (GetShowProgress())
        {
            _progressItems = new TestProgressState[workerCount];
            _terminal.StartBusyIndicator();
            // If we crash unexpectedly without completing this thread we don't want it to keep the process running.
            _refresher = new Thread(ThreadProc) { IsBackground = true };
            _refresher.Start();
        }
    }

    internal void StopShowingProgress()
    {
        if (GetShowProgress())
        {
            _cts.Cancel();
            _refresher?.Join();

            _terminal.EraseProgress();
            _terminal.StopBusyIndicator();
        }
    }

    public void Dispose() =>
        ((IDisposable)_cts).Dispose();

    internal void WriteToTerminal(Action<ITerminal> write)
    {
        if (GetShowProgress())
        {
            lock (_lock)
            {
                _terminal.EraseProgress();
                _terminal.StartUpdate();
                write(_terminal);
                _terminal.StopUpdate();
            }
        }
        else
        {
            _terminal.StartUpdate();
            write(_terminal);
            _terminal.StopUpdate();
        }
    }

    internal void RemoveWorker(int slotIndex)
    {
        if (GetShowProgress())
        {
            _progressItems[slotIndex] = null;
        }
    }

    internal void UpdateWorker(int slotIndex, TestProgressState update)
    {
        if (GetShowProgress())
        {
            _progressItems[slotIndex] = update;
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
}

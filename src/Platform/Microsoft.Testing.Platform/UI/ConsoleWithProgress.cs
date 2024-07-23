// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal partial class ConsoleWithProgress : IDisposable
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
    private readonly bool _showProgress;
    private readonly int _updateEvery;
    private TestWorker?[] _workers = Array.Empty<TestWorker>();

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
                    _terminal.RenderProgress(_workers);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // When we dispose _cts too early this will throw.
        }

        _terminal.EraseProgress();
    }

    public ConsoleWithProgress(ITerminal terminal, bool showProgress, int updateEvery)
    {
        _terminal = terminal;
        _showProgress = showProgress;
        _updateEvery = updateEvery;
    }

    public int AddWorker(TestWorker testWorker)
    {
        for (int i = 0; i < _workers.Length; i++)
        {
            if (_workers[i] == null)
            {
                _workers[i] = testWorker;
                return i;
            }
        }

        throw new InvalidOperationException("No empty slot found");
    }

    public void StartShowingProgress(int workerCount)
    {
        if (_showProgress)
        {
            _workers = new TestWorker[workerCount];
            _terminal.StartBusyIndicator();
            _refresher = new Thread(ThreadProc);
            _refresher.Start();
        }
    }

    internal void StopShowingProgress()
    {
        if (_showProgress)
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
        if (_showProgress)
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

    internal void RemoveProgress(int slotIndex)
    {
        if (_showProgress)
        {
            _workers[slotIndex] = null;
        }
    }

    internal void UpdateProgress(int slotIndex, TestWorker update)
    {
        if (_showProgress)
        {
            _workers[slotIndex] = update;
        }
    }
}

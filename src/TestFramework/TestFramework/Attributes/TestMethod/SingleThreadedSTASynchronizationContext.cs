// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class SingleThreadedSTASynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _threadCompleteSemaphore = new ManualResetEventSlim(initialState: false);

    public SingleThreadedSTASynchronizationContext()
    {
#if !NETFRAMEWORK
        if (!OperatingSystem.IsWindows())
        {
            throw new NotSupportedException("SingleThreadedSTASynchronizationContext is only supported on Windows.");
        }
#endif

        _thread = new Thread(() =>
        {
            SetSynchronizationContext(this);
            foreach (Action callback in _queue.GetConsumingEnumerable())
            {
                callback();
            }

            _threadCompleteSemaphore.Set();
        })
        {
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state)
        => _queue.Add(() => d(state));

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            d(state);
        }
        else
        {
            using var done = new ManualResetEventSlim();
            _queue.Add(() =>
            {
                try
                {
                    d(state);
                }
                finally
                {
                    done.Set();
                }
            });
            done.Wait();
        }
    }

    public void Complete() => _queue.CompleteAdding();

    public void Dispose()
    {
        // Ensure we are disposing the BlockingCollection after the thread has finished.
        // This avoids a race condition where we could dispose the queue while the thread is still in GetConsumingEnumerable.
        // The complete flow is:
        // 1. We call CompleteAdding
        // 2. We call Dispose
        // 3. Dispose waits thread completion.
        // 4. GetConsumingEnumerable exits.
        // 5. Thread completion is signaled
        // 6. Dispose is unblocked, and disposes the queue.
        _threadCompleteSemaphore.Wait();
        _threadCompleteSemaphore.Dispose();
        _queue.Dispose();
    }
}

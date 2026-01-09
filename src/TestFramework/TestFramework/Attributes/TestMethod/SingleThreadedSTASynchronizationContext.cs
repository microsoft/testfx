// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class SingleThreadedSTASynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

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
}

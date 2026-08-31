// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Messages;

[Embedded]
internal sealed class SingleConsumerUnboundedChannel<T>
{
    // On .NET Framework, these are not cached internally.
    private static readonly Task<bool> TrueTask = Task.FromResult(true);
    private static readonly Task<bool> FalseTask = Task.FromResult(false);

    // Items published to the channel are stored in this concurrent queue.
    private readonly ConcurrentQueue<T> _items = [];

    // When WaitToReadAsync is called while we don't have any items, we create this TCS
    // and return its task.
    // Later, when something is published, we complete this task with value true.
    // If Complete is called instead, we complete this task with value false.
    private TaskCompletionSource<bool>? _waitingReader;

    private CancellationTokenRegistration? _cancellationRegistration;

    // A flag indicating whether or not complete has been called.
    private bool _completed;

    // It's safe to lock on _items rather than a dedicated lock object.
    // It's a private field and we own it.
    // This avoids allocating additional object that adds no value.
    private object SyncObj => _items;

    public void Write(T item)
    {
        lock (SyncObj)
        {
            if (_completed)
            {
                throw new InvalidOperationException("Channel is already completed.");
            }

            _items.Enqueue(item);

            // Wake up a consumer that is blocked inside the synchronous WaitToRead.
            // This is a no-op when nobody is waiting on the monitor (e.g. consumers using WaitToReadAsync).
            Monitor.Pulse(SyncObj);

            // If WaitToReadAsync was called previously, we want to complete the task it returned.
            // We complete it with value true because we have an item that can be read now.
            if (_waitingReader is { } waitingReader)
            {
                _waitingReader = null;
                _cancellationRegistration?.Dispose();
                _cancellationRegistration = null;
                waitingReader.TrySetResult(true);
            }
        }
    }

    public bool TryRead(out T item)
        => _items.TryDequeue(out item);

    /// <summary>
    /// Synchronously blocks the calling thread until an item is available to read or the channel is completed.
    /// Returns <see langword="true"/> when there may be items to read, or <see langword="false"/> when the channel
    /// is completed and empty.
    /// </summary>
    /// <remarks>
    /// This is intended for a single dedicated consumer that drains the channel synchronously and therefore does not
    /// use <see cref="WaitToReadAsync"/>. Because the loop never yields back to the thread pool, it cannot be starved
    /// (e.g. during process shutdown under heavy thread-pool contention).
    /// </remarks>
    public bool WaitToRead()
    {
        lock (SyncObj)
        {
            while (_items.IsEmpty && !_completed)
            {
                Monitor.Wait(SyncObj);
            }

            return !_items.IsEmpty;
        }
    }

    public Task<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        // We have something already in the channel.
        // We return true.
        if (!_items.IsEmpty)
        {
            return TrueTask;
        }

        lock (SyncObj)
        {
            // Re-check again under the lock.
            if (!_items.IsEmpty)
            {
                return TrueTask;
            }

            // It's completed and empty. Nothing more to read.
            if (_completed)
            {
                return FalseTask;
            }

            if (_waitingReader is not null)
            {
                // Two calls happened to WaitToReadAsync without anything being written in between.
                // This indicates multiple consumers (or single consumer calling this twice without await), which should never happen.
                throw new UnreachableException();
            }

            // Without RunContinuationsAsynchronously, task continuation will run inline inside of the SetResult call.
            // This task completes under a lock, and we don't want to run arbitrary code while holding the lock.
            // NOTE: For the net8.0+ implementation where we use .NET channels, we create the channel with
            // AllowSynchronousContinuations = false
            // This achieves the same effect.
            _waitingReader = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellationRegistration = cancellationToken.Register(tcs => ((TaskCompletionSource<bool>)tcs!).TrySetCanceled(cancellationToken), _waitingReader);
            return _waitingReader.Task;
        }
    }

    public void Complete()
    {
        lock (SyncObj)
        {
            _completed = true;

            // Wake up a consumer that is blocked inside the synchronous WaitToRead so it can observe completion.
            // This is a no-op when nobody is waiting on the monitor (e.g. consumers using WaitToReadAsync).
            Monitor.Pulse(SyncObj);

            // If there was previously a call to WaitToReadAsync, and we had no items in the queue, and we are completing now.
            // Then there is nothing to read. So we set the task value to false.
            if (_waitingReader is { } waitingReader)
            {
                _waitingReader = null;
                _cancellationRegistration?.Dispose();
                _cancellationRegistration = null;
                waitingReader.TrySetResult(false);
            }
        }
    }
}
#endif

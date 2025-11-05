// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Messages;

internal sealed class SingleConsumerUnboundedChannel<T>
{
    // Items published to the channel are stored in this concurrent queue.
    private readonly ConcurrentQueue<T> _items = [];

    // When ReadAsync is called while we don't have any items, we create a TCS and return it.
    // Later on, when something is published, we dequeue and set the result on that TCS.
    private readonly Queue<TaskCompletionSource<T>> _readers = [];

    // When WaitToReadAsync is called while we don't have any items, we return the
    // task associated with this TCS.
    // Later, when something is published, we complete this task with value true.
    // If Complete is called instead, we complete this task with value false.
    private TaskCompletionSource<bool>? _waitingReaders;

    // A flag indicating whether or not complete has been called.
    private bool _completed;

    // It's safe to lock on _items rather than a dedicated lock object.
    // It's a private field and we own it.
    // There is no need to extra an extra object for no value.
    private object SyncObj => _items;

    public Task WriteAsync(T item)
    {
        lock (SyncObj)
        {
            if (_completed)
            {
                return Task.FromException(new InvalidOperationException("Channel is already completed."));
            }

            // Already have a pending reader. Complete it.
            if (_readers.Count > 0)
            {
                _readers.Dequeue().SetResult(item);
            }
            else
            {
                // No pending readers. We add to the ConcurrentQueue.
                _items.Enqueue(item);

                // If WaitToReadAsync was called previously, we want to complete the task it returned.
                // We complete it with value true because we have an item that can be read now.
                TaskCompletionSource<bool>? waitingReaders = _waitingReaders;
                _waitingReaders = null;
                waitingReaders?.SetResult(true);
            }
        }

        return Task.CompletedTask;
    }

    public bool TryRead([MaybeNullWhen(false)] out T item)
        => _items.TryDequeue(out item);

    public Task<T> ReadAsync()
    {
        // If we have something in the queue, we can just return it immediately.
        if (_items.TryDequeue(out T? item))
        {
            return Task.FromResult(item);
        }

        lock (SyncObj)
        {
            // We still need to check this again under the lock.
            if (_items.TryDequeue(out item))
            {
                return Task.FromResult(item);
            }

            // The channel is empty and is completed already.
            if (_completed)
            {
                return Task.FromException<T>(new InvalidOperationException());
            }

            // We return a task that we will complete the next time something is written to the channel.
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readers.Enqueue(tcs);
            return tcs.Task;
        }
    }

    public Task<bool> WaitToReadAsync()
    {
        // We have something already in the channel.
        // We return true.
        if (!_items.IsEmpty)
        {
            return Task.FromResult(true);
        }

        lock (SyncObj)
        {
            // Re-check again under the lock.
            if (!_items.IsEmpty)
            {
                return Task.FromResult(true);
            }

            // It's completed.
            if (_completed)
            {
                return Task.FromResult(false);
            }

            _waitingReaders ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _waitingReaders.Task;
        }
    }

    public void Complete()
    {
        lock (SyncObj)
        {
            _completed = true;

            while (_readers.Count > 0)
            {
                _readers.Dequeue().SetException(new InvalidOperationException());
            }

            // If there was previously a call to WaitToReadAsync, and we had no items in the queue, and we are completing now.
            // Then there is nothing to read. So we set the task value to false.
            TaskCompletionSource<bool>? waitingReaders = _waitingReaders;
            _waitingReaders = null;
            waitingReaders?.SetResult(false);
        }
    }
}

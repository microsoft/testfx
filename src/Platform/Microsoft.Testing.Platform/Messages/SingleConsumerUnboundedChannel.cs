// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
namespace Microsoft.Testing.Platform.Messages;

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
    // There is no need to add an extra object for no value.
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

    public Task<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
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

            _waitingReader = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellationRegistration = cancellationToken.Register(static (tcs, ct) => ((TaskCompletionSource<bool>)tcs!).TrySetCanceled(ct), _waitingReader);
            return _waitingReader.Task;
        }
    }

    public void Complete()
    {
        lock (SyncObj)
        {
            _completed = true;

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

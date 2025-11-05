// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class AsyncConsumerDataProcessor : IDisposable
{
    // The default underlying collection is a ConcurrentQueue<T> object, which provides first in, first out (FIFO) behavior.
    private readonly ConcurrentQueue<(IDataProducer DataProducer, IData Data)> _payloads = [];
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;

    // This is needed to avoid possible race condition between drain and _totalPayloadProcessed race condition.
    // This is the "logical" consume workflow state.
    private readonly TaskCompletionSource<object> _consumerState = new();
    private readonly Task _consumeTask;
    private bool _disposed;
    private long _totalPayloadReceived;
    private long _totalPayloadProcessed;
    private bool _isAddingCompleted;

    public AsyncConsumerDataProcessor(IDataConsumer dataConsumer, ITask task, CancellationToken cancellationToken)
    {
        DataConsumer = dataConsumer;
        _task = task;
        _cancellationToken = cancellationToken;
        _consumeTask = task.Run(ConsumeAsync, cancellationToken);
    }

    public IDataConsumer DataConsumer { get; }

    public Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (_isAddingCompleted)
        {
            throw new UnreachableException();
        }

        Interlocked.Increment(ref _totalPayloadReceived);
        _payloads.Enqueue((dataProducer, data));
        _signal.Release();
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (!_isAddingCompleted || _signal.CurrentCount > 0 || !_payloads.IsEmpty)
            {
                await _signal.WaitAsync(_cancellationToken).ConfigureAwait(false);
                if (!_payloads.TryDequeue(out (IDataProducer DataProducer, IData Data) payload))
                {
                    throw new UnreachableException();
                }

                try
                {
                    // We don't enqueue the data if the consumer is the producer of the data.
                    // We could optimize this if and make a get with type/all but producers, but it
                    // could be over-engineering.
                    if (payload.DataProducer.Uid == DataConsumer.Uid)
                    {
                        continue;
                    }

                    try
                    {
                        await DataConsumer.ConsumeAsync(payload.DataProducer, payload.Data, _cancellationToken).ConfigureAwait(false);
                    }

                    // We let the catch below to handle the graceful cancellation of the process
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // If we're draining before to increment the _totalPayloadProcessed we need to signal that we should throw because
                        // it's possible we have a race condition where the payload check at line 106 return false and the current task is not yet in a
                        // "faulted state".
                        _consumerState.SetException(ex);

                        // We let current task to move to fault state, checked inside CompleteAddingAsync.
                        throw;
                    }
                }
                finally
                {
                    Interlocked.Increment(ref _totalPayloadProcessed);
                }
            }
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // Ignore we're shutting down
        }
        catch (ObjectDisposedException)
        {
            // It's rare but possible that we DrainDataAsync/CompleteAddingAsync/Dispose and we didn't reach yet the GetConsumingEnumerable wait point
            // after the last item elaboration. If this happen and the _payload is disposed and not "completed" we get an ObjectDisposedException.
        }
        catch (Exception ex)
        {
            // For all other exception we signal the state if not already faulted
            if (!_consumerState.Task.IsFaulted)
            {
                _consumerState.SetException(ex);
            }

            // let the exception bubble up
            throw;
        }

        // We're exiting gracefully, signal the correct state.
        _consumerState.SetResult(new object());
    }

    public async Task<long> DrainDataAsync()
    {
        if (_isAddingCompleted)
        {
            throw new InvalidOperationException("Unexpected IsAddingCompleted state");
        }

        // We go volatile because we race with Interlocked.Increment in PublishAsync
        long totalPayloadProcessed = Volatile.Read(ref _totalPayloadProcessed);
        long totalPayloadReceived = Volatile.Read(ref _totalPayloadReceived);
        const int minDelayTimeMs = 25;
        int currentDelayTimeMs = minDelayTimeMs;
        while (Interlocked.CompareExchange(ref _totalPayloadReceived, totalPayloadReceived, totalPayloadProcessed) != totalPayloadProcessed)
        {
            // When we cancel we throw inside ConsumeAsync and we won't drain anymore any data
            if (_cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await _task.Delay(currentDelayTimeMs).ConfigureAwait(false);
            currentDelayTimeMs = Math.Min(currentDelayTimeMs + minDelayTimeMs, 200);

            if (_consumerState.Task.IsFaulted)
            {
                // Rethrow the exception
                await _consumerState.Task.ConfigureAwait(false);
            }

            // Wait for the consumer to complete the current enqueued items
            totalPayloadProcessed = Volatile.Read(ref _totalPayloadProcessed);
            totalPayloadReceived = Volatile.Read(ref _totalPayloadReceived);
        }

        // It' possible that we fail and we have consumed the item
        if (_consumerState.Task.IsFaulted)
        {
            // Rethrow the exception
            await _consumerState.Task.ConfigureAwait(false);
        }

        return _totalPayloadReceived;
    }

    public async Task CompleteAddingAsync()
    {
        // Signal that no more items will be added to the collection
        _isAddingCompleted = true;

        // Wait for the consumer to complete
        await _consumeTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _signal.Dispose();
        _disposed = true;
    }
}
#endif

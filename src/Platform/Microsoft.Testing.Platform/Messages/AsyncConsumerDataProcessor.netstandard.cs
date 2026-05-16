// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class AsyncConsumerDataProcessor : IAsyncConsumerDataProcessor
{
    private readonly CancellationToken _cancellationToken;

    private readonly SingleConsumerUnboundedChannel<AsyncConsumerDataProcessorMessage> _channel = new();
    private readonly Task _consumeTask;

    // Number of data payloads dequeued by the consumer. Used by DrainDataAsync to detect publisher/consumer loops.
    // Only the single consumer task increments this field; other threads read it via Volatile.Read.
    private long _processedCount;

    public AsyncConsumerDataProcessor(IDataConsumer dataConsumer, ITask task, CancellationToken cancellationToken)
    {
        DataConsumer = dataConsumer;
        _cancellationToken = cancellationToken;
        _consumeTask = task.Run(ConsumeAsync, cancellationToken);
    }

    public IDataConsumer DataConsumer { get; }

    public Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _channel.Write(AsyncConsumerDataProcessorMessage.CreateData(dataProducer, data));
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _channel.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
            {
                while (_channel.TryRead(out AsyncConsumerDataProcessorMessage message))
                {
                    if (message.DrainMarker is { } drainMarker)
                    {
                        // The drain marker passed all the data items previously enqueued so we can signal the drain caller.
                        drainMarker.TrySetResult(true);
                        continue;
                    }

                    Interlocked.Increment(ref _processedCount);

                    // We don't enqueue the data if the consumer is the producer of the data.
                    // We could optimize this if and make a get with type/all but producers, but it
                    // could be over-engineering.
                    if (message.DataProducer!.Uid == DataConsumer.Uid)
                    {
                        continue;
                    }

                    await DataConsumer.ConsumeAsync(message.DataProducer, message.Data!, _cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // Ignore we're shutting down
        }
    }

    public async Task CompleteAddingAsync()
    {
        // Signal that no more items will be added to the collection
        // It's possible that we call this method multiple times
        _channel.Complete();

        // Wait for the consumer to complete
        await _consumeTask.ConfigureAwait(false);
    }

    public async Task<bool> DrainDataAsync()
    {
        long before = Volatile.Read(ref _processedCount);
        var drainMarker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _channel.Write(AsyncConsumerDataProcessorMessage.CreateDrainMarker(drainMarker));
        }
        catch (InvalidOperationException)
        {
            // The channel was already completed (e.g., by DisableAsync). Nothing left to drain.
            return false;
        }

        // Wait either for the drain marker to be dequeued, or for the consume task to finish/fault.
        // If the consume task ends before the marker is reached, propagate any failure it surfaced.
        Task completed = await Task.WhenAny(drainMarker.Task, _consumeTask).ConfigureAwait(false);
        if (completed == _consumeTask)
        {
            await _consumeTask.ConfigureAwait(false);
        }
        else
        {
            await drainMarker.Task.ConfigureAwait(false);
        }

        return Volatile.Read(ref _processedCount) != before;
    }

    public void Dispose()
        => _channel.Complete();
}
#endif

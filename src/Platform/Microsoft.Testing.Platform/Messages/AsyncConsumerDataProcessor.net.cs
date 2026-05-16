// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

[DebuggerDisplay("DataConsumer = {DataConsumer.Uid}")]
internal sealed class AsyncConsumerDataProcessor : IAsyncConsumerDataProcessor
{
    private readonly CancellationToken _cancellationToken;

    private readonly Channel<AsyncConsumerDataProcessorMessage> _channel = Channel.CreateUnbounded<AsyncConsumerDataProcessorMessage>(new UnboundedChannelOptions
    {
        // We process only 1 data at a time
        SingleReader = true,

        // We don't know how many threads will call the publish on the message bus
        SingleWriter = false,

        // We want to unlink the publish that's the message bus
        AllowSynchronousContinuations = false,
    });

    private readonly Task _consumeTask;

    // Number of data payloads enqueued via PublishAsync. The message bus reads this via
    // ReceivedCount to detect publisher/consumer cycles across drain rounds.
    private long _receivedCount;

    public AsyncConsumerDataProcessor(IDataConsumer consumer, ITask task, CancellationToken cancellationToken)
    {
        DataConsumer = consumer;
        _cancellationToken = cancellationToken;
        _consumeTask = task.Run(ConsumeAsync, cancellationToken);
    }

    public IDataConsumer DataConsumer { get; }

    public long ReceivedCount => Volatile.Read(ref _receivedCount);

    public async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        Interlocked.Increment(ref _receivedCount);
        await _channel.Writer.WriteAsync(AsyncConsumerDataProcessorMessage.CreateData(dataProducer, data), _cancellationToken).ConfigureAwait(false);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
            {
                AsyncConsumerDataProcessorMessage message = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

                if (message.DrainMarker is { } drainMarker)
                {
                    // The drain marker passed all the data items previously enqueued so we can signal the drain caller.
                    drainMarker.TrySetResult(true);
                    continue;
                }

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
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // Ignore we're shutting down
        }
    }

    public async Task CompleteAddingAsync()
    {
        // Signal that no more items will be added to the collection
        // It's possible that we call this method multiple times
        _channel.Writer.TryComplete();

        // Wait for the consumer to complete
        await _consumeTask.ConfigureAwait(false);
    }

    public async Task DrainDataAsync()
    {
        var drainMarker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await _channel.Writer.WriteAsync(AsyncConsumerDataProcessorMessage.CreateDrainMarker(drainMarker), _cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // The channel was already completed (e.g., by DisableAsync). Nothing left to drain.
            return;
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // The application is shutting down. Treat the drain as a graceful no-op,
            // matching the previous behavior of bailing out of DrainDataAsync on cancellation.
            return;
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
    }

    // At this point we simply signal the channel as complete and we don't wait for the consumer to complete.
    // We expect that the CompleteAddingAsync() is already done correctly and so we prefer block the loop and in
    // case get exception inside the PublishAsync()
    public void Dispose()
        => _channel.Writer.TryComplete();
}
#endif

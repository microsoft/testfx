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
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;

    private Channel<(IDataProducer DataProducer, IData Data)> _channel = CreateChannel();
    private Task _consumeTask;

    public AsyncConsumerDataProcessor(IDataConsumer consumer, ITask task, CancellationToken cancellationToken)
    {
        DataConsumer = consumer;
        _task = task;
        _cancellationToken = cancellationToken;
        _consumeTask = task.Run(ConsumeAsync, cancellationToken);
    }

    public IDataConsumer DataConsumer { get; }

    public async Task PublishAsync(IDataProducer dataProducer, IData data)
        => await _channel.Writer.WriteAsync((dataProducer, data), _cancellationToken).ConfigureAwait(false);

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
            {
                (IDataProducer dataProducer, IData data) = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

                // We don't enqueue the data if the consumer is the producer of the data.
                // We could optimize this if and make a get with type/all but producers, but it
                // could be over-engineering.
                if (dataProducer.Uid == DataConsumer.Uid)
                {
                    continue;
                }

                await DataConsumer.ConsumeAsync(dataProducer, data, _cancellationToken).ConfigureAwait(false);
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
        _channel.Writer.Complete();

        // Wait for the consumer to complete
        await _consumeTask.ConfigureAwait(false);
    }

    public async Task DrainDataAsync()
    {
        _channel.Writer.Complete();
        await _consumeTask.ConfigureAwait(false);

        _channel = CreateChannel();
        _consumeTask = _task.Run(ConsumeAsync, _cancellationToken);
    }

    // At this point we simply signal the channel as complete and we don't wait for the consumer to complete.
    // We expect that the CompleteAddingAsync() is already done correctly and so we prefer block the loop and in
    // case get exception inside the PublishAsync()
    public void Dispose()
        => _channel.Writer.Complete();

    private static Channel<(IDataProducer DataProducer, IData Data)> CreateChannel()
        => Channel.CreateUnbounded<(IDataProducer DataProducer, IData Data)>(new UnboundedChannelOptions
        {
            // We process only 1 data at a time
            SingleReader = true,

            // We don't know how many threads will call the publish on the message bus
            SingleWriter = false,

            // We want to unlink the publish that's the message bus
            AllowSynchronousContinuations = false,
        });
}
#endif

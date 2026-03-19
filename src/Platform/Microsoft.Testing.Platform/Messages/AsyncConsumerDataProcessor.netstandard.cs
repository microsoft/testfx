// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class AsyncConsumerDataProcessor : IAsyncConsumerDataProcessor
{
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;

    private SingleConsumerUnboundedChannel<(IDataProducer DataProducer, IData Data)> _channel = new();
    private Task _consumeTask;

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
        _cancellationToken.ThrowIfCancellationRequested();
        _channel.Write((dataProducer, data));
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _channel.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
            {
                while (_channel.TryRead(out (IDataProducer DataProducer, IData Data) item))
                {
                    // We don't enqueue the data if the consumer is the producer of the data.
                    // We could optimize this if and make a get with type/all but producers, but it
                    // could be over-engineering.
                    if (item.DataProducer.Uid == DataConsumer.Uid)
                    {
                        continue;
                    }

                    await DataConsumer.ConsumeAsync(item.DataProducer, item.Data, _cancellationToken).ConfigureAwait(false);
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

    public async Task DrainDataAsync()
    {
        _channel.Complete();
        await _consumeTask.ConfigureAwait(false);

        _channel = new();
        _consumeTask = _task.Run(ConsumeAsync, _cancellationToken);
    }

    public void Dispose()
        => _channel.Complete();
}
#endif

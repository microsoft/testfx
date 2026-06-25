// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Processor used for <see cref="IBlockingDataConsumer"/> instances. Unlike <see cref="AsyncConsumerDataProcessor"/>,
/// which queues data and processes it on a background loop, this processor consumes the data inline so the publisher
/// blocks until <see cref="IDataConsumer.ConsumeAsync"/> has completed.
/// </summary>
[DebuggerDisplay("DataConsumer = {DataConsumer.Uid}")]
internal sealed class BlockingConsumerDataProcessor : IAsyncConsumerDataProcessor
{
    private readonly CancellationToken _cancellationToken;

    // Serialize the inline consumption so that we honor the same "process only 1 data at a time"
    // guarantee that the asynchronous processor provides through its single reader.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private long _receivedCount;

    public BlockingConsumerDataProcessor(IDataConsumer dataConsumer, CancellationToken cancellationToken)
    {
        DataConsumer = dataConsumer;
        _cancellationToken = cancellationToken;
    }

    public IDataConsumer DataConsumer { get; }

    public long ReceivedCount => Volatile.Read(ref _receivedCount);

    public async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // We don't consume the data if the consumer is the producer of the data.
        if (dataProducer.Uid == DataConsumer.Uid)
        {
            return;
        }

        Interlocked.Increment(ref _receivedCount);

        await _semaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);
        try
        {
            await DataConsumer.ConsumeAsync(dataProducer, data, _cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // The data is consumed inline in PublishAsync, so there is never anything left to drain.
    public Task DrainDataAsync() => Task.CompletedTask;

    // The data is consumed inline in PublishAsync, so there is no background loop to complete.
    public Task CompleteAddingAsync() => Task.CompletedTask;

    public void Dispose()
        => _semaphore.Dispose();
}

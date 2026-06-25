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

        // Increment unconditionally (even for self-produced data we skip below) to keep the same
        // ReceivedCount semantics as AsyncConsumerDataProcessor.
        Interlocked.Increment(ref _receivedCount);

        // We don't consume the data if the consumer is the producer of the data.
        if (dataProducer.Uid == DataConsumer.Uid)
        {
            return;
        }

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

    public async Task CompleteAddingAsync()
    {
        // The data is consumed inline in PublishAsync, so there is no background loop to complete.
        // We still wait for any in-flight inline consumption to finish before the processor can be
        // disposed: acquiring the single permit guarantees no consumer is currently executing, and we
        // release it immediately afterwards. The message bus marks itself disabled before calling this,
        // so the platform stops issuing new publishes to this processor while we wait.
        //
        // We intentionally wait without the cancellation token: an in-flight consumption already
        // receives the token and will unwind promptly on shutdown, but we must still wait for it to
        // release the permit before Dispose runs to avoid an ObjectDisposedException on the release.
        // This mirrors AsyncConsumerDataProcessor.CompleteAddingAsync, which unconditionally awaits its
        // consume task on every path.
        await _semaphore.WaitAsync().ConfigureAwait(false);
        _semaphore.Release();
    }

    // We intentionally do not dispose the SemaphoreSlim. Disposing it would introduce an
    // ObjectDisposedException failure mode if AsynchronousMessageBus.Dispose ran while a PublishAsync
    // was still in-flight (the in-flight call would hit _semaphore.Release on a disposed instance).
    // SemaphoreSlim only owns an unmanaged WaitHandle if its AvailableWaitHandle is accessed, which we
    // never do, so it is safe to let the GC reclaim it. This matches AsyncConsumerDataProcessor, which
    // does not dispose a synchronization primitive either.
    public void Dispose()
    {
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Diagnostics;
using System.Threading.Channels;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Messages;

[DebuggerDisplay("DataConsumer = {DataConsumer.Uid}")]
internal sealed class AsyncConsumerDataProcessor : IDisposable
{
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
    private readonly Channel<(IDataProducer DataProducer, IData Data)> _channel = Channel.CreateUnbounded<(IDataProducer DataProducer, IData Data)>(new UnboundedChannelOptions
    {
        // We process only 1 data at a time
        SingleReader = true,

        // We don't know how many threads will call the publish on the message bus
        SingleWriter = false,

        // We want to unlink the publish that's the message bus
        AllowSynchronousContinuations = false,
    });

    // This is needed to avoid possible race condition between drain and _totalPayloadProcessed race condition.
    // This is the "logical" consume workflow state.
    private readonly TaskCompletionSource _consumerState = new();
    private readonly Task _consumeTask;
    private long _totalPayloadReceived;
    private long _totalPayloadProcessed;

    public AsyncConsumerDataProcessor(IDataConsumer consumer, ITask task, CancellationToken cancellationToken)
    {
        DataConsumer = consumer;
        _task = task;
        _cancellationToken = cancellationToken;
        _consumeTask = task.Run(ConsumeAsync, cancellationToken);
    }

    public IDataConsumer DataConsumer { get; }

    public async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        Interlocked.Increment(ref _totalPayloadReceived);
        await _channel.Writer.WriteAsync((dataProducer, data), _cancellationToken);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cancellationToken))
            {
                (IDataProducer dataProducer, IData data) = await _channel.Reader.ReadAsync(_cancellationToken);

                try
                {
                    // We don't enqueue the data if the consumer is the producer of the data.
                    // We could optimize this if and make a get with type/all but producers, but it
                    // could be over-engineering.
                    if (dataProducer.Uid == DataConsumer.Uid)
                    {
                        continue;
                    }

                    try
                    {
                        await DataConsumer.ConsumeAsync(dataProducer, data, _cancellationToken);
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
        _consumerState.SetResult();
    }

    public async Task CompleteAddingAsync()
    {
        // Signal that no more items will be added to the collection
        // It's possible that we call this method multiple times
        _channel.Writer.TryComplete();

        // Wait for the consumer to complete
        await _consumeTask;
    }

    public async Task<long> DrainDataAsync()
    {
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

            await _task.Delay(currentDelayTimeMs);
            currentDelayTimeMs = Math.Min(currentDelayTimeMs + minDelayTimeMs, 200);

            if (_consumerState.Task.IsFaulted)
            {
                // Rethrow the exception
                await _consumerState.Task;
            }

            // Wait for the consumer to complete the current enqueued items
            totalPayloadProcessed = Volatile.Read(ref _totalPayloadProcessed);
            totalPayloadReceived = Volatile.Read(ref _totalPayloadReceived);
        }

        // It' possible that we fail and we have consumed the item
        if (_consumerState.Task.IsFaulted)
        {
            // Rethrow the exception
            await _consumerState.Task;
        }

        return _totalPayloadReceived;
    }

    // At this point we simply signal the channel as complete and we don't wait for the consumer to complete.
    // We expect that the CompleteAddingAsync() is already done correctly and so we prefer block the loop and in
    // case get exception inside the PublishAsync()
    public void Dispose()
        => _channel.Writer.TryComplete();
}
#endif

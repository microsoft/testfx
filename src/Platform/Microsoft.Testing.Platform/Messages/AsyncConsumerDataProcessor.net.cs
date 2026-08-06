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
    private readonly TimeSpan _canceledShutdownTimeout;

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
        : this(consumer, task, cancellationToken, ShutdownTimeouts.DefaultCanceledConsumerCompletion)
    {
    }

    public AsyncConsumerDataProcessor(IDataConsumer consumer, ITask task, CancellationToken cancellationToken, TimeSpan canceledShutdownTimeout)
    {
        DataConsumer = consumer;
        _cancellationToken = cancellationToken;
        _canceledShutdownTimeout = canceledShutdownTimeout;

        // The pump is scheduled without the run token on purpose. Task.Run cancels the work item before it
        // ever starts if the token fires while it is still queued, and that would abandon payloads the channel
        // has already accepted - exactly what the loop below exists to drain. The token reaches the consumer
        // through ConsumeAsync instead.
        _consumeTask = task.Run(ConsumeAsync, CancellationToken.None);
    }

    public IDataConsumer DataConsumer { get; }

    public bool IsConsumerRunning => !_consumeTask.IsCompleted;

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
            // The pump itself is deliberately not cancelable. The shutdown handshake stops the producers and
            // completes the channel, and the loop then ends by itself once it has drained whatever was already
            // queued. Cancelling the read instead would abandon those payloads, which is how a canceled run
            // used to lose results it had already produced. Individual consumers still receive the run's token,
            // so a cooperative consumer can bail out of its own work immediately.
            while (await _channel.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
            {
                AsyncConsumerDataProcessorMessage message = await _channel.Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);

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
            // The consumer observed the cancellation and unwound, which means it opted out of the payloads
            // that are still queued. Stop pumping instead of calling it again with the same canceled token.
        }
    }

    public async Task CompleteAddingAsync()
    {
        // Signal that no more items will be added to the collection
        // It's possible that we call this method multiple times
        _channel.Writer.TryComplete();

        try
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Normal shutdown: wait as long as the consumer needs to flush its final results, because
                    // cutting off a large TRX or coverage report would lose them. We still observe the token so
                    // that a cancellation arriving mid-wait downgrades us to the bounded wait below instead of
                    // leaving the abort waiting on the whole remaining backlog.
                    await _consumeTask.WaitAsync(Timeout.InfiniteTimeSpan, _cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
                {
                    // The run was canceled while we were waiting. Fall through to the bounded wait, which
                    // returns (or rethrows the consumer's real failure) immediately if the loop happened to
                    // finish in the meantime. Deciding that from an IsCompleted check in this filter would be a
                    // race that silently turns a consumer failure into a swallowed cancellation.
                }
            }

            // The run is being aborted, so the wait has to be bounded: a cooperative consumer unwinds as soon
            // as it observes the token, but one that ignores it must not be able to hang the abort. An
            // interactive Ctrl+C can be pressed a second time to force the exit, but a '--timeout' or a
            // '--maximum-failed-tests' abort has no such escape hatch.
            await _consumeTask.WaitAsync(_canceledShutdownTimeout).ConfigureAwait(false);
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // The consume loop ended because the test application is shutting down. That is not a failure:
            // all we need to guarantee here is that the loop is no longer running before the consumer gets
            // disposed, and awaiting the task above already did that.
        }
        catch (TimeoutException)
        {
            if (_consumeTask.IsCompleted)
            {
                // WaitAsync surfaces the consume task's own failure with the same exception type it uses for
                // its timeout, so a consumer that threw TimeoutException would otherwise be silently swallowed.
                // Re-awaiting the completed task rethrows the real exception with its stack.
                await _consumeTask.ConfigureAwait(false);
                return;
            }

            // The consumer is ignoring the cancellation. We give up waiting so the abort can complete;
            // IsConsumerRunning keeps reporting true so the platform does not dispose it underneath itself.
        }
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

    // Completing the writer is what makes the consume loop terminate. We deliberately do not block here: the
    // shutdown handshake (BaseMessageBus.DisableAsync -> CompleteAddingAsync) is what waits for the loop, and
    // IsConsumerRunning tells the platform not to dispose a consumer whose loop is still going. A caller that
    // skips the handshake therefore degrades into leaking the consumer rather than racing its disposal.
    public void Dispose()
    {
        _channel.Writer.TryComplete();

        // On the timeout path the loop is deliberately still running, so reading Exception now would observe
        // nothing and a later failure would resurface as an unobserved task exception. Observe it whenever it
        // eventually completes instead.
        ObserveConsumeTaskFailure(_consumeTask);
    }

    private static void ObserveConsumeTaskFailure(Task consumeTask)
        => _ = consumeTask.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
#endif

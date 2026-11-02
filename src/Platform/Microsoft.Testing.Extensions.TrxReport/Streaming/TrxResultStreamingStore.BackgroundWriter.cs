// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

// BlockingCollection<T> is annotated [UnsupportedOSPlatform("browser")] which propagates to every
// member access. On single-threaded WebAssembly runtimes this type never allocates the queue and never
// starts the background writer (see RuntimeFeatureHelper.IsMultiThreaded and the inline mode), so
// the browser-unsafe members are unreachable there. Suppress CA1416 file-scoped rather than propagating
// the attribute through the ctor / call chain, which would force every caller to add platform guards for
// a code path that browser never hits.
// NOTE: This file is the only one that touches BlockingCollection<T>. Keep it that way so the
// suppression stays scoped to the producer/consumer queue helpers.
#pragma warning disable CA1416

/// <content>
/// The multithreaded producer/consumer path: a single background writer task drains the queue and
/// hands batches to the file writer.
/// </content>
internal sealed partial class TrxResultStreamingStore
{
    // Both fields are written exactly once, during construction, from StartBackgroundWriter. They cannot
    // be readonly because that initialization lives in this partial rather than in the constructor.
    private BlockingCollection<TrxTestResult>? _queue;
    // Null in inline mode: there is no background writer to await.
    private Task? _writerTask;

    private void StartBackgroundWriter()
    {
#pragma warning disable IDE0028 // Inner ConcurrentQueue type must be explicit so the writer thread sees FIFO semantics.
        _queue = new(new ConcurrentQueue<TrxTestResult>());
#pragma warning restore IDE0028

        // BlockingCollection<T>.TryTake blocks the calling thread for up to _flushIntervalMs when
        // the queue is idle. Running the writer on a dedicated long-running thread instead of the
        // shared threadpool keeps it from starving threadpool consumers while it sleeps on the queue.
        _writerTask = _task.RunLongRunning(WriteLoopAsync, "TRX streaming store writer", CancellationToken.None);
    }

    private void EnqueueToQueue(TrxTestResult result)
    {
        ApplicationStateGuard.Ensure(_queue is not null);

        if (_queue.IsAddingCompleted || _faulted)
        {
            LogDrop("writer is completed or faulted");
            return;
        }

        try
        {
            _queue.Add(result);
        }
        catch (ObjectDisposedException)
        {
            // Race: Dispose ran between the check and Add. (Catch before InvalidOperationException
            // because ObjectDisposedException derives from it.)
            LogDrop("queue disposed during enqueue");
        }
        catch (InvalidOperationException)
        {
            // Race: completed between the IsAddingCompleted check and Add.
            LogDrop("queue completed during enqueue");
        }
    }

    private async Task CompleteBackgroundWriterAsync(CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_queue is not null);

        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }

        Task timeout = _task.Delay(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken);
        Task completed = await Task.WhenAny(_writerTask!, timeout).ConfigureAwait(false);
        if (completed != _writerTask)
        {
            // If the timeout fired because of cancellation, surface that as the real cause rather than
            // logging a misleading "hang" warning.
            cancellationToken.ThrowIfCancellationRequested();

            // Mark as timed out so the caller knows not to delete the sidecar — a stuck writer may
            // still hold useful records that crash recovery (or an out-of-band tool) can salvage.
            _completionTimedOut = true;
            int stillQueued = _queue.Count;
            TryLogWarning(
                $"TRX streaming store writer did not drain within the hang timeout; intermediate file may be incomplete. Approximately {stillQueued} record(s) are still queued and will not appear in the TRX.");
        }
    }

    private Task WriteLoopAsync()
    {
        ApplicationStateGuard.Ensure(_queue is not null);
        var batch = new List<TrxTestResult>(_batchSize);
        try
        {
            while (!_queue.IsCompleted)
            {
                if (!_queue.TryTake(out TrxTestResult? first, _flushIntervalMs))
                {
                    // Timeout fired: nothing pending. Loop and re-check completion.
                    continue;
                }

                batch.Add(first);

                // Drain whatever else is immediately available, up to batch size.
                while (batch.Count < _batchSize && _queue.TryTake(out TrxTestResult? next))
                {
                    batch.Add(next);
                }

                WriteBatch(batch);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
            // Dispose() asked the writer to stop. Best-effort drain, no fault, no scary log line.
            // We deliberately do NOT increment _droppedCount: producers stopped enqueuing as soon as
            // Dispose() called CompleteAdding, and anything left in the queue was unobserved by the
            // user (the session was being shut down).
            TryLogDebug("TRX streaming store writer cancelled during shutdown.");
        }
        catch (Exception ex)
        {
            _faulted = true;

            // Stop accepting further records immediately so producers don't busy-drop for the rest
            // of the session via the IsAddingCompleted-or-_faulted gate in Enqueue. Account for records
            // that the writer pulled into the local batch but never wrote, plus any records that were
            // already in the queue when we faulted. Without this the consumer sees DroppedCount == 0 and
            // reports "TRX is complete" when it isn't.
            int discarded = batch.Count + CompleteAndDrainQueue();

            if (discarded > 0)
            {
                Interlocked.Add(ref _droppedCount, discarded);
            }

            TryLogError(
                $"TRX streaming store writer faulted; intermediate file may be incomplete. {discarded} record(s) were dropped from the in-memory queue and will not appear in the TRX.",
                ex);
        }
        finally
        {
            CloseFile();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop accepting new records and drop whatever is still queued, returning how many were dropped.
    /// Returns 0 in inline mode, where there is no queue and <see cref="Enqueue"/> is gated by the
    /// faulted/completed flags instead.
    /// </summary>
    private int CompleteAndDrainQueue()
    {
        if (_queue is null)
        {
            return 0;
        }

        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }

        int dropped = 0;
        while (_queue.TryTake(out _))
        {
            dropped++;
        }

        return dropped;
    }

    private void DisposeBackgroundWriter()
    {
        ApplicationStateGuard.Ensure(_queue is not null);

        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }

        _disposeCts.Cancel();

        try
        {
            _writerTask!.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }
        catch (AggregateException)
        {
            // Already logged inside WriteLoopAsync.
        }
        catch (OperationCanceledException)
        {
        }

        _queue.Dispose();
    }
}

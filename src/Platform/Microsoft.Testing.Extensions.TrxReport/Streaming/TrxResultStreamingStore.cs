// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

// BlockingCollection<T> is annotated [UnsupportedOSPlatform("browser")] which propagates to every
// member access. On single-threaded WebAssembly runtimes this type never allocates the queue and never
// starts the background writer (see RuntimeFeatureHelper.IsMultiThreaded and the inline mode below), so
// the browser-unsafe members are unreachable there. Suppress CA1416 file-scoped rather than propagating
// the attribute through the ctor / call chain, which would force every caller to add platform guards for
// a code path that browser never hits.
// NOTE: When adding new code to this file, prefer to keep browser-unsafe APIs limited to the queue
// helpers below — the suppression is intentionally broad only because BlockingCollection touches
// every method that drains it.
#pragma warning disable CA1416

/// <summary>
/// Append-only store that streams <see cref="TrxTestResult"/> records to a sidecar file. On multithreaded
/// runtimes, producers (test result consumers) enqueue without blocking on disk I/O: a single background
/// writer task drains a <see cref="BlockingCollection{T}"/>, batching records by size or time window and
/// flushing to disk so a crash after the flush leaves a recoverable file on disk.
/// </summary>
/// <remarks>
/// Single-threaded WebAssembly runtimes (<c>browser-wasm</c>, <c>wasi-wasm</c>) cannot create the dedicated
/// writer thread and cannot block on the queue, so the store switches to an <em>inline</em> mode: records are
/// serialized synchronously on the calling thread as they are enqueued. The on-disk format, retry/truncation
/// behavior, and drop accounting are identical; only the hand-off is different.
/// </remarks>
internal sealed class TrxResultStreamingStore : IDisposable
{
    // Tuned for typical test runs. A large enough batch to amortize syscalls, small enough to flush
    // often so a crash loses at most one window of results.
    private const int DefaultBatchSize = 64;
    private const int DefaultFlushIntervalMs = 500;
    private const int MaxWriteRetries = 3;
    private const int RetryBaseDelayMs = 50;

    private readonly IFileSystem _fileSystem;
    private readonly ITask _task;
    private readonly ILogger _logger;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;
    private readonly BlockingCollection<TrxTestResult>? _queue;
    // Null in inline mode: there is no background writer to await.
    private readonly Task? _writerTask;
    // Reused single-record buffer for inline writes so enqueueing does not allocate a list per record.
    private readonly List<TrxTestResult>? _inlineBatch;
    private readonly CancellationTokenSource _disposeCts = new();
    private IFileStream? _fileStream;
    private BinaryWriter? _writer;
    private bool _initialized;
    private bool _inlineCompleted;
    private volatile bool _faulted;
    private volatile bool _completionTimedOut;
    private int _writtenCount;
    private int _droppedCount;

    public TrxResultStreamingStore(string filePath, IFileSystem fileSystem, ITask task, ILogger logger)
        : this(filePath, fileSystem, task, logger, DefaultBatchSize, DefaultFlushIntervalMs)
    {
    }

    internal TrxResultStreamingStore(string filePath, IFileSystem fileSystem, ITask task, ILogger logger, int batchSize, int flushIntervalMs)
        : this(filePath, fileSystem, task, logger, batchSize, flushIntervalMs, RuntimeFeatureHelper.IsMultiThreaded)
    {
    }

    internal TrxResultStreamingStore(string filePath, IFileSystem fileSystem, ITask task, ILogger logger, int batchSize, int flushIntervalMs, bool useBackgroundWriter)
    {
        FilePath = filePath;
        _fileSystem = fileSystem;
        _task = task;
        _logger = logger;
        _batchSize = batchSize;
        _flushIntervalMs = flushIntervalMs;

        if (!useBackgroundWriter)
        {
            // Inline mode: no queue, no thread, no blocking wait. Enqueue serializes on the caller.
            _inlineBatch = [];
            return;
        }

#pragma warning disable IDE0028 // Inner ConcurrentQueue type must be explicit so the writer thread sees FIFO semantics.
        _queue = new(new ConcurrentQueue<TrxTestResult>());
#pragma warning restore IDE0028

        // BlockingCollection<T>.TryTake blocks the calling thread for up to _flushIntervalMs when
        // the queue is idle. Running the writer on a dedicated long-running thread instead of the
        // shared threadpool keeps it from starving threadpool consumers while it sleeps on the queue.
        _writerTask = task.RunLongRunning(WriteLoopAsync, "TRX streaming store writer", CancellationToken.None);
    }

    /// <summary>
    /// Gets a value indicating whether records are serialized synchronously on the enqueueing thread
    /// instead of being handed to a background writer. True on single-threaded WebAssembly runtimes,
    /// where <see cref="RuntimeFeatureHelper.IsMultiThreaded"/> is false.
    /// </summary>
    internal bool IsInline => _queue is null;

    /// <summary>
    /// Gets the path of the sidecar file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the writer task has faulted (an unrecoverable exception bubbled out
    /// of the write loop). Faulted stores stop accepting new records; already-written records remain on disk.
    /// </summary>
    public bool IsFaulted => _faulted;

    /// <summary>
    /// Gets the number of records the writer has handed off to the OS in completed batches. Buffered
    /// (not necessarily fsync'd) at the OS level, but durable enough that a process crash leaves them
    /// on disk for recovery. Used by tests and diagnostics; does not include records still in the queue.
    /// </summary>
    public int BufferedCount => Volatile.Read(ref _writtenCount);

    /// <summary>
    /// Gets the number of records that were dropped because the writer was completed or faulted at the
    /// time of <see cref="Enqueue"/>. Surfaced at session end so a partial TRX can be explained.
    /// </summary>
    public int DroppedCount => Volatile.Read(ref _droppedCount);

    /// <summary>
    /// Gets a value indicating whether the writer task did not complete draining within the hang
    /// timeout. When true, the sidecar file may still be in use and the caller MUST NOT delete it
    /// (it remains valuable for crash recovery).
    /// </summary>
    public bool CompletionTimedOut => _completionTimedOut;

    /// <summary>
    /// Enqueue a record for asynchronous write. Returns immediately. In inline mode the record is instead
    /// serialized synchronously before returning, so it is already durable. If the writer is faulted or
    /// completed the record is dropped (with a debug log). We do not throw because losing TRX intermediate
    /// records must never break the test session.
    /// </summary>
    public void Enqueue(TrxTestResult result)
    {
        if (_queue is null)
        {
            EnqueueInline(result);
            return;
        }

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

    // Inline mode has no producer/consumer hand-off: the record is serialized on the calling thread.
    // WriteBatch already accounts per-record write failures, so only failures escaping it (file/directory
    // provisioning) fault the store.
    private void EnqueueInline(TrxTestResult result)
    {
        if (_inlineCompleted || _faulted)
        {
            LogDrop("writer is completed or faulted");
            return;
        }

        ApplicationStateGuard.Ensure(_inlineBatch is not null);
        _inlineBatch.Clear();
        _inlineBatch.Add(result);
        try
        {
            WriteBatch(_inlineBatch);
        }
        catch (Exception ex)
        {
            _faulted = true;
            Interlocked.Increment(ref _droppedCount);
            TryLogError(
                "TRX streaming store inline writer faulted; intermediate file may be incomplete. 1 record was dropped and will not appear in the TRX.",
                ex);
        }
        finally
        {
            _inlineBatch.Clear();
        }
    }

    /// <summary>
    /// Signal completion and wait for the writer task to drain. Bounded by the platform hang timeout
    /// so a stuck writer (slow network drive, locked file) cannot hang the session. In inline mode there
    /// is nothing to drain, so this only closes the file.
    /// </summary>
    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        if (_queue is null)
        {
            CompleteInline();
            return;
        }

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

    private void CompleteInline()
    {
        if (_inlineCompleted)
        {
            return;
        }

        _inlineCompleted = true;
        CloseFile();
    }

#pragma warning disable VSTHRD103 // The writer runs on a dedicated long-running thread; synchronous waits keep queue polling off the threadpool.
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

    private void CloseFile()
    {
        try
        {
            _fileStream?.Stream.Flush();

            _writer?.Dispose();
            _fileStream?.Dispose();
        }
        catch (Exception ex)
        {
            TryLogError("Failed to close TRX streaming store file.", ex);
        }
    }

    private void WriteBatch(List<TrxTestResult> batch)
    {
        EnsureFileOpen();
        ApplicationStateGuard.Ensure(_writer is not null);
        ApplicationStateGuard.Ensure(_fileStream is not null);

        Stream rawStream = _fileStream.Stream;
        int written = 0;

        for (int i = 0; i < batch.Count; i++)
        {
            // Snapshot position BEFORE each record so a mid-record failure (whether transient and
            // recovered by retry, or fatal) can rewind to a known record boundary. Without this, a
            // partial [length][partial-payload] write would leave the file misaligned and ReadAll
            // would return garbage or stop mid-stream.
            long preRecordPosition = rawStream.Position;
            try
            {
                WriteRecordWithRetry(_writer, batch[i], rawStream, preRecordPosition);
                written++;
            }
            catch (Exception ex)
            {
                int unwritten = batch.Count - written;
                Interlocked.Add(ref _droppedCount, unwritten);

                TryLogError(
                    $"Failed to write TRX record {i + 1}/{batch.Count} after up to {MaxWriteRetries} retries; truncating to last good record. {unwritten} record(s) from this batch will not appear in the TRX.",
                    ex);
                try
                {
                    rawStream.Seek(preRecordPosition, SeekOrigin.Begin);
                    rawStream.SetLength(preRecordPosition);
                }
                catch (Exception truncEx)
                {
                    // If we cannot truncate, the file is corrupt; mark the writer faulted AND complete
                    // the queue so producers stop adding and any already-queued records are accounted
                    // as dropped instead of being silently written past the corruption.
                    // Note: the outer catch already counted (batch.Count - written) as dropped.
                    _faulted = true;
                    int additionalDropped = CompleteAndDrainQueue();

                    if (additionalDropped > 0)
                    {
                        Interlocked.Add(ref _droppedCount, additionalDropped);
                    }

                    TryLogError(
                        $"Failed to truncate TRX streaming store after write failure; marking store faulted. {additionalDropped} additional record(s) from the queue were dropped.",
                        truncEx);
                    return;
                }

                // Stop processing further records in this batch — a persistent error will hit them too,
                // and we want to preserve the file in a known-good state.
                break;
            }
        }

        try
        {
            rawStream.Flush();
        }
        catch (Exception ex)
        {
            TryLogError("Failed to flush TRX streaming store; records remain in OS buffer.", ex);
        }

        if (written > 0)
        {
            Interlocked.Add(ref _writtenCount, written);
        }
    }

    private void WriteRecordWithRetry(BinaryWriter writer, TrxTestResult record, Stream rawStream, long preRecordPosition)
    {
        // Mirrors the retry policy of TrxReportEngine.RetryWhenIOExceptionAsync but bounded so a
        // permanently broken file does not stall the writer indefinitely. Critically: each retry
        // rewinds + truncates to the pre-record offset so a partial write from a failed attempt
        // doesn't leave a torn record on disk that a successful retry would then run on top of.
        Exception? lastError = null;
        for (int attempt = 1; attempt <= MaxWriteRetries; attempt++)
        {
            try
            {
                TrxTestResultSerializer.Write(writer, record);
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }

            // Truncate any partial write before the next attempt. If we can't, give up immediately
            // with the original error — retrying on a misaligned file would corrupt it.
            try
            {
                rawStream.Seek(preRecordPosition, SeekOrigin.Begin);
                rawStream.SetLength(preRecordPosition);
            }
            catch
            {
                ExceptionDispatchInfo.Capture(lastError!).Throw();
            }

            try
            {
                if (_queue is null)
                {
                    // Inline mode runs on the (single) calling thread — often the sole WebAssembly thread —
                    // where a blocking wait would deadlock the runtime. Retry immediately instead of backing off.
                    _disposeCts.Token.ThrowIfCancellationRequested();
                }
                else
                {
                    _task.Delay(TimeSpan.FromMilliseconds(RetryBaseDelayMs * attempt), _disposeCts.Token).GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(lastError!).Throw();
            }
        }

        ExceptionDispatchInfo.Capture(lastError!).Throw();
    }
#pragma warning restore VSTHRD103

    // Must only be called from the writer thread (or, in inline mode, from the enqueueing thread — the
    // platform serializes ConsumeAsync per data consumer, so there is only ever one). Lazy because most
    // test runs may not produce results before they hit cancellation/discovery; we don't want to provision
    // a file we never use.
    private void EnsureFileOpen()
    {
        if (_initialized)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(FilePath);
        if (!RoslynString.IsNullOrEmpty(directory) && !_fileSystem.ExistDirectory(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        // FileShare.Read so a slow / hung writer can't block ReadAll if CompleteAsync times out.
        _fileStream = _fileSystem.NewFileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_fileStream.Stream, Encoding.UTF8, leaveOpen: true);
        _initialized = true;
    }

    /// <summary>
    /// Read back all records that were durably written. Caller is responsible for ensuring the writer
    /// has completed (call <see cref="CompleteAsync"/> first) when reading from the same process.
    /// </summary>
    public IReadOnlyList<TrxTestResult> ReadAll()
    {
        if (!_fileSystem.ExistFile(FilePath))
        {
            return [];
        }

        // FileShare.ReadWrite so we can still read what was flushed even if a slow writer hasn't fully drained.
        using IFileStream stream = _fileSystem.NewFileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return [.. TrxTestResultSerializer.ReadAll(stream.Stream, _logger)];
    }

    /// <summary>
    /// Best-effort delete of the intermediate file. Failures are swallowed because the file is a
    /// crash-recovery sidecar — leaving it around is harmless and will be overwritten next run.
    /// </summary>
    public void TryDelete()
    {
        try
        {
            if (_fileSystem.ExistFile(FilePath))
            {
                _fileSystem.DeleteFile(FilePath);
            }
        }
        catch (Exception ex)
        {
            TryLogDebug($"Failed to delete TRX streaming store file '{FilePath}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_queue is null)
        {
            // Inline mode: nothing to drain and no thread to join. Blocking here would deadlock the
            // single WebAssembly thread.
            CompleteInline();
            _disposeCts.Dispose();
            return;
        }

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
        _disposeCts.Dispose();
    }

    private void LogDrop(string reason)
    {
        Interlocked.Increment(ref _droppedCount);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            TryLogDebug($"TRX streaming store dropped a record ({reason}). Total dropped: {Volatile.Read(ref _droppedCount)}.");
        }
    }

    private void TryLogDebug(string message)
    {
        try
        {
            _logger.LogDebug(message);
        }
        catch
        {
            // Logging must remain best-effort and must not change writer failure behavior.
        }
    }

    private void TryLogWarning(string message)
    {
        try
        {
            _logger.LogWarning(message);
        }
        catch
        {
            // Logging must remain best-effort and must not change writer failure behavior.
        }
    }

    private void TryLogError(string message, Exception ex)
    {
        try
        {
            _logger.LogError(message, ex);
        }
        catch
        {
            // Logging must remain best-effort and must not change writer failure behavior.
        }
    }
}

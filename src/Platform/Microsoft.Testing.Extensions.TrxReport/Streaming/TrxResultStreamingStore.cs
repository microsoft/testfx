// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

/// <summary>
/// Append-only store that streams <see cref="TrxTestResult"/> records to a sidecar file. On multithreaded
/// runtimes, producers (test result consumers) enqueue without blocking on disk I/O: a single background
/// writer task drains a <see cref="BlockingCollection{T}"/>, batching records by size or time window and
/// flushing to disk so a crash after the flush leaves a recoverable file on disk.
/// </summary>
/// <remarks>
/// <para>
/// Single-threaded WebAssembly runtimes (<c>browser-wasm</c>, <c>wasi-wasm</c>) cannot create the dedicated
/// writer thread and cannot block on the queue, so the store switches to an <em>inline</em> mode: records are
/// serialized synchronously on the calling thread as they are enqueued. The on-disk format, retry/truncation
/// behavior, and drop accounting are identical; only the hand-off is different.
/// </para>
/// <para>
/// The implementation is split across partials: <c>BackgroundWriter</c> (queue and writer task),
/// <c>InlineWriter</c> (synchronous path), <c>FileIO</c> (batched writes, retries, truncation) and
/// <c>Logging</c> (best-effort log helpers). This file holds the public surface and lifecycle orchestration.
/// </para>
/// </remarks>
internal sealed partial class TrxResultStreamingStore : IDisposable
{
    // Tuned for typical test runs. A large enough batch to amortize syscalls, small enough to flush
    // often so a crash loses at most one window of results.
    private const int DefaultBatchSize = 64;
    private const int DefaultFlushIntervalMs = 500;

    private readonly IFileSystem _fileSystem;
    private readonly ITask _task;
    private readonly ILogger _logger;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;
    private readonly CancellationTokenSource _disposeCts = new();
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

        if (useBackgroundWriter)
        {
            StartBackgroundWriter();
        }
        else
        {
            InitializeInline();
        }
    }

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
        if (IsInline)
        {
            EnqueueInline(result);
        }
        else
        {
            EnqueueToQueue(result);
        }
    }

    /// <summary>
    /// Signal completion and wait for the writer task to drain. Bounded by the platform hang timeout
    /// so a stuck writer (slow network drive, locked file) cannot hang the session. In inline mode there
    /// is nothing to drain, so this only closes the file.
    /// </summary>
    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        if (IsInline)
        {
            CompleteInline();
            return;
        }

        await CompleteBackgroundWriterAsync(cancellationToken).ConfigureAwait(false);
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
        if (IsInline)
        {
            // Inline mode: nothing to drain and no thread to join. Blocking here would deadlock the
            // single WebAssembly thread.
            CompleteInline();
        }
        else
        {
            DisposeBackgroundWriter();
        }

        _disposeCts.Dispose();
    }
}

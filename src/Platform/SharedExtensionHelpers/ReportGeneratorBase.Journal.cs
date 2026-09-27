// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Reporter lifecycle internals are shared-source implementation detail, not package API.
#pragma warning disable CA1416 // BlockingCollection is unreachable on single-threaded browser/WASI runtimes.

internal abstract partial class ReportGeneratorBase<TGenerator, TCapturedTestResult>
    where TGenerator : ReportGeneratorBase<TGenerator, TCapturedTestResult>
    where TCapturedTestResult : class
{
    private const int JournalBatchSize = 64;
    private const int JournalCompletionReserveBytes = 64 * 1024;
    private const int JournalFlushIntervalMilliseconds = 500;
    private const int JournalQueueCapacity = 4096;
    private static readonly TimeSpan JournalShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly string? _journalPath;

    private IFileStream? _journalStream;
    private BlockingCollection<ReportJournalRecord<TCapturedTestResult>>? _journalQueue;
    private Task? _journalWriterTask;
    private volatile bool _journalFailed;
    private volatile bool _journalBudgetExceeded;
    private volatile bool _journalCompletionTimedOut;
    private volatile bool _writeCompletionRecord;
    private string? _completedReportFileName;
    private long _journalBytesWritten;
    private int _journalRecordsWritten;
    private int _droppedJournalRecords;
    private bool _disposed;

    internal async Task<(string FileName, string? Warning)> GenerateRecoveredReportAsync(
        ReportJournalReadResult<TCapturedTestResult> journal,
        int exitCode,
        CancellationToken cancellationToken)
    {
        foreach (ReportJournalParentEntry parent in journal.Parents)
        {
            RestoreParentEntry(parent);
        }

        return await GenerateReportAsync(
            [.. journal.Results],
            journal.Metadata.StartTime,
            exitCode,
            cancellationToken).ConfigureAwait(false);
    }

    private void StartJournalWriter()
    {
#pragma warning disable IDE0028 // Explicit ConcurrentQueue documents FIFO ordering.
        _journalQueue = new(new ConcurrentQueue<ReportJournalRecord<TCapturedTestResult>>(), JournalQueueCapacity);
#pragma warning restore IDE0028
        _journalWriterTask = _task.RunLongRunning(WriteJournalLoopAsync, $"{DisplayName} recovery journal writer", CancellationToken.None);
    }

    private void EnqueueJournalRecord(
        ReportJournalRecord<TCapturedTestResult> record,
        bool inlineAlreadyWritten = false)
    {
        if (inlineAlreadyWritten || _journalFailed)
        {
            return;
        }

        if (_journalBudgetExceeded)
        {
            RecordDroppedJournalRecords(1, "its configured size or record limit was reached");
            return;
        }

        if (_journalQueue is null)
        {
            TryWriteJournalBatch([record]);
            return;
        }

        try
        {
            if (_journalQueue.IsAddingCompleted || !_journalQueue.TryAdd(record))
            {
                RecordDroppedJournalRecords(1, "its writer queue is full or unavailable");
            }
        }
        catch (ObjectDisposedException)
        {
            RecordDroppedJournalRecords(1, "its writer queue is full or unavailable");
        }
        catch (InvalidOperationException)
        {
            RecordDroppedJournalRecords(1, "its writer queue is full or unavailable");
        }
    }

    private void RecordDroppedJournalRecords(int count, string reason)
    {
        if (Interlocked.Add(ref _droppedJournalRecords, count) == count)
        {
            _logger.LogWarning(
                $"Report recovery journal '{_journalPath}' dropped records because {reason}. Normal report generation is unaffected; crash recovery may be partial.");
        }
    }

    private async Task WriteJournalLoopAsync()
    {
        ApplicationStateGuard.Ensure(_journalQueue is not null);
        var batch = new List<ReportJournalRecord<TCapturedTestResult>>(JournalBatchSize);
        try
        {
            while (!_journalQueue.IsCompleted)
            {
                if (!_journalQueue.TryTake(out ReportJournalRecord<TCapturedTestResult>? first, JournalFlushIntervalMilliseconds))
                {
                    continue;
                }

                batch.Add(first);
                while (batch.Count < JournalBatchSize
                    && _journalQueue.TryTake(out ReportJournalRecord<TCapturedTestResult>? next))
                {
                    batch.Add(next);
                }

                await WriteJournalBatchAsync(batch).ConfigureAwait(false);
                batch.Clear();
            }

            if (_writeCompletionRecord)
            {
                ApplicationStateGuard.Ensure(_completedReportFileName is not null);
                await WriteJournalBatchAsync(
                    [ReportJournalRecord<TCapturedTestResult>.CreateCompletion(
                        _completedReportFileName,
                        Volatile.Read(ref _droppedJournalRecords))],
                    isCompletion: true).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _journalFailed = true;
            int dropped = batch.Count + DrainJournalQueue();
            Interlocked.Add(ref _droppedJournalRecords, dropped);
            await LogJournalFailureAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            DisposeJournalStream();
        }
    }

    private void TryWriteJournalBatch(
        IReadOnlyList<ReportJournalRecord<TCapturedTestResult>> records,
        bool isCompletion = false)
    {
        try
        {
            WriteJournalBatchAsync(records, isCompletion).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _journalFailed = true;
            _ = LogJournalFailureAsync(ex);
            DisposeJournalStream();
        }
    }

    private async Task WriteJournalBatchAsync(
        IReadOnlyList<ReportJournalRecord<TCapturedTestResult>> records,
        bool isCompletion = false)
    {
        _journalStream ??= FileSystem.NewFileStream(
            _journalPath!,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);

        var batch = new StringBuilder();
        int accepted = 0;
        long maxBytes = ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult>.MaxJournalBytes
            - (isCompletion ? 0 : JournalCompletionReserveBytes);
        int maxRecords = ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult>.MaxJournalRecordCount
            - (isCompletion ? 0 : 1);
        foreach (ReportJournalRecord<TCapturedTestResult> record in records)
        {
            string serializedRecord = SerializeJournalRecord(record);
            int recordBytes = Encoding.UTF8.GetByteCount(serializedRecord) + 1;
            if (recordBytes > ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult>.MaxJournalRecordBytes
                || _journalBytesWritten + recordBytes > maxBytes
                || _journalRecordsWritten >= maxRecords)
            {
                _journalBudgetExceeded = true;
                RecordDroppedJournalRecords(records.Count - accepted, "its configured size or record limit was reached");
                break;
            }

            batch.Append(serializedRecord).Append('\n');
            _journalBytesWritten += recordBytes;
            _journalRecordsWritten++;
            accepted++;
        }

        if (batch.Length == 0)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(batch.ToString());
#if NETCOREAPP
        await _journalStream.Stream.WriteAsync(bytes.AsMemory(), CancellationToken.None).ConfigureAwait(false);
#else
        await _journalStream.Stream.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None).ConfigureAwait(false);
#endif
        await _journalStream.Stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task CompleteJournalAsync(
        bool writeCompletionRecord,
        string? reportFileName,
        CancellationToken cancellationToken)
    {
        if (_journalPath is null || _journalCompletionTimedOut)
        {
            return;
        }

        if (_journalQueue is null)
        {
            if (writeCompletionRecord && !_journalFailed)
            {
                ApplicationStateGuard.Ensure(reportFileName is not null);
                TryWriteJournalBatch(
                    [ReportJournalRecord<TCapturedTestResult>.CreateCompletion(
                        reportFileName,
                        Volatile.Read(ref _droppedJournalRecords))],
                    isCompletion: true);
            }

            DisposeJournalStream();
            return;
        }

        if (!_journalQueue.IsAddingCompleted)
        {
            _writeCompletionRecord = writeCompletionRecord;
            _completedReportFileName = reportFileName;
            _journalQueue.CompleteAdding();
        }

        if (_journalWriterTask is not null)
        {
            Task timeout = _task.Delay(JournalShutdownTimeout, cancellationToken);
            Task completed = await Task.WhenAny(_journalWriterTask, timeout).ConfigureAwait(false);
            if (completed != _journalWriterTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _journalCompletionTimedOut = true;
                await _logger.LogWarningAsync(
                    $"Report recovery journal '{_journalPath}' writer did not drain within the hang timeout. Normal report generation is unaffected, but crash recovery may be partial.").ConfigureAwait(false);
                return;
            }

            await _journalWriterTask.ConfigureAwait(false);
        }

        int dropped = Volatile.Read(ref _droppedJournalRecords);
        if (dropped > 0)
        {
            await _logger.LogWarningAsync(
                $"Report recovery journal '{_journalPath}' dropped {dropped} record(s) because its bounded writer queue was full or unavailable, or its safety limits were reached. Normal report generation is unaffected, but crash recovery may be partial.").ConfigureAwait(false);
        }
    }

    private int DrainJournalQueue()
    {
        if (_journalQueue is null)
        {
            return 0;
        }

        if (!_journalQueue.IsAddingCompleted)
        {
            _journalQueue.CompleteAdding();
        }

        int count = 0;
        while (_journalQueue.TryTake(out _))
        {
            count++;
        }

        return count;
    }

    private Task LogJournalFailureAsync(Exception exception)
        => _logger.LogWarningAsync(
            $"Report recovery journal '{_journalPath}' could not be written. The report will still be generated in process, but it might not be recoverable after a crash. Reason: {exception.Message}");

    private void DisposeJournalStream()
    {
        IFileStream? stream = Interlocked.Exchange(ref _journalStream, null);
        try
        {
            stream?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to close report recovery journal '{_journalPath}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_journalQueue is not null)
        {
            if (!_journalQueue.IsAddingCompleted)
            {
                _journalQueue.CompleteAdding();
            }

            if (_journalCompletionTimedOut)
            {
                _disposed = true;
                return;
            }

            try
            {
                if (_journalWriterTask is not null
                    && !_journalWriterTask.Wait(JournalShutdownTimeout))
                {
                    _journalCompletionTimedOut = true;
                    _logger.LogWarning(
                        $"Report recovery journal '{_journalPath}' writer did not stop within the hang timeout during disposal. The writer will be abandoned until process exit.");
                    _disposed = true;
                    return;
                }

                _journalWriterTask?.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to drain report recovery journal '{_journalPath}' during disposal: {ex.Message}");
            }

            _journalQueue.Dispose();
        }
        else
        {
            DisposeJournalStream();
        }

        _disposed = true;
    }
}

#pragma warning restore RS0051
#pragma warning restore CA1416

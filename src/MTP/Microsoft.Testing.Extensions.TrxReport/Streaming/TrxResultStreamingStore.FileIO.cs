// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

/// <content>
/// Low-level file I/O shared by both write paths: lazy file provisioning, batched record writes, the
/// bounded retry policy and the truncate-to-last-good-record recovery.
/// </content>
internal sealed partial class TrxResultStreamingStore
{
    private const int MaxWriteRetries = 3;
    private const int RetryBaseDelayMs = 50;

    private IFileStream? _fileStream;
    private BinaryWriter? _writer;
    private bool _initialized;

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

#pragma warning disable VSTHRD103 // The writer runs on a dedicated long-running thread; synchronous waits keep queue polling off the threadpool.
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
                if (IsInline)
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
}

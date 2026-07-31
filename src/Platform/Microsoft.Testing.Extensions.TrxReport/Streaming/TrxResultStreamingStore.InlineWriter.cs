// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

/// <content>
/// The inline (single-threaded / WebAssembly) write path: records are serialized synchronously on the
/// enqueueing thread because the runtime cannot host a dedicated writer thread.
/// </content>
internal sealed partial class TrxResultStreamingStore
{
    // Reused single-record buffer for inline writes so enqueueing does not allocate a list per record.
    // Written exactly once, during construction, from InitializeInline.
    private List<TrxTestResult>? _inlineBatch;
    private bool _inlineCompleted;

    /// <summary>
    /// Gets a value indicating whether records are serialized synchronously on the enqueueing thread
    /// instead of being handed to a background writer. True on single-threaded WebAssembly runtimes,
    /// where <see cref="RuntimeFeatureHelper.IsMultiThreaded"/> is false.
    /// </summary>
    internal bool IsInline => _inlineBatch is not null;

    // Inline mode: no queue, no thread, no blocking wait. Enqueue serializes on the caller.
    private void InitializeInline()
        => _inlineBatch = [];

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

    private void CompleteInline()
    {
        if (_inlineCompleted)
        {
            return;
        }

        _inlineCompleted = true;
        CloseFile();
    }
}

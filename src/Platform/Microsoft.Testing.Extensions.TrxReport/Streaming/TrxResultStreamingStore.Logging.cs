// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

/// <content>
/// Logging helpers. Logging is best-effort: a failing logger must never change the behavior of the
/// writer, so every helper swallows exceptions.
/// </content>
internal sealed partial class TrxResultStreamingStore
{
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

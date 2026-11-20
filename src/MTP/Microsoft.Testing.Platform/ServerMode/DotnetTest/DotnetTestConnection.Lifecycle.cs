// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform;

internal sealed partial class DotnetTestConnection
{
    public async Task OnExitAsync()
    {
#if NET
        if (_serverControlListenerCts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }
#else
#pragma warning disable VSTHRD103 // CancellationTokenSource.CancelAsync is not available on this target framework.
        _serverControlListenerCts?.Cancel();
#pragma warning restore VSTHRD103
#endif

        // Cancelling the token is enough to abort an in-flight named-pipe read on modern .NET, but on .NET
        // Framework cancellation of an already-parked overlapped read is not reliable - disposing the stream is
        // what forces it to unblock. We cancelled first (above) so the listener treats the resulting failure as
        // shutdown rather than "host gone => cancel".
        if (!OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi())
        {
            _serverControlPipeClient?.Dispose();
        }

        if (_serverControlListenerTask is { } listenerTask)
        {
            // Bounded wait so a stuck listener can never hang the exit path on any target framework.
            Task completed = await Task.WhenAny(listenerTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (completed != listenerTask)
            {
                await TryLogAsync(LogLevel.Debug, $"Server control pipe '{_serverControlPipeName}' listener did not finish within the 5s bounded wait during exit; continuing without waiting further.").ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _serverControlListenerCts?.Cancel();

        // Disposing the pipe client forces a parked read to abort even where token cancellation alone would not.
        if (!OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi())
        {
            _serverControlPipeClient?.Dispose();
        }

        if (_serverControlListenerTask is { } listenerTask)
        {
            try
            {
                // Bounded wait: the cancel + dispose above unblock the parked read quickly. Never hang exit.
                listenerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (ex is AggregateException or ObjectDisposedException)
            {
                // Best-effort shutdown. Logging providers have already been disposed by this phase; diagnostics
                // belong in OnExitAsync, which runs while logging is still available.
            }
        }

        _serverControlListenerCts?.Dispose();
        _transportClient?.Dispose();
    }

    private async Task TryLogAsync(LogLevel logLevel, string message)
    {
        var loggingTask = Task.Run(async () =>
        {
            try
            {
                await _logger.LogAsync(logLevel, message, null, LoggingExtensions.Formatter).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Control-channel diagnostics must never alter connection, cancellation, or shutdown behavior.
            }
        });

        // Isolate synchronous provider work and bound providers that return a task which never completes.
        await Task.WhenAny(loggingTask, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }
}

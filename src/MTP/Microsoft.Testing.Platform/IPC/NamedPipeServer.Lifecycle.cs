// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class NamedPipeServer
{
    internal async Task<bool> WaitForDisconnectAsync(TimeSpan timeout)
    {
        bool wasConnected;
        Task? loopTask;
        lock (_lifecycleSync)
        {
            wasConnected = WasConnected;
            loopTask = _loopTask;
        }

        if (!wasConnected)
        {
            return true;
        }

        if (loopTask is null)
        {
            return false;
        }

        Task completedTask = await Task.WhenAny(loopTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (completedTask != loopTask)
        {
            return false;
        }

        await loopTask.ConfigureAwait(false);
        return true;
    }

    public void Dispose()
    {
        Task? loopTask;
        bool wasConnected;
        lock (_lifecycleSync)
        {
            // Stryker disable once Block: every owned resource below has idempotent disposal.
            if (_disposed)
            {
                // Stryker disable once Statement: every owned resource below also has idempotent disposal.
                return;
            }

            _disposed = true;
            wasConnected = WasConnected;
            loopTask = _loopTask;
        }

        if (wasConnected)
        {
#if !NET
            // On .NET Framework (netstandard2.0 build), PipeStream.ReadAsync(byte[], int, int, CancellationToken)
            // does not reliably interrupt the underlying blocking read when only the cancellation token is
            // canceled. Force-closing the stream here unblocks that pending read. On modern .NET the read uses
            // the Memory<byte>-based overload, which honors cancellation without requiring the stream to be
            // disposed mid-operation; eagerly disposing it there would race with the in-flight cancellation and
            // can surface to the connected client as an abrupt reset instead of a graceful disconnect.
            if (_cancellationToken.IsCancellationRequested)
            {
                _namedPipeServerStream.Dispose();
            }
#endif

            // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
            if (loopTask is not null && !loopTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                _logger.LogError($"NamedPipeServer.Dispose: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={loopTask.Status}.");
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();
    }

#if NET
    public async ValueTask DisposeAsync()
    {
        Task? loopTask;
        bool wasConnected;
        lock (_lifecycleSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            wasConnected = WasConnected;
            loopTask = _loopTask;
        }

        if (wasConnected)
        {
            // Unlike the netstandard2.0 build used by Dispose(), this method is only compiled for modern .NET,
            // where PipeStream.ReadAsync(Memory<byte>, CancellationToken) honors cancellation without needing the
            // stream to be force-disposed mid-operation. Eagerly disposing it here would race with the in-flight
            // cancellation of the pending read and can surface to the connected client as an abrupt reset instead
            // of a graceful disconnect.
            if (loopTask is not null)
            {
                try
                {
                    // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
                    await loopTask.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await _logger.LogErrorAsync($"NamedPipeServer.DisposeAsync: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={loopTask.Status}.").ConfigureAwait(false);
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                        nameof(InternalLoopAsync)));
                }
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();
    }
#endif
}

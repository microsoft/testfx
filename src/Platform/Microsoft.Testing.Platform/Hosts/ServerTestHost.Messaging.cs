// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    private bool _showMessage;

    private async Task SendErrorAsync(
        int reqId,
        int errorCode,
        string message,
        object? data,
        CancellationToken cancellationToken,
        string? stringId = null)
    {
        AssertInitialized();
        ErrorMessage error = new(reqId, errorCode, message, data) { StringId = stringId };

        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteMessageAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendResponseAsync(
        int reqId,
        object result,
        CancellationToken cancellationToken,
        string? stringId = null)
    {
        AssertInitialized();
        ResponseMessage response = new(reqId, result) { StringId = stringId };

        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteMessageAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private void EndOutputConnection()
    {
        Interlocked.Exchange(ref _outputConnectionClosed, 1);
        ServiceProvider.GetRequiredService<ProxyOutputDevice>().EndConnection();
    }

    private async Task WriteMessageAsync(RpcMessage message, CancellationToken cancellationToken)
    {
        AssertInitialized();
        try
        {
            await _messageHandler.WriteRequestAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            EndOutputConnection();
            throw;
        }
    }

    private async Task<bool> SendMessageAsync(string method, object? @params, CancellationToken cancellationToken, bool checkServerExit = false, bool rethrowException = true)
    {
        if (checkServerExit && (Volatile.Read(ref _outputConnectionClosed) != 0 || _messageHandlerStopPlusGlobalTokenSource.IsCancellationRequested))
        {
            return false;
        }

        _requestCounter.AddCount();
        try
        {
            NotificationMessage notification = new(method, @params);

            using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (checkServerExit && Volatile.Read(ref _outputConnectionClosed) != 0)
                {
                    return false;
                }

                AssertInitialized();
                await WriteMessageAsync(notification, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            if (rethrowException)
            {
                throw;
            }

            // This path is only reachable for best-effort forwarding (checkServerExit: true call sites, e.g.
            // log/telemetry forwarding to the client) where the caller explicitly opted out of propagating the
            // failure. Log it so a silently dropped message stays diagnosable, without escalating expected
            // cancellation/shutdown noise above Trace.
            if (ex is OperationCanceledException)
            {
                QueueLog(LogLevel.Trace, $"Suppressed cancellation while sending '{method}': {ex}");
            }
            else
            {
                QueueLog(LogLevel.Debug, $"Suppressed failure while sending '{method}': {ex}");
            }

            return false;
        }
        finally
        {
            _requestCounter.Signal();
        }
    }

    private async Task TryLogAsync(LogLevel logLevel, string message)
    {
        try
        {
            await _logger.LogAsync(logLevel, message, null, LoggingExtensions.Formatter).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Diagnostics emitted from best-effort paths must not change their suppression behavior.
        }
    }

    private void QueueLog(LogLevel logLevel, string message)
        => _ = Task.Run(() => TryLogAsync(logLevel, message));

    internal Task SendTestUpdateCompleteAsync(Guid runId, CancellationToken cancellationToken)
        => SendTestUpdateCompleteAsync(runId, cancellationToken, bestEffort: false);

    private Task SendTestUpdateCompleteAsync(Guid runId, CancellationToken cancellationToken, bool bestEffort)
        => SendTestUpdateAsync(new TestNodeStateChangedEventArgs(runId, Changes: null), cancellationToken, bestEffort);

    public Task SendTestUpdateAsync(TestNodeStateChangedEventArgs update, CancellationToken cancellationToken)
        => SendTestUpdateAsync(update, cancellationToken, bestEffort: false);

    private Task SendTestUpdateAsync(TestNodeStateChangedEventArgs update, CancellationToken cancellationToken, bool bestEffort)
        => SendMessageAsync(
            method: JsonRpcMethods.TestingTestUpdatesTests,
            @params: update,
            cancellationToken,
            checkServerExit: bestEffort,
            rethrowException: !bestEffort);

    public Task SendTelemetryEventUpdateAsync(TelemetryEventArgs args, CancellationToken cancellationToken)
        => SendMessageAsync(
            method: JsonRpcMethods.TelemetryUpdate,
            @params: args,
            cancellationToken);

    public async Task PushDataAsync(IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case ServerLogMessage logMessage:
                await TryPushLogAsync(logMessage, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    internal Task<bool> TryPushLogAsync(ServerLogMessage message, CancellationToken cancellationToken)
        => SendMessageAsync(
            method: _showMessage ? JsonRpcMethods.ClientShowMessage : JsonRpcMethods.ClientLog,
            @params: new LogEventArgs(message),
            cancellationToken,
            checkServerExit: true,
            rethrowException: false);

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
}

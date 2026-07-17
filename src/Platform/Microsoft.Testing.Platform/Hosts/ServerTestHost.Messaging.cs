// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    private async Task SendErrorAsync(int reqId, int errorCode, string message, object? data, CancellationToken cancellationToken)
    {
        AssertInitialized();
        ErrorMessage error = new(reqId, errorCode, message, data);

        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await _messageHandler.WriteRequestAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendResponseAsync(int reqId, object result, CancellationToken cancellationToken)
    {
        AssertInitialized();
        ResponseMessage response = new(reqId, result);

        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await _messageHandler.WriteRequestAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendMessageAsync(string method, object? @params, CancellationToken cancellationToken, bool checkServerExit = false, bool rethrowException = true)
    {
        if (checkServerExit && _messageHandlerStopPlusGlobalTokenSource.IsCancellationRequested)
        {
            return;
        }

        _requestCounter.AddCount();
        try
        {
            NotificationMessage notification = new(method, @params);

            using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                AssertInitialized();
                await _messageHandler.WriteRequestAsync(notification, cancellationToken).ConfigureAwait(false);
            }
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
        => SendTestUpdateAsync(new TestNodeStateChangedEventArgs(runId, Changes: null), cancellationToken);

    public Task SendTestUpdateAsync(TestNodeStateChangedEventArgs update, CancellationToken cancellationToken)
        => SendMessageAsync(
            method: JsonRpcMethods.TestingTestUpdatesTests,
            @params: update,
            cancellationToken);

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
                await SendMessageAsync(
                    method: JsonRpcMethods.ClientLog,
                    @params: new LogEventArgs(logMessage),
                    cancellationToken,
                    checkServerExit: true,
                    rethrowException: false).ConfigureAwait(false);
                break;
        }
    }

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
}

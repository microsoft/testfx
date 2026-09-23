// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    /// <summary>
    /// The main server loop.
    /// It receives messages from the client and then runs a corresponding handler.
    /// </summary>
    private async Task HandleMessagesAsync(CancellationToken cancellationToken)
    {
        AssertInitialized();

        CancellationToken messageHandlerStopPlusGlobalToken = _messageHandlerStopPlusGlobalTokenSource.Token;
        while (!_messageHandlerStopPlusGlobalTokenSource.IsCancellationRequested)
        {
            try
            {
                RpcMessage? message = await _messageHandler.ReadAsync(messageHandlerStopPlusGlobalToken).ConfigureAwait(false);

                // In case of issue on underneath handler we expect a null rpc message to signal that we should close
                // because we're no more able to process things.
                if (message is null)
                {
                    return;
                }

                // Signal that we have to handle this request
                _requestCounter.AddCount();

                if (message is NotificationMessage { Method: JsonRpcMethods.Exit })
                {
                    // Signal only one time
                    if (!_serverClosingTokenSource.IsCancellationRequested)
                    {
                        await _logger.LogDebugAsync("Server requested to shutdown").ConfigureAwait(false);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                        _serverClosingTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                    }

                    // Signal the exit call
                    _requestCounter.Signal();

                    // If there're no in-flight request we can close the server
                    if (_clientToServerRequests.IsEmpty)
                    {
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                        _stopMessageHandler.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                    }

                    continue;
                }

                // Note: Handle the requests and notifications asynchronously, so that
                // we can keep reading further messages.
                // For instance we should be able to handle a cancellation request
                // while a discovery request is being handled.
                switch (message)
                {
                    case RequestMessage request:
                        // This task is recorded inside the _clientToServerRequests
                        _ = HandleRequestAsync(request, _serverClosingTokenSource.Token, cancellationToken);
                        break;

                    case NotificationMessage notification:
                        // This task is recorded inside the _clientToServerRequests
                        // Cancellation is applied synchronously so queued requests observe it before they resume.
                        _ = HandleNotificationAsync(notification, _serverClosingTokenSource.Token);
                        break;
                    case ResponseMessage response:
                        CompleteRequest(
                            ref _serverToClientRequests,
                            GetRequestKey(response.Id, response.StringId),
                            completion => completion.TrySetResult(response));
                        break;

                    case ErrorMessage error:
                        RemoteInvocationException exception = new(error.ErrorCode, error.Message, error.Data);
                        CompleteRequest(
                            ref _serverToClientRequests,
                            GetRequestKey(error.Id, error.StringId),
                            completion => completion.TrySetException(exception));
                        break;
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == messageHandlerStopPlusGlobalToken)
            {
                // We're shutting down the reader
            }
        }

        // subtract the default count
        _requestCounter.Signal();

        // Wait to drain all in-flight requests HandleRequestCoreAsync/CompleteRequest
        await _requestCounter.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, CancellationToken.None).ConfigureAwait(false);
    }
}

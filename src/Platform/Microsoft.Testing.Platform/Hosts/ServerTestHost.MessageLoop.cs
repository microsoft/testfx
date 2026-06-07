// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

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
                        _ = HandleNotificationAsync(notification, _serverClosingTokenSource.Token);
                        break;
                    case ResponseMessage response:
                        CompleteRequest(ref _serverToClientRequests, response.Id, completion => completion.TrySetResult(response));
                        break;

                    case ErrorMessage error:
                        RemoteInvocationException exception = new(error.ErrorCode, error.Message, error.Data);
                        CompleteRequest(ref _serverToClientRequests, error.Id, completion => completion.TrySetException(exception));
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

    private async Task HandleNotificationAsync(NotificationMessage message, CancellationToken serverClosing)
    {
        // We need to guarantee that all notification received before the "exit" are handled.
        // We check it before to "enqueue" the task that handle it
        if (serverClosing.IsCancellationRequested)
        {
            try
            {
                // We're closing we don't handle the "new notification"
                return;
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }

        // Note: Yield, so that the main message reading loop can continue.
        await Task.Yield();

        try
        {
            switch (message.Method, message.Params)
            {
                case (JsonRpcMethods.CancelRequest, CancelRequestArgs args):
                    if (_clientToServerRequests.TryGetValue(args.CancelRequestId, out RpcInvocationState? rpcState))
                    {
                        Exception? cancellationException = rpcState.CancelRequest();
                        if (cancellationException is not null)
                        {
                            // This is intentionally not using PlatformResources.ExceptionDuringCancellationWarningMessage
                            // It's meant for troubleshooting and shouldn't be localized.
                            // The localized message that is user-facing will be displayed in the DisplayAsync call next line.
                            await _logger.LogWarningAsync($"Exception during the cancellation of request id '{args.CancelRequestId}'").ConfigureAwait(false);

                            await ServiceProvider.GetOutputDevice().DisplayAsync(
                                this,
                                new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExceptionDuringCancellationWarningMessage, args.CancelRequestId)), serverClosing).ConfigureAwait(false);
                        }
                    }

                    break;
            }
        }
        finally
        {
            // Signal the notification
            _requestCounter.Signal();
        }
    }

    private async Task HandleRequestAsync(RequestMessage request, CancellationToken serverClosing, CancellationToken cancellationToken)
    {
        // We're closing so we don't handle anymore any requests
        if (serverClosing.IsCancellationRequested)
        {
            try
            {
                await SendErrorAsync(reqId: request.Id, errorCode: ErrorCodes.InvalidRequest, message: "Server is closing", data: null, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }
        else
        {
            // We enqueue the request before to "unlink" the current thread so we're sure that we
            // correctly handle the completion also after the "exit"
            RpcInvocationState rpcState = new();
            _clientToServerRequests.TryAdd(request.Id, rpcState);

            // Note: Yield, so that the main message reading loop can continue.
            await Task.Yield();

            try
            {
                object response = await HandleRequestCoreAsync(request, rpcState, cancellationToken).ConfigureAwait(false);
                await SendResponseAsync(reqId: request.Id, result: response, cancellationToken).ConfigureAwait(false);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.TrySetResult(response));
            }
            catch (OperationCanceledException e)
            {
                // We don't return the stack of the exception if we're canceling the single request because it's expected and it's not an exception.
                (string errorMessage, int errorCode) = rpcState.CancellationToken.IsCancellationRequested
                    ? (string.Empty, ErrorCodes.RequestCanceled)
                    : (e.ToString(), ErrorCodes.RequestCanceled);

                await SendErrorAsync(reqId: request.Id, errorCode: errorCode, message: errorMessage, data: null, cancellationToken).ConfigureAwait(false);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.TrySetCanceled());
            }
            catch (JsonRpcException e)
            {
                await SendErrorAsync(reqId: request.Id, errorCode: e.ErrorCode, message: e.Message, data: null, cancellationToken).ConfigureAwait(false);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.TrySetException(e));
            }
            catch (Exception e)
            {
                await SendErrorAsync(reqId: request.Id, errorCode: 0, message: e.ToString(), data: null, cancellationToken).ConfigureAwait(false);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.SetException(e));
            }
        }
    }

    private void CompleteRequest(
        ref ConcurrentDictionary<int, RpcInvocationState> rpcStates,
        int reqId,
        Action<TaskCompletionSource<object>> completion)
    {
        try
        {
            if (rpcStates.TryRemove(reqId, out RpcInvocationState? completedInvocation))
            {
                completion(completedInvocation.CompletionSource);
                completedInvocation.Dispose();
            }

            // If we don't have anymore rpc call to handle and "exit" was called we stop the reader and
            // we go to wait to drain the send to the clients.
            if (rpcStates.IsEmpty && _serverClosingTokenSource.IsCancellationRequested)
            {
                _stopMessageHandler.Cancel();
            }
        }
        finally
        {
            // We handled the request
            _requestCounter.Signal();
        }
    }

    private sealed class RpcInvocationState : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _cancellationTokenSourceLock = new();
#else
        private readonly object _cancellationTokenSourceLock = new();
#endif
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private volatile bool _isDisposed;

        /// <remarks>
        /// For outbound requests, this is populated with the response from the client.
        /// For inbound requests, this is set when the invoked request is completed
        /// in <see cref="HandleRequestAsync(RequestMessage, CancellationToken, CancellationToken)"/>.
        /// </remarks>
        public TaskCompletionSource<object> CompletionSource { get; } = new();

        // We don't expose directly the source because we need to synchronize the complete/cancel
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public AggregateException? CancelRequest()
        {
            if (!_isDisposed)
            {
                lock (_cancellationTokenSourceLock)
                {
                    if (!_isDisposed)
                    {
                        try
                        {
                            _cancellationTokenSource.Cancel();
                        }
                        catch (AggregateException ex)
                        {
                            // We don't want to crash the server if cancellation fails due to improper usage of token.
                            // We report it to the caller for logging purposes.
                            return ex;
                        }
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            lock (_cancellationTokenSourceLock)
            {
                if (!_isDisposed)
                {
                    _cancellationTokenSource.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }
}

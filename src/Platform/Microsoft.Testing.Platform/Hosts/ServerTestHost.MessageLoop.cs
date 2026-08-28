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

        if (Volatile.Read(ref _initializeState) != Initialized
            && message.Method != JsonRpcMethods.CancelRequest)
        {
            _requestCounter.Signal();
            return;
        }

        try
        {
            switch (message.Method, message.Params)
            {
                case (JsonRpcMethods.CancelRequest, CancelRequestArgs args):
                    if (_clientToServerRequests.TryGetValue(
                        GetRequestKey(args.CancelRequestId, args.StringId),
                        out RpcInvocationState? rpcState))
                    {
                        if (!rpcState.TryRequestCancellation())
                        {
                            break;
                        }

                        // Record cancellation synchronously so a queued request cannot resume into execution.
                        // Run token callbacks asynchronously so extension code cannot block the message reader.
                        Exception? cancellationException = await Task.Run(rpcState.CancelRequest).ConfigureAwait(false);
                        if (cancellationException is not null)
                        {
                            // This is intentionally not using PlatformResources.ExceptionDuringCancellationWarningMessage
                            // It's meant for troubleshooting and shouldn't be localized.
                            // The localized message that is user-facing will be displayed in the DisplayAsync call next line.
                            QueueLog(LogLevel.Warning, $"Exception during the cancellation of request id '{args.CancelRequestId}': {cancellationException}");

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
                await SendErrorAsync(
                    reqId: request.Id,
                    errorCode: ErrorCodes.InvalidRequest,
                    message: "Server is closing",
                    data: null,
                    cancellationToken,
                    stringId: request.StringId).ConfigureAwait(false);
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }
        else
        {
            bool isInitializeRequest = request.Method == JsonRpcMethods.Initialize;
            bool rejectRequest;
            Task<bool>? initializationTask = null;
            lock (_initializeStateLock)
            {
                if (isInitializeRequest)
                {
                    rejectRequest = _initializeState != NotInitialized;
                    if (!rejectRequest)
                    {
                        _initializeState = Initializing;
                        _initializationCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }
                else
                {
                    rejectRequest = _initializeState == NotInitialized;
                    if (_initializeState == Initializing)
                    {
                        RoslynDebug.Assert(_initializationCompletionSource is not null);
                        initializationTask = _initializationCompletionSource.Task;
                    }
                }
            }

            if (isInitializeRequest && rejectRequest)
            {
                try
                {
                    await SendErrorAsync(
                        reqId: request.Id,
                        errorCode: ErrorCodes.InvalidRequest,
                        message: "The server has already received an initialize request.",
                        data: null,
                        cancellationToken,
                        stringId: request.StringId).ConfigureAwait(false);
                }
                finally
                {
                    _requestCounter.Signal();
                }

                return;
            }

            RpcInvocationState? rpcState = null;
            bool requestRegistered = false;
            if (initializationTask is not null)
            {
                rpcState = new RpcInvocationState();
                requestRegistered = _clientToServerRequests.TryAdd(
                    GetRequestKey(request.Id, request.StringId),
                    rpcState);
                bool initialized = await initializationTask.ConfigureAwait(false);
                rejectRequest = !initialized;
            }

            if (!isInitializeRequest && rejectRequest)
            {
                try
                {
                    await SendErrorAsync(
                        reqId: request.Id,
                        errorCode: ErrorCodes.ServerNotInitialized,
                        message: "The server must be initialized before this request can be processed.",
                        data: null,
                        cancellationToken,
                        stringId: request.StringId).ConfigureAwait(false);
                }
                finally
                {
                    if (requestRegistered)
                    {
                        var exception = new JsonRpcException(
                            ErrorCodes.ServerNotInitialized,
                            "The server must be initialized before this request can be processed.");
                        CompleteRequest(
                            ref _clientToServerRequests,
                            GetRequestKey(request.Id, request.StringId),
                            completion => completion.TrySetException(exception));
                    }
                    else
                    {
                        _requestCounter.Signal();
                    }
                }

                return;
            }

            // We enqueue the request before to "unlink" the current thread so we're sure that we
            // correctly handle the completion also after the "exit"
            rpcState ??= new RpcInvocationState();
            if (!requestRegistered)
            {
                _clientToServerRequests.TryAdd(GetRequestKey(request.Id, request.StringId), rpcState);
            }

            // Note: Yield, so that the main message reading loop can continue.
            await Task.Yield();

            bool testUpdateCompletionSent = false;
            try
            {
                rpcState.ThrowIfCancellationRequested();
                object response = await HandleRequestCoreAsync(request, rpcState, cancellationToken).ConfigureAwait(false);
                testUpdateCompletionSent = await SendTestUpdateCompleteIfNeededAsync(request, cancellationToken).ConfigureAwait(false);
                await SendResponseAsync(
                    reqId: request.Id,
                    result: response,
                    cancellationToken,
                    stringId: request.StringId).ConfigureAwait(false);
                if (isInitializeRequest)
                {
                    CompleteInitialization();
                }

                CompleteRequest(
                    ref _clientToServerRequests,
                    GetRequestKey(request.Id, request.StringId),
                    completion => completion.TrySetResult(response));
            }
            catch (OperationCanceledException e)
            {
                TaskCompletionSource<bool>? failedInitialization = isInitializeRequest
                    ? MakeInitializationRetryable(
                        GetRequestKey(request.Id, request.StringId),
                        rpcState)
                    : null;
                try
                {
                    if (!testUpdateCompletionSent)
                    {
                        await SendTestUpdateCompleteIfNeededAsync(request, cancellationToken, bestEffort: true).ConfigureAwait(false);
                    }

                    // We don't return the stack of the exception if we're canceling the single request because it's expected and it's not an exception.
                    (string errorMessage, int errorCode) = rpcState.IsCancellationRequested
                        ? (string.Empty, ErrorCodes.RequestCanceled)
                        : (e.ToString(), ErrorCodes.RequestCanceled);

                    await SendErrorAsync(
                        reqId: request.Id,
                        errorCode: errorCode,
                        message: errorMessage,
                        data: null,
                        cancellationToken,
                        stringId: request.StringId).ConfigureAwait(false);
                }
                finally
                {
                    failedInitialization?.TrySetResult(false);

                    CompleteFailedRequest(
                        isInitializeRequest,
                        GetRequestKey(request.Id, request.StringId),
                        rpcState,
                        completion => completion.TrySetCanceled());
                }
            }
            catch (JsonRpcException e)
            {
                TaskCompletionSource<bool>? failedInitialization = isInitializeRequest
                    ? MakeInitializationRetryable(
                        GetRequestKey(request.Id, request.StringId),
                        rpcState)
                    : null;
                try
                {
                    if (!testUpdateCompletionSent)
                    {
                        await SendTestUpdateCompleteIfNeededAsync(request, cancellationToken, bestEffort: true).ConfigureAwait(false);
                    }

                    await SendErrorAsync(
                        reqId: request.Id,
                        errorCode: e.ErrorCode,
                        message: e.Message,
                        data: null,
                        cancellationToken,
                        stringId: request.StringId).ConfigureAwait(false);
                }
                finally
                {
                    failedInitialization?.TrySetResult(false);

                    CompleteFailedRequest(
                        isInitializeRequest,
                        GetRequestKey(request.Id, request.StringId),
                        rpcState,
                        completion => completion.TrySetException(e));
                }
            }
            catch (Exception e)
            {
                TaskCompletionSource<bool>? failedInitialization = isInitializeRequest
                    ? MakeInitializationRetryable(
                        GetRequestKey(request.Id, request.StringId),
                        rpcState)
                    : null;
                try
                {
                    if (!testUpdateCompletionSent)
                    {
                        await SendTestUpdateCompleteIfNeededAsync(request, cancellationToken, bestEffort: true).ConfigureAwait(false);
                    }

                    await SendErrorAsync(
                        reqId: request.Id,
                        errorCode: ErrorCodes.InternalError,
                        message: e.ToString(),
                        data: null,
                        cancellationToken,
                        stringId: request.StringId).ConfigureAwait(false);
                }
                finally
                {
                    failedInitialization?.TrySetResult(false);

                    CompleteFailedRequest(
                        isInitializeRequest,
                        GetRequestKey(request.Id, request.StringId),
                        rpcState,
                        completion => completion.TrySetException(e));
                }
            }
        }
    }

    private void CompleteInitialization()
    {
        TaskCompletionSource<bool>? completionSource;
        lock (_initializeStateLock)
        {
            _initializeState = Initialized;
            completionSource = _initializationCompletionSource;
            _initializationCompletionSource = null;
        }

        completionSource?.TrySetResult(true);
    }

    private TaskCompletionSource<bool>? MakeInitializationRetryable(
        (int Id, bool IsString) requestKey,
        RpcInvocationState rpcState)
    {
        lock (_initializeStateLock)
        {
            RoslynDebug.Assert(_initializeState == Initializing);
            RoslynDebug.Assert(_initializationCompletionSource is not null);
            bool requestDetached = ((ICollection<KeyValuePair<(int Id, bool IsString), RpcInvocationState>>)_clientToServerRequests)
                .Remove(new(requestKey, rpcState));
            RoslynDebug.Assert(requestDetached);
            _initializeState = NotInitialized;
            TaskCompletionSource<bool>? completionSource = _initializationCompletionSource;
            _initializationCompletionSource = null;
            return completionSource;
        }
    }

    private void CompleteFailedRequest(
        bool requestWasDetached,
        (int Id, bool IsString) requestKey,
        RpcInvocationState rpcState,
        Action<TaskCompletionSource<object>> completion)
    {
        if (!requestWasDetached)
        {
            CompleteRequest(ref _clientToServerRequests, requestKey, completion);
            return;
        }

        try
        {
            completion(rpcState.CompletionSource);
            rpcState.Dispose();
            if (_clientToServerRequests.IsEmpty && _serverClosingTokenSource.IsCancellationRequested)
            {
                _stopMessageHandler.Cancel();
            }
        }
        finally
        {
            _requestCounter.Signal();
        }
    }

    private async Task<bool> SendTestUpdateCompleteIfNeededAsync(
        RequestMessage request,
        CancellationToken cancellationToken,
        bool bestEffort = false)
    {
        if (request.Params is not RequestArgsBase args)
        {
            return false;
        }

        await SendTestUpdateCompleteAsync(args.RunId, cancellationToken, bestEffort).ConfigureAwait(false);
        return true;
    }

    private void CompleteRequest(
        ref ConcurrentDictionary<(int Id, bool IsString), RpcInvocationState> rpcStates,
        (int Id, bool IsString) requestKey,
        Action<TaskCompletionSource<object>> completion)
    {
        try
        {
            if (rpcStates.TryRemove(requestKey, out RpcInvocationState? completedInvocation))
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

    private static (int Id, bool IsString) GetRequestKey(int id, string? stringId)
        => (id, stringId is not null);

    private sealed class RpcInvocationState : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _cancellationTokenSourceLock = new();
#else
        private readonly object _cancellationTokenSourceLock = new();
#endif
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private volatile bool _isDisposed;
        private int _cancellationRequested;

        /// <remarks>
        /// For outbound requests, this is populated with the response from the client.
        /// For inbound requests, this is set when the invoked request is completed
        /// in <see cref="HandleRequestAsync(RequestMessage, CancellationToken, CancellationToken)"/>.
        /// </remarks>
        public TaskCompletionSource<object> CompletionSource { get; } = new();

        // We don't expose directly the source because we need to synchronize the complete/cancel
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public bool IsCancellationRequested
            => Volatile.Read(ref _cancellationRequested) != 0 || _cancellationTokenSource.IsCancellationRequested;

        public bool TryRequestCancellation()
            => Interlocked.Exchange(ref _cancellationRequested, 1) == 0;

        public void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
            {
                throw new OperationCanceledException(CancellationToken);
            }
        }

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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
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
                // We don't return the stack of the exception if we're canceling the single request because it's expected and it's not an exception.
                (string errorMessage, int errorCode) = rpcState.IsCancellationRequested
                    ? (string.Empty, ErrorCodes.RequestCanceled)
                    : (e.ToString(), ErrorCodes.RequestCanceled);

                await HandleRequestFailureAsync(
                    request,
                    rpcState,
                    isInitializeRequest,
                    testUpdateCompletionSent,
                    cancellationToken,
                    errorCode,
                    errorMessage,
                    completion => completion.TrySetCanceled()).ConfigureAwait(false);
            }
            catch (JsonRpcException e)
            {
                await HandleRequestFailureAsync(
                    request,
                    rpcState,
                    isInitializeRequest,
                    testUpdateCompletionSent,
                    cancellationToken,
                    e.ErrorCode,
                    e.Message,
                    completion => completion.TrySetException(e)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await HandleRequestFailureAsync(
                    request,
                    rpcState,
                    isInitializeRequest,
                    testUpdateCompletionSent,
                    cancellationToken,
                    ErrorCodes.InternalError,
                    e.ToString(),
                    completion => completion.TrySetException(e)).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleRequestFailureAsync(
        RequestMessage request,
        RpcInvocationState rpcState,
        bool isInitializeRequest,
        bool testUpdateCompletionSent,
        CancellationToken cancellationToken,
        int errorCode,
        string errorMessage,
        Action<TaskCompletionSource<object>> completion)
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
                completion);
        }
    }
}

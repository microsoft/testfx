// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
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
}

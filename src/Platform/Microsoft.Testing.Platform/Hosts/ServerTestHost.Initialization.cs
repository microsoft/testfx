// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
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
}

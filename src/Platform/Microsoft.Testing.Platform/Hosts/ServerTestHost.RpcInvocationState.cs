// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemAsyncMonitor : IAsyncMonitor, IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async Task<IDisposable> LockAsync(TimeSpan timeout)
    {
        AsyncDisposableMonitor asyncDisposableMonitor = new(_semaphoreSlim);
        await asyncDisposableMonitor.WaitAsync(timeout).ConfigureAwait(false);
        return asyncDisposableMonitor;
    }

    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken)
    {
        AsyncDisposableMonitor asyncDisposableMonitor = new(_semaphoreSlim);
        await asyncDisposableMonitor.WaitAsync(cancellationToken).ConfigureAwait(false);
        return asyncDisposableMonitor;
    }

    public void Dispose()
        => _semaphoreSlim.Dispose();

    private readonly struct AsyncDisposableMonitor(SemaphoreSlim semaphoreSlim) : IDisposable
    {
        private readonly SemaphoreSlim _semaphoreSlim = semaphoreSlim;

        public async Task WaitAsync(TimeSpan timeout)
        {
            if (!await _semaphoreSlim.WaitAsync(timeout).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Timeout of '{timeout}' while waiting for the semaphore");
            }
        }

        public async Task WaitAsync(CancellationToken cancellationToken) => await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);

        public void Dispose()
            => _semaphoreSlim.Release();
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemAsyncMonitor : IAsyncMonitor, IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async Task<IDisposable> LockAsync(TimeSpan timeout)
    {
        AsyncDisposableMonitor asyncDisposableMonitor = new(_semaphoreSlim);
        await asyncDisposableMonitor.WaitAsync(timeout);
        return asyncDisposableMonitor;
    }

    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken)
    {
        AsyncDisposableMonitor asyncDisposableMonitor = new(_semaphoreSlim);
        await asyncDisposableMonitor.WaitAsync(cancellationToken);
        return asyncDisposableMonitor;
    }

    public void Dispose()
        => _semaphoreSlim.Dispose();

    private readonly struct AsyncDisposableMonitor(SemaphoreSlim semaphoreSlim) : IDisposable
    {
        public async Task WaitAsync(TimeSpan timeout)
        {
            if (!await semaphoreSlim.WaitAsync(timeout))
            {
                throw new InvalidOperationException($"Timeout of '{timeout}' while waiting for the semaphore");
            }
        }

        public async Task WaitAsync(CancellationToken cancellationToken) => await semaphoreSlim.WaitAsync(cancellationToken);

        public void Dispose()
            => semaphoreSlim.Release();
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemSemaphore : ISemaphore
{
    private readonly SemaphoreSlim _semaphore;

    public SystemSemaphore(int initial, int maximum)
    {
        _semaphore = new(initial, maximum);
    }

    public void Dispose()
        => _semaphore.Dispose();

    public int Release()
        => _semaphore.Release();

    public bool Wait(TimeSpan timeout)
        => _semaphore.Wait(timeout);

    public Task<bool> WaitAsync(TimeSpan timeout)
        => _semaphore.WaitAsync(timeout);
}

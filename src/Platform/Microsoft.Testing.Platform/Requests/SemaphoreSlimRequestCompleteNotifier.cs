// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

internal class SemaphoreSlimRequestCompleteNotifier : IRequestCompleteNotifier
{
    private readonly SemaphoreSlim _semaphore;

    public SemaphoreSlimRequestCompleteNotifier(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public void Complete()
        => _semaphore.Release();
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface ISemaphore : IDisposable
{
    bool Wait(TimeSpan timeout);

    Task<bool> WaitAsync(TimeSpan timeout);

    int Release();
}

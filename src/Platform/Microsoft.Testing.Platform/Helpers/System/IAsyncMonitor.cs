// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IAsyncMonitor
{
    Task<IDisposable> LockAsync(CancellationToken cancellationToken);

    Task<IDisposable> LockAsync(TimeSpan timeout);
}

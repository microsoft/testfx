// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
namespace Microsoft.Testing.Platform.Helpers;

internal interface IChannel<T>
{
    bool TryWrite(T item);

    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);

    ValueTask<T> ReadAsync(CancellationToken cancellationToken = default);

    bool TryComplete(Exception? ex = null);
}
#endif

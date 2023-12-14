// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemBlockingCollection<T> : IBlockingCollection<T>
{
    private readonly BlockingCollection<T> _blockingCollection = new();

    public void Add(T item)
        => _blockingCollection.Add(item);

    public void CompleteAdding()
        => _blockingCollection.CompleteAdding();

    public IEnumerable<T> GetConsumingEnumerable()
        => _blockingCollection.GetConsumingEnumerable();

    public void Dispose()
        => _blockingCollection.Dispose();
}
#endif

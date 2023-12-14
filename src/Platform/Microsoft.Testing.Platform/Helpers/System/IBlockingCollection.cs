// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
namespace Microsoft.Testing.Platform.Helpers;

internal interface IBlockingCollection<T> : IDisposable
{
    void Add(T item);

    IEnumerable<T> GetConsumingEnumerable();

    void CompleteAdding();
}
#endif

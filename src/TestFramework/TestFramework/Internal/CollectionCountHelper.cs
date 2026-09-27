// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class CollectionCountHelper
{
    /// <summary>
    /// Gets the number of elements in <paramref name="collection"/>, preferring
    /// <see cref="ICollection{T}.Count"/> or <see cref="ICollection.Count"/> over enumerating
    /// the whole sequence when possible.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection to count.</param>
    /// <returns>The number of elements in <paramref name="collection"/>.</returns>
    internal static int GetCount<T>(IEnumerable<T> collection)
        => collection is ICollection<T> genericCollection
            ? genericCollection.Count
            : collection is ICollection nonGenericCollection
                ? nonGenericCollection.Count
                : collection.Count();
}

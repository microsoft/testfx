// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Testing.Platform.Helpers;

internal static class IEnumerableExtensions
{
    public static IReadOnlyCollection<T> ToReadOnlyCollectionEnumerator<T>(this IEnumerable<T> enumerable)
        => new ReadOnlyCollectionIEnumerableWrapper<T>(enumerable);

    private readonly struct ReadOnlyCollectionIEnumerableWrapper<T>(IEnumerable<T> enumerable) : IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _enumerable = enumerable;

        public int Count => _enumerable.Count();

        public IEnumerator<T> GetEnumerator() => _enumerable.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _enumerable.GetEnumerator();
    }
}

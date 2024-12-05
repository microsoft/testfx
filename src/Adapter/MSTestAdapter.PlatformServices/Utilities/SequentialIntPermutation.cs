// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Collections;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Permutation of integers from 0 to (numberOfObjects - 1) returned by increment of 1.
/// Used to get sequential permutation for data row access in data driven test.
/// </summary>
internal sealed class SequentialIntPermutation : IEnumerable<int>
{
    private readonly int _numberOfObjects;

    public SequentialIntPermutation(int numberOfObjects)
    {
        if (numberOfObjects < 0)
        {
            throw new ArgumentException(Resource.WrongNumberOfObjects, nameof(numberOfObjects));
        }

        _numberOfObjects = numberOfObjects;
    }

    public IEnumerator<int> GetEnumerator()
    {
        for (int i = 0; i < _numberOfObjects; ++i)
        {
            yield return i;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
#endif

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Collections;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Permutation of integers from 0 to (numberOfObjects - 1), in random order and in the end all values are returned.
/// Used to get random permutation for data row access in data driven test.
/// </summary>
internal sealed class RandomIntPermutation : IEnumerable<int>
{
    private readonly int[] _objects;

    public RandomIntPermutation(int numberOfObjects)
    {
        if (numberOfObjects < 0)
        {
            throw new ArgumentException(Resource.WrongNumberOfObjects, nameof(numberOfObjects));
        }

        _objects = new int[numberOfObjects];
        for (int i = 0; i < numberOfObjects; ++i)
        {
            _objects[i] = i;
        }

        Random random = new();
        for (int last = _objects.Length - 1; last > 0; --last)
        {
            // Swap last and at random position which can be last in which case we don't swap.
            int position = random.Next(last);   // 0 .. last - 1
            (_objects[position], _objects[last]) = (_objects[last], _objects[position]);
        }
    }

    public IEnumerator<int> GetEnumerator()
    {
        // Iterate over created permutation, do not change it.
        for (int i = 0; i < _objects.Length; ++i)
        {
            yield return _objects[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

#endif

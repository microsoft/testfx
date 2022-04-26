// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Permutation of integers from 0 to (numberOfObjects - 1), in random order and in the end all values are returned.
    /// Used to get random permutation for data row access in data driven test.
    /// </summary>
    internal class RandomIntPermutation : IEnumerable<int>
    {
        private int[] objects;

        public RandomIntPermutation(int numberOfObjects)
        {
            if (numberOfObjects < 0)
            {
                throw new ArgumentException(Resource.WrongNumberOfObjects, nameof(numberOfObjects));
            }

            objects = new int[numberOfObjects];
            for (int i = 0; i < numberOfObjects; ++i)
            {
                objects[i] = i;
            }

            Random random = new Random();
            for (int last = objects.Length - 1; last > 0; --last)
            {
                // Swap last and at random position which can be last in which case we don't swap.
                int position = random.Next(last);   // 0 .. last - 1
                int temp = objects[last];
                objects[last] = objects[position];
                objects[position] = temp;
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            // Iterate over created permutation, do not change it.
            for (int i = 0; i < objects.Length; ++i)
            {
                yield return objects[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

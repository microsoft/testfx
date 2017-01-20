// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Permutation of integers from 0 to (numberOfObjects - 1) returned by increment of 1.
    /// Used to get sequential permutation for data row access in data driven test.
    /// </summary>
    internal class SequentialIntPermutation : IEnumerable<int>
    {
        private int m_numberOfObjects;

        public SequentialIntPermutation(int numberOfObjects)
        {
            if (numberOfObjects < 0)
            {
                throw new ArgumentException(Resource.WrongNumberOfObjects, "numberOfObjects");
            }
            m_numberOfObjects = numberOfObjects;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < m_numberOfObjects; ++i)
            {
                yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Permutation of integers from 0 to (numberOfObjects - 1), in random order and in the end all values are returned.
    /// Used to get random permutation for data row access in data driven test.
    /// </summary>
    internal class RandomIntPermutation : IEnumerable<int>
    {
        private int[] m_objects;

        public RandomIntPermutation(int numberOfObjects)
        {
            if (numberOfObjects < 0)
            {
                throw new ArgumentException(Resource.WrongNumberOfObjects, "numberOfObjects");
            }

            m_objects = new int[numberOfObjects];
            for (int i = 0; i < numberOfObjects; ++i)
            {
                m_objects[i] = i;
            }

            Random random = new Random();
            for (int last = m_objects.Length - 1; last > 0; --last)
            {
                // Swap last and at random position which can be last in which case we don't swap.
                int position = random.Next(last);   // 0 .. last - 1
                int temp = m_objects[last];
                m_objects[last] = m_objects[position];
                m_objects[position] = temp;
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            // Iterate over created permutation, do not change it.
            for (int i = 0; i < m_objects.Length; ++i)
            {
                yield return m_objects[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

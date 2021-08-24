// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private int numberOfObjects;

        public SequentialIntPermutation(int numberOfObjects)
        {
            if (numberOfObjects < 0)
            {
                throw new ArgumentException(Resource.WrongNumberOfObjects, nameof(numberOfObjects));
            }

            this.numberOfObjects = numberOfObjects;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < this.numberOfObjects; ++i)
            {
                yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}

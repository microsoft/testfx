// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Extensibility.Samples
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A grouping of Assert statements with similar functionality.
    /// </summary>
    public class AssertIs
    {
        /// <summary>
        /// Determines if the 'divisor' is actually a divisor of 'number'.
        /// </summary>
        /// <param name="number">A number.</param>
        /// <param name="divisor">Its proclaimed divisor.</param>
        /// <returns>True if it is a divisor</returns>
        /// <exception cref="AssertFailedException">If it is not a divisor.</exception>
        public bool Divisor(int number, int divisor)
        {
            if (number % divisor == 0)
            {
                return true;
            }

            throw new AssertFailedException(string.Format("{0} is not a divisor of {1}", divisor, number));
        }

        /// <summary>
        /// Determines if a number is positive.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>True if it is positive.</returns>
        /// <exception cref="AssertFailedException">If the number is not positive.</exception>
        public bool Positive(int number)
        {
            if (number > 0)
            {
                return true;
            }

            throw new AssertFailedException(string.Format("{0} is not positive", number));
        }
    }
}

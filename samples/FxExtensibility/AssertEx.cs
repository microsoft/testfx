// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Extensibility.Samples
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class AssertEx
    {
        private static AssertIs assertis;

        /// <summary>
        /// A simple assert extension to validate if an object is of a given type.
        /// </summary>
        /// <typeparam name="T">The type to check.</typeparam>
        /// <param name="assert">Assert class.</param>
        /// <param name="obj">The object.</param>
        /// <returns>True if object is of the given type.</returns>
        /// <exception cref="AssertFailedException">If object is not of the given type.</exception>
        public static bool IsOfType<T>(this Assert assert, object obj)
        {
            if (obj is T)
            {
                return true;
            }

            throw new AssertFailedException(
                string.Format(
                "Expected object of type {0} but found object of type {1}",
                typeof(T),
                obj ?? obj.GetType()));
        }

        /// <summary>
        /// A chain/grouping of assert statements.
        /// </summary>
        /// <param name="assert">The Assert class.</param>
        /// <returns>The class that contains the assert methods for this grouping.</returns>
        public static AssertIs Is(this Assert assert)
        {
            if (assertis == null)
            {
                assertis = new AssertIs();
            }

            return assertis;
        }
    }
}

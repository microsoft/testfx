// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;

#pragma warning disable SA1649 // File name must match first type name

    internal class Validate
    {
        /// <summary>
        /// Throws an exception if the condition is not false
        /// </summary>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="errorMessage">Error Message to be used in the exception thrown</param>
        public static void IsFalse(bool condition, string errorMessage)
        {
            if (!condition)
            {
                return;
            }

            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// Throws an exception if the condition is not true
        /// </summary>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="errorMessage">Error Message to be used in the exception thrown</param>
        public static void IsTrue(bool condition, string errorMessage)
        {
            if (condition)
            {
                return;
            }

            throw new InvalidOperationException(errorMessage);
        }
    }
}

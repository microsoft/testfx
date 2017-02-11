// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.TestUtilities
{
    extern alias FrameworkV1;

    using System;
    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Utility function for Action
    /// </summary>
    public class ActionUtility
    {
        /// <summary>
        /// Check for a particular exception 
        /// </summary>
        /// <param name="action"> Action to invoke</param>
        /// <param name="type">Type of expected exception</param>
        public static void ActionShouldThrowExceptionOfType(Action action, Type type)
        {
            try
            {
                action.Invoke();
                TestFrameworkV1.Assert.Fail("It should throw Exception of type {0}", type);
            }
            catch (Exception ex)
            {
                TestFrameworkV1.Assert.AreEqual(type, ex.GetType());
            }
        }

        /// <summary>
        /// Check for a particular inner exception 
        /// </summary>
        /// <param name="action"> Action to invoke</param>
        /// <param name="type">Type of expected inner exception</param>
        public static void ActionShouldThrowInnerExceptionOfType(Action action, Type type)
        {
            try
            {
                action.Invoke();
                TestFrameworkV1.Assert.Fail("It should throw Exception of type {0}", type);
            }
            catch (Exception ex)
            {
                TestFrameworkV1.Assert.IsNotNull(ex.InnerException);
                TestFrameworkV1.Assert.AreEqual(type, ex.InnerException.GetType());
            }
        }

        /// <summary>
        /// Performs an action and return the exception.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>The exception thrown if any. Returns null on no exception.</returns>
        public static Exception PerformActionAndReturnException(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }
    }
}

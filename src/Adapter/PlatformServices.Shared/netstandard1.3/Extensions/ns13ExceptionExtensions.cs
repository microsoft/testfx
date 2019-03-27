// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Extension methods for the exception class.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Returns an exception message with all inner exceptions messages.
        /// </summary>
        /// <param name="exception"> The exception. </param>
        /// <returns> Custom exception message that includes inner exceptions. </returns>
        internal static string GetExceptionMessage(this Exception exception)
        {
            Debug.Assert(exception != null, "Exception is null");

            if (exception == null)
            {
                return string.Empty;
            }

            var exceptionString = exception.Message;
            var inner = exception.InnerException;
            while (inner != null)
            {
                exceptionString += Environment.NewLine + inner.Message;
                inner = inner.InnerException;
            }

            return exceptionString;
        }
    }
}

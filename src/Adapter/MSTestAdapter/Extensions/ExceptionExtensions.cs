// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using System;
    using System.Globalization;

    using Execution;
    using ObjectModel;

    /// <summary>
    /// Extensions to <see cref="Exception"/> type.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Get the InnerException if available, else return the current Exception.
        /// </summary>
        /// <returns>
        /// An <see cref="Exception"/> instance.
        /// </returns>
        internal static Exception GetInnerExceptionOrDefault(this Exception exception)
        {
            return exception?.InnerException ?? exception;
        }

        /// <summary>
        /// Get the exception message if available, empty otherwise.
        /// </summary>
        /// <param name="exception">An <see cref="Exception"/> object</param>
        /// <returns>Exception message</returns>
        internal static string TryGetMessage(this Exception exception)
        {
            if (exception == null)
            {
                return string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, "null");
            }

            // It is safe to retrieve an exception message, it should not throw in any case.
            return exception.Message ?? string.Empty;
        }

        /// <summary>
        /// Gets the <see cref="StackTraceInformation"/> for an exception.
        /// </summary>
        /// <param name="exception">An <see cref="Exception"/> instance.</param>
        /// <returns>StackTraceInformation for the exception</returns>
        internal static StackTraceInformation TryGetStackTraceInformation(this Exception exception)
        {
            if (!string.IsNullOrEmpty(exception?.StackTrace))
            {
                return StackTraceHelper.CreateStackTraceInformation(exception, false, exception.StackTrace);
            }

            return null;
        }
    }
}

// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using System;
    using System.Globalization;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
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

            try
            {
                return exception.Message ?? string.Empty;
            }
            catch (Exception ex)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTF_FailedToGetExceptionMessage,
                    ex.GetType().FullName);
            }
        }

        /// <summary>
        /// Gets the <see cref="StackTraceInformation"/> for an exception.
        /// </summary>
        /// <param name="exception">An <see cref="Exception"/> instance.</param>
        /// <returns>StackTraceInformation for the exception</returns>
        internal static StackTraceInformation TryGetStackTraceInformation(this Exception exception)
        {
            try
            {
                if (!string.IsNullOrEmpty(exception?.StackTrace))
                {
                    return StackTraceHelper.CreateStackTraceInformation(exception, false, exception.StackTrace);
                }
            }
            catch(Exception ex)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                        "UnitTestExecuter.GetExceptionStackTrace: Exception while getting stack trace for exception type '{0}'",
                        ex.GetType().FullName);
            }

            return null;
        }
    }
}

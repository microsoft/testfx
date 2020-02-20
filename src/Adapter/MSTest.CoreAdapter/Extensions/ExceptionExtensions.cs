// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using System;
    using System.Globalization;
    using System.Text;
    using Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Extensions to <see cref="Exception"/> type.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Get the InnerException if available, else return the current Exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
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

        /// <summary>
        /// Checks whether exception is an Assert exception
        /// </summary>
        /// <param name="exception">An <see cref="Exception"/> instance.</param>
        /// <param name="outcome"> Framework's Outcome depending on type of assertion.</param>
        /// <param name="exceptionMessage">Exception message.</param>
        /// <param name="exceptionStackTrace">StackTraceInformation for the exception</param>
        /// <returns>True, if Assert exception. False, otherwise.</returns>
        internal static bool TryGetUnitTestAssertException(this Exception exception, out UTF.UnitTestOutcome outcome, out string exceptionMessage, out StackTraceInformation exceptionStackTrace)
        {
            if (exception is UTF.UnitTestAssertException)
            {
                outcome = exception is UTF.AssertInconclusiveException ?
                            UTF.UnitTestOutcome.Inconclusive : UTF.UnitTestOutcome.Failed;

                exceptionMessage = exception.TryGetMessage();
                exceptionStackTrace = exception.TryGetStackTraceInformation();
                return true;
            }
            else
            {
                outcome = UTF.UnitTestOutcome.Failed;
                exceptionMessage = null;
                exceptionStackTrace = null;
                return false;
            }
        }

        /// <summary>
        /// Checks whether exception is an Assert exception
        /// </summary>
        /// <param name="exception">An <see cref="Exception"/> instance.</param>
        /// <param name="outcome"> Adapter's Outcome depending on type of assertion.</param>
        /// <param name="exceptionMessage">Exception message.</param>
        /// <param name="exceptionStackTrace">StackTraceInformation for the exception</param>
        /// <returns>True, if Assert exception. False, otherwise.</returns>
        internal static bool TryGetUnitTestAssertException(this Exception exception, out UnitTestOutcome outcome, out string exceptionMessage, out StackTraceInformation exceptionStackTrace)
        {
            if (exception is UTF.UnitTestAssertException)
            {
                outcome = exception is UTF.AssertInconclusiveException ?
                            UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;

                exceptionMessage = exception.TryGetMessage();
                exceptionStackTrace = exception.TryGetStackTraceInformation();
                return true;
            }
            else
            {
                outcome = UnitTestOutcome.Failed;
                exceptionMessage = null;
                exceptionStackTrace = null;
                return false;
            }
        }

        /// <summary>
        /// Gets exception message and stack trace for TestFailedException.
        /// </summary>
        /// <param name="testFailureException">An <see cref="TestFailedException"/> instance.</param>
        /// <param name="message"> Appends TestFailedException message to this message.</param>
        /// <param name="stackTrace"> Appends TestFailedExeption stack trace to this stackTrace</param>
        internal static void TryGetTestFailureExceptionMessageAndStackTrace(this TestFailedException testFailureException, StringBuilder message, StringBuilder stackTrace)
        {
            if (testFailureException != null)
            {
                if (!string.IsNullOrEmpty(testFailureException.Message))
                {
                    message.Append(testFailureException.Message);
                    message.AppendLine();
                }

                if (testFailureException.StackTraceInformation != null && !string.IsNullOrEmpty(testFailureException.StackTraceInformation.ErrorStackTrace))
                {
                    stackTrace.Append(testFailureException.StackTraceInformation.ErrorStackTrace);
                    stackTrace.Append(Environment.NewLine);
                }
            }
        }
    }
}

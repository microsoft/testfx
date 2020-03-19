// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    /// <summary>
    /// Provides helper methods to parse stack trace.
    /// </summary>
    internal static class StackTraceHelper
    {
        /// <summary>
        /// Type that need to be excluded.
        /// </summary>
        private static List<string> typesToBeExcluded;

        /// <summary>
        /// Gets the types whose methods should be ignored in the reported call stacks.
        /// This is used to remove our stack that the user will not care about.
        /// </summary>
        private static List<string> TypeToBeExcluded
        {
            get
            {
                if (typesToBeExcluded == null)
                {
                    typesToBeExcluded = new List<string>();
                    typesToBeExcluded.Add(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.Assert).Namespace);
                    typesToBeExcluded.Add(typeof(MSTestExecutor).Namespace);
                }

                return typesToBeExcluded;
            }
        }

        /// <summary>
        /// Gets the stack trace for an exception, including all stack traces for inner
        /// exceptions.
        /// </summary>
        /// <param name="ex">
        /// The exception.
        /// </param>
        /// <returns>
        /// The <see cref="StackTraceInformation"/> for the provided exception.
        /// </returns>
        internal static StackTraceInformation GetStackTraceInformation(Exception ex)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            Stack<string> stackTraces = new Stack<string>();

            for (Exception curException = ex;
                curException != null;
                curException = curException.InnerException)
            {
                // TODO:Fix the shadow stack-trace used in Private Object
                // (Look-in Assertion.cs in the UnitTestFramework assembly)

                // Sometimes the stack trace can be null, but the inner stack trace
                // contains information. We are not interested in null stack traces
                // so we simply ignore this case
                try
                {
                    if (curException.StackTrace != null)
                    {
                        stackTraces.Push(curException.StackTrace);
                    }
                }
                catch (Exception e)
                {
                    // curException.StackTrace can throw exception, Although MSDN doc doesn't say that.
                    try
                    {
                        // try to get stack trace
                        if (e.StackTrace != null)
                        {
                            stackTraces.Push(e.StackTrace);
                        }
                    }
                    catch (Exception)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                            "StackTraceHelper.GetStackTraceInformation: Failed to get stack trace info.");
                    }
                }
            }

            StringBuilder result = new StringBuilder();
            bool first = true;
            while (stackTraces.Count != 0)
            {
                result.Append(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} {1}{2}",
                        first ? string.Empty : (Resource.UTA_EndOfInnerExceptionTrace + Environment.NewLine),
                        stackTraces.Pop(),
                        Environment.NewLine));
                first = false;
            }

            return CreateStackTraceInformation(ex, true, result.ToString());
        }

        /// <summary>
        /// Removes all stack frames that refer to Microsoft.VisualStudio.TestTools.UnitTesting.Assertion
        /// </summary>
        /// <param name="stackTrace">
        /// The stack Trace.
        /// </param>
        /// <returns>
        /// The trimmed stack trace removing traces of the framework and adapter from the stack.
        /// </returns>
        internal static string TrimStackTrace(string stackTrace)
        {
            Debug.Assert(stackTrace != null && stackTrace.Length > 0, "stack trace should be non-empty.");

            StringBuilder result = new StringBuilder(stackTrace.Length);
            string[] stackFrames = Regex.Split(stackTrace, Environment.NewLine);

            foreach (string stackFrame in stackFrames)
            {
                if (string.IsNullOrEmpty(stackFrame))
                {
                    continue;
                }

                // Add the frame to the result if it does not refer to
                // the assertion class in the test framework
                bool hasReference = HasReferenceToUTF(stackFrame);
                if (!hasReference)
                {
                    result.Append(stackFrame);
                    result.Append(Environment.NewLine);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the exception messages, including the messages for all inner exceptions
        /// recursively
        /// </summary>
        /// <param name="ex">
        /// The exception.
        /// </param>
        /// <returns>
        /// The aggregated exception message that considers inner exceptions.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        internal static string GetExceptionMessage(Exception ex)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            StringBuilder result = new StringBuilder();
            bool first = true;
            for (Exception curException = ex;
                 curException != null;
                 curException = curException.InnerException)
            {
                // Get the exception message. Need to check for errors because the Message property
                // may have been overridden by the exception type in user code.
                string msg;
                try
                {
                    msg = curException.Message;
                }
                catch
                {
                    msg = string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, curException.GetType());
                }

                result.Append(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}{1}: {2}",
                        first ? string.Empty : " ---> ",
                        curException.GetType(),
                        msg));
                first = false;
            }

            return result.ToString();
        }

        /// <summary>
        /// Create stack trace information
        /// </summary>
        /// <param name="ex">
        /// The exception.
        /// </param>
        /// <param name="checkInnerExceptions">
        /// Whether the inner exception needs to be checked too.
        /// </param>
        /// <param name="stackTraceString">
        /// The stack Trace String.
        /// </param>
        /// <returns>
        /// The <see cref="StackTraceInformation"/>.
        /// </returns>
        internal static StackTraceInformation CreateStackTraceInformation(
            Exception ex,
            bool checkInnerExceptions,
            string stackTraceString)
        {
            if (checkInnerExceptions && ex.InnerException != null)
            {
                return CreateStackTraceInformation(ex.InnerException, checkInnerExceptions, stackTraceString);
            }

            var stackTrace = StackTraceHelper.TrimStackTrace(stackTraceString);

            if (!string.IsNullOrEmpty(stackTrace))
            {
                return new StackTraceInformation(stackTrace, null, 0, 0);
            }

            return null;
        }

        /// <summary>
        /// Returns whether the parameter stackFrame has reference to UTF
        /// </summary>
        /// <param name="stackFrame">
        /// The stack Frame.
        /// </param>
        /// <returns>
        /// True if the framework or the adapter methods are in the stack frame.
        /// </returns>
        internal static bool HasReferenceToUTF(string stackFrame)
        {
            foreach (var type in TypeToBeExcluded)
            {
                if (stackFrame.IndexOf(type, StringComparison.Ordinal) > -1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
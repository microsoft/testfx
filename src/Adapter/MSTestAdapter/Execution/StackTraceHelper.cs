// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using ObjectModel;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides helper methods to parse stack trace. 
    /// </summary>
    internal static class StackTraceHelper
    {
        /// <summary>
        /// Gets the stack trace for an exception, including all stack traces for inner
        /// exceptions.
        /// </summary>
        internal static StackTraceInformation GetStackTraceInformation(Exception ex)
        {
            Debug.Assert(ex != null);

            Stack<string> stackTraces = new Stack<string>();

            for (Exception curException = ex;
                curException != null;
                curException = curException.InnerException)
            {
                // TODO:Aseem Fix the shadow stack-trace used in Private Object 
                // (Look-in Assertion.cs in the UnitTestFramework assembly)

                // Sometimes the stacktrace can be null, but the inner stacktrace
                // contains information. We are not interested in null stacktraces
                // so we simply ignore this case
                if (curException.StackTrace != null)
                {
                    stackTraces.Push(curException.StackTrace);
                }
            }

            StringBuilder result = new StringBuilder();
            bool first = true;
            while (stackTraces.Count != 0)
            {
                result.Append(
                    string.Format(CultureInfo.CurrentCulture, "{0} {1}{2}",
                    first ? String.Empty : (Resource.UTA_EndOfInnerExceptionTrace + Environment.NewLine),
                    stackTraces.Pop(),
                    Environment.NewLine));
                first = false;
            }

            return CreateStackTraceInformation(ex, true, result.ToString());
        }

        /// <summary>
        /// Removes all stack frames that refer to Microsoft.VisualStudio.TestTools.UnitTesting.Assertion
        /// </summary>
        internal static string TrimStackTrace(string stackTrace)
        {
            Debug.Assert(stackTrace != null && stackTrace.Length > 0);

            StringBuilder result = new StringBuilder(stackTrace.Length);
            string[] stackFrames = Regex.Split(stackTrace, Environment.NewLine);

            foreach (string stackFrame in stackFrames)
            {
                if (String.IsNullOrEmpty(stackFrame))
                {
                    continue;
                }

                // Add the frame to the result if it does not refer to
                // the assertion class in the test framework
                //
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static string GetExceptionMessage(Exception ex)
        {
            Debug.Assert(ex != null);

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
                    string.Format(CultureInfo.CurrentCulture, "{0}{1}: {2}",
                        first ? String.Empty : " ---> ",
                        curException.GetType(),
                        msg));
                first = false;
            }

            return result.ToString();
        }

        /// <summary>
        /// Create stack trace information
        /// </summary>
        internal static StackTraceInformation CreateStackTraceInformation(Exception ex,
                                    bool checkInnerExceptions, string stackTraceString)
        {
#if TODO
            StackTrace stackTrace = new StackTrace(ex, true);

            StackFrame[] frames =  stackTrace.GetFrames();
            if (frames != null && frames.Length > 0)
            {
                foreach (StackFrame frame in frames)
                {
                    if (!StackTraceHelper.HasReferenceToUTF(frame))
                    {
                        string fileName = frame.GetFileName();

                        if (!String.IsNullOrEmpty(fileName) && File.Exists(fileName))
                        {
                            return new StackTraceInformation(StackTraceHelper.TrimStackTrace(stackTraceString),
                                                             fileName,
                                                             frame.GetFileLineNumber(),
                                                             frame.GetFileColumnNumber());
                        }

                    }
                }
            }
#endif

            if (checkInnerExceptions && ex.InnerException != null)
            {
                return CreateStackTraceInformation(ex.InnerException, checkInnerExceptions, stackTraceString);
            }

            return new StackTraceInformation(StackTraceHelper.TrimStackTrace(stackTraceString), null, 0, 0);
        }



        /// <summary>
        /// Returns whether the parameter stackFrame has reference to UTF
        /// </summary>
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

#if TODO
        /// <summary>
        /// Returns whether the parameter stackFrame has reference to UTF
        /// </summary>
        internal static bool HasReferenceToUTF(StackFrame stackFrame)
        {
            return HasReferenceToUTF(stackFrame.ToString());
        }
#endif

        /// <summary>
        /// Type whose methods should be ignored in the reported call stacks.
        /// This is used to remove our cruft that the user will not care about.
        /// </summary>
        private static List<string> TypeToBeExcluded
        {
            get
            {
                if (s_typeToBeExcluded == null)
                {
                    s_typeToBeExcluded = new List<string>();
                    s_typeToBeExcluded.Add(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.Assert).Namespace);
                    s_typeToBeExcluded.Add(typeof(MSTestExecutor).Namespace);
                }

                return s_typeToBeExcluded;
            }
        }

        /// <summary>
        /// Type that need to be excluded. 
        /// </summary>
        private static List<string> s_typeToBeExcluded;
    }

}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Provides helper methods to parse stack trace.
/// </summary>
internal static class ExceptionHelper
{
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
    internal static StackTraceInformation? GetStackTraceInformation(this Exception ex)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        Stack<string> stackTraces = new();

        for (Exception? curException = ex;
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

        StringBuilder result = new();
        bool first = true;
        while (stackTraces.Count != 0)
        {
            if (!first)
            {
                result.AppendLine(Resource.UTA_EndOfInnerExceptionTrace);
            }

            result.AppendLine(stackTraces.Pop());

            first = false;
        }

        return new StackTraceInformation(result.ToString(), null, 0, 0);
    }

    /// <summary>
    /// Gets the exception messages, including the messages for all inner exceptions
    /// recursively.
    /// </summary>
    /// <param name="ex">
    /// The exception.
    /// </param>
    /// <returns>
    /// The aggregated exception message that considers inner exceptions.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    internal static string GetFormattedExceptionMessage(this Exception ex)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        StringBuilder result = new();
        bool first = true;
        for (Exception? curException = ex;
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

            result.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "{0}{1}: {2}",
                    first ? string.Empty : " ---> ",
                    curException.GetType(),
                    msg);
            first = false;
        }

        return result.ToString();
    }
}

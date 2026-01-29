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

        // Special handling for AggregateException to include all inner exceptions
        if (ex is AggregateException aggregateException)
        {
            // Include the AggregateException's own stack trace if it exists
            try
            {
                if (aggregateException.StackTrace != null)
                {
                    stackTraces.Push(aggregateException.StackTrace);
                }
            }
            catch (Exception e)
            {
                try
                {
                    if (e.StackTrace != null)
                    {
                        stackTraces.Push(e.StackTrace);
                    }
                }
                catch (Exception)
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Error(
                            "StackTraceHelper.GetStackTraceInformation: Failed to get stack trace info.");
                    }
                }
            }

            // Process each inner exception
            HashSet<Exception> visitedExceptions = [aggregateException];
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (visitedExceptions.Contains(innerException))
                {
                    // Skip circular references
                    continue;
                }

                StackTraceInformation? innerStackTrace = GetStackTraceInformationHelper(innerException, visitedExceptions);
                if (innerStackTrace?.ErrorStackTrace != null)
                {
                    stackTraces.Push(innerStackTrace.ErrorStackTrace);
                }
            }
        }
        else
        {
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
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Error(
                                "StackTraceHelper.GetStackTraceInformation: Failed to get stack trace info.");
                        }
                    }
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
    /// Helper method for GetStackTraceInformation that tracks visited exceptions to prevent circular references.
    /// </summary>
    private static StackTraceInformation? GetStackTraceInformationHelper(Exception ex, HashSet<Exception> visitedExceptions)
    {
        Stack<string> stackTraces = new();

        if (ex is AggregateException aggregateException)
        {
            visitedExceptions.Add(aggregateException);

            try
            {
                if (aggregateException.StackTrace != null)
                {
                    stackTraces.Push(aggregateException.StackTrace);
                }
            }
            catch
            {
                // Ignore exceptions from StackTrace property
            }

            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (visitedExceptions.Contains(innerException))
                {
                    continue;
                }

                StackTraceInformation? innerStackTrace = GetStackTraceInformationHelper(innerException, visitedExceptions);
                if (innerStackTrace?.ErrorStackTrace != null)
                {
                    stackTraces.Push(innerStackTrace.ErrorStackTrace);
                }
            }
        }
        else
        {
            for (Exception? curException = ex;
                curException != null;
                curException = curException.InnerException)
            {
                if (visitedExceptions.Contains(curException))
                {
                    break;
                }

                visitedExceptions.Add(curException);

                try
                {
                    if (curException.StackTrace != null)
                    {
                        stackTraces.Push(curException.StackTrace);
                    }
                }
                catch
                {
                    // Ignore exceptions from StackTrace property
                }
            }
        }

        if (stackTraces.Count == 0)
        {
            return null;
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

        // Special handling for AggregateException to include all inner exceptions
        if (ex is AggregateException aggregateException)
        {
            // Include the AggregateException's own type and message
            string msg;
            try
            {
                msg = aggregateException.Message;
            }
            catch
            {
                msg = string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, aggregateException.GetType());
            }

            result.AppendFormat(
                CultureInfo.CurrentCulture,
                "{0}: {1}",
                aggregateException.GetType(),
                msg);
            first = false;

            // Process each inner exception
            HashSet<Exception> visitedExceptions = [aggregateException];
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (visitedExceptions.Contains(innerException))
                {
                    // Skip circular references
                    continue;
                }

                result.Append(" ---> ");
                result.Append(GetFormattedExceptionMessageHelper(innerException, visitedExceptions));
            }

            return result.ToString();
        }

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

    /// <summary>
    /// Helper method for GetFormattedExceptionMessage that tracks visited exceptions to prevent circular references.
    /// </summary>
    private static string GetFormattedExceptionMessageHelper(Exception ex, HashSet<Exception> visitedExceptions)
    {
        StringBuilder result = new();

        if (ex is AggregateException aggregateException)
        {
            visitedExceptions.Add(aggregateException);

            string msg;
            try
            {
                msg = aggregateException.Message;
            }
            catch
            {
                msg = string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, aggregateException.GetType());
            }

            result.AppendFormat(
                CultureInfo.CurrentCulture,
                "{0}: {1}",
                aggregateException.GetType(),
                msg);

            bool first = false;
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (visitedExceptions.Contains(innerException))
                {
                    continue;
                }

                if (!first)
                {
                    result.Append(" ---> ");
                }

                result.Append(GetFormattedExceptionMessageHelper(innerException, visitedExceptions));
                first = false;
            }

            return result.ToString();
        }

        bool isFirst = true;
        for (Exception? curException = ex;
             curException != null;
             curException = curException.InnerException)
        {
            if (visitedExceptions.Contains(curException))
            {
                break;
            }

            visitedExceptions.Add(curException);

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
                isFirst ? string.Empty : " ---> ",
                curException.GetType(),
                msg);
            isFirst = false;
        }

        return result.ToString();
    }
}

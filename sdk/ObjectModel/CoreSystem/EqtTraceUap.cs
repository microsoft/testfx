// ---------------------------------------------------------------------------
// <copyright file="EqtTraceUap.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Exception thrown by Run Settings when an error with a settings provider
//     is encountered.
// </summary>
// <owner>svajjala</owner> 
// ---------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Diagnostics.Tracing;

    /// <summary>
    /// EqtTrace for Universal Apps
    /// Same might work for WinStore 8.1 Apps as well
    /// Migrate there if we required
    /// </summary>
    public static class EqtTrace
    {
        #region Fields

        private static object initLock = new object();

        private static bool isInitialized = false;

        #endregion

#if dotnet
        /// <summary>
        /// Ensure the trace is initialized
        /// </summary>
        public static void InitializeTrace(string traceLevel)
        {
            lock(initLock)
            {
                if(!isInitialized)
                {
                    isInitialized = true;
                }
            }
        }
#else
        /// <summary>
        /// Ensure the trace is initialized
        /// </summary>
        public static void InitializeTrace(string traceLevel)
        {
            lock(initLock)
            {
                if(!isInitialized)
                {
                    var eventListener = new StorageFileEventListener("UnitTestLog");

                    short traceNumber = 0;
                    try
                    {
                        Int16.TryParse(traceLevel, out traceNumber);
                    }
                    catch(Exception)
                    {
                        // ignore
                    }

                    if(traceNumber > 0)
                    {
                        eventListener.EnableEvents(UnitTestEventSource.Log, EventLevel.Error);
                        IsErrorEnabled = true;
                    }

                    if(traceNumber > 1)
                    {
                        eventListener.EnableEvents(UnitTestEventSource.Log, EventLevel.Warning);
                        IsWarningEnabled = true;
                    }

                    if(traceNumber > 2)
                    {
                        eventListener.EnableEvents(UnitTestEventSource.Log, EventLevel.Informational);
                        IsInfoEnabled = true;
                    }

                    if (traceNumber > 3)
                    {
                        eventListener.EnableEvents(UnitTestEventSource.Log, EventLevel.Verbose);
                        IsVerboseEnabled = true;
                    }

                    isInitialized = true;
                }
            }
        }
#endif
        /// <summary>
        /// Setup trace listenrs. It should be called when setting trace listener for child domain.
        /// </summary>
        /// <param name="listener"></param>
        internal static void SetupRemoteListeners()
        {

        }


        #region TraceLevel, ShouldTrace
        /// <summary>
        ///     Boolean flag to know if tracing error statements is enabled.
        /// </summary>
        public static bool IsErrorEnabled
        {
            get; private set;
        }

        /// <summary>
        ///     Boolean flag to know if tracing info statements is enabled. 
        /// </summary>
        public static bool IsInfoEnabled
        {
            get; private set;
        }

        /// <summary>
        ///     Boolean flag to know if tracing verbose statements is enabled. 
        /// </summary>
        public static bool IsVerboseEnabled
        {
            get; private set;
        }

        /// <summary>
        ///     Boolean flag to know if tracing warning statements is enabled.
        /// </summary>
        public static bool IsWarningEnabled
        {
            get; private set;
        }

        /// <summary>
        /// returns true if tracing is enabled for the passed
        /// trace level
        /// </summary>
        /// <param name="traceLevel"></param>
        /// <returns></returns>
        public static bool ShouldTrace()
        {
            return IsErrorEnabled;
        }
        #endregion

        #region Error

        /// <summary>
        /// Prints an error message and prompts with a Debug dialog
        /// </summary>
        /// <param name="message">the error message</param>
        [ConditionalAttribute("TRACE")]
        public static void Fail(string message)
        {
            Error(message);
        }


        /// <summary>
        /// Combines together EqtTrace.Fail and Debug.Fail:
        /// Prints an formatted error message and prompts with a Debug dialog.
        /// </summary>
        /// <param name="format">the formatted error message</param>
        /// <param name="args">arguments to the format</param>
        [ConditionalAttribute("TRACE")]
        public static void Fail(string format, params object[] args)
        {
            Error(FormatAndReturn(format, args));
        }


#if dotnet
        [ConditionalAttribute("TRACE")]
        public static void Error(string message)
        {
            
        }
#else
        [ConditionalAttribute("TRACE")]
        public static void Error(string message)
        {
            UnitTestEventSource.Log.Error(message);
        }
#endif

        /// <summary>
        /// Only prints the message if the condition is true
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorIf(bool condition, string message)
        {
            if (condition)
            {
                Error(message);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorUnless(bool condition, string message)
        {
            ErrorIf(!condition, message);
        }

        /// <summary>
        /// Traces an formatted error message
        /// </summary>
        /// <param name="format">the formatted error message</param>
        /// <param name="args">arguments to the format</param>
        [ConditionalAttribute("TRACE")]
        public static void Error(string format, params object[] args)
        {
            Error(FormatAndReturn(format, args));
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorUnless(bool condition, string format, params object[] args)
        {
            ErrorIf(!condition, format, args);
        }

        /// <summary>
        /// Only prints the formatted message if the condition is true
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorIf(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                Error(format, args);
            }
        }

        /// <summary>
        /// Write a exception if tracing for error is enabled
        /// </summary>
        /// <param name="exceptionToTrace">The exception to write.</param>
        [ConditionalAttribute("TRACE")]
        public static void Error(Exception exceptionToTrace)
        {
            if (exceptionToTrace != null)
            {
                Error(string.Format("{0}: {1}", exceptionToTrace.Message, exceptionToTrace));
            }
        }

        #endregion

        #region Warning

#if dotnet
        [ConditionalAttribute("TRACE")]
        public static void Warning(string message)
        {
            
        }
#else
        [ConditionalAttribute("TRACE")]
        public static void Warning(string message)
        {
            UnitTestEventSource.Log.Warn(message);
        }
#endif

        /// <summary>
        /// Only prints the formatted message if the condition is true
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void WarningIf(bool condition, string message)
        {
            if (condition)
            {
                Warning(message);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void WarningUnless(bool condition, string message)
        {
            WarningIf(!condition, message);
        }

        [ConditionalAttribute("TRACE")]
        public static void Warning(string format, params object[] args)
        {
            Warning(FormatAndReturn(format, args));
        }

        [ConditionalAttribute("TRACE")]
        public static void WarningIf(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                Warning(format, args);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void WarningUnless(bool condition, string format, params object[] args)
        {
            WarningIf(!condition, format, args);
        }

        #endregion

        #region Info

#if dotnet
        [ConditionalAttribute("TRACE")]
        public static void Info(string message)
        {
                        
        }
#else
        [ConditionalAttribute("TRACE")]
        public static void Info(string message)
        {
            UnitTestEventSource.Log.Info(message);
        }
#endif

        [ConditionalAttribute("TRACE")]
        public static void InfoIf(bool condition, string message)
        {
            if (condition)
            {
                Info(message);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void InfoUnless(bool condition, string message)
        {
            InfoIf(!condition, message);
        }

        [ConditionalAttribute("TRACE")]
        public static void Info(string format, params object[] args)
        {
            Info(FormatAndReturn(format, args));
        }

        [ConditionalAttribute("TRACE")]
        public static void InfoIf(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                Info(format, args);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void InfoUnless(bool condition, string format, params object[] args)
        {
            InfoIf(!condition, format, args);
        }

        #endregion

        #region Verbose

#if dotnet
        [ConditionalAttribute("TRACE")]
        public static void Verbose(string message)
        {
            
        }
#else
        [ConditionalAttribute("TRACE")]
        public static void Verbose(string message)
        {
            UnitTestEventSource.Log.Debug(message);
        }
#endif

        [ConditionalAttribute("TRACE")]
        public static void VerboseIf(bool condition, string message)
        {
            if (condition)
            {
                Verbose(message);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void VerboseUnless(bool condition, string message)
        {
            VerboseIf(!condition, message);
        }

        [ConditionalAttribute("TRACE")]
        public static void Verbose(string format, params object[] args)
        {
            Verbose(FormatAndReturn(format, args));
        }

        [ConditionalAttribute("TRACE")]
        public static void VerboseIf(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                Verbose(format, args);
            }
        }

        /// <summary>
        /// Only prints the formatted message if the condition is false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void VerboseUnless(bool condition, string format, params object[] args)
        {
            VerboseIf(!condition, format, args);
        }

#endregion

#region Helpers

        /// <summary>
        /// Formats an exception into a nice looking message.
        /// </summary>
        /// <param name="exceptionToTrace">The exception to write.</param>
        /// <returns>The formatted string.</returns>
        private static string FormatException(Exception exceptionToTrace)
        {
            if (exceptionToTrace == null) return string.Empty;

            // Prefix for each line
            string prefix = Environment.NewLine + '\t';

            // Format this exception
            StringBuilder message = new StringBuilder();
            message.Append(string.Format(CultureInfo.InvariantCulture,
                "Exception: {0}{1}Message: {2}{3}Stack Trace: {4}",
                exceptionToTrace.GetType(), prefix, exceptionToTrace.Message,
                prefix, exceptionToTrace.StackTrace));

            // If there is base exception, add that to message
            if (exceptionToTrace.GetBaseException() != null)
            {
                message.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0}BaseExceptionMessage: {1}",
                    prefix, exceptionToTrace.GetBaseException().Message));
            }

            // If there is inner exception, add that to message
            // We deliberately avoid recursive calls here.
            if (exceptionToTrace.InnerException != null)
            {
                // Format same as outer exception except
                // "InnerException" is prefixed to each line
                Exception inner = exceptionToTrace.InnerException;
                prefix += "InnerException";
                message.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1}{2} Message: {3}{4} Stack Trace: {5}",
                    prefix, inner.GetType(), prefix, inner.Message, prefix, inner.StackTrace));

                if (inner.GetBaseException() != null)
                {
                    message.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0}BaseExceptionMessage: {1}",
                        prefix, inner.GetBaseException().Message));
                }
            }

            // Append a new line
            message.Append(Environment.NewLine);

            return message.ToString();
        }

        private static string FormatAndReturn(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(format))
            {
                try
                {
                    return string.Format(CultureInfo.InvariantCulture, format, args);
                }
                catch (Exception)
                {
                    // Ignore any log errors
                }
            }
            return string.Empty;
        }
#endregion
    }
}

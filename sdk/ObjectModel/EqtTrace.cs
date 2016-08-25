//*************************************************************************************************
//
// NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE
// This code is duplicated from QualityTools.Common.dll with minimal change.
// The duplication was needed because there are SxS issues that prevent using
// the original code.  Once those issues are fixed, consider removing this.
//
// EqtTrace.cs
// Owner: mkolt
//
// Wrapper class for tracing. Refer to Trace class summary for more details.
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//*************************************************************************************************

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Configuration;
    using Microsoft.Win32;
    using System.Collections.Generic;

    /// <summary>
    /// Wrapper class for tracing.
    ///     - Shortcut-methods for Error, Warning, Info, Verbose. 
    ///     - Adds additional information to the trace: calling process name, PID, ThreadID, Time.
    ///     - Uses custom switch EqtTraceLevel from .config file. 
    ///     - By default tracing if OFF.
    ///     - Our build environment always sets the /d:TRACE so this class is always enabled,
    ///       the Debug class is enabled only in debug builds (/d:DEBUG).
    ///     - We ignore exceptions thrown by underlying TraceSwitch (e.g. due to config file error).
    ///       We log ignored exceptions to system Application log.
    ///       We pass through exceptions thrown due to incorrect arguments to EqtTrace methods.
    /// Usage: EqtTrace.Info("Here's how to trace info");
    /// </summary>
    public static class EqtTrace
    {
        #region Fields

        /// <summary>
        /// The switch for tracing TestPlaform messages only. Initialize Trace listener as a part of this
        /// to avoid a separate Static concstructor (to fix CA1810)
        /// </summary>
        private static readonly TraceSwitch s_traceLevelSwitch = new TraceSwitch("TpTraceLevel", null);

        // Current process name/id that called trace so that it's easier to read logs.
        // We cache them for performance reason.
        private static readonly string s_processName = GetProcessName();

        private static readonly int s_processId = GetProcessId();

        /// <summary>
        /// We log to system log only this # of times in order not to overflow it.
        /// </summary>
        private static int s_timesCanWriteToSystemLog = 10;

        /// <summary>
        /// To which system event log we log if cannot log to trace file.
        /// </summary>
        private const string SystemEventLogSource = "Application";

        /// Specifies whether the trace is initialized or not
        /// </summary>
        private static bool s_Initialized = false;

        /// <summary>
        /// Name of the trace listener.
        /// </summary>
        private const string ListenerName = "TptTraceListener";

        /// <summary>
        /// Trace listener object to which all Testplatform traces are written.
        /// </summary>
        private static TraceListener s_listener;

        /// <summary>
        /// Lock over initialization
        /// </summary>
        private static object s_InitializationLock = new object();

        private static int s_TraceFileSize = 0;
        private static int s_DefaultTraceFileSize = 10240; // 10Mb.
        #endregion


        /// <summary>
        /// Ensure the trace is initialized
        /// </summary>
        static void EnsureTraceIsInitialized()
        {
            if (s_Initialized)
            {
                return;
            }

            lock (s_InitializationLock)
            {
                if (s_Initialized)
                {
                    return;
                }

                string logsDirectory = Path.GetTempPath();
                // Set the trace level and add the trace listener
                string logFileName =
                    Path.Combine(
                            logsDirectory,
                            Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName) +
                                ".TpTrace.log"
                        );

                string traceFileSizeStr = ConfigurationManager.AppSettings[Constants.TraceLogMaxFileSizeInKB];
                if (!int.TryParse(traceFileSizeStr, out s_TraceFileSize)
                        || s_TraceFileSize <= 0)
                {
                    s_TraceFileSize = s_DefaultTraceFileSize;
                }

                s_listener = new RollingFileTraceListener(logFileName, ListenerName, s_TraceFileSize);
                Trace.Listeners.Add(s_listener);                
                // Set the auto-flush flag, so that log entries are written immediately. This is done so that if the
                // process exits or is killed, we will have as much log information as possible in the log.
                Trace.AutoFlush = true;

                s_Initialized = true;
            }
        }



        /// <summary>
        /// Setup remote trace listener in the child domain.
        /// If calling domain, doesn't have tracing enabled nothing is done.
        /// </summary>
        /// <param name="childDomain"></param>
        public static void SetupRemoteEqtTraceListeners(AppDomain childDomain)
        {
            Debug.Assert(childDomain != null, "domain");
            if (null != childDomain)
            {
                RemoteEqtTrace remoteEqtTrace = (RemoteEqtTrace)childDomain.CreateInstanceFromAndUnwrap(
                    typeof(RemoteEqtTrace).Assembly.Location,
                    typeof(RemoteEqtTrace).FullName);

                remoteEqtTrace.TraceLevel = TraceLevel;

                if (!Enum.Equals(TraceLevel, TraceLevel.Off))
                {

                    TraceListener tptListner = null;
                    foreach (TraceListener listener in Trace.Listeners)
                    {
                        if (String.Equals(listener.Name, ListenerName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Assert(tptListner == null, "Multiple TptListeners found.");
                            tptListner = listener;
                        }
                    }
                    remoteEqtTrace.SetupRemoteListeners(tptListner);
                }
            }

        }


        /// <summary>
        /// Setup a custom trace listener instead of default trace listener created by rocksteady.
        /// This is needed by DTA Agent where it needs to listen rocksteady traces but doesn't use rocksteady listener.
        /// </summary>
        public static void SetupListener(TraceListener listener)
        {
            lock (s_InitializationLock)
            {

                // Add new listeners.
                s_listener = listener;
                if (null != listener)
                {
                    Trace.Listeners.Add(listener);
                }
                s_Initialized = true;
            }
        }

        /// <summary>
        /// Setup trace listenrs. It should be called when setting trace listener for child domain.
        /// </summary>
        /// <param name="listener"></param>
        internal static void SetupRemoteListeners(TraceListener listener)
        {

            lock (s_InitializationLock)
            {

                // Add new listeners.
                if (null != listener)
                {
                    Trace.Listeners.Add(listener);
                    s_listener = listener;
                }
                s_Initialized = true;
            }
        }



        #region TraceLevel, ShouldTrace
        public static System.Diagnostics.TraceLevel TraceLevel
        {
            get
            {
                return s_traceLevelSwitch.Level;
            }

            set
            {
                try
                {
                    s_traceLevelSwitch.Level = value;
                }
                catch (ArgumentException e)
                {
                    LogIgnoredException(e);
                }
            }
        }
        /// <summary>
        ///     Boolean flag to know if tracing error statements is enabled.
        /// </summary>
        public static bool IsErrorEnabled
        {
            get
            {
                return ShouldTrace(TraceLevel.Error);
            }
        }
        
        /// <summary>
        ///     Boolean flag to know if tracing info statements is enabled. 
        /// </summary>
        public static bool IsInfoEnabled
        {
            get
            {
                return ShouldTrace(TraceLevel.Info);
            }
        }

        /// <summary>
        ///     Boolean flag to know if tracing verbose statements is enabled. 
        /// </summary>
        public static bool IsVerboseEnabled
        {
            get
            {
                return ShouldTrace(TraceLevel.Verbose);
            }
        }

        /// <summary>
        ///     Boolean flag to know if tracing warning statements is enabled.
        /// </summary>
        public static bool IsWarningEnabled
        {
            get
            {
                return ShouldTrace(TraceLevel.Warning);
            }
        }

        /// <summary>
        /// returns true if tracing is enabled for the passed
        /// trace level
        /// </summary>
        /// <param name="traceLevel"></param>
        /// <returns></returns>
        public static bool ShouldTrace(TraceLevel traceLevel)
        {
            switch (traceLevel)
            {
                case TraceLevel.Off:
                    return false;
                case TraceLevel.Error:
                    return s_traceLevelSwitch.TraceError;
                case TraceLevel.Warning:
                    return s_traceLevelSwitch.TraceWarning;
                case TraceLevel.Info:
                    return s_traceLevelSwitch.TraceInfo;
                case TraceLevel.Verbose:
                    return s_traceLevelSwitch.TraceVerbose;
                default:
                    Debug.Fail("Should never get here!");
                    return false;
            }
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
            Debug.Fail(message);
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
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            Error(message);
#if DEBUG
            Debug.Fail(message);
#endif
        }



        [ConditionalAttribute("TRACE")]
        public static void Error(string message)
        {
            if (ShouldTrace(TraceLevel.Error))
            {
                WriteLine(TraceLevel.Error, message);
            }
        }

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
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string message)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, message);
            }
            else
            {
                Error(message);
            }
        }

        [ConditionalAttribute("TRACE")]
        public static void Error(string format, params object[] args)
        {
            Debug.Assert(format != null);

            // Check level before doing string.Format to avoid string creation if tracing is off.
            if (ShouldTrace(TraceLevel.Error))
            {
                Error(string.Format(CultureInfo.InvariantCulture, format, args));
            }
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
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string format, params object[] args)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, format, args);
            }
            else
            {
                Error(format, args);
            }
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
        /// EqtTrace.Error and Debug.Fail combined in one call.
        /// </summary>
        /// <param name="message">The message to send to Debug.Fail and EqtTrace.Error.</param>
        /// <param name="args">Params to string.Format.</param>
        [ConditionalAttribute("TRACE")]
        public static void ErrorAssert(string format, params object[] args)
        {
            Debug.Assert(format != null);
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            Error(message);
            Debug.Fail(message);
        }

        /// <summary>
        /// Write a exception if tracing for error is enabled
        /// </summary>
        /// <param name="exceptionToTrace">The exception to write.</param>
        [ConditionalAttribute("TRACE")]
        public static void Error(Exception exceptionToTrace)
        {
            Debug.Assert(exceptionToTrace != null);

            // Write only if tracing for error is enabled.
            // Done upfront to avoid perf hit.
            if (ShouldTrace(TraceLevel.Error))
            {
                // Write at error level
                WriteLine(TraceLevel.Error, FormatException(exceptionToTrace));
            }
        }

        #endregion

        #region Warning

        [ConditionalAttribute("TRACE")]
        public static void Warning(string message)
        {
            if (ShouldTrace(TraceLevel.Warning))
            {
                WriteLine(TraceLevel.Warning, message);
            }
        }

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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void WarningUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string message)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, message);
            }
            else
            {
                Warning(message);
            }
        }

        [ConditionalAttribute("TRACE")]
        public static void Warning(string format, params object[] args)
        {
            Debug.Assert(format != null);

            // Check level before doing string.Format to avoid string creation if tracing is off.
            if (ShouldTrace(TraceLevel.Warning))
            {
                Warning(string.Format(CultureInfo.InvariantCulture, format, args));
            }
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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void WarningUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string format, params object[] args)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, format, args);
            }
            else
            {
                Warning(format, args);
            }
        }
        #endregion

        #region Info

        [ConditionalAttribute("TRACE")]
        public static void Info(string message)
        {
            if (ShouldTrace(TraceLevel.Info))
            {
                WriteLine(TraceLevel.Info, message);
            }
        }

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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void InfoUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string message)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, message);
            }
            else
            {
                Info(message);
            }
        }

        [ConditionalAttribute("TRACE")]
        public static void Info(string format, params object[] args)
        {
            Debug.Assert(format != null);

            // Check level before doing string.Format to avoid string creation if tracing is off.
            if (ShouldTrace(TraceLevel.Info))
            {
                Info(string.Format(CultureInfo.InvariantCulture, format, args));
            }
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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void InfoUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string format, params object[] args)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, format, args);
            }
            else
            {
                Info(format, args);
            }
        }
        #endregion

        #region Verbose

        [ConditionalAttribute("TRACE")]
        public static void Verbose(string message)
        {
            if (ShouldTrace(TraceLevel.Verbose))
            {
                WriteLine(TraceLevel.Verbose, message);
            }
        }

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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void VerboseUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string message)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, message);
            }
            else
            {
                Verbose(message);
            }
        }

        [ConditionalAttribute("TRACE")]
        public static void Verbose(string format, params object[] args)
        {
            Debug.Assert(format != null);

            // Check level before doing string.Format to avoid string creation if tracing is off.
            if (ShouldTrace(TraceLevel.Verbose))
            {
                Verbose(string.Format(CultureInfo.InvariantCulture, format, args));
            }
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

        /// <summary>
        /// Prints the message if the condition is false. If the condition is true,
        /// the message is instead printed at the specified trace level.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [ConditionalAttribute("TRACE")]
        public static void VerboseUnlessAlterTrace(bool condition, TraceLevel bumpLevel, string format, params object[] args)
        {
            if (condition)
            {
                WriteAtLevel(bumpLevel, format, args);
            }
            else
            {
                Verbose(format, args);
            }
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

        /// <summary>
        /// Get the process name. Note: we cache it, use m_processName.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031")]
        private static string GetProcessName()
        {
            try
            {
                //return ProcessHelper.GetProcessName();
                string processName = null;

                string[] args = Environment.GetCommandLineArgs();

                if (args != null && args.Length != 0)
                {
                    // Leave the extension if specified, otherwise don't add it (e.g. case a.exe.exe).
                    // It seems that if .exe suffix is not specified Framework adds .EXE to agrs[0].
                    processName = Path.GetFileName(args[0]);
                }

                // If we still have not got process name from command line - use the slow way.
                // This should never happen unless the process is called from execv with empty cmdline.
                if (processName == null || processName.Length == 0)
                {
                    Debug.Fail("Could not get process name from command line, will try to use the slow way.");
                    using (Process process = Process.GetCurrentProcess())
                    {
                        processName = process.ProcessName;
                    }
                }

                return processName;
            }
            catch (Exception e) // valid suppress
            {
                Debug.Fail("Could not get process name: " + e.ToString());
                LogIgnoredException(e);
                return EqtMessages.Utility_ProcessNameWhenCannotGetIt;
            }
        }

        private static int GetProcessId()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    return process.Id;
                }
            }
            catch (InvalidOperationException e)
            {
                Debug.Fail("Could not get process id: " + e.ToString());
                LogIgnoredException(e);
                return -1;
            }
        }

        private static void WriteAtLevel(TraceLevel level, string message)
        {
            switch (level)
            {
                case TraceLevel.Off:
                    return;
                case TraceLevel.Error:
                    Error(message);
                    break;
                case TraceLevel.Warning:
                    Warning(message);
                    break;
                case TraceLevel.Info:
                    Info(message);
                    break;
                case TraceLevel.Verbose:
                    Verbose(message);
                    break;
                default:
                    Debug.Fail("We should never get here!");
                    break;
            }
        }

        private static void WriteAtLevel(TraceLevel level, string format, params object[] args)
        {
            Debug.Assert(format != null);
            WriteAtLevel(level, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        /// <summary>
        /// Adds the message to the trace log.
        /// The line becomes: 
        ///     [I, PID, ThreadID, 2003/06/11 11:56:07.445] CallingAssemblyName: message.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message">The message to add to trace.</param>
        [SuppressMessage("Microsoft.Design", "CA1031")]
        [SuppressMessage("Microsoft.Globalization", "CA1303")]
        private static void WriteLine(System.Diagnostics.TraceLevel level, string message)
        {
            Debug.Assert(message != null);
            Debug.Assert(s_processName != null && s_processName.Length != 0);

            // Ensure trace is initlized
            EnsureTraceIsInitialized();

            string entryType = null;
            switch (level)
            {
                case TraceLevel.Error:
                    entryType = "E"; break;

                case TraceLevel.Warning:
                    entryType = "W"; break;

                case TraceLevel.Info:
                    entryType = "I"; break;

                case TraceLevel.Verbose:
                    entryType = "V"; break;

                default:
                    Debug.Fail("Should never get here. Unexpected TraceLevel: " + level.ToString()); break;
            }

            // The format below is a CSV so that Excel could be used easily to
            // view/filter the logs.
            string log = string.Format(
                CultureInfo.InvariantCulture,
                "{0}, {1}, {2}, {3:yyyy}/{3:MM}/{3:dd}, {3:HH}:{3:mm}:{3:ss}.{3:fff}, {6}, {4}, {5}",
                entryType,
                s_processId,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now,
                s_processName,
                message,
                Stopwatch.GetTimestamp());

            try
            {
                if (null != s_listener)
                {
                    s_listener.WriteLine(log);
                    s_listener.Flush();
                }
                else
                {
                    Trace.WriteLine(log);
                }
            }
            catch (Exception e) // valid suppress
            {
                // Log exception from tracing into event viewer. 
                LogIgnoredException(e);
            }
        }

        /// <summary>
        /// Auxillary method: logs the exception that is being ignored.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        [SuppressMessage("Microsoft.Design", "CA1031")]
        private static void LogIgnoredException(Exception e)
        {
            Debug.Assert(e != null);

            EnsureTraceIsInitialized();

            try
            {
                //string message = Messages.Utility_IgnoredThrownException(e.ToString(), e.StackTrace);
                string message = string.Format(CultureInfo.CurrentCulture,
                    EqtMessages.Utility_IgnoredThrownException, e.ToString(), e.StackTrace);

                // Write to the system event log and to debug listener. 
                // Note: Debug.WriteLine may throw if there is a problem in .config file.
                WriteEventLogEntry(message, EventLogEntryType.Error);
                Debug.WriteLine(message);
            }
            catch // valid suppress
            {
                // Ignore everything at this point.
            }
        }

        private static void WriteEventLogEntry(string message, EventLogEntryType logType)
        {
            Debug.Assert(message != null);

            if (s_timesCanWriteToSystemLog > 0)
            {
                EventLog.WriteEntry(SystemEventLogSource, message, logType);

                if (Interlocked.Decrement(ref s_timesCanWriteToSystemLog) == 0)
                {
                    EventLog.WriteEntry(SystemEventLogSource, EqtMessages.Common_EqtTraceReachedMaxEventLogEntries, EventLogEntryType.Warning);
                }
            }
        }

        #endregion
    }


    /// <summary>
    /// A class used to expose EqtTrace functionality across AppDomains.
    /// <see cref="EqtTrace.GetRemoteEqtTrace"/>
    /// </summary>
    public sealed class RemoteEqtTrace : MarshalByRefObject
    {
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Used in remote objects.")]
        public TraceLevel TraceLevel
        {
            get
            {
                return EqtTrace.TraceLevel;
            }

            set
            {
                EqtTrace.TraceLevel = value;
            }
        }

        /// <summary>
        /// Register listeners from parent domain in current domain.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Used in remote objects.")]        
        internal void SetupRemoteListeners(TraceListener listener)
        {
            EqtTrace.SetupRemoteListeners(listener);
        }
    }



}

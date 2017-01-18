// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// To help user logging / write traces from unit tests to for diagnostics.
    /// </summary>
    public class Logger
    {

        /// <summary>
        /// Handler for LogMessage.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public delegate void LogMessageHandler(string message);

        /// <summary>
        /// Event to listen. Raised when unit test writter writes some message.
        /// Mainly to consume by adapter.
        /// </summary>
        public static event LogMessageHandler OnLogMessage;

        /// <summary>
        /// API for test writer to call to Log messages.
        /// </summary>
        /// <param name="format">String format with placeholders.</param>
        /// <param name="args">Parameters for placeholders.</param>
        public static void LogMessage(string format, params object[] args)
        {            
            if (OnLogMessage != null)
            {
                if (format == null)
                {
                    throw new ArgumentNullException("format");
                }

                string message = string.Format(CultureInfo.InvariantCulture, format, args);

                // Making sure all event handlers are called in sunc on same thread.
                foreach (var invoker in OnLogMessage.GetInvocationList())
                {
                    try
                    {
                        invoker.GetMethodInfo().Invoke(invoker.Target, new object[] { message });
                    }
                    catch (Exception) { } // Catch and ignore all exceptions thrown by event handlers.
                }
            }
        }
    }
}

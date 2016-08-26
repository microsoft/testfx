// ---------------------------------------------------------------------------
// <copyright file="LogMessageListner.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Log message listener class.
// </summary>
// ---------------------------------------------------------------------------

#define TODO

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Globalization;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Listens for log messages and Debug.WriteLine
    /// Note that this class is not thread-safe and thus should only be used when unit tests are being run serially.    
    /// </summary>
    public class LogMessageListener : IDisposable
    {
        private static LogMessageListener activeRedirector;
        private readonly LogMessageListener previousRedirector;
        private readonly TextWriter redirectLoggerOut;
        private readonly bool captureDebugTraces;

        /// <summary>
        /// Trace listener to capture Trace.WriteLines in the test cases
        /// </summary>
#if !TODO
        private TextWriterTraceListener m_traceListener;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="LogMessageListener"/> class.
        /// </summary>
        /// <param name="captureDebugTraces">Captures debug traces if true.</param>
        public LogMessageListener(bool captureDebugTraces)
        {
            this.captureDebugTraces = captureDebugTraces;

            // Cache the original output/error streams and replace it with the own stream. 
            this.redirectLoggerOut = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);
            Logger.OnLogMessage += this.redirectLoggerOut.WriteLine;

            // Cache the previous redirector if any and replace the trace listener.
            this.previousRedirector = activeRedirector;

            if (this.captureDebugTraces)
            {
#if !TODO
                m_traceListener = new TextWriterTraceListener(new ThreadSafeStringWriter(CultureInfo.InvariantCulture));

                try
                {
                    // If there was a previous ConsoleOutputRedirector active, remove its
                    // TraceListener (it will be restored when this one is disposed).
                    if (previousRedirector != null && previousRedirector.m_traceListener != null)
                    {
                        Trace.Listeners.Remove(previousRedirector.m_traceListener);
                    }

                    Trace.Listeners.Add(m_traceListener);
                }
                catch (Exception ex)
                {
                    // Catch exceptions if the configuration file is invalid and allow a stack
                    // trace to show the error on the test method instead of here.
                    if (!(ex.InnerException is System.Configuration.ConfigurationErrorsException))
                    {
                        throw;
                    }
                }
#endif
            }

            activeRedirector = this;
        }

        /// <summary>
        /// Logger output
        /// </summary>
        public string LoggerOut => this.redirectLoggerOut.ToString();

        /// <summary>
        /// 'Trace' Output from the redirected stream
        /// </summary>
        public string DebugTrace
        {
            get
            {
#if !TODO
                return (m_traceListener == null || m_traceListener.Writer == null)? 
                    string.Empty : m_traceListener.Writer.ToString();
#else
                return null;
#endif
            }
        }

        ~LogMessageListener()
        {
            this.Dispose(false);
        }


        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Logger.OnLogMessage -= this.redirectLoggerOut.WriteLine;

                this.redirectLoggerOut.Dispose();

                if (this.captureDebugTraces)
                {
                    try
                    {
#if !TODO
                        Trace.Listeners.Remove(m_traceListener);

                        // Restore the previous ConsoleOutputRedirector's TraceListener (if there was one)
                        if (previousRedirector != null && previousRedirector.m_traceListener != null)
                        {
                            Trace.Listeners.Add(previousRedirector.m_traceListener);
                        }
#endif
                    }
                    catch (Exception e)
                    {
                        // Catch all exceptions since Dispose should not throw.
                        if (EqtTrace.IsErrorEnabled)
                        {
                            EqtTrace.Error("ConsoleOutputRedirector.Dispose threw exception: {0}", e);
                        }
                    }

#if !TODO
                    if (m_traceListener != null)
                    {
                        m_traceListener.Close();
                        m_traceListener.Dispose();
                        m_traceListener = null;
                    }
#endif
                }

                activeRedirector = this.previousRedirector;
            }
        }
    }
}
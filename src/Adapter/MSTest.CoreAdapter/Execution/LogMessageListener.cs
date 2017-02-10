// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Globalization;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;


    /// <summary>
    /// Listens for log messages and Debug.WriteLine
    /// Note that this class is not thread-safe and thus should only be used when unit tests are being run serially.    
    /// </summary>
    public class LogMessageListener : IDisposable
    {
        private static LogMessageListener activeRedirector;
        private readonly LogMessageListener previousRedirector;
        private readonly TextWriter redirectLoggerOut;
        private readonly TextWriter redirectStdErr;
        private readonly bool captureDebugTraces;

        /// <summary>
        /// Trace listener to capture Trace.WriteLines in the test cases
        /// </summary>
        private ITraceListener traceListener;

        /// <summary>
        /// Trace listener Manager to perform operation on tracelistener objects.
        /// </summary>
        private ITraceListenerManager traceListenerManager;

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

            this.redirectStdErr = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);
            Logger.OnLogMessage += this.redirectStdErr.WriteLine;

            // Cache the previous redirector if any and replace the trace listener.
            this.previousRedirector = activeRedirector;

            if (this.captureDebugTraces)
            {
                traceListener = PlatformServiceProvider.Instance.GetTraceListener(new ThreadSafeStringWriter(CultureInfo.InvariantCulture));
                traceListenerManager = PlatformServiceProvider.Instance.GetTraceListenerManager(this.redirectLoggerOut, this.redirectStdErr);

                // If there was a previous LogMessageListener active, remove its
                // TraceListener (it will be restored when this one is disposed).
                if (previousRedirector != null && previousRedirector.traceListener != null)
                {
                    traceListenerManager.Remove(previousRedirector.traceListener);
                }
                traceListenerManager.Add(traceListener);
            }

            activeRedirector = this;
        }

        /// <summary>
        /// Logger output
        /// </summary>
        public string StandardOutput => this.redirectLoggerOut.ToString();
       
        /// <summary>
        /// 'Error' Output from the redirected stream
        /// </summary>
        public string StandardError => this.redirectStdErr.ToString();

        /// <summary>
        /// 'Trace' Output from the redirected stream
        /// </summary>
        public string DebugTrace
        {
            get
            {
                return (traceListener == null || traceListener.GetWriter() == null)? 
                    string.Empty : traceListener.GetWriter().ToString();
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
                Logger.OnLogMessage -= this.redirectStdErr.WriteLine;

                this.redirectLoggerOut.Dispose();
                this.redirectStdErr.Dispose();

                if (this.captureDebugTraces)
                {
                    try
                    {
                        traceListenerManager.Remove(traceListener);

                        // Restore the previous LogMessageListener's TraceListener (if there was one)
                        if (previousRedirector != null && previousRedirector.traceListener != null)
                        {
                            traceListenerManager.Add(previousRedirector.traceListener);
                        }
                    }
                    catch (Exception e)
                    {
                        // Catch all exceptions since Dispose should not throw.
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                            "ConsoleOutputRedirector.Dispose threw exception: {0}",
                            e);
                    }

                    if (traceListener != null)
                    {
                        // Dispose trace manager and listeners
                        traceListenerManager.Close(traceListener);
                        traceListenerManager.Dispose(traceListener);
                        traceListenerManager = null;
                        traceListener = null;
                    }
                }

                activeRedirector = this.previousRedirector;
            }
        }
    }
}
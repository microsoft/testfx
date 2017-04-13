// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            this.redirectStdErr = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);

            Logger.OnLogMessage += this.redirectLoggerOut.WriteLine;

            // Cache the previous redirector if any and replace the trace listener.
            this.previousRedirector = activeRedirector;

            if (this.captureDebugTraces)
            {
                this.traceListener = PlatformServiceProvider.Instance.GetTraceListener(new ThreadSafeStringWriter(CultureInfo.InvariantCulture));
                this.traceListenerManager = PlatformServiceProvider.Instance.GetTraceListenerManager(this.redirectLoggerOut, this.redirectStdErr);

                // If there was a previous LogMessageListener active, remove its
                // TraceListener (it will be restored when this one is disposed).
                if (this.previousRedirector != null && this.previousRedirector.traceListener != null)
                {
                    this.traceListenerManager.Remove(this.previousRedirector.traceListener);
                }

                this.traceListenerManager.Add(this.traceListener);
            }

            activeRedirector = this;
        }

        ~LogMessageListener()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets logger output
        /// </summary>
        public string StandardOutput => this.redirectLoggerOut.ToString();

        /// <summary>
        /// Gets 'Error' Output from the redirected stream
        /// </summary>
        public string StandardError => this.redirectStdErr.ToString();

        /// <summary>
        /// Gets 'Trace' Output from the redirected stream
        /// </summary>
        public string DebugTrace
        {
            get
            {
                return (this.traceListener == null || this.traceListener.GetWriter() == null) ?
                    string.Empty : this.traceListener.GetWriter().ToString();
            }
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
                        this.traceListenerManager.Remove(this.traceListener);

                        // Restore the previous LogMessageListener's TraceListener (if there was one)
                        if (this.previousRedirector != null && this.previousRedirector.traceListener != null)
                        {
                            this.traceListenerManager.Add(this.previousRedirector.traceListener);
                        }
                    }
                    catch (Exception e)
                    {
                        // Catch all exceptions since Dispose should not throw.
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                            "ConsoleOutputRedirector.Dispose threw exception: {0}",
                            e);
                    }

                    if (this.traceListener != null)
                    {
                        // Dispose trace manager and listeners
                        this.traceListenerManager.Dispose(this.traceListener);
                        this.traceListenerManager = null;
                        this.traceListener = null;
                    }
                }

                activeRedirector = this.previousRedirector;
            }
        }
    }
}
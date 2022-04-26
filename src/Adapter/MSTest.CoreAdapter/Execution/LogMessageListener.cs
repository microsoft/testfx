// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

    /// <summary>
    /// Listens for log messages and Debug.WriteLine
    /// Note that this class is not thread-safe and thus should only be used when unit tests are being run serially.
    /// </summary>
    public class LogMessageListener : IDisposable
    {
        private static object traceLock = new object();
        private static int listenerCount;
        private static ThreadSafeStringWriter redirectedDebugTrace;

        /// <summary>
        /// Trace listener to capture Trace.WriteLines in the test cases
        /// </summary>
        private static ITraceListener traceListener;
        private readonly ThreadSafeStringWriter redirectedStandardOutput;
        private readonly ThreadSafeStringWriter redirectedStandardError;
        private readonly bool captureDebugTraces;

        /// <summary>
        /// Trace listener Manager to perform operation on tracelistener objects.
        /// </summary>
        private ITraceListenerManager traceListenerManager;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogMessageListener"/> class.
        /// </summary>
        /// <param name="captureDebugTraces">Captures debug traces if true.</param>
        public LogMessageListener(bool captureDebugTraces)
        {
            this.captureDebugTraces = captureDebugTraces;

            // Cache the original output/error streams and replace it with the own stream.
            this.redirectedStandardOutput = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out");
            this.redirectedStandardError = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "err");

            Logger.OnLogMessage += this.redirectedStandardOutput.WriteLine;

            if (this.captureDebugTraces)
            {
                // This is awkward, it has a side-effect of setting up Console output redirection, but the naming is suggesting that we are
                // just getting TraceListener manager.
                this.traceListenerManager = PlatformServiceProvider.Instance.GetTraceListenerManager(this.redirectedStandardOutput, this.redirectedStandardError);

                // The Debug listener uses Debug.WriteLine and Debug.Write to write the messages, which end up written into Trace.Listeners.
                // These listeners are static and hence shared across the whole process. We need to capture Debug output only for the current
                // test, which was historically done by registering a listener in constructor of this class, and by removing the listener on Dispose.
                // The newly created listener replaced previously registered listener, which was remembered, and put back on dispose.
                //
                // This works well as long as there are no tests running in parallel. But as soon as there are tests running in parallel. Then all the
                // debug output of all tests will be output into the test that was most recently created (because it registered the listener most recently).
                //
                // To prevent mixing of outputs, the ThreadSafeStringWriter was re-implemented for net46 and newer to leverage AsyncLocal, which allows the writer to
                // write only to the output of the current test. This leaves the LogMessageListener with only one task. Make sure that a trace listener is registered
                // as long as there is any active test. This is still done by constructor and Dispose, but instead of replacing the listener every time, we use listenerCount
                // to only add the listener when there is none, and remove it when we are the last one to dispose.
                //
                // This would break the behavior for net451, but that functionality was moved further into ThreadSafeStringWriter.
                lock (traceLock)
                {
                    if (listenerCount == 0)
                    {
                        redirectedDebugTrace = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "trace");
                        traceListener = PlatformServiceProvider.Instance.GetTraceListener(redirectedDebugTrace);
                        this.traceListenerManager.Add(traceListener);
                    }

                    listenerCount++;
                }
            }
        }

        ~LogMessageListener()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets logger output
        /// </summary>
        public string StandardOutput => this.redirectedStandardOutput.ToString();

        /// <summary>
        /// Gets 'Error' Output from the redirected stream
        /// </summary>
        public string StandardError => this.redirectedStandardError.ToString();

        /// <summary>
        /// Gets 'Trace' Output from the redirected stream
        /// </summary>
        public string DebugTrace
        {
            get
            {
                return redirectedDebugTrace?.ToString();
            }
        }

        public string GetAndClearStandardOutput()
        {
            var output = this.redirectedStandardOutput.ToStringAndClear();
            return output;
        }

        public string GetAndClearStandardError()
        {
            var output = this.redirectedStandardError.ToStringAndClear();
            return output;
        }

        public string GetAndClearDebugTrace()
        {
            if (redirectedDebugTrace == null)
            {
                return null;
            }

            var output = redirectedDebugTrace.ToStringAndClear();
            return output;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;
                Logger.OnLogMessage -= this.redirectedStandardOutput.WriteLine;
                Logger.OnLogMessage -= this.redirectedStandardError.WriteLine;

                this.redirectedStandardOutput.Dispose();
                this.redirectedStandardError.Dispose();

                if (this.captureDebugTraces)
                {
                    lock (traceLock)
                    {
                        if (listenerCount == 1)
                        {
                            try
                            {
                                if (traceListener != null)
                                {
                                    this.traceListenerManager.Remove(traceListener);
                                }
                            }
                            catch (Exception e)
                            {
                                // Catch all exceptions since Dispose should not throw.
                                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError("ConsoleOutputRedirector.Dispose threw exception: {0}", e);
                            }

                            if (traceListener != null)
                            {
                                // Dispose trace manager and listeners
                                this.traceListenerManager.Dispose(traceListener);
                                this.traceListenerManager = null;
                                traceListener = null;
                            }
                        }

                        listenerCount--;
                    }
                }
            }
        }
    }
}
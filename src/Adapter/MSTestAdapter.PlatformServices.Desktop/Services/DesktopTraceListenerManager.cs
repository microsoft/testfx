// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using System.Diagnostics;
    using System.IO;
    using System.Globalization;

    /// <summary>
    /// Internal implementation of TraceListenerManager exposed to the user.
    /// Responsible for performing Add(), Remove(), Close(), Dispose() operations on traceListener object.
    /// </summary>
    public class TraceListenerManager : ITraceListenerManager
    {
        /// <summary>
        /// Original output stream
        /// </summary>
        private TextWriter origStdOut;

        /// <summary>
        /// Original error stream
        /// </summary>
        private TextWriter origStdErr;

        /// <summary>
        ///     Initializes a new instance of a TraceListenerManager object.
        ///     Also, updates the output/error streams with redirected outputWriter and errorWriter
        /// </summary>
        public TraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
        {
            origStdOut = Console.Out;
            origStdErr = Console.Error;

            // Update the output/error streams with redirected streams
            Console.SetOut(outputWriter);
            Console.SetError(errorWriter);
        }

        /// <summary>
        /// Adds the arguement traceListener object to System.Diagnostics.TraceListenerCollection.
        /// </summary>
        public void Add(ITraceListener traceListener)
        {
            try
            {
                Trace.Listeners.Add(traceListener as TextWriterTraceListener);
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
        }

        /// <summary>
        /// Removes the arguement traceListener object from System.Diagnostics.TraceListenerCollection.
        /// </summary>
        public void Remove(ITraceListener traceListner)
        {
            Trace.Listeners.Remove(traceListner as TextWriterTraceListener);
        }

        /// <summary>
        /// Wrapper over Close() of ITraceListener.
        /// </summary>        
        public void Close(ITraceListener traceListener)
        {
            traceListener.Close();
        }

        /// <summary>
        /// Wrapper over Dispose() of ITraceListener.
        /// Also resets the standard output/error streams.
        /// </summary> 
        public void Dispose(ITraceListener traceListener)
        {
            traceListener.Dispose();
            Console.SetOut(origStdOut);
            Console.SetError(origStdErr);

        }
    }
}

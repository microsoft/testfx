// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using System.IO;

    /// <summary>
    /// Internal implementation of TraceListenerManager exposed to the user.
    /// Responsible for performing Add(), Remove(), Close(), Dispose() operations on traceListener object.
    /// </summary>
    public class TraceListenerManager : ITraceListenerManager
    {
        /// <summary>
        ///     Initializes a new instance of a TraceListenerManager object.
        /// </summary>
        public TraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
        {           
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Add(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Close(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Dispose(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Remove(ITraceListener traceListner)
        {
            return;
        }
    }
}

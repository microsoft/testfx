// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

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

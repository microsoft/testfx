// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Internal implementation of TraceListenerManager exposed to the user.
    /// Responsible for performing Add(), Remove(), Close(), Dispose() operations on traceListener object.
    /// </summary>
    public class TraceListenerManager : ITraceListenerManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceListenerManager"/> class.
        /// </summary>
        /// <param name="outputWriter">A writer instance to log output messages.</param>
        /// <param name="errorWriter">A writer instance to log error messages.</param>
        public TraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
        {
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Add(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Close(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Dispose(ITraceListener traceListener)
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Remove(ITraceListener traceListener)
        {
            return;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}

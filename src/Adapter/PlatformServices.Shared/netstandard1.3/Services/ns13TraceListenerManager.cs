// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Diagnostics;
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
        /// Original output stream
        /// </summary>
        private TextWriter origStdOut;

        /// <summary>
        /// Original error stream
        /// </summary>
        private TextWriter origStdErr;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceListenerManager"/> class.
        /// </summary>
        /// <param name="outputWriter">A writer instance to log output messages.</param>
        /// <param name="errorWriter">A writer instance to log error messages.</param>
        public TraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
        {
            this.origStdOut = Console.Out;
            this.origStdErr = Console.Error;

            // Update the output/error streams with redirected streams
            Console.SetOut(outputWriter);
            Console.SetError(errorWriter);
        }

        /// <summary>
        /// Adds the argument traceListener object to System.Diagnostics.TraceListenerCollection.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Add(ITraceListener traceListener)
        {
            // NOTE: Listeners will not get Debug events in dotnet core due to platform limitation.
            // Refer https://github.com/Microsoft/testfx/pull/218 for more details.
            Trace.Listeners.Add(traceListener as TextWriterTraceListener);
        }

        /// <summary>
        /// Removes the argument traceListener object from System.Diagnostics.TraceListenerCollection.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Remove(ITraceListener traceListener)
        {
            Trace.Listeners.Remove(traceListener as TextWriterTraceListener);
        }

        /// <summary>
        /// Wrapper over Dispose() of ITraceListener.
        /// Also resets the standard output/error streams.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        public void Dispose(ITraceListener traceListener)
        {
            traceListener.Dispose();
            Console.SetOut(this.origStdOut);
            Console.SetError(this.origStdErr);
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}

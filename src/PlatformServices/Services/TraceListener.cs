// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Diagnostics;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    /// <summary>
    /// Internal implementation of TraceListener exposed to the user.
    /// The virtual operations of the TraceListener are implemented here
    /// like Close(), Dispose() etc.
    /// </summary>
    public class TraceListenerWrapper : TextWriterTraceListener, ITraceListener
    {
        /// <inheritdoc />
        public TraceListenerWrapper(TextWriter textWriter)
            : base(textWriter)
        {
        }

        /// <summary>
        /// Gets the text writer of System.Diagnostics.TextWriterTraceListener.Writer
        /// that receives the tracing or debugging output.
        /// </summary>
        /// <returns></returns>
        public TextWriter GetWriter()
        {
            return this.Writer;
        }
    }
}

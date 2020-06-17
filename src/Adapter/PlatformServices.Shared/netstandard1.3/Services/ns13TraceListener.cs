// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Diagnostics;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Internal implementation of TraceListener exposed to the user.
    /// The virtual operations of the TraceListener are implemented here
    /// like Close(), Dispose() etc.
    /// </summary>
    public class TraceListenerWrapper : TextWriterTraceListener, ITraceListener
    {
        // Summary:
        //     Initializes a new instance of an object that derives from System.Diagnostics.TextWriterTraceListener
        //     Class and initializes TextWriterTraceListener object using the specified writer as recipient of the
        //     tracing or debugging output.
        public TraceListenerWrapper(TextWriter textWriter)
            : base(textWriter)
        {
        }

        // Summary:
        //     Wrapper over Dispose() of System.Diagnostics.TextWriterTraceListener object
        public new void Dispose()
        {
            base.Dispose();
        }

        // Summary:
        //     Gets the text writer of System.Diagnostics.TextWriterTraceListener.Writer
        //     that receives the tracing or debugging output.
        public TextWriter GetWriter()
        {
            return this.Writer;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}

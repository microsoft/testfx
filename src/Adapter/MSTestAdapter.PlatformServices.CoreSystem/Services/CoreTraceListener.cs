// Copyright (c) Microsoft. All rights reserved.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using System.IO;
using System;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    /// <summary>
    /// Internal implementation of TraceListener exposed to the user.
    /// </summary>
    /// <remarks>
    /// The virtual operations of the TraceListener are implemented here
    /// like Close(), Dispose() etc.
    /// </remarks>
    public class TraceListenerWrapper : ITraceListener
    {

        /// <summary>
        ///     Initializes a new instance of an object using the specified writer as recipient of the 
        ///     tracing or debugging output.
        /// </summary>
        public TraceListenerWrapper(TextWriter textWriter)
        {
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Close()
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public void Dispose()
        {
            return;
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        public TextWriter GetWriter()
        {
            return null;
        }
    }
}

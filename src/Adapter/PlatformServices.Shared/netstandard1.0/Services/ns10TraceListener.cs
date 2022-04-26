// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

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
        /// Initializes a new instance of the <see cref="TraceListenerWrapper"/> class.
        /// </summary>
        /// <param name="textWriter">Writer instance for tracing or debugging output.</param>
        public TraceListenerWrapper(TextWriter textWriter)
        {
        }

        /// <summary>
        /// Returning as this feature is not supported in ASP .net and UWP
        /// </summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep as instance member.")]
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
        /// Returning null as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <returns>A TextWriter instance. Null for now since this is unsupported.</returns>
        public TextWriter GetWriter() => null;
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}

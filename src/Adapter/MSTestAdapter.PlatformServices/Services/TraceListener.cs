// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Diagnostics;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Internal implementation of TraceListener exposed to the user.
/// </summary>
/// <remarks>
/// The virtual operations of the TraceListener are implemented here
/// like Close(), Dispose() etc.
/// </remarks>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TraceListenerWrapper :
#if !WINDOWS_UWP && !WIN_UI
    TextWriterTraceListener,
#endif
    ITraceListener
{
#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceListenerWrapper"/> class.
    /// that derives from System.Diagnostics.TextWriterTraceListener
    /// class and initializes TextWriterTraceListener object using the specified writer as recipient of the tracing or debugging output.
    /// </summary>
    /// <param name="textWriter">Writer instance for tracing or debugging output.</param>
#else
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceListenerWrapper"/> class.
    /// that derives from System.Diagnostics.TextWriterTraceListener
    /// class and initializes TextWriterTraceListener object using the specified writer as recipient of the tracing or debugging output.
    /// </summary>
    /// <param name="textWriter">Writer instance for tracing or debugging output.</param>
#endif
    public TraceListenerWrapper(TextWriter textWriter)
#if !WINDOWS_UWP && !WIN_UI
        : base(textWriter)
#endif
    {
    }

#if WIN_UI || WINDOWS_UWP
    /// <summary>
    /// Returning as this feature is not supported in ASP .NET and UWP.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Returning as this feature is not supported in ASP .NET and UWP.
    /// </summary>
    public void Close()
        => Dispose();
#endif

    /// <summary>
    /// Gets the text writer of System.Diagnostics.TextWriterTraceListener.Writer
    /// that receives the tracing or debugging output.
    /// </summary>
    public TextWriter? GetWriter() =>
#if !WINDOWS_UWP && !WIN_UI
        Writer;
#else
        null;
#endif

}

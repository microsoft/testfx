// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETSTANDARD || NETFRAMEWORK || NETCOREAPP || WINDOWS_UWP
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System.Diagnostics;
using System.IO;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.Utilities;

/// <summary>
/// Internal implementation of TraceListener exposed to the user.
/// </summary>
/// <remarks>
/// The virtual operations of the TraceListener are implemented here
/// like Close(), Dispose() etc.
/// </remarks>
public class TraceListenerWrapper :
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
    TextWriterTraceListener,
#endif
    ITraceListener
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceListenerWrapper"/> class
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
    /// that derives from System.Diagnostics.TextWriterTraceListener
    /// class and initializes TextWriterTraceListener object using the specified writer as recipient of the tracing or debugging output
#endif
    /// .
    /// </summary>
    /// <param name="textWriter">Writer instance for tracing or debugging output.</param>
    public TraceListenerWrapper(TextWriter textWriter)
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        : base(textWriter)
#endif
    {
    }

#if NETCOREAPP || WIN_UI || WINDOWS_UWP || NETSTANDARD_PORTABLE
    /// <summary>
    /// Returning as this feature is not supported in ASP .NET and UWP
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Returning as this feature is not supported in ASP .NET and UWP
    /// </summary>
    public void Close()
        => Dispose();
#endif

    // Summary:
    //     Gets the text writer of System.Diagnostics.TextWriterTraceListener.Writer
    //     that receives the tracing or debugging output.
    public TextWriter GetWriter()
    {
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        return Writer;
#else
        return null;
#endif
    }
}
#endif

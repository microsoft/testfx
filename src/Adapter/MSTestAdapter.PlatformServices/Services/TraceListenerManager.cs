// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Diagnostics;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Internal implementation of TraceListenerManager exposed to the user.
/// Responsible for performing Add(), Remove(), Close(), Dispose() operations on traceListener object.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TraceListenerManager : ITraceListenerManager
{
#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Original output stream.
    /// </summary>
    private readonly TextWriter _origStdOut;

    /// <summary>
    /// Original error stream.
    /// </summary>
    private readonly TextWriter _origStdErr;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceListenerManager"/> class.
    /// </summary>
    /// <param name="outputWriter">A writer instance to log output messages.</param>
    /// <param name="errorWriter">A writer instance to log error messages.</param>
    public TraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
    {
#if !WINDOWS_UWP && !WIN_UI
        _origStdOut = Console.Out;
        _origStdErr = Console.Error;

        // Update the output/error streams with redirected streams
        Console.SetOut(outputWriter);
        Console.SetError(errorWriter);
#endif
    }

    /// <summary>
    /// Adds the argument traceListener object to System.Diagnostics.TraceListenerCollection.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Add(ITraceListener traceListener)
    {
#if !WINDOWS_UWP && !WIN_UI
        // NOTE: Listeners will not get Debug events in dotnet core due to platform limitation.
        // Refer https://github.com/Microsoft/testfx/pull/218 for more details.
#pragma warning disable IDE0022 // Use expression body for method
        Trace.Listeners.Add((TextWriterTraceListener)traceListener);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Removes the argument traceListener object from System.Diagnostics.TraceListenerCollection.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Remove(ITraceListener traceListener)
    {
#if !WINDOWS_UWP && !WIN_UI
#pragma warning disable IDE0022 // Use expression body for method
        Trace.Listeners.Remove(traceListener as TextWriterTraceListener);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Wrapper over Dispose() of ITraceListener.
    /// Also resets the standard output/error streams.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Dispose(ITraceListener traceListener)
    {
#if !WINDOWS_UWP && !WIN_UI
        traceListener.Dispose();
        Console.SetOut(_origStdOut);
        Console.SetError(_origStdErr);
#endif
    }

#if WIN_UI || WINDOWS_UWP
    /// <summary>
    /// Returning as this feature is not supported in ASP .net and UWP.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Close(ITraceListener traceListener)
        => Dispose(traceListener);
#endif
}

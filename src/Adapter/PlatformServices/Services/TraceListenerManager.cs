// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP || WINDOWS_UWP
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.Diagnostics;
using System.IO;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Internal implementation of TraceListenerManager exposed to the user.
/// Responsible for performing Add(), Remove(), Close(), Dispose() operations on traceListener object.
/// </summary>
public class TraceListenerManager : ITraceListenerManager
{
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
    /// <summary>
    /// Original output stream
    /// </summary>
    private readonly TextWriter _origStdOut;

    /// <summary>
    /// Original error stream
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
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
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
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        // NOTE: Listeners will not get Debug events in dotnet core due to platform limitation.
        // Refer https://github.com/Microsoft/testfx/pull/218 for more details.
        Trace.Listeners.Add(traceListener as TextWriterTraceListener);
#endif
    }

    /// <summary>
    /// Removes the argument traceListener object from System.Diagnostics.TraceListenerCollection.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Remove(ITraceListener traceListener)
    {
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        Trace.Listeners.Remove(traceListener as TextWriterTraceListener);
#endif
    }

    /// <summary>
    /// Wrapper over Dispose() of ITraceListener.
    /// Also resets the standard output/error streams.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Dispose(ITraceListener traceListener)
    {
#if NETFRAMEWORK || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        traceListener.Dispose();
        Console.SetOut(_origStdOut);
        Console.SetError(_origStdErr);
#endif
    }

#if NETCOREAPP || WIN_UI || WINDOWS_UWP || NETSTANDARD_PORTABLE
    /// <summary>
    /// Returning as this feature is not supported in ASP .net and UWP
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    public void Close(ITraceListener traceListener)
        => Dispose(traceListener);
#endif
}
#endif

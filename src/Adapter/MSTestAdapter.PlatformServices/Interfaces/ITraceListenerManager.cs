// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Manager to perform operations on the TraceListener object passed as parameter.
/// These operations are implemented differently for each platform service.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface ITraceListenerManager
{
    /// <summary>
    /// Adds the argument traceListener object to TraceListenerCollection.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    void Add(ITraceListener traceListener);

    /// <summary>
    /// Removes the argument traceListener object from TraceListenerCollection.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    void Remove(ITraceListener traceListener);

    /// <summary>
    /// Disposes the traceListener object passed as argument.
    /// </summary>
    /// <param name="traceListener">The trace listener instance.</param>
    void Dispose(ITraceListener traceListener);
}

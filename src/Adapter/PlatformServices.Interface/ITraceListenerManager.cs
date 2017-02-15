// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using System.IO;

    /// <summary>
    /// Manager to perform operations on the TraceListener object passed as parameter.
    /// These operations are implemented differently for each platform service.
    /// </summary>
    public interface ITraceListenerManager
    {
        /// <summary>
        /// Adds the arguement traceListener object to TraceListenerCollection.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        void Add(ITraceListener traceListener);

        /// <summary>
        /// Removes the arguement traceListener object from TraceListenerCollection.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        void Remove(ITraceListener traceListener);

        /// <summary>
        /// Closes the writer which is monitored by the arguement traceListener object.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        void Close(ITraceListener traceListener);

        /// <summary>
        /// Disposes the traceListener object passed as arguement.
        /// </summary>
        /// <param name="traceListener">The trace listener instance.</param>
        void Dispose(ITraceListener traceListener);
    }
}

// Copyright (c) Microsoft. All rights reserved.

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
        void Add(ITraceListener traceListener);

        /// <summary>
        /// Removes the arguement traceListener object from TraceListenerCollection.
        /// </summary>
        void Remove(ITraceListener traceListener);

        /// <summary>
        /// Closes the writer which is monitored by the arguement traceListener object.
        /// </summary>
        void Close(ITraceListener traceListener);

        /// <summary>
        /// Disposes the traceListener object passed as arguement.
        /// </summary>
        void Dispose(ITraceListener traceListener);
    }
}

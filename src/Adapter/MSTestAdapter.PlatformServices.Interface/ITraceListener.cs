// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using System.IO;

    /// <summary>
    /// Operations on the TraceListener object that is implemented differently for each platform.
    /// </summary>
    public interface ITraceListener
    {
        /// <summary>
        /// Gets the text writer that receives the tracing or debugging output.
        /// </summary>   
        TextWriter GetWriter();

        /// <summary>
        /// Closes the TextWriter so that it no longer receives tracing or debugging output.
        /// </summary>    
        void Close();

        /// <summary>
        ///  Disposes this TraceListener object.
        /// </summary>  
        void Dispose();
    }
}

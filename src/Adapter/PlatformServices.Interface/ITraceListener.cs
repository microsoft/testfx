// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

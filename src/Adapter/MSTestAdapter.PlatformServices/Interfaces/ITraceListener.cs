// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Operations on the TraceListener object that is implemented differently for each platform.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface ITraceListener
{
    /// <summary>
    /// Gets the text writer that receives the tracing or debugging output.
    /// </summary>
    /// <returns>The writer instance.</returns>
    TextWriter? GetWriter();

    /// <summary>
    ///  Disposes this TraceListener object.
    /// </summary>
    void Dispose();
}

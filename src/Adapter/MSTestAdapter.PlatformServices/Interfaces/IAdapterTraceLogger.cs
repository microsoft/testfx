// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1716 // Do not use reserved keywords
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#pragma warning restore CA1716 // Do not use reserved keywords

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface IAdapterTraceLogger
{
    /// <summary>
    /// Log an error in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    void LogError(string format, params object?[] args);

    /// <summary>
    /// Log a warning in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    void LogWarning(string format, params object?[] args);

    /// <summary>
    /// Log an information message in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    void LogInfo(string format, params object?[] args);
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1716 // Do not use reserved keywords
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#pragma warning restore CA1716 // Do not use reserved keywords

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
internal interface IAdapterTraceLogger
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

    /// <summary>
    /// Log a verbose message in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    void LogVerbose(string format, params object?[] args);

    bool IsInfoEnabled { get; }

    bool IsWarningEnabled { get; }

    bool IsErrorEnabled { get; }

    bool IsVerboseEnabled { get; }

#if NETFRAMEWORK
    /// <summary>
    /// Prepares a remote AppDomain for tracing by setting up the trace listeners.
    /// </summary>
    /// <param name="appDomain">The AppDomain to prepare.</param>
    void PrepareRemoteAppDomain(AppDomain appDomain);
#endif
}

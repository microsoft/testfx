// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// Provides information about the built-in diagnostic file logger.
/// </summary>
/// <remarks>
/// This service is registered only when diagnostic logging is enabled.
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IDiagnosticLoggingInformation
{
    /// <summary>
    /// Gets a value indicating whether diagnostic log entries are written synchronously.
    /// </summary>
    bool SynchronousWrite { get; }

    /// <summary>
    /// Gets the diagnostic log file that is currently receiving log entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path can change during test application construction if the platform relocates the log to
    /// the effective results directory. Extensions created during that phase should retain this service
    /// and read this property when the path is needed instead of caching the returned <see cref="FileInfo"/>.
    /// </para>
    /// <para>
    /// If the platform cannot safely move an earlier log file during recovery, that partial file can remain
    /// in its original location while this property identifies the replacement file that receives new entries.
    /// </para>
    /// <para>
    /// Use <see cref="FileInfo.Directory"/> or <see cref="FileInfo.DirectoryName"/> to get the resolved
    /// diagnostic output directory.
    /// </para>
    /// </remarks>
    FileInfo LogFile { get; }

    /// <summary>
    /// Gets the minimum log level written to the diagnostic log file.
    /// </summary>
    LogLevel LogLevel { get; }
}

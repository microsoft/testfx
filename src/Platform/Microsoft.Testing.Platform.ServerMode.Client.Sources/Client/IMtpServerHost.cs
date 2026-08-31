// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// A launched Microsoft.Testing.Platform (MTP) server the client owns: either an external process
/// (<see cref="MtpServerProcess"/>) or an application hosted in the caller's own process
/// (<see cref="MtpServerInProcessHost"/>).
/// </summary>
/// <remarks>
/// Disposing the host tears the server down. <see cref="MtpServerClient"/> holds the host and disposes it,
/// so the two launch paths share one ownership rule: whoever launched the server closes the transport and
/// stops the server.
/// </remarks>
internal interface IMtpServerHost : IDisposable
{
    /// <summary>
    /// Gets the transport connection to the launched application. The read loop is NOT started yet; the
    /// owner must attach handlers and call <see cref="MtpJsonRpcConnection.Start"/>.
    /// </summary>
    MtpJsonRpcConnection Connection { get; }

    /// <summary>
    /// Gets the process id of the application, or 0 when it is not known (for example a process that has
    /// already exited).
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Gets the exit code the application reported, or <see langword="null"/> while it is still running (or
    /// when it failed rather than exiting).
    /// </summary>
    int? ExitCode { get; }

    /// <summary>
    /// Tears the server down without blocking the calling thread.
    /// </summary>
    /// <remarks>
    /// This is the preferred teardown on platforms with a responsiveness watchdog (Android ANR, the iOS
    /// watchdog), where the synchronous <see cref="IDisposable.Dispose"/> wait is not acceptable. Calling
    /// <see cref="IDisposable.Dispose"/> afterwards is safe and returns immediately.
    /// </remarks>
    Task ShutdownAsync();
}

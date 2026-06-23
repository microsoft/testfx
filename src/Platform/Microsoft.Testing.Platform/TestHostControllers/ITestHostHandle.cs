// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents a launched test host that the platform monitors for completion.
/// </summary>
/// <remarks>
/// The handle is intentionally agnostic of the underlying launch mechanism: it does not assume a
/// local OS process. <see cref="ProcessId"/> is therefore optional and used only for diagnostics;
/// the platform tracks completion through <see cref="WaitForExitAsync"/>, <see cref="HasExited"/>,
/// <see cref="ExitCode"/>, and the <see cref="Exited"/> event.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostHandle
{
    /// <summary>
    /// Occurs when the test host exits.
    /// </summary>
    event EventHandler Exited;

    /// <summary>
    /// Gets the operating-system process identifier of the test host, when one is available.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the launch mechanism does not expose a local, queryable
    /// process id (for example a container or a remote launch). The value is used only for logging.
    /// </remarks>
    int? ProcessId { get; }

    /// <summary>
    /// Gets the exit code of the test host. Only valid once <see cref="HasExited"/> is
    /// <see langword="true"/>.
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the test host has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Waits asynchronously for the test host to exit.
    /// </summary>
    /// <returns>A task that completes when the test host has exited.</returns>
    Task WaitForExitAsync();

    /// <summary>
    /// Terminates the test host. The platform calls this for best-effort teardown (for example when
    /// hang dump decides to abort the run).
    /// </summary>
    void Terminate();
}

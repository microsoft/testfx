// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents a launched test host that the platform monitors for completion.
/// </summary>
/// <remarks>
/// The handle is intentionally agnostic of the underlying launch mechanism: it does not assume a
/// local OS process. <see cref="Identifier"/> is therefore an optional, free-form string used only
/// for diagnostics; the platform tracks completion through <see cref="WaitForExitAsync"/>,
/// <see cref="HasExited"/>, and <see cref="ExitCode"/>.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostHandle
{
    /// <summary>
    /// Gets an optional, free-form identifier for the launched test host, used only for diagnostics.
    /// </summary>
    /// <remarks>
    /// The value can be anything meaningful for the launch mechanism — for example a process id, a
    /// container id, or a remote <c>host:pid</c>. Returns <see langword="null"/> when the launcher
    /// has nothing useful to surface. The platform never relies on this value for control flow.
    /// </remarks>
    string? Identifier { get; }

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
    /// Waits asynchronously for the test host to exit. The platform may await this more than once.
    /// </summary>
    /// <returns>A task that completes when the test host has exited.</returns>
    Task WaitForExitAsync();

    /// <summary>
    /// Terminates the test host. The platform calls this for best-effort teardown (for example when
    /// hang dump decides to abort the run).
    /// </summary>
    void Terminate();
}

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
/// <see cref="HasExited"/>, and <see cref="ExitCode"/>. The platform owns the handle for the whole
/// lifetime of the test host and disposes it (via <see cref="IDisposable"/>) once the host has
/// exited, so implementations should release any OS resources they hold (process objects, sockets,
/// container clients, …) in <see cref="IDisposable.Dispose"/>.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostHandle : IDisposable
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
    /// Gets the exit code of the test host.
    /// </summary>
    /// <remarks>
    /// Only valid once <see cref="HasExited"/> is <see langword="true"/> (or after
    /// <see cref="WaitForExitAsync"/> has completed). Reading it while the host is still running is
    /// undefined; implementations are not required to throw.
    /// </remarks>
    int ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the test host has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Waits asynchronously for the test host to exit, or for <paramref name="cancellationToken"/> to
    /// be canceled. The platform may await this more than once.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the test host has exited.</returns>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Terminates the test host. The platform calls this for best-effort teardown (for example when
    /// hang dump decides to abort the run).
    /// </summary>
    void Terminate();
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Implemented by an <see cref="ITestHostLauncher"/> that starts the test host inside a Windows
/// AppContainer, to authorize that container on the controller-to-host IPC pipe.
/// </summary>
/// <remarks>
/// <para>
/// The platform creates the controller pipe with the equivalent of
/// <c>PipeOptions.CurrentUserOnly</c>: the pipe is owned by the creating token's owner SID and its DACL
/// grants only that SID. An AppContainer client runs with a <em>restricted</em> token, and Windows grants
/// access only when both the normal check and the restricted-SID check succeed. A DACL that names only the
/// user therefore rejects an AppContainer host even though it belongs to the same signed-in user, no matter
/// that the host knows the pipe name.
/// </para>
/// <para>
/// The pipe is created before the launcher runs (it has to be listening before the host is started), so a
/// launcher that needs this cannot supply the package identity from
/// <see cref="ITestHostLauncher.LaunchTestHostAsync"/>. The platform instead calls this interface just
/// before creating the pipe, which is why it is a separate interface an <see cref="ITestHostLauncher"/>
/// opts into rather than a member of that interface.
/// </para>
/// <para>
/// <strong>Security.</strong> Only a specific AppContainer package SID (<c>S-1-15-2-…</c>) may be
/// authorized, and it receives only the rights required to connect and exchange messages. The platform
/// rejects anything else — user SIDs, group SIDs, <c>Everyone</c>, and in particular the catch-all
/// <c>ALL APPLICATION PACKAGES</c> (<c>S-1-15-2-1</c>) and <c>ALL RESTRICTED APPLICATION PACKAGES</c>
/// (<c>S-1-15-2-2</c>) SIDs — and fails the run with an actionable error. The current-user and elevation
/// protection of the pipe is always preserved, and no mandatory integrity label is lowered, so Mandatory
/// Integrity Control stays a second gate behind the DACL. Returning an empty collection keeps the default
/// pipe exactly as it is today.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostControllerPipeAuthorizer
{
    /// <summary>
    /// Returns the AppContainer package SIDs, in SDDL string form (<c>S-1-15-2-…</c>), that must be able to
    /// connect to the controller-to-host IPC pipe in addition to the current user.
    /// </summary>
    /// <remarks>
    /// Return an empty collection when no extra authorization is needed, which is the case for every
    /// non-AppContainer host — including a packaged full-trust MSIX desktop app — and on every operating
    /// system other than Windows, where the returned values are ignored.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The AppContainer package SIDs to authorize.</returns>
    Task<IReadOnlyList<string>> GetAuthorizedAppContainerSecurityIdentifiersAsync(CancellationToken cancellationToken);
}

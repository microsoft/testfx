// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Implemented by an <see cref="ITestHostLauncher"/> that starts the test host under an operating-system
/// security identity of its own, to authorize that identity on the controller-to-host connection.
/// </summary>
/// <remarks>
/// <para>
/// The platform protects the controller-to-host connection so that only the account that started the run
/// can use it. On Windows that is the equivalent of <c>PipeOptions.CurrentUserOnly</c>: the pipe is owned
/// by the creating token's owner SID and its access control list grants only that SID.
/// </para>
/// <para>
/// A launcher that starts the host in a sandbox rather than as a plain child process leaves that host with
/// a different, more restricted identity, which the connection does not yet admit — knowing the connection
/// name is not enough. This interface is how such a launcher declares the identity that must additionally
/// be allowed through.
/// </para>
/// <para>
/// The connection is established before the launcher runs (it has to be listening before the host is
/// started), so a launcher that needs this cannot supply the identity from
/// <see cref="ITestHostLauncher.LaunchTestHostAsync"/>. The platform instead calls this interface just
/// before creating the connection, which is why it is a separate interface an
/// <see cref="ITestHostLauncher"/> opts into rather than a member of that interface.
/// </para>
/// <para>
/// <strong>Security.</strong> The platform does not grant whatever it is handed. It validates every value
/// and honors only the identity of a <em>single sandboxed application</em>, granting it just the rights
/// required to connect and exchange messages. Anything else — a user, a group, <c>Everyone</c>, or an
/// identity shared by every sandboxed application on the machine — is rejected and fails the run with an
/// actionable error rather than widening the connection. The current-user and elevation protection is
/// always preserved. Returning an empty collection keeps the default behavior exactly as it is.
/// </para>
/// <para>
/// This is meaningful only where the operating system can express such an identity; today that is Windows,
/// where a sandboxed application is an AppContainer and the values below are its package SID. Elsewhere
/// the returned values are ignored and the connection is created unchanged.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostControllerConnectionAuthorizer
{
    /// <summary>
    /// Returns the operating-system security identities that must be able to reach the controller-to-host
    /// connection in addition to the current user.
    /// </summary>
    /// <remarks>
    /// On Windows an identity is a security identifier (SID) in SDDL string form, and only the SID of a
    /// single sandboxed application is honored. Return an empty collection when no extra authorization is
    /// needed, which is the case for every host started as an ordinary child process and on every operating
    /// system that cannot express such an identity.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The security identities to authorize.</returns>
    Task<IReadOnlyList<string>> GetAuthorizedSecurityIdentitiesAsync(CancellationToken cancellationToken);
}

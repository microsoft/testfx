// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// Derives the AppContainer package security identifier (SID) of a packaged Windows app from its package
/// family name, so the launcher can ask the platform to authorize exactly that package — and nothing else —
/// on the test host controller pipe.
/// </summary>
/// <remarks>
/// <para>
/// A UWP (or AppContainer-configured WinUI) test host runs with a restricted token whose restricting SIDs
/// contain the package SID. Windows grants access to a securable object only when both the normal access
/// check and the restricted-SID check succeed, so an object whose DACL names only the user SID is
/// unreachable from the AppContainer even though it belongs to the same signed-in user. Handing the package
/// SID to the platform is what closes that gap, while keeping the grant scoped to this one application
/// instead of the catch-all <c>ALL APPLICATION PACKAGES</c> SID.
/// </para>
/// <para>
/// <b>Why not <c>DeriveAppContainerSidFromAppContainerName</c>.</b> That Win32 API looks like the obvious
/// choice, but its result depends on the <em>calling</em> process: invoked from a process that itself has
/// package identity it returns a <em>child</em> AppContainer SID (the caller's package SID followed by four
/// extra sub-authorities) instead of the package SID of the requested name. The MTP controller is very often
/// exactly such a process here — it is the packaged test application itself — so the API would yield a SID
/// no test host ever runs under and the pipe would still reject the host. The package SID is instead
/// computed with the stable derivation Windows uses to build its own <c>AppContainer\Mappings</c> table:
/// <c>S-1-15-2-</c> followed by the first seven little-endian <c>uint32</c> values of the SHA-256 hash of
/// the lower-cased package family name encoded as UTF-16LE. A unit test cross-checks this against the
/// AppContainer mappings Windows registered on the machine.
/// </para>
/// </remarks>
internal static class AppContainerSecurityIdentifier
{
    /// <summary>
    /// The identifier authority and base RID shared by every AppContainer SID
    /// (<c>SECURITY_APP_PACKAGE_AUTHORITY</c> + <c>SECURITY_APP_PACKAGE_BASE_RID</c>).
    /// </summary>
    private const string AppContainerSidPrefix = "S-1-15-2";

    /// <summary>
    /// A package SID carries seven sub-authorities after the base RID, that is the first 28 bytes of the
    /// 32-byte SHA-256 hash. The remaining four bytes are not part of the SID.
    /// </summary>
    private const int SubAuthorityCount = 7;

    /// <summary>
    /// Derives the AppContainer package SID of <paramref name="packageFamilyName"/> in SDDL string form
    /// (<c>S-1-15-2-…</c>), or returns <see langword="null"/> when there is nothing to derive it from.
    /// </summary>
    /// <remarks>
    /// A missing package family name is reported as <see langword="null"/> rather than as an exception: not
    /// being able to authorize the package makes the AppContainer connect-back fail with its own actionable
    /// error, and must not take down an otherwise valid run (for example a full-trust packaged app that
    /// never needed the grant).
    /// </remarks>
    /// <param name="packageFamilyName">The package family name (<c>{PackageName}_{publisherId}</c>).</param>
    /// <returns>The AppContainer package SID, or <see langword="null"/>.</returns>
    public static string? TryDerive(string? packageFamilyName)
    {
        if (packageFamilyName is null || packageFamilyName.Trim().Length == 0)
        {
            return null;
        }

        // Windows lower-cases the moniker before hashing it; a package family name is case-insensitive, so
        // 'Contoso.App_8wekyb3d8bbwe' and 'contoso.app_8wekyb3d8bbwe' must derive the same SID.
        byte[] moniker = Encoding.Unicode.GetBytes(packageFamilyName.Trim().ToLowerInvariant());

        byte[] hash;
        using (var sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(moniker);
        }

        var builder = new StringBuilder(AppContainerSidPrefix);
        for (int i = 0; i < SubAuthorityCount; i++)
        {
            uint subAuthority = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(i * sizeof(uint), sizeof(uint)));
            builder.Append(CultureInfo.InvariantCulture, $"-{subAuthority}");
        }

        return builder.ToString();
    }
}

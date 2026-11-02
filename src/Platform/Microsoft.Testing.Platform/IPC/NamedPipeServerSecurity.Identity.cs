// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Testing.Platform.IPC;

internal static partial class NamedPipeServerSecurity
{
    // The expected shapes are exact, not minimums:
    //   package SID: 'S', revision, identifier authority (15), SECURITY_APP_PACKAGE_BASE_RID (2), then
    //                the seven sub-authorities derived from the package family name = 11 dash-separated parts;
    //   child SID:   package SID plus four child sub-authorities = 15 parts.
    // Accepting a partial or overlong descendant would break the fail-closed policy and defer it to an
    // unlocalized SDDL/Win32 error.
    private const int AppContainerPackageSidPartCount = 11;
    private const int ChildAppContainerSidPartCount = 15;

    private const uint TokenQuery = 0x0008;
    private const int TokenOwnerInformationClass = 4;
    private const int ErrorInsufficientBuffer = 122;

    /// <summary>
    /// Qualifies <paramref name="pipeName"/> for the login-session-local named-pipe namespace required by
    /// packaged/AppContainer clients. The operation is idempotent.
    /// </summary>
    internal static string GetPipeNameForSandboxedApplication(string pipeName)
        => pipeName.StartsWith(SandboxedApplicationPipeNamePrefix, StringComparison.OrdinalIgnoreCase)
            ? pipeName
            : SandboxedApplicationPipeNamePrefix + pipeName;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="securityIdentifier"/> identifies a
    /// <em>single sandboxed application</em> and may therefore be authorized on the controller pipe. On
    /// Windows that is an AppContainer SID — a package SID or a child-AppContainer SID.
    /// </summary>
    /// <remarks>
    /// This is the platform-side least-privilege guard: it is applied to whatever an extension asks for, so
    /// a buggy or hostile extension cannot turn the extension point into a way of granting access to
    /// <c>Everyone</c>, <c>Authenticated Users</c>, another user, or every packaged application on the
    /// machine. It is an allow-list rather than a deny-list precisely so that an identity nobody thought
    /// about is refused by default. The check is purely syntactic on purpose — it is a policy filter, not a
    /// proof that the SID exists.
    /// </remarks>
    /// <param name="securityIdentifier">The SID in SDDL string form (<c>S-1-15-2-…</c>).</param>
    /// <returns><see langword="true"/> when the SID may be added to the pipe DACL.</returns>
    internal static bool IsAuthorizableSandboxedApplicationIdentity([NotNullWhen(true)] string? securityIdentifier)
    {
        if (RoslynString.IsNullOrWhiteSpace(securityIdentifier))
        {
            return false;
        }

        string normalized = Normalize(securityIdentifier);

        if (!normalized.StartsWith(AppContainerSidPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized is AllApplicationPackagesSid or AllRestrictedApplicationPackagesSid)
        {
            return false;
        }

        string[] parts = normalized.Split('-');
        if (parts.Length is not (AppContainerPackageSidPartCount or ChildAppContainerSidPartCount))
        {
            return false;
        }

        // Every sub-authority after the 'S-1-15' prefix must be a plain unsigned 32-bit number, which also
        // rules out anything that merely starts with the AppContainer prefix but is not a SID.
        for (int i = 3; i < parts.Length; i++)
        {
            if (!uint.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Normalizes a SID string to the canonical upper-case form used in the security descriptor, so the
    /// same SID written in different cases produces the same DACL.
    /// </summary>
    /// <param name="securityIdentifier">The SID in SDDL string form.</param>
    /// <returns>The normalized SID.</returns>
    internal static string Normalize(string securityIdentifier)
        => securityIdentifier.Trim().ToUpperInvariant();

    /// <summary>
    /// Returns the current process token's <i>owner</i> SID — the very SID
    /// <c>PipeOptions.CurrentUserOnly</c> uses — in SDDL string form.
    /// </summary>
    /// <remarks>
    /// The owner (rather than the user) SID is what preserves the elevation split: an elevated token's owner
    /// is <c>BUILTIN\Administrators</c>, so an elevated controller's pipe is not reachable from a
    /// non-elevated process of the same user, and vice versa.
    /// </remarks>
    /// <returns>The owner SID.</returns>
    [SupportedOSPlatform("windows")]
    internal static string GetCurrentProcessOwnerSid()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out SafeTokenHandle token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open the current process token.");
        }

        using (token)
        {
            bool sizeQuerySucceeded = GetTokenInformation(token.Value, TokenOwnerInformationClass, IntPtr.Zero, 0, out int length);
            if (sizeQuerySucceeded)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the size of the current process token owner.");
            }

            if (Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the size of the current process token owner.");
            }

            using var buffer = SafeHGlobalHandle.Allocate(length);
            if (!GetTokenInformation(token.Value, TokenOwnerInformationClass, buffer.Value, length, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the current process token owner.");
            }

            // TOKEN_OWNER is a single PSID field.
            return ConvertSidToString(Marshal.ReadIntPtr(buffer.Value));
        }
    }

    [SupportedOSPlatform("windows")]
    private static string ConvertSidToString(IntPtr sid)
    {
        if (!ConvertSidToStringSid(sid, out SafeLocalAllocHandle stringSid))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to convert a SID to its string form.");
        }

        using (stringSid)
        {
            return Marshal.PtrToStringUni(stringSid.Value)!;
        }
    }
}

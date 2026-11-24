// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// Windows-only helpers that create a controller named pipe whose discretionary access control list
/// (DACL) keeps the current-user protection of <c>PipeOptions.CurrentUserOnly</c> while
/// additionally authorizing a small, explicitly requested set of AppContainer package SIDs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> On Windows, .NET implements <c>PipeOptions.CurrentUserOnly</c> with a
/// security descriptor whose owner is the creating token's owner SID and whose DACL contains a single
/// ACE granting that same SID full control. An AppContainer (UWP, or a WinUI app configured for
/// AppContainer) runs with a <i>restricted</i> token: Windows performs the normal access check against
/// the token's user/group SIDs <i>and</i> a second check against the token's restricting SIDs, which for
/// an AppContainer contain the package SID. Access is granted only when both checks succeed, so a DACL
/// that names only the user SID denies the AppContainer client even though it belongs to the same
/// signed-in user. Knowing the pipe name is therefore not enough — the package SID must appear in the
/// DACL. See https://learn.microsoft.com/windows/win32/secauthz/restricted-tokens.
/// </para>
/// <para>
/// <b>Security model.</b> The descriptor produced here is deliberately minimal and fully explicit:
/// </para>
/// <list type="bullet">
///   <item>
///     Owner and group are the current token's <i>owner</i> SID, exactly like
///     <c>PipeOptions.CurrentUserOnly</c>. This preserves the elevation split (an elevated token's
///     owner is <c>BUILTIN\Administrators</c>, a non-elevated one's is the user) and keeps the client-side
///     <c>PipeOptions.CurrentUserOnly</c> owner validation working.
///   </item>
///   <item>The DACL is protected (<c>P</c>), so no inherited ACE can widen it.</item>
///   <item>The owner SID gets <c>FILE_ALL_ACCESS</c>-equivalent rights (the same mask .NET grants).</item>
///   <item>
///     Every authorized package SID gets only <see cref="PipeAccessRightsReadWriteSynchronize"/> — read,
///     write, read the security descriptor (needed by the client-side owner validation) and synchronize.
///     Notably it does <i>not</i> include <c>FILE_CREATE_PIPE_INSTANCE</c>, so an authorized package can
///     never create another instance of the pipe and impersonate the controller.
///   </item>
///   <item>
///     Only genuine AppContainer SIDs may be authorized (see <see cref="IsAuthorizableSandboxedApplicationIdentity"/>).
///     The catch-all <c>ALL APPLICATION PACKAGES</c> / <c>ALL RESTRICTED APPLICATION PACKAGES</c> SIDs,
///     user SIDs, group SIDs and <c>Everyone</c> are rejected by construction, so the extension point that
///     feeds this type can only ever widen access to one specific packaged application.
///   </item>
///   <item>
///     The pipe additionally rejects remote clients (<c>PIPE_REJECT_REMOTE_CLIENTS</c>), which the default
///     .NET pipe does not do.
///   </item>
///   <item>
///     No mandatory integrity label is emitted, so the pipe keeps the controller's own integrity level and
///     Mandatory Integrity Control remains a second gate behind the DACL. An AppContainer client is
///     admitted by its package-SID ACE alone.
///   </item>
/// </list>
/// <para>
/// The implementation goes through <c>CreateNamedPipeW</c> rather than <c>PipeSecurity</c> because the
/// managed ACL types are not part of the <c>netstandard2.0</c> surface this assembly also builds for, and
/// because <c>PipeOptions.CurrentUserOnly</c> cannot be combined with an explicit descriptor.
/// </para>
/// </remarks>
[Embedded]
internal static partial class NamedPipeServerSecurity
{
    /// <summary>
    /// The namespace segment Windows requires for named pipes opened by packaged/AppContainer processes.
    /// <c>NamedPipeClientStream</c> adds <c>\\.\pipe\</c> itself, so peers exchange
    /// <c>LOCAL\&lt;name&gt;</c>.
    /// </summary>
    internal const string SandboxedApplicationPipeNamePrefix = "LOCAL\\";

    /// <summary>
    /// The well-known <c>ALL APPLICATION PACKAGES</c> SID. Granting it would let every packaged
    /// application on the machine reach the controller pipe, so it is never authorized.
    /// </summary>
    internal const string AllApplicationPackagesSid = "S-1-15-2-1";

    /// <summary>
    /// The well-known <c>ALL RESTRICTED APPLICATION PACKAGES</c> SID. Rejected for the same reason as
    /// <see cref="AllApplicationPackagesSid"/>.
    /// </summary>
    internal const string AllRestrictedApplicationPackagesSid = "S-1-15-2-2";

    /// <summary>
    /// The identifier-authority/sub-authority prefix shared by every AppContainer SID
    /// (<c>SECURITY_APP_PACKAGE_AUTHORITY</c> + <c>SECURITY_APP_PACKAGE_BASE_RID</c>).
    /// </summary>
    internal const string AppContainerSidPrefix = "S-1-15-2-";

    /// <summary>
    /// <c>PipeAccessRights.FullControl</c>. This is the exact mask .NET grants to the owner when
    /// <c>PipeOptions.CurrentUserOnly</c> is used, so the hardened descriptor is a strict superset
    /// of the default one only by the additional package ACEs.
    /// </summary>
    internal const int PipeAccessRightsFullControl = 0x1F019F;

    /// <summary>
    /// <c>PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize</c>: the minimum a client needs to
    /// open the pipe, exchange messages, and read the security descriptor for its own
    /// <c>PipeOptions.CurrentUserOnly</c> owner validation. Deliberately excludes
    /// <c>FILE_CREATE_PIPE_INSTANCE</c> (<c>0x4</c>), <c>WRITE_DAC</c>, <c>WRITE_OWNER</c> and
    /// <c>DELETE</c>.
    /// </summary>
    internal const int PipeAccessRightsReadWriteSynchronize = 0x12019B;

    /// <summary>
    /// Gets a value indicating whether the hardened pipe path can be used on the current operating system.
    /// AppContainers, SIDs and named-pipe DACLs are Windows concepts; every other platform keeps the
    /// existing pipe implementation untouched.
    /// </summary>
    [SupportedOSPlatformGuard("windows")]
    internal static bool IsSupported =>
#if NET
        OperatingSystem.IsWindows();
#else
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

}

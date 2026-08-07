// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Win32.SafeHandles;

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
internal static class NamedPipeServerSecurity
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

    // The expected shapes are exact, not minimums:
    //   package SID: 'S', revision, identifier authority (15), SECURITY_APP_PACKAGE_BASE_RID (2), then
    //                the seven sub-authorities derived from the package family name = 11 dash-separated parts;
    //   child SID:   package SID plus four child sub-authorities = 15 parts.
    // Accepting a partial or overlong descendant would break the fail-closed policy and defer it to an
    // unlocalized SDDL/Win32 error.
    private const int AppContainerPackageSidPartCount = 11;
    private const int ChildAppContainerSidPartCount = 15;

    private const uint SddlRevision1 = 1;
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint PipeTypeByte = 0x00000000;
    private const uint PipeReadModeByte = 0x00000000;
    private const uint PipeWait = 0x00000000;
    private const uint PipeRejectRemoteClients = 0x00000008;
    private const uint PipeUnlimitedInstances = 255;
    private const uint TokenQuery = 0x0008;
    private const int TokenOwnerInformationClass = 4;
    private const int ErrorInsufficientBuffer = 122;

    private static readonly IntPtr InvalidHandleValue = new(-1);

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
    /// Builds the SDDL security descriptor for the controller pipe.
    /// </summary>
    /// <remarks>
    /// This is the sink where caller-supplied strings are concatenated into a security descriptor, so it
    /// validates every identity itself rather than trusting that a caller already did. That is deliberate:
    /// a caller that validates one enumeration of a sequence and then hands the sequence on cannot
    /// guarantee the second enumeration yields the same values, and an unvalidated value containing
    /// <c>)</c> or <c>(</c> would break out of its ACE and inject additional ones. Validating at the point
    /// of concatenation makes that class of bug impossible regardless of how the sequence behaves.
    /// </remarks>
    /// <param name="ownerSid">The current token's owner SID, which owns the pipe and gets full control.</param>
    /// <param name="authorizedSecurityIdentities">
    /// The identities that additionally get the minimum connect/exchange rights.
    /// </param>
    /// <returns>The SDDL string.</returns>
    /// <exception cref="ArgumentException">
    /// An entry does not identify a single sandboxed application.
    /// </exception>
    internal static string BuildSecurityDescriptor(string ownerSid, IReadOnlyList<string> authorizedSecurityIdentities)
    {
        string owner = Normalize(ownerSid);

        // Copy to an array first. The sequence can come from an extension, so its Count and indexer are not
        // guaranteed to be stable or even self-consistent; once the values are in an array, the value that
        // is validated below is provably the same value that is appended.
        //
        // The snapshot alone is NOT sufficient, and the validation below must not be removed on the grounds
        // that a caller already validated: when the runtime type also implements ICollection<T>, this spread
        // lowers to ICollection<T>.CopyTo, so a hostile implementation still chooses what lands in the
        // array. Validating here — against the very local that is then appended — is what actually closes
        // the hole, because string is immutable and Normalize is pure.
        string[] securityIdentities = [.. authorizedSecurityIdentities];

        var builder = new StringBuilder();

        // Owner and group mirror what PipeOptions.CurrentUserOnly does; 'P' protects the DACL from
        // inheritance so nothing can widen it behind our back.
        builder.Append(CultureInfo.InvariantCulture, $"O:{owner}G:{owner}D:P");
        builder.Append(CultureInfo.InvariantCulture, $"(A;;0x{PipeAccessRightsFullControl:x};;;{owner})");

        foreach (string securityIdentity in securityIdentities)
        {
            if (!IsAuthorizableSandboxedApplicationIdentity(securityIdentity))
            {
                throw new ArgumentException(
                    $"'{securityIdentity ?? "<null>"}' does not identify a single sandboxed application and cannot be authorized on the test host controller pipe.",
                    nameof(authorizedSecurityIdentities));
            }

            builder.Append(CultureInfo.InvariantCulture, $"(A;;0x{PipeAccessRightsReadWriteSynchronize:x};;;{Normalize(securityIdentity)})");
        }

        // No mandatory label is emitted: the pipe keeps the implicit (medium) integrity level of the
        // controller, so Mandatory Integrity Control stays a second gate behind the DACL. An AppContainer
        // client is admitted by its package-SID ACE alone — a lowbox token's access check is satisfied by
        // that ACE and is not blocked by the object's medium label.
        return builder.ToString();
    }

    /// <summary>
    /// Creates a named pipe server stream whose DACL grants the current token's owner full control and each
    /// identity in <paramref name="authorizedSecurityIdentities"/> the minimum rights required to connect
    /// and exchange messages. Every identity is validated here, at the point it is composed into the
    /// security descriptor.
    /// </summary>
    /// <param name="pipeName">The pipe name, without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="maxNumberOfServerInstances">The maximum number of concurrent server instances.</param>
    /// <param name="authorizedSecurityIdentities">The identities to authorize.</param>
    /// <returns>An asynchronous, not-yet-connected server stream.</returns>
    [SupportedOSPlatform("windows")]
    internal static NamedPipeServerStream CreateServerStream(string pipeName, int maxNumberOfServerInstances, IReadOnlyList<string> authorizedSecurityIdentities)
        => CreateServerStreamWithExplicitSecurityDescriptor(pipeName, maxNumberOfServerInstances, BuildSecurityDescriptor(GetCurrentProcessOwnerSid(), authorizedSecurityIdentities));

    /// <summary>
    /// Creates a named pipe server stream protected by a caller-supplied SDDL security descriptor.
    /// </summary>
    /// <remarks>
    /// <strong>This overload performs no validation</strong> — the descriptor is passed to Windows verbatim.
    /// It exists so tests can exercise arbitrary descriptors, including deliberately malformed ones. Product
    /// code must use <see cref="CreateServerStream(string, int, IReadOnlyList{string})"/>, which validates
    /// every identity at the point it is composed. The name is deliberately unlike the validating overload
    /// so this one can never be selected by overload-resolution accident.
    /// </remarks>
    /// <param name="pipeName">The pipe name, without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="maxNumberOfServerInstances">The maximum number of concurrent server instances.</param>
    /// <param name="securityDescriptorSddl">The security descriptor in SDDL form.</param>
    /// <returns>An asynchronous, not-yet-connected server stream.</returns>
    [SupportedOSPlatform("windows")]
    internal static NamedPipeServerStream CreateServerStreamWithExplicitSecurityDescriptor(string pipeName, int maxNumberOfServerInstances, string securityDescriptorSddl)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(securityDescriptorSddl, SddlRevision1, out IntPtr securityDescriptor, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to build the security descriptor for the named pipe '{pipeName}'.");
        }

        try
        {
            SecurityAttributes securityAttributes = new()
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = securityDescriptor,

                // The controller must never leak the listening handle into the test host (or any other
                // child), which would bypass the DACL entirely.
                InheritHandle = 0,
            };

            uint openMode = PipeAccessDuplex
                | FileFlagOverlapped
                // Mirrors what NamedPipeServerStream does: for a single-instance pipe, refuse to attach to a
                // name somebody else already created (anti-squatting).
                | (maxNumberOfServerInstances == 1 ? FileFlagFirstPipeInstance : 0);

            uint pipeMode = PipeTypeByte | PipeReadModeByte | PipeWait | PipeRejectRemoteClients;
            uint maxInstances = maxNumberOfServerInstances == -1 ? PipeUnlimitedInstances : (uint)maxNumberOfServerInstances;

            IntPtr handle = CreateNamedPipe(
                $@"\\.\pipe\{pipeName}",
                openMode,
                pipeMode,
                maxInstances,
                nOutBufferSize: 0,
                nInBufferSize: 0,
                nDefaultTimeOut: 0,
                ref securityAttributes);

            if (handle == InvalidHandleValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to create the named pipe '{pipeName}'.");
            }

            var safePipeHandle = new SafePipeHandle(handle, ownsHandle: true);
            try
            {
                return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, safePipeHandle);
            }
            catch
            {
                safePipeHandle.Dispose();
                throw;
            }
        }
        finally
        {
            LocalFree(securityDescriptor);
        }
    }

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
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out IntPtr token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open the current process token.");
        }

        try
        {
            if (GetTokenInformation(token, TokenOwnerInformationClass, IntPtr.Zero, 0, out int length)
                || Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the size of the current process token owner.");
            }

            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(token, TokenOwnerInformationClass, buffer, length, out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the current process token owner.");
                }

                // TOKEN_OWNER is a single PSID field.
                return ConvertSidToString(Marshal.ReadIntPtr(buffer));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [SupportedOSPlatform("windows")]
    private static string ConvertSidToString(IntPtr sid)
    {
        if (!ConvertSidToStringSid(sid, out IntPtr stringSid))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to convert a SID to its string form.");
        }

        try
        {
            return Marshal.PtrToStringUni(stringSid)!;
        }
        finally
        {
            LocalFree(stringSid);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSecurityDescriptorRevision,
        out IntPtr securityDescriptor,
        IntPtr securityDescriptorSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr stringSid);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint nOutBufferSize,
        uint nInBufferSize,
        uint nDefaultTimeOut,
        ref SecurityAttributes securityAttributes);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}

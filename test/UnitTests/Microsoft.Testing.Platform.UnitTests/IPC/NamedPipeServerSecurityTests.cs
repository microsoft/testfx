// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Covers the security model of the test host controller pipe: which identities may be authorized at all,
/// what the resulting DACL looks like, and — through real Windows access checks — which identities the
/// resulting pipe actually grants and denies.
/// </summary>
[TestClass]
public sealed class NamedPipeServerSecurityTests
{
    // A real AppContainer package SID: the SID Windows derives for the package family name
    // 'Contoso.MyTestApp_8wekyb3d8bbwe'.
    private const string PackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    // The package SID of 'Fabrikam.OtherApp_8wekyb3d8bbwe', used as the "other package" that must be denied.
    private const string OtherPackageSid = "S-1-15-2-2070028120-3905321563-993292099-3146763764-3369997054-704336693-2436638954";

    private const string EveryoneSid = "S-1-1-0";
    private const string AuthenticatedUsersSid = "S-1-5-11";
    private const string LocalSystemSid = "S-1-5-18";
    private const string BuiltinUsersSid = "S-1-5-32-545";

    [TestMethod]
    public void IsAuthorizableAppContainerSid_WithPackageSid_IsAllowed()
    {
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableAppContainerSid(PackageSid));
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableAppContainerSid(OtherPackageSid));

        // A child AppContainer SID appends further sub-authorities to the package SID and is still a
        // specific container.
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableAppContainerSid($"{PackageSid}-1234567890"));
    }

    [TestMethod]
    public void IsAuthorizableAppContainerSid_IsCaseAndWhitespaceInsensitive()
    {
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableAppContainerSid(PackageSid.ToLowerInvariant()));
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableAppContainerSid($"  {PackageSid}  "));
    }

    // The whole point of the extension point is that it can only ever widen access to one specific packaged
    // application. Everything below would widen it to a user, a group, or every packaged app on the machine,
    // so the platform must refuse it regardless of which extension asked.
    [TestMethod]
    [DataRow(null, DisplayName = "null")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    [DataRow("not-a-sid", DisplayName = "not a SID")]
    [DataRow(NamedPipeServerSecurity.AllApplicationPackagesSid, DisplayName = "ALL APPLICATION PACKAGES")]
    [DataRow(NamedPipeServerSecurity.AllRestrictedApplicationPackagesSid, DisplayName = "ALL RESTRICTED APPLICATION PACKAGES")]
    [DataRow(EveryoneSid, DisplayName = "Everyone")]
    [DataRow(AuthenticatedUsersSid, DisplayName = "Authenticated Users")]
    [DataRow(LocalSystemSid, DisplayName = "LocalSystem")]
    [DataRow(BuiltinUsersSid, DisplayName = "BUILTIN\\Users")]
    [DataRow("S-1-5-21-1111111111-2222222222-3333333333-1001", DisplayName = "a user")]
    [DataRow("S-1-15-3-1", DisplayName = "a capability SID")]
    [DataRow("S-1-15-2-1-2-3", DisplayName = "too few sub-authorities to be a package SID")]
    [DataRow("S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-notanumber", DisplayName = "non-numeric sub-authority")]
    [DataRow("AC", DisplayName = "the SDDL alias of ALL APPLICATION PACKAGES")]
    public void IsAuthorizableAppContainerSid_WithAnythingButASpecificAppContainer_IsRejected(string? securityIdentifier)
        => Assert.IsFalse(NamedPipeServerSecurity.IsAuthorizableAppContainerSid(securityIdentifier));

    [TestMethod]
    public void BuildSecurityDescriptor_GrantsOwnerFullControlAndPackagesTheMinimum()
    {
        const string owner = "S-1-5-21-1111111111-2222222222-3333333333-1001";

        string sddl = NamedPipeServerSecurity.BuildSecurityDescriptor(owner, [PackageSid]);

        Assert.AreEqual(
            $"O:{owner}G:{owner}D:P(A;;0x1f019f;;;{owner})(A;;0x12019b;;;{PackageSid})",
            sddl);
    }

    [TestMethod]
    public void BuildSecurityDescriptor_WithoutPackages_IsEquivalentToCurrentUserOnly()
    {
        const string owner = "S-1-5-21-1111111111-2222222222-3333333333-1001";

        string sddl = NamedPipeServerSecurity.BuildSecurityDescriptor(owner, []);

        // 0x1f019f is PipeAccessRights.FullControl, which is exactly what .NET grants the owner for
        // PipeOptions.CurrentUserOnly, and it is the only ACE.
        Assert.AreEqual($"O:{owner}G:{owner}D:P(A;;0x1f019f;;;{owner})", sddl);
    }

    [TestMethod]
    public void BuildSecurityDescriptor_NormalizesSecurityIdentifiers()
    {
        string sddl = NamedPipeServerSecurity.BuildSecurityDescriptor("  s-1-5-18  ", [$" {PackageSid.ToLowerInvariant()} "]);

        Assert.AreEqual($"O:S-1-5-18G:S-1-5-18D:P(A;;0x1f019f;;;S-1-5-18)(A;;0x12019b;;;{PackageSid})", sddl);
    }

    // The package SID must not be able to create another instance of the pipe: that is how a rogue app could
    // impersonate the controller and take the test host's connection.
    [TestMethod]
    [DataRow(0x00000004, "FILE_CREATE_PIPE_INSTANCE")]
    [DataRow(0x00010000, "DELETE")]
    [DataRow(0x00040000, "WRITE_DAC")]
    [DataRow(0x00080000, "WRITE_OWNER")]
    public void AuthorizedPackageMask_DoesNotIncludeDangerousRights(int right, string name)
        => Assert.AreEqual(0, NamedPipeServerSecurity.PipeAccessRightsReadWriteSynchronize & right, $"The authorized package mask must not include {name}.");

    [TestMethod]
    public void IsSupported_MatchesWindows()
        => Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), NamedPipeServerSecurity.IsSupported);

    // Defense in depth: even if a caller forgets to filter, the pipe must never be created with a widened
    // DACL.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void NamedPipeServer_WithNonAppContainerSid_Throws()
    {
        PipeNameDescription pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => _ = new NamedPipeServer(
            pipeName,
            static _ => throw new UnreachableException(),
            new Mock<IEnvironment>().Object,
            new Mock<ILogger>().Object,
            new Mock<ITask>().Object,
            maxNumberOfServerInstances: 1,
            [NamedPipeServerSecurity.AllApplicationPackagesSid],
            CancellationToken.None));

        Assert.Contains(NamedPipeServerSecurity.AllApplicationPackagesSid, exception.Message);
    }

    /// <summary>
    /// Creates a real pipe through the product code path and reads the security descriptor Windows actually
    /// materialized for it, so the assertion is about the live kernel object rather than about a string we
    /// composed.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_ProducesADaclWithOnlyTheOwnerAndTheAuthorizedPackage()
    {
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        using NamedPipeServerStream stream = NamedPipeServerSecurity.CreateServerStream(pipeName, maxNumberOfServerInstances: 1, [PackageSid]);

        string sddl = WindowsSecurity.GetSecurityDescriptorSddl(stream.SafePipeHandle);

        // Exactly two ACEs: the owner and the one authorized package.
        Assert.AreEqual(2, CountAces(sddl), $"Unexpected security descriptor '{sddl}'.");
        Assert.Contains($"(A;;0x12019b;;;{PackageSid})", sddl);
        Assert.Contains("D:P", sddl, "The DACL must be protected so no inherited ACE can widen it.");

        // 'AC' is the SDDL alias Windows renders ALL APPLICATION PACKAGES with; the trailing ')' keeps the
        // check from matching a package SID that merely starts with the same digits.
        Assert.IsFalse(sddl.Contains($";;;{NamedPipeServerSecurity.AllApplicationPackagesSid})", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sddl.Contains(";;;AC)", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sddl.Contains(OtherPackageSid, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Authorizing a package must not relax Mandatory Integrity Control, which is a second gate behind the
    /// DACL: a lowbox token is admitted by its package-SID ACE, so lowering the pipe's integrity label would
    /// buy nothing and would only widen what a low-integrity process may attempt. The security descriptor is
    /// read back with <c>LABEL_SECURITY_INFORMATION</c> so the absence of a label is actually observed on the
    /// live object rather than merely assumed.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    [DataRow(true, DisplayName = "with an authorized package")]
    [DataRow(false, DisplayName = "without an authorized package")]
    public void CreateServerStream_NeverLowersTheIntegrityLabel(bool authorizePackage)
    {
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        string ownerSid = NamedPipeServerSecurity.GetCurrentProcessOwnerSid();
        using NamedPipeServerStream stream = NamedPipeServerSecurity.CreateServerStream(
            pipeName,
            maxNumberOfServerInstances: 1,
            NamedPipeServerSecurity.BuildSecurityDescriptor(ownerSid, authorizePackage ? [PackageSid] : []));

        string sddl = WindowsSecurity.GetSecurityDescriptorSddl(stream.SafePipeHandle);

        Assert.IsFalse(sddl.Contains("(ML;", StringComparison.OrdinalIgnoreCase), $"Unexpected mandatory label in '{sddl}'.");
    }

    /// <summary>
    /// Asks Windows itself, through the Authz access-check API, which identities the descriptor grants. This
    /// is the allowed/denied identity matrix the feature is about.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    [DataRow(PackageSid, true, DisplayName = "the authorized package is granted access")]
    [DataRow(OtherPackageSid, false, DisplayName = "a different package is denied")]
    [DataRow(NamedPipeServerSecurity.AllApplicationPackagesSid, false, DisplayName = "ALL APPLICATION PACKAGES is denied")]
    [DataRow(EveryoneSid, false, DisplayName = "Everyone is denied")]
    [DataRow(AuthenticatedUsersSid, false, DisplayName = "Authenticated Users is denied")]
    [DataRow(LocalSystemSid, false, DisplayName = "another account is denied")]
    [DataRow("S-1-5-21-1111111111-2222222222-3333333333-1001", false, DisplayName = "another user is denied")]
    public void SecurityDescriptor_GrantsOnlyTheOwnerAndTheAuthorizedPackage(string securityIdentifier, bool expectedGranted)
    {
        string ownerSid = NamedPipeServerSecurity.GetCurrentProcessOwnerSid();
        string sddl = NamedPipeServerSecurity.BuildSecurityDescriptor(ownerSid, [PackageSid]);

        Assert.AreEqual(
            expectedGranted,
            WindowsSecurity.IsAccessGranted(sddl, securityIdentifier, NamedPipeServerSecurity.PipeAccessRightsReadWriteSynchronize));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void SecurityDescriptor_GrantsTheCreatingIdentity()
    {
        string ownerSid = NamedPipeServerSecurity.GetCurrentProcessOwnerSid();
        string sddl = NamedPipeServerSecurity.BuildSecurityDescriptor(ownerSid, [PackageSid]);

        Assert.IsTrue(WindowsSecurity.IsAccessGranted(sddl, ownerSid, NamedPipeServerSecurity.PipeAccessRightsFullControl));
    }

    /// <summary>
    /// The current user must keep being able to connect: this is the ordinary test-run path, which the
    /// hardened descriptor must not regress.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task CreateServerStream_TheCurrentUserCanStillConnect()
    {
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        using NamedPipeServerStream server = NamedPipeServerSecurity.CreateServerStream(pipeName, maxNumberOfServerInstances: 1, [PackageSid]);

        Task waitForConnection = server.WaitForConnectionAsync(CancellationToken.None);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if NET
            | PipeOptions.CurrentUserOnly
#endif
            );
        await client.ConnectAsync(30_000, CancellationToken.None);
        await waitForConnection;

        Assert.IsTrue(client.IsConnected);
        Assert.IsTrue(server.IsConnected);
    }

    /// <summary>
    /// A real, live denial: while impersonating the anonymous token the process is no longer the pipe's
    /// owner, and Windows refuses the open. This proves the DACL is enforced rather than merely well-formed.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_AnUnauthorizedIdentityCannotConnect()
    {
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        using NamedPipeServerStream server = NamedPipeServerSecurity.CreateServerStream(pipeName, maxNumberOfServerInstances: 1, [PackageSid]);

        if (!WindowsSecurity.TryImpersonateAnonymous())
        {
            Assert.Inconclusive("The anonymous token could not be impersonated on this machine.");
        }

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);

            Assert.ThrowsExactly<UnauthorizedAccessException>(() => client.Connect(timeout: 5_000));
        }
        finally
        {
            WindowsSecurity.RevertToSelf();
        }
    }

    /// <summary>
    /// End-to-end through <see cref="NamedPipeServer"/> itself: the hardened pipe is created from a raw
    /// handle rather than by the <see cref="NamedPipeServerStream"/> name constructor, so the whole
    /// connect / request / reply / dispose cycle has to keep working on it.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task NamedPipeServer_WithAuthorizedPackage_StillCompletesARequestReplyRoundTrip()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        using var server = new NamedPipeServer(
            pipeNameDescription,
            static _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            new SystemEnvironment(),
            new Mock<ILogger>().Object,
            new SystemTask(),
            maxNumberOfServerInstances: 1,
            [PackageSid],
            CancellationToken.None);
        server.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        server.RegisterSerializer(new TestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));

        using var client = new NamedPipeClient(pipeNameDescription.Name);
        client.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        client.RegisterSerializer(new TestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));

        Task waitConnection = server.WaitConnectionAsync(CancellationToken.None);
        await client.ConnectAsync(CancellationToken.None);
        await waitConnection;

        VoidResponse response = await client.RequestReplyAsync<TestHostCompletedRequest, VoidResponse>(
            new TestHostCompletedRequest(returnCode: 0),
            CancellationToken.None);

        Assert.IsNotNull(response);
    }

    private static int CountAces(string sddl)
        => sddl.Count(static c => c == '(');

    /// <summary>
    /// Thin wrappers over the Windows security APIs the assertions need. They deliberately go through the OS
    /// rather than reimplementing an access check, so the tests assert on real Windows behavior.
    /// </summary>
    private static class WindowsSecurity
    {
        private const int SeKernelObject = 6;
        private const int OwnerSecurityInformation = 1;
        private const int GroupSecurityInformation = 2;
        private const int DaclSecurityInformation = 4;
        private const int LabelSecurityInformation = 0x10;
        private const uint SddlRevision1 = 1;
        private const uint AuthzRmFlagNoAudit = 1;
        private const uint AuthzSkipTokenGroups = 2;

        private const int SecurityInformation = OwnerSecurityInformation | GroupSecurityInformation | DaclSecurityInformation | LabelSecurityInformation;

        [SupportedOSPlatform("windows")]
        public static string GetSecurityDescriptorSddl(SafeHandle handle)
        {
            uint error = GetSecurityInfo(
                handle,
                SeKernelObject,
                SecurityInformation,
                out _,
                out _,
                out _,
                out _,
                out IntPtr securityDescriptor);

            if (error != 0)
            {
                throw new Win32Exception((int)error);
            }

            try
            {
                if (!ConvertSecurityDescriptorToStringSecurityDescriptor(
                        securityDescriptor,
                        SddlRevision1,
                        SecurityInformation,
                        out IntPtr sddl,
                        out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    return Marshal.PtrToStringUni(sddl)!;
                }
                finally
                {
                    LocalFree(sddl);
                }
            }
            finally
            {
                LocalFree(securityDescriptor);
            }
        }

        /// <summary>
        /// Asks Windows whether <paramref name="securityIdentifier"/> would be granted
        /// <paramref name="desiredAccess"/> on <paramref name="sddl"/>.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsAccessGranted(string sddl, string securityIdentifier, int desiredAccess)
        {
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, SddlRevision1, out IntPtr securityDescriptor, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr sid = IntPtr.Zero;
            IntPtr resourceManager = IntPtr.Zero;
            IntPtr clientContext = IntPtr.Zero;
            IntPtr grantedAccess = IntPtr.Zero;
            IntPtr errors = IntPtr.Zero;

            try
            {
                if (!ConvertStringSidToSid(securityIdentifier, out sid))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (!AuthzInitializeResourceManager(AuthzRmFlagNoAudit, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, "MTP pipe security tests", out resourceManager))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (!AuthzInitializeContextFromSid(AuthzSkipTokenGroups, sid, resourceManager, IntPtr.Zero, default, IntPtr.Zero, out clientContext))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                grantedAccess = Marshal.AllocHGlobal(sizeof(uint));
                errors = Marshal.AllocHGlobal(sizeof(uint));

                AuthzAccessRequest request = new()
                {
                    DesiredAccess = (uint)desiredAccess,
                    PrincipalSelfSid = IntPtr.Zero,
                    ObjectTypeList = IntPtr.Zero,
                    ObjectTypeListLength = 0,
                    OptionalArguments = IntPtr.Zero,
                };

                AuthzAccessReply reply = new()
                {
                    ResultListLength = 1,
                    GrantedAccessMask = grantedAccess,
                    SaclEvaluationResults = IntPtr.Zero,
                    Error = errors,
                };

                return !AuthzAccessCheck(0, clientContext, ref request, IntPtr.Zero, securityDescriptor, IntPtr.Zero, 0, ref reply, IntPtr.Zero)
                    ? throw new Win32Exception(Marshal.GetLastWin32Error())
                    : (uint)Marshal.ReadInt32(grantedAccess) == (uint)desiredAccess;
            }
            finally
            {
                if (errors != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(errors);
                }

                if (grantedAccess != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(grantedAccess);
                }

                if (clientContext != IntPtr.Zero)
                {
                    AuthzFreeContext(clientContext);
                }

                if (resourceManager != IntPtr.Zero)
                {
                    AuthzFreeResourceManager(resourceManager);
                }

                if (sid != IntPtr.Zero)
                {
                    LocalFree(sid);
                }

                LocalFree(securityDescriptor);
            }
        }

        [SupportedOSPlatform("windows")]
        public static bool TryImpersonateAnonymous() => ImpersonateAnonymousToken(GetCurrentThread());

        [SupportedOSPlatform("windows")]
        public static void RevertToSelf() => RevertToSelfCore();

        [StructLayout(LayoutKind.Sequential)]
        private struct AuthzAccessRequest
        {
            public uint DesiredAccess;
            public IntPtr PrincipalSelfSid;
            public IntPtr ObjectTypeList;
            public uint ObjectTypeListLength;
            public IntPtr OptionalArguments;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AuthzAccessReply
        {
            public uint ResultListLength;
            public IntPtr GrantedAccessMask;
            public IntPtr SaclEvaluationResults;
            public IntPtr Error;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern uint GetSecurityInfo(
            SafeHandle handle,
            int objectType,
            int securityInformation,
            out IntPtr owner,
            out IntPtr group,
            out IntPtr dacl,
            out IntPtr sacl,
            out IntPtr securityDescriptor);

        [DllImport("advapi32.dll", EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptor(
            IntPtr securityDescriptor,
            uint requestedStringSDRevision,
            int securityInformation,
            out IntPtr stringSecurityDescriptor,
            out int stringSecurityDescriptorLength);

        [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string stringSecurityDescriptor,
            uint stringSDRevision,
            out IntPtr securityDescriptor,
            IntPtr securityDescriptorSize);

        [DllImport("advapi32.dll", EntryPoint = "ConvertStringSidToSidW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertStringSidToSid(string stringSid, out IntPtr sid);

        [DllImport("authz.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AuthzInitializeResourceManager(
            uint flags,
            IntPtr accessCheckCallback,
            IntPtr computeDynamicGroups,
            IntPtr freeDynamicGroups,
            string resourceManagerName,
            out IntPtr authzResourceManager);

        [DllImport("authz.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AuthzInitializeContextFromSid(
            uint flags,
            IntPtr userSid,
            IntPtr authzResourceManager,
            IntPtr expirationTime,
            Luid identifier,
            IntPtr dynamicGroupArgs,
            out IntPtr authzClientContext);

        [DllImport("authz.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AuthzAccessCheck(
            uint flags,
            IntPtr authzClientContext,
            ref AuthzAccessRequest request,
            IntPtr auditEvent,
            IntPtr securityDescriptor,
            IntPtr optionalSecurityDescriptorArray,
            uint optionalSecurityDescriptorCount,
            ref AuthzAccessReply reply,
            IntPtr authzAccessCheckResults);

        [DllImport("authz.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AuthzFreeContext(IntPtr authzClientContext);

        [DllImport("authz.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AuthzFreeResourceManager(IntPtr authzResourceManager);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImpersonateAnonymousToken(IntPtr thread);

        [DllImport("advapi32.dll", EntryPoint = "RevertToSelf", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RevertToSelfCore();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}

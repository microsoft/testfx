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
using Microsoft.Win32.SafeHandles;

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
    public void IsAuthorizableSandboxedApplicationIdentity_WithPackageSid_IsAllowed()
    {
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(PackageSid));
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(OtherPackageSid));

        // A child AppContainer SID appends exactly four sub-authorities to the package SID and is still a
        // specific container.
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity($"{PackageSid}-1-2-3-4"));
    }

    [TestMethod]
    public void IsAuthorizableSandboxedApplicationIdentity_IsCaseAndWhitespaceInsensitive()
    {
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(PackageSid.ToLowerInvariant()));
        Assert.IsTrue(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity($"  {PackageSid}  "));
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
    [DataRow(PackageSid + "-1", DisplayName = "a partial child SID with one extra sub-authority")]
    [DataRow(PackageSid + "-1-2", DisplayName = "a partial child SID with two extra sub-authorities")]
    [DataRow(PackageSid + "-1-2-3", DisplayName = "a partial child SID with three extra sub-authorities")]
    [DataRow(PackageSid + "-1-2-3-4-5", DisplayName = "an overlong child SID")]
    [DataRow("S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-notanumber", DisplayName = "non-numeric sub-authority")]
    [DataRow("AC", DisplayName = "the SDDL alias of ALL APPLICATION PACKAGES")]
    public void IsAuthorizableSandboxedApplicationIdentity_WithAnythingButASingleSandboxedApplication_IsRejected(string? securityIdentifier)
        => Assert.IsFalse(NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(securityIdentifier));

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

    [TestMethod]
    public void GetPipeNameForSandboxedApplication_AddsTheRequiredLocalNamespaceIdempotently()
    {
        Assert.AreEqual(
            @"LOCAL\testingplatform.pipe.test",
            NamedPipeServerSecurity.GetPipeNameForSandboxedApplication("testingplatform.pipe.test"));
        Assert.AreEqual(
            @"LOCAL\testingplatform.pipe.test",
            NamedPipeServerSecurity.GetPipeNameForSandboxedApplication(@"LOCAL\testingplatform.pipe.test"));
    }

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
    /// Regression test for a time-of-check/time-of-use hole. The sequence of identities is supplied by an
    /// extension, so a hostile implementation can return a list that yields a valid SID when the platform
    /// validates it and an SDDL-injection payload when the security descriptor is composed. Before the fix
    /// that put <c>Everyone</c> — with <c>FILE_CREATE_PIPE_INSTANCE</c>, the very right that lets a client
    /// impersonate the controller — into the DACL of a real pipe.
    /// </summary>
    /// <remarks>
    /// The assertion is on the resulting DACL rather than on an exception: the platform now snapshots the
    /// sequence once, so the validated value is the value that gets composed and the payload is simply never
    /// observed. Asserting the descriptor is what pins the actual security property, and it holds however
    /// many times the sequence is read.
    /// </remarks>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_WithASequenceThatChangesBetweenEnumerations_ComposesOnlyValidatedValues()
    {
        // Yields a legitimate package SID first, then an ACE-breakout payload.
        var shapeShifting = new ShapeShiftingIdentityList(PackageSid, "WD)(A;;FA;;;WD");
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";

        using NamedPipeServerStream stream = NamedPipeServerSecurity.CreateServerStream(pipeName, maxNumberOfServerInstances: 1, shapeShifting);

        string sddl = WindowsSecurity.GetSecurityDescriptorSddl(stream.SafePipeHandle);

        // Exactly the owner ACE and the one authorized package: no injected ACE survived.
        Assert.AreEqual(2, CountAces(sddl), $"Unexpected security descriptor '{sddl}'.");
        Assert.Contains($"(A;;0x12019b;;;{PackageSid})", sddl);
        Assert.IsFalse(sddl.Contains(";;;WD)", StringComparison.OrdinalIgnoreCase), $"'Everyone' was injected into '{sddl}'.");
        Assert.IsFalse(sddl.Contains(";;;S-1-1-0)", StringComparison.OrdinalIgnoreCase), $"'Everyone' was injected into '{sddl}'.");
    }

    /// <summary>
    /// The composition function is the sink where caller strings become a security descriptor, so it must
    /// refuse an unvalidated value itself rather than trusting that some caller already checked it.
    /// </summary>
    [TestMethod]
    public void BuildSecurityDescriptor_ValidatesAtThePointOfConcatenation()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => NamedPipeServerSecurity.BuildSecurityDescriptor("S-1-5-18", ["WD)(A;;FA;;;WD"]));

        Assert.Contains("does not identify a single sandboxed application", exception.Message);
    }

    /// <summary>
    /// The same shape-shifting sequence through the <see cref="NamedPipeServer"/> entry point the platform
    /// actually uses: the pipe must still be created with a DACL that names only the owner and the validated
    /// package.
    /// </summary>
    /// <remarks>
    /// Reading the descriptor back by name connects a client and consumes this single-instance pipe, so this
    /// test deliberately never calls <c>WaitConnectionAsync</c> and performs exactly one such read.
    /// </remarks>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void NamedPipeServer_WithASequenceThatChangesBetweenEnumerations_DoesNotWidenTheDacl()
    {
        PipeNameDescription pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        using var server = new NamedPipeServer(
            pipeName,
            static _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            new SystemEnvironment(),
            new Mock<ILogger>().Object,
            new SystemTask(),
            maxNumberOfServerInstances: 1,
            new ShapeShiftingIdentityList(PackageSid, "WD)(A;;FA;;;WD"),
            CancellationToken.None);

        string sddl = WindowsSecurity.ConnectAndGetSecurityDescriptorSddl(server.PipeName.Name);

        Assert.AreEqual(2, CountAces(sddl), $"Unexpected security descriptor '{sddl}'.");
        Assert.IsFalse(sddl.Contains(";;;WD)", StringComparison.OrdinalIgnoreCase), $"'Everyone' was injected into '{sddl}'.");
        Assert.IsFalse(sddl.Contains(";;;S-1-1-0)", StringComparison.OrdinalIgnoreCase), $"'Everyone' was injected into '{sddl}'.");
    }

    /// <summary>
    /// The snapshot the platform takes lowers to <see cref="ICollection{T}.CopyTo"/> when the runtime type
    /// implements it, so a hostile collection still chooses what lands in the snapshot array. This pins that
    /// the validation at the point of concatenation — not the snapshot — is what closes the hole.
    /// </summary>
    [TestMethod]
    public void BuildSecurityDescriptor_WithAHostileCopyTo_StillRejectsTheInjectedValue()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => NamedPipeServerSecurity.BuildSecurityDescriptor("S-1-5-18", new HostileCopyToIdentityList(PackageSid, "WD)(A;;FA;;;WD")));

        Assert.Contains("does not identify a single sandboxed application", exception.Message);
    }

    /// <summary>
    /// An <see cref="IReadOnlyList{T}"/> that also implements <see cref="ICollection{T}"/> so the collection
    /// expression takes the <see cref="ICollection{T}.CopyTo"/> fast path, and whose <c>CopyTo</c> writes a
    /// different value than its enumerator yields.
    /// </summary>
    private sealed class HostileCopyToIdentityList(string enumeratedValue, string copiedValue) : IReadOnlyList<string>, ICollection<string>
    {
        public int Count => 1;

        public bool IsReadOnly => true;

        public string this[int index] => enumeratedValue;

        public void CopyTo(string[] array, int arrayIndex) => array[arrayIndex] = copiedValue;

        public IEnumerator<string> GetEnumerator()
        {
            yield return enumeratedValue;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(string item) => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();
    }

    /// <summary>
    /// An <see cref="IReadOnlyList{T}"/> whose indexer returns a different value on every read, modelling a
    /// hostile or simply non-deterministic extension. A real attacker does not need a type like this — a
    /// plain <see cref="List{T}"/> mutated from another thread between the check and the use has the same
    /// effect — but this makes the race deterministic.
    /// </summary>
    private sealed class ShapeShiftingIdentityList(string firstValue, string subsequentValue) : IReadOnlyList<string>
    {
        public int ReadCount { get; private set; }

        public int Count => 1;

        public string this[int index]
        {
            get
            {
                ReadCount++;
                return ReadCount == 1 ? firstValue : subsequentValue;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            yield return this[0];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
        using NamedPipeServerStream stream = NamedPipeServerSecurity.CreateServerStreamWithExplicitSecurityDescriptor(
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
        // Use a stable synthetic owner that differs from every row. Windows CI runs as LocalSystem, so using
        // the live process owner makes the "another account" row the actual owner — which must be granted.
        // SecurityDescriptor_GrantsTheCreatingIdentity separately covers the real owner on every machine.
        const string OwnerSid = "S-1-5-21-1111111111-2222222222-3333333333-1002";
        string ownerSid = OwnerSid;
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

        Assert.StartsWith(NamedPipeServerSecurity.SandboxedApplicationPipeNamePrefix, server.PipeName.Name);
        using var client = new NamedPipeClient(server.PipeName.Name);
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

    /// <summary>
    /// The acceptance criterion of the feature, exercised against a real AppContainer identity: a live
    /// AppContainer process's token is duplicated and impersonated, and that restricted token is used to
    /// open three pipes — one that authorizes its package, one that authorizes a different package, and one
    /// that authorizes nothing. Only the first may succeed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a genuine restricted-token access check, which is what makes it meaningful: the same DACL a
    /// normal token is admitted by is rejected for an AppContainer unless its package SID is named.
    /// </para>
    /// <para>
    /// Token impersonation does not place the desktop test process inside the AppContainer named-object
    /// namespace: trying to resolve <c>LOCAL\</c> under impersonation returns
    /// <c>ERROR_FILE_NOT_FOUND</c>. This test therefore isolates the restricted-token/DACL check on a bare
    /// pipe name. <see cref="NamedPipeServer_WithAuthorizedPackage_StillCompletesARequestReplyRoundTrip"/>
    /// separately proves the product publishes and connects through the Windows-required <c>LOCAL\</c>
    /// namespace. A real package-activated process remains the only way to exercise both constraints in one
    /// process boundary.
    /// </para>
    /// <para>
    /// Candidates are filtered to the shape a real test host has. An AppContainer at <em>untrusted</em>
    /// integrity (<c>S-1-16-0</c>, which is what Chromium's hardened renderers use) is denied by Mandatory
    /// Integrity Control before the DACL is consulted and can never connect; an ordinary UWP/WinUI app runs
    /// at <em>low</em> integrity (<c>S-1-16-4096</c>) and connects with no mandatory label on the pipe. The
    /// test then picks the first candidate that can actually reach an authorizing pipe and asserts the
    /// denials against that validated token, so the outcome does not depend on which processes happen to be
    /// running. It self-skips when the machine exposes no usable AppContainer at all.
    /// </para>
    /// </remarks>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_AnAppContainerConnectsOnlyWhenItsOwnPackageIsAuthorized()
    {
        List<(SafeHandle Token, string Sid)> candidates = WindowsSecurity.EnumerateAppContainerTokens();
        if (candidates.Count == 0)
        {
            Assert.Inconclusive("This machine exposes no low-integrity AppContainer process whose token can be duplicated.");
            return;
        }

        try
        {
            foreach ((SafeHandle token, string sid) in candidates)
            {
                Assert.IsTrue(
                    NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(sid),
                    $"A live AppContainer SID must satisfy the platform's authorization policy, but '{sid}' did not.");

                // Establish that this token is a viable low-integrity AppContainer with the granular
                // kernel open first. The managed-client assertion below is the behavior under test and must
                // fail — not make the candidate look unusable — if its generic open cannot use this ACE.
                if (!TryConnectAsAppContainer(token, [sid]))
                {
                    // This container cannot reach even an authorizing pipe (a nested container, or one
                    // restricted further than a test host would be); it cannot prove anything either way.
                    continue;
                }

                Assert.IsTrue(
                    TryConnectAsAppContainer(token, [sid], useManagedProductClient: true),
                    "The actual MTP NamedPipeClientStream options must connect when this package SID is authorized.");
                Assert.IsFalse(
                    TryConnectAsAppContainer(token, [OtherPackageSid]),
                    "An AppContainer must not reach a pipe that authorizes a different package.");
                Assert.IsFalse(
                    TryConnectAsAppContainer(token, []),
                    "An AppContainer must not reach a pipe that authorizes no package at all.");
                return;
            }

            Assert.Fail(
                $"Found {candidates.Count} duplicable low-integrity AppContainer token(s), but none could open a pipe that authorized its exact package SID.");
        }
        finally
        {
            foreach ((SafeHandle token, _) in candidates)
            {
                token.Dispose();
            }
        }
    }

    /// <summary>
    /// Creates a controller pipe authorizing <paramref name="authorizedSids"/> and reports whether the
    /// AppContainer behind <paramref name="impersonationToken"/> can open it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool TryConnectAsAppContainer(
        SafeHandle impersonationToken,
        string[] authorizedSids,
        bool useManagedProductClient = false)
    {
        // Deliberately bare: an impersonated token does not acquire AppContainer namespace resolution.
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        string ownerSid = NamedPipeServerSecurity.GetCurrentProcessOwnerSid();
        using NamedPipeServerStream server = NamedPipeServerSecurity.CreateServerStreamWithExplicitSecurityDescriptor(
            pipeName,
            maxNumberOfServerInstances: 1,
            NamedPipeServerSecurity.BuildSecurityDescriptor(ownerSid, authorizedSids));

        // Only the client open is impersonated; the server keeps running as the test.
        Task waitForConnection = server.WaitForConnectionAsync(CancellationToken.None);

        bool connected = useManagedProductClient
            ? WindowsSecurity.TryConnectManagedClientAs(impersonationToken, pipeName)
            : WindowsSecurity.TryOpenPipeAs(impersonationToken, pipeName, NamedPipeServerSecurity.PipeAccessRightsReadWriteSynchronize);
        if (connected)
        {
            waitForConnection.Wait(TimeSpan.FromSeconds(10));
        }

        return connected;
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

        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint TokenQuery = 0x0008;
        private const uint TokenDuplicate = 0x0002;
        private const uint TokenImpersonate = 0x0004;
        private const int SecurityImpersonation = 2;
        private const int TokenImpersonationType = 2;
        private const int TokenIsAppContainerClass = 29;
        private const int TokenAppContainerSidClass = 31;
        private const int TokenIntegrityLevelClass = 25;
        private const string LowIntegritySid = "S-1-16-4096";
        private const uint OpenExisting = 3;
        private const int ErrorAccessDenied = 5;

        private const int SecurityInformation = OwnerSecurityInformation | GroupSecurityInformation | DaclSecurityInformation | LabelSecurityInformation;

        /// <summary>
        /// Opens an existing named pipe by name and returns its security descriptor, for cases where the
        /// test does not hold the server stream (for example when the pipe is owned by a
        /// <c>NamedPipeServer</c>).
        /// </summary>
        /// <remarks>
        /// <strong>This connects a client and consumes a pipe instance.</strong> NPFS treats any successful
        /// <c>CreateFileW</c> on <c>\\.\pipe\name</c> as a client connect regardless of the requested access
        /// mask, so even a <c>READ_CONTROL</c>-only open transitions a single-instance server to CONNECTED.
        /// Call it at most once per pipe, and never on a server whose own connection is under test: a second
        /// call fails with <c>ERROR_PIPE_BUSY</c>, and satisfying a pending <c>WaitConnectionAsync</c> with
        /// this phantom client would start the read loop against an already-closed peer, which the server
        /// treats as a fatal error and fails the process rather than the test.
        /// </remarks>
        [SupportedOSPlatform("windows")]
        public static string ConnectAndGetSecurityDescriptorSddl(string pipeName)
        {
            // READ_CONTROL is the minimum needed to read the descriptor. It does not avoid the connect.
            const uint readControl = 0x00020000;

            IntPtr handle = CreateFile($@"\\.\pipe\{pipeName}", readControl, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open the pipe '{pipeName}' to read its security descriptor.");
            }

            using var safeHandle = new SafeTokenHandle(handle);
            return GetSecurityDescriptorSddl(safeHandle);
        }

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
        public static bool TryImpersonate(SafeHandle impersonationToken) => ImpersonateLoggedOnUser(impersonationToken);

        [SupportedOSPlatform("windows")]
        public static void RevertToSelf() => RevertToSelfCore();

        /// <summary>
        /// Duplicates the token of every live <em>low-integrity</em> AppContainer process whose token can be
        /// opened. Requiring <c>S-1-16-4096</c> keeps the test on the exact integrity level of an ordinary
        /// UWP/WinUI host: a medium-integrity container could hide a MIC regression, while an untrusted
        /// Chromium-style renderer is intentionally more restricted than this product scenario.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static List<(SafeHandle Token, string Sid)> EnumerateAppContainerTokens()
        {
            List<(SafeHandle, string)> tokens = [];

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    IntPtr processHandle = OpenProcess(ProcessQueryLimitedInformation, bInheritHandle: false, process.Id);
                    if (processHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        if (!OpenProcessToken(processHandle, TokenQuery | TokenDuplicate, out IntPtr token))
                        {
                            continue;
                        }

                        try
                        {
                            if (!IsAppContainerToken(token)
                                || GetSidTokenInformation(token, TokenIntegrityLevelClass) != LowIntegritySid
                                || GetSidTokenInformation(token, TokenAppContainerSidClass) is not { } appContainerSid)
                            {
                                continue;
                            }

                            if (DuplicateTokenEx(token, TokenQuery | TokenDuplicate | TokenImpersonate, IntPtr.Zero, SecurityImpersonation, TokenImpersonationType, out IntPtr duplicated))
                            {
                                tokens.Add((new SafeTokenHandle(duplicated), appContainerSid));
                            }
                        }
                        finally
                        {
                            CloseHandle(token);
                        }
                    }
                    finally
                    {
                        CloseHandle(processHandle);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // The process went away or is not inspectable; keep looking.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return tokens;
        }

        /// <summary>
        /// Opens the pipe while impersonating <paramref name="impersonationToken"/> and reports whether
        /// Windows granted the access. The open goes through <c>CreateFileW</c> on the impersonating thread
        /// so the result is the kernel's access check on the pipe, with no managed client behavior in
        /// between.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool TryOpenPipeAs(SafeHandle impersonationToken, string pipeName, int desiredAccess)
        {
            if (!ImpersonateLoggedOnUser(impersonationToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                IntPtr handle = CreateFile($@"\\.\pipe\{pipeName}", (uint)desiredAccess, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (handle == new IntPtr(-1))
                {
                    int error = Marshal.GetLastWin32Error();
                    return error switch
                    {
                        ErrorAccessDenied => false,
                        _ => throw new Win32Exception(
                            error,
                            $"Unexpected Win32 error {error} opening '{pipeName}' as an AppContainer."),
                    };
                }

                CloseHandle(handle);
                return true;
            }
            finally
            {
                RevertToSelfCore();
            }
        }

        /// <summary>
        /// Connects while impersonating <paramref name="impersonationToken"/> through the same managed
        /// <see cref="NamedPipeClientStream"/> options the product uses. This proves the explicit package
        /// ACE admits the generic read/write open and, on modern .NET, the client-side
        /// <see cref="PipeOptions.CurrentUserOnly"/> owner validation.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool TryConnectManagedClientAs(SafeHandle impersonationToken, string pipeName)
        {
            if (!ImpersonateLoggedOnUser(impersonationToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous
#if NET
                    | PipeOptions.CurrentUserOnly
#endif
                    );

                try
                {
                    client.Connect(timeout: 10_000);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
            finally
            {
                RevertToSelfCore();
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool IsAppContainerToken(IntPtr token)
        {
            IntPtr buffer = Marshal.AllocHGlobal(sizeof(uint));
            try
            {
                return GetTokenInformation(token, TokenIsAppContainerClass, buffer, sizeof(uint), out _)
                    && Marshal.ReadInt32(buffer) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Reads a token information class whose payload starts with a single <c>PSID</c> — which is the
        /// layout of both <c>TOKEN_APPCONTAINER_INFORMATION</c> and <c>TOKEN_MANDATORY_LABEL</c>.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static string? GetSidTokenInformation(IntPtr token, int tokenInformationClass)
        {
            GetTokenInformation(token, tokenInformationClass, IntPtr.Zero, 0, out int length);
            if (length <= 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(token, tokenInformationClass, buffer, length, out _))
                {
                    return null;
                }

                IntPtr sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero || !ConvertSidToStringSidCore(sid, out IntPtr stringSid))
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(stringSid);
                }
                finally
                {
                    LocalFree(stringSid);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeTokenHandle(IntPtr handle)
                : base(ownsHandle: true)
                => SetHandle(handle);

            protected override bool ReleaseHandle() => CloseHandle(handle);
        }

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

        [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertSidToStringSidCore(IntPtr sid, out IntPtr stringSid);

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

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImpersonateLoggedOnUser(SafeHandle token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            int tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr newToken);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", EntryPoint = "RevertToSelf", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RevertToSelfCore();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}

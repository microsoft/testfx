// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using System.Diagnostics;
using System.Security.Cryptography;

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppLifecycleBehaviorTests
{
    [TestMethod]
    public void LooseHandle_DisposesOwnedDeploymentAndToleratesAnUnstartedProcess()
    {
        using var directory = new OwnedDirectory();
        string deployment = Path.Combine(directory.Root, "deployment");
        Directory.CreateDirectory(Path.Combine(deployment, "nested"));
        File.WriteAllText(Path.Combine(deployment, "nested", "evidence.txt"), "preserved until disposal");
        using var process = new Process();
        var handle = new PackagedAppTestHostHandle(process, deployment);

        handle.Terminate();
        handle.Terminate();
        handle.Dispose();
        handle.Dispose();

        Assert.IsFalse(Directory.Exists(deployment));
        Assert.IsTrue(Directory.Exists(directory.Root));
        Assert.IsNull(handle.Identifier);
    }

    [TestMethod]
    public async Task LooseHandle_CanceledWaitDoesNotTerminateObservedProcess()
    {
        using var directory = new OwnedDirectory();
        // This is a query-only wrapper for this process. Never call Terminate on it.
        using Process process = Process.GetCurrentProcess();
        using var handle = new PackagedAppTestHostHandle(process, directory.Root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => handle.WaitForExitAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsFalse(handle.HasExited);
        Assert.IsNull(handle.Identifier);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void LooseHandle_CleanupFailureDoesNotReplaceRunOutcome()
    {
        using var directory = new OwnedDirectory();
        string path = Path.Combine(directory.Root, "readonly.txt");
        File.WriteAllText(path, "owned evidence");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        using var process = new Process();
        var handle = new PackagedAppTestHostHandle(process, directory.Root);

        handle.Dispose();

        Assert.IsTrue(Directory.Exists(directory.Root));
        Assert.AreEqual("owned evidence", File.ReadAllText(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false, DisplayName = "Handshake delete denied by file sharing")]
    [DataRow(true, DisplayName = "Handshake delete denied by owned readonly evidence")]
    public void Handshake_DeleteFailurePreservesDecodedValuesAndCanBeRetried(bool readOnly)
    {
        using var directory = new OwnedDirectory();
        string path = Path.Combine(directory.Root, "owned.handshake");
        File.WriteAllText(path, "KEY=VdmFsdWU=\nEMPTY=V\nNULL=N\n");
        if (readOnly)
        {
            File.SetAttributes(path, FileAttributes.ReadOnly);
        }

        using (FileStream? held = readOnly ? null : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            IReadOnlyDictionary<string, string?>? values = PackagedAppConnectBackHandshake.ReadAndDelete(path);
            Assert.IsNotNull(values);
            Assert.AreSequenceEqual(new[] { "EMPTY", "KEY", "NULL" }, values.Keys.OrderBy(value => value, StringComparer.Ordinal));
            Assert.AreEqual("value", values["KEY"]);
            Assert.AreEqual(string.Empty, values["EMPTY"]);
            Assert.IsNull(values["NULL"]);
            Assert.IsTrue(File.Exists(path));
            PackagedAppConnectBackHandshake.TryDelete(path);
            Assert.IsTrue(File.Exists(path));
        }

        File.SetAttributes(path, FileAttributes.Normal);
        PackagedAppConnectBackHandshake.TryDelete(path);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(0, DisplayName = "Authorization invalid XML is fail closed")]
    [DataRow(1, DisplayName = "Authorization ambiguous host is fail closed")]
    [DataRow(2, DisplayName = "Authorization unreadable manifest is fail closed")]
    public async Task Authorization_UnreadableOrAmbiguousManifestNeverWidensPipeAccess(int failure)
    {
        using var directory = new OwnedDirectory();
        string path = Path.Combine(directory.Root, "AppxManifest.xml");
        const string valid = """
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" Version="1.0.0.0" />
              <Applications><Application Id="One" Executable="host.exe" /></Applications>
            </Package>
            """;
        string content = failure switch
        {
            0 => "<broken",
            1 => valid.Replace("</Applications>", "<Application Id=\"Two\" Executable=\"host.exe\" /></Applications>", StringComparison.Ordinal),
            _ => valid,
        };
        File.WriteAllText(path, content);
        var launcher = new PackagedAppTestHostLauncher(
            directory.Root, name => name == PackagedAppTestHostLauncher.PipeAuthorizationModeEnvironmentVariable ? "always" : null);
        using (FileStream? held = failure == 2 ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None) : null)
        {
            IReadOnlyList<string> identities = await launcher.GetAuthorizedSecurityIdentitiesAsync(Path.Combine(directory.Root, "host.exe"), CancellationToken.None);
            Assert.IsEmpty(identities);
        }

        File.WriteAllText(path, valid);
        IReadOnlyList<string> validIdentities = await launcher.GetAuthorizedSecurityIdentitiesAsync(Path.Combine(directory.Root, "host.exe"), CancellationToken.None);
        Assert.AreSequenceEqual(new[] { "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942" }, validIdentities);
    }

    [TestMethod]
    public void ActivationWrapper_RestoresIndependentWireVector()
    {
        // Three UTF-16LE arguments: empty, --help, and lambda. No product encoder is used.
        byte[] payload = [3, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 45, 0, 45, 0, 104, 0, 101, 0, 108, 0, 112, 0, 1, 0, 0, 0, 187, 3];

        string[] actual = PackagedAppExtensions.GetTestApplicationArguments("mtp:v1:inline:" + Convert.ToBase64String(payload));

        Assert.AreSequenceEqual(new[] { string.Empty, "--help", "\u03bb" }, actual);
        ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() => PackagedAppExtensions.GetTestApplicationArguments(null!));
        Assert.AreEqual("activationArguments", error.ParamName);
    }

    [TestMethod]
    public void PublicRegistration_NullBuilderRejectsBeforeConnectBack()
    {
        ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() => PackagedAppExtensions.AddPackagedAppDeployment(null!));

        Assert.AreEqual("builder", error.ParamName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Registration_HookAndDirectPathMaterializeCancelableLauncher(bool hook)
    {
        var builder = new Mock<ITestApplicationBuilder>();
        var controllers = new Mock<ITestHostControllersManager>();
        builder.SetupGet(value => value.TestHostControllers).Returns(controllers.Object);
        Func<IServiceProvider, ITestHostLauncher>? factory = null;
        controllers.Setup(value => value.AddTestHostLauncher(It.IsAny<Func<IServiceProvider, ITestHostLauncher>>()))
            .Callback<Func<IServiceProvider, ITestHostLauncher>>(value => factory = value);
        if (hook)
        {
            PackagedApp.TestingPlatformBuilderHook.AddExtensions(builder.Object, []);
        }
        else
        {
            PackagedAppExtensions.AddPackagedAppDeployment(builder.Object, []);
        }

        Assert.IsNotNull(factory);
        ITestHostLauncher launcher = factory(Mock.Of<IServiceProvider>());
        var authorizer = Assert.IsInstanceOfType<ITestHostControllerConnectionAuthorizer>(launcher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => authorizer.GetAuthorizedSecurityIdentitiesAsync("never-inspected.exe", cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        controllers.Verify(value => value.AddTestHostLauncher(It.IsAny<Func<IServiceProvider, ITestHostLauncher>>()), Times.Once);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task PublicFamilyLock_OwnedLeaseUsesNormalizedIdentityAndReleasesExclusiveAccess()
    {
        string family = "Coverage.Owned." + Guid.NewGuid().ToString("N");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(family.ToUpperInvariant())));
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft.Testing.Platform", "PackagedApp", "RegistrationLocks", hash + ".lock");
        try
        {
            using (FileStream first = await PackageRegistrationLock.AcquireAsync(family, CancellationToken.None))
            {
                Assert.AreEqual(expected, first.Name);
                first.WriteByte(71);
                _ = Assert.ThrowsExactly<IOException>(() => new FileStream(expected, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            }

            using FileStream second = await PackageRegistrationLock.AcquireAsync(family.ToLowerInvariant(), CancellationToken.None);
            Assert.AreEqual(expected, second.Name);
            Assert.AreEqual(71, second.ReadByte());
        }
        finally
        {
            File.Delete(expected);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PublicLockEntryPoints_PreCanceledTokenStopsBeforeIo(bool manifest)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manifest
            ? PackageRegistrationLock.WithManifestAsync("must-not-open.manifest", _ => Task.FromException<int>(new AssertFailedException("Action must not run.")), cancellation.Token)
            : PackageRegistrationLock.AcquireAsync("must-not-create", cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }

    private sealed class OwnedDirectory : IDisposable
    {
        public OwnedDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), nameof(PackagedAppLifecycleBehaviorTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Root))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}

#endif

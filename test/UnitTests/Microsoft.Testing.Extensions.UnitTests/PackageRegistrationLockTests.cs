// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
[SupportedOSPlatform("windows")]
public sealed class PackageRegistrationLockTests
{
    private const string PackageFamilyName = "Contoso.TestApp_abcdefghijklm";

    private const string ManifestXml = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
          <Identity Name="Contoso.ManifestApp" Publisher="CN=ManifestPublisher" Version="1.0.0.0" />
          <Applications>
            <Application Id="App" Executable="App.exe" />
          </Applications>
        </Package>
        """;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task WithManifestAsync_KeepsIdentityStableThroughActivation(bool changePublisher)
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            string manifestPath = CreateManifestFile(directory);
            string replacementXml = changePublisher
                ? ManifestXml.Replace("CN=ManifestPublisher", "CN=ReplacementPublisher", StringComparison.Ordinal)
                : ManifestXml.Replace("Contoso.ManifestApp", "Contoso.ReplacementApp", StringComparison.Ordinal);
            string replacementPath = Path.Combine(directory, "replacement.xml");
            File.WriteAllText(replacementPath, replacementXml);
            string? lockedFamily = null;

            AppxManifestInfo snapshot = await PackageRegistrationLock.WithManifestAsync(
                manifestPath,
                (family, token) =>
                {
                    lockedFamily = family;
                    Assert.ThrowsExactly<IOException>(() => File.WriteAllText(manifestPath, replacementXml));
                    return PackageRegistrationLock.AcquireAsync(family, Path.Combine(directory, "locks"), token);
                },
                async manifestInfo =>
                {
                    Assert.AreEqual(lockedFamily, manifestInfo.PackageFamilyName);
                    var deploymentRead = AppxManifestInfo.ReadFromManifest(manifestPath);
                    Assert.AreEqual(manifestInfo.PackageFamilyName, deploymentRead.PackageFamilyName);
                    await Task.Yield();
                    Exception exception = Assert.Throws<Exception>(() => File.Move(replacementPath, manifestPath, overwrite: true));
                    Assert.IsTrue(exception is IOException or UnauthorizedAccessException);
                    return manifestInfo;
                },
                cancellationToken);

            File.Move(replacementPath, manifestPath, overwrite: true);
            var replacement = AppxManifestInfo.ReadFromManifest(manifestPath);
            Assert.AreNotEqual(snapshot.PackageFamilyName, replacement.PackageFamilyName);
        });

    [TestMethod]
    public Task WithManifestAsync_WhenCanceledWhileWaiting_ReleasesManifest()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            string manifestPath = CreateManifestFile(directory);
            var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);
            string lockDirectory = Path.Combine(directory, "locks");
            using FileStream first = await PackageRegistrationLock.AcquireAsync(manifestInfo.PackageFamilyName, lockDirectory, cancellationToken);
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<int> waiting = PackageRegistrationLock.WithManifestAsync(
                manifestPath,
                (family, token) => PackageRegistrationLock.AcquireAsync(family, lockDirectory, token),
                _ => Task.FromException<int>(new InvalidOperationException("The canceled action must not run.")),
                canceled.Token);

            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(manifestPath, ManifestXml));
            canceled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);

            File.WriteAllText(manifestPath, ManifestXml);
            Assert.AreEqual(manifestInfo.PackageFamilyName, AppxManifestInfo.ReadFromManifest(manifestPath).PackageFamilyName);
        });

    [TestMethod]
    public Task WithManifestAsync_WhenActionFails_ReleasesManifestAndRegistrationLock()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            string manifestPath = CreateManifestFile(directory);
            var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);
            string lockDirectory = Path.Combine(directory, "locks");
            var failure = new InvalidOperationException("Activation failed.");

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => PackageRegistrationLock.WithManifestAsync(
                    manifestPath,
                    (family, token) => PackageRegistrationLock.AcquireAsync(family, lockDirectory, token),
                    _ => Task.FromException<int>(failure),
                    cancellationToken));

            Assert.AreSame(failure, exception);
            File.WriteAllText(manifestPath, ManifestXml);
            using FileStream next = await PackageRegistrationLock.AcquireAsync(manifestInfo.PackageFamilyName, lockDirectory, cancellationToken);
            Assert.IsTrue(File.Exists(next.Name));
        });

    [TestMethod]
    public Task WithManifestAsync_WhenManifestIsInvalid_ReleasesReadHandleWithoutTakingRegistrationLock()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            string manifestPath = CreateManifestFile(directory);
            File.WriteAllText(manifestPath, "<invalid");

            await Assert.ThrowsExactlyAsync<XmlException>(
                () => PackageRegistrationLock.WithManifestAsync(
                    manifestPath,
                    (_, _) => throw new InvalidOperationException("An invalid manifest must not acquire a family lock."),
                    _ => Task.FromResult(0),
                    cancellationToken));

            File.Delete(manifestPath);
            Assert.IsFalse(File.Exists(manifestPath));
        });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AcquireAsync_SerializesRegistrationThroughActivation(bool differentCase)
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            var registrationVerified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowActivation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> operations = [];
            string registeredLayout = string.Empty;

            async Task FirstLaunchAsync()
            {
                using FileStream lease = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
                registeredLayout = "layout-a";
                operations.Add("register:a");
                registrationVerified.SetResult();
                await allowActivation.Task.WaitAsync(cancellationToken);
                Assert.AreEqual("layout-a", registeredLayout);
                operations.Add("activate:a");
            }

            async Task SecondLaunchAsync()
            {
                string family = differentCase ? PackageFamilyName.ToUpperInvariant() : PackageFamilyName;
                using FileStream lease = await PackageRegistrationLock.AcquireAsync(family, directory, cancellationToken);
                registeredLayout = "layout-b";
                operations.Add("register:b");
                Assert.AreEqual("layout-b", registeredLayout);
                operations.Add("activate:b");
            }

            Task firstLaunch = FirstLaunchAsync();
            await registrationVerified.Task.WaitAsync(cancellationToken);
            Task secondLaunch = SecondLaunchAsync();
            try
            {
                Assert.IsFalse(secondLaunch.IsCompleted, "Another controller must wait until the verified layout has been activated.");
            }
            finally
            {
                allowActivation.SetResult();
                await Task.WhenAll(firstLaunch, secondLaunch);
            }

            string[] expected = ["register:a", "activate:a", "register:b", "activate:b"];
            Assert.AreSequenceEqual(expected, operations);
        });

    [TestMethod]
    public Task AcquireAsync_DifferentPackageFamiliesDoNotBlockEachOther()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            using FileStream first = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            using FileStream second = await PackageRegistrationLock.AcquireAsync("Contoso.OtherApp_abcdefghijklm", directory, cancellationToken);

            Assert.AreNotEqual(first.Name, second.Name);
        });

    [TestMethod]
    public Task AcquireAsync_WhenAlreadyCanceled_DoesNotCreateLockDirectory()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            canceled.Cancel();

            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, canceled.Token));

            Assert.AreEqual(canceled.Token, exception.CancellationToken);
            Assert.IsFalse(Directory.Exists(directory));
        });

    [TestMethod]
    public Task AcquireAsync_WhenCanceledWhileWaiting_DoesNotReleaseAnotherControllersLease()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            using FileStream first = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<FileStream> waiting = PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, canceled.Token);
            canceled.Cancel();

            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);

            Assert.AreEqual(canceled.Token, exception.CancellationToken);
            Assert.ThrowsExactly<IOException>(() =>
            {
                using var probe = new FileStream(first.Name, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
        });

    [TestMethod]
    public Task AcquireAsync_LeaseCanBeReleasedFromAnotherThread()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            using FileStream first = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            await Task.Factory.StartNew(first.Dispose, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            using FileStream second = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            Assert.AreEqual(first.Name, second.Name);
        });

    [TestMethod]
    public Task AcquireAsync_LeaseIsReleasedWhenLaunchFails()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            var failure = new InvalidOperationException("Activation failed.");
            async Task FailingLaunchAsync()
            {
                using FileStream lease = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
                await Task.Yield();
                throw failure;
            }

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(FailingLaunchAsync);

            Assert.AreSame(failure, exception);
            using FileStream next = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            Assert.IsTrue(File.Exists(next.Name), "The stable lock file must remain in place between leases.");
        });

    [TestMethod]
    public Task AcquireAsync_PermanentFileErrorIsNotRetried()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            string lockPath;
            using (FileStream lease = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken))
            {
                lockPath = lease.Name;
            }

            File.Delete(lockPath);
            Directory.CreateDirectory(lockPath);

            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
                () => PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken));
        });

    [TestMethod]
    public Task AcquireAsync_ExcludesAnotherProcessUntilReleased()
        => RunInTemporaryDirectoryAsync(async (directory, cancellationToken) =>
        {
            using FileStream lease = await PackageRegistrationLock.AcquireAsync(PackageFamilyName, directory, cancellationToken);
            string lockPath = lease.Name;

            Assert.AreEqual(2, await TryOpenInAnotherProcessAsync(lockPath, cancellationToken));

            lease.Dispose();

            Assert.AreEqual(0, await TryOpenInAnotherProcessAsync(lockPath, cancellationToken));
        });

    private static string CreateManifestFile(string directory)
    {
        Directory.CreateDirectory(directory);
        string manifestPath = Path.Combine(directory, AppxManifestInfo.AppxManifestFileName);
        File.WriteAllText(manifestPath, ManifestXml);
        return manifestPath;
    }

    private static async Task<int> TryOpenInAnotherProcessAsync(string lockPath, CancellationToken cancellationToken)
    {
        const string Script = """
            $ErrorActionPreference = 'Stop'
            $lease = $null
            try {
                $lease = [System.IO.File]::Open(
                    $env:MTP_REGISTRATION_LOCK_TEST_PATH,
                    [System.IO.FileMode]::OpenOrCreate,
                    [System.IO.FileAccess]::ReadWrite,
                    [System.IO.FileShare]::None)
                exit 0
            }
            catch {
                $exception = $_.Exception.GetBaseException()
                if ($exception -is [System.IO.IOException] -and $exception.HResult -eq -2147024864) {
                    exit 2
                }

                [Console]::Error.WriteLine($exception.ToString())
                exit 1
            }
            finally {
                if ($null -ne $lease) {
                    $lease.Dispose()
                }
            }
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(Script)));
        startInfo.Environment["MTP_REGISTRATION_LOCK_TEST_PATH"] = lockPath;

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the lock probe process.");
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            string diagnostics = await output + await error;
            Assert.IsTrue(process.ExitCode is 0 or 2, $"Lock probe failed with exit code {process.ExitCode}: {diagnostics}");
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private async Task RunInTemporaryDirectoryAsync(Func<string, CancellationToken, Task> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(PackageRegistrationLockTests), Guid.NewGuid().ToString("N"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await action(directory, cancellation.Token);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    public TestContext TestContext { get; set; }
}

#endif

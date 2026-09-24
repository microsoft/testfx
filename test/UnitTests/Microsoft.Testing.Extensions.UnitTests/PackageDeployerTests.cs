// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackageDeployerTests
{
    private const string PackageFullName = "Contoso.LayoutTests_1.0.0.0_neutral__abcdefghijklm";

    [DataRow("x64", "x64", true)]
    [DataRow("X64", "x64", true)]
    [DataRow("neutral", "x64", true)]
    [DataRow("NEUTRAL", "arm64", true)]
    [DataRow("x86", "x64", false)]
    [TestMethod]
    public void IsApplicableDependencyArchitecture_ReturnsExpectedResult(
        string dependencyArchitecture,
        string targetArchitecture,
        bool expected)
    {
        bool actual = (bool)typeof(PackageDeployer)
            .GetMethod(
                "IsApplicableDependencyArchitecture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [dependencyArchitecture, targetArchitecture])!;

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task RegisterAsync_WithUnregisteredPackage_RegistersRequestedLayout()
    {
        var packageManager = new TestPackageManager();
        string layout = GetLayoutDirectory("layout-a");

        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);

        Assert.HasCount(1, packageManager.Packages);
        Assert.AreEqual(layout, packageManager.Packages[0].InstalledPath);
        Assert.AreEqual(PackageFullName, packageManager.Packages[0].FullName);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    [DataRow("layout-a", "layout-b")]
    [DataRow("layout-b", "layout-a")]
    public async Task RegisterAsync_WithSameVersionInAnotherLayout_ReplacesDevelopmentRegistration(string previousLayout, string requestedLayout)
    {
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory(previousLayout), isDevelopmentMode: true)],
        };
        string layout = GetLayoutDirectory(requestedLayout);

        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);

        Assert.HasCount(1, packageManager.Packages);
        Assert.AreEqual(layout, packageManager.Packages[0].InstalledPath);
        Assert.AreEqual(PackageFullName, packageManager.Packages[0].FullName);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WithRepeatedRunsInSameLayout_DoesNotRemoveRegistration()
    {
        var packageManager = new TestPackageManager();
        string layout = GetLayoutDirectory("layout-a");
        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);
        RegisteredPackageInfo registration = packageManager.Packages[0];

        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);

        Assert.AreSame(registration, packageManager.Packages[0]);
        string[] expectedOperations = ["register", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WithEquivalentRegisteredPath_DoesNotRemoveRegistration()
    {
        string layout = GetLayoutDirectory("layout-a");
        string registeredPath = Path.Combine(layout.ToUpperInvariant(), "nested", "..") + Path.DirectorySeparatorChar;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, registeredPath, isDevelopmentMode: true)],
        };

        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);

        Assert.AreEqual(registeredPath, packageManager.Packages[0].InstalledPath);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenNativeRegistrationUpdatesLocation_DoesNotRemoveRegistration()
    {
        string layout = GetLayoutDirectory("layout-b");
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.RegisterOverride = _ =>
        {
            packageManager.Packages = [new(PackageFullName, layout, isDevelopmentMode: true)];
            return Task.CompletedTask;
        };

        await packageManager.RegisterAsync(layout, TestContext.CancellationToken);

        Assert.AreEqual(layout, packageManager.Packages[0].InstalledPath);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenRegistrationFails_ReportsManifestAndKeepsPreviousRegistration()
    {
        string layout = GetLayoutDirectory("layout-b");
        var failure = new InvalidOperationException("Invalid package manifest.");
        var previousPackage = new RegisteredPackageInfo(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true);
        var packageManager = new TestPackageManager
        {
            Packages = [previousPackage],
            RegisterOverride = _ => Task.FromException(failure),
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(layout, TestContext.CancellationToken));

        Assert.AreSame(failure, exception.InnerException);
        Assert.Contains(Path.Combine(layout, AppxManifestInfo.AppxManifestFileName), exception.Message);
        Assert.Contains(failure.Message, exception.Message);
        Assert.AreSame(previousPackage, packageManager.Packages[0]);
        string[] expectedOperations = ["register"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenRemovalFails_DoesNotRetryRegistration()
    {
        var failure = new InvalidOperationException("Package is in use.");
        string requestedLayout = GetLayoutDirectory("layout-b");
        var previousPackage = new RegisteredPackageInfo(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true);
        var packageManager = new TestPackageManager
        {
            Packages = [previousPackage],
            RemoveOverride = (_, _) => Task.FromException(failure),
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, TestContext.CancellationToken));

        Assert.AreSame(failure, exception.InnerException);
        Assert.Contains(PackageFullName, exception.Message);
        Assert.Contains(Path.Combine(requestedLayout, AppxManifestInfo.AppxManifestFileName), exception.Message);
        Assert.Contains(failure.Message, exception.Message);
        Assert.DoesNotContain("Developer Mode", exception.Message);
        Assert.AreSame(previousPackage, packageManager.Packages[0]);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenRemovalCompletesButPackageRemains_DoesNotRetryRegistration()
    {
        string previousLayout = GetLayoutDirectory("layout-a");
        string requestedLayout = GetLayoutDirectory("layout-b");
        var previousPackage = new RegisteredPackageInfo(PackageFullName, previousLayout, isDevelopmentMode: true);
        var packageManager = new TestPackageManager
        {
            Packages = [previousPackage],
            RemoveOverride = (_, _) => Task.CompletedTask,
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, TestContext.CancellationToken));

        Assert.Contains(PackageFullName, exception.Message);
        Assert.Contains(previousLayout, exception.Message);
        Assert.Contains(requestedLayout, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.AreSame(previousPackage, packageManager.Packages[0]);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenReplacementRegistrationFails_ReportsFailure()
    {
        string layout = GetLayoutDirectory("layout-b");
        var failure = new InvalidOperationException("Replacement registration failed.");
        int registrationCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
            RegisterOverride = _ => ++registrationCalls == 1 ? Task.CompletedTask : Task.FromException(failure),
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(layout, TestContext.CancellationToken));

        Assert.AreSame(failure, exception.InnerException);
        Assert.Contains(layout, exception.Message);
        Assert.IsEmpty(packageManager.Packages);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenRetryStillRegistersWrongLayout_ReportsBothLocations()
    {
        string previousLayout = GetLayoutDirectory("layout-a");
        string requestedLayout = GetLayoutDirectory("layout-b");
        var packageManager = new TestPackageManager();
        packageManager.RegisterOverride = _ =>
        {
            packageManager.Packages = [new(PackageFullName, previousLayout, isDevelopmentMode: true)];
            return Task.CompletedTask;
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, TestContext.CancellationToken));

        Assert.Contains(previousLayout, exception.Message);
        Assert.Contains(requestedLayout, exception.Message);
        Assert.IsNull(exception.InnerException);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WithNonDevelopmentRegistration_RefusesRemoval()
    {
        string previousLayout = GetLayoutDirectory("layout-a");
        string requestedLayout = GetLayoutDirectory("layout-b");
        var previousPackage = new RegisteredPackageInfo(PackageFullName, previousLayout, isDevelopmentMode: false);
        var packageManager = new TestPackageManager { Packages = [previousPackage] };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, TestContext.CancellationToken));

        Assert.Contains(PackageFullName, exception.Message);
        Assert.Contains(previousLayout, exception.Message);
        Assert.Contains(requestedLayout, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.AreSame(previousPackage, packageManager.Packages[0]);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WithNoRegistrationAfterSuccess_ReportsFailure()
    {
        string layout = GetLayoutDirectory("layout-a");
        var packageManager = new TestPackageManager { RegisterOverride = _ => Task.CompletedTask };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(layout, TestContext.CancellationToken));

        Assert.Contains(layout, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsEmpty(packageManager.Packages);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WithMultipleRegistrations_RefusesRemoval()
    {
        string previousLayout = GetLayoutDirectory("layout-a");
        string requestedLayout = GetLayoutDirectory("layout-b");
        var packageManager = new TestPackageManager
        {
            Packages =
            [
                new(PackageFullName, previousLayout, isDevelopmentMode: true),
                new("Contoso.LayoutTests_2.0.0.0_neutral__abcdefghijklm", requestedLayout, isDevelopmentMode: true),
            ],
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, TestContext.CancellationToken));

        Assert.Contains(previousLayout, exception.Message);
        Assert.Contains(requestedLayout, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.HasCount(2, packageManager.Packages);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenCanceledBeforeRegistration_DoesNotCallPackageManager()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var packageManager = new TestPackageManager();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => packageManager.RegisterAsync(GetLayoutDirectory("layout-a"), cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsEmpty(packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenCanceledAfterRegistration_DoesNotRemoveRegistration()
    {
        using var cancellation = new CancellationTokenSource();
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
            RegisterOverride = token =>
            {
                Assert.AreEqual(cancellation.Token, token);
                cancellation.Cancel();
                return Task.CompletedTask;
            },
        };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => packageManager.RegisterAsync(GetLayoutDirectory("layout-b"), cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        string[] expectedOperations = ["register"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenCanceledWhileFindingRegistration_DoesNotRemoveRegistration()
    {
        using var cancellation = new CancellationTokenSource();
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.FindOverride = () =>
        {
            cancellation.Cancel();
            return packageManager.Packages;
        };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => packageManager.RegisterAsync(GetLayoutDirectory("layout-b"), cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        string[] expectedOperations = ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenCanceledAfterRemoval_CompletesReplacementBeforePropagatingCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        string requestedLayout = GetLayoutDirectory("layout-b");
        int registrationCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.RegisterOverride = token =>
        {
            registrationCalls++;
            if (registrationCalls == 1)
            {
                Assert.AreEqual(cancellation.Token, token);
            }
            else
            {
                Assert.AreEqual(CancellationToken.None, token);
                packageManager.Packages = [new(PackageFullName, requestedLayout, isDevelopmentMode: true)];
            }

            return Task.CompletedTask;
        };
        packageManager.RemoveOverride = (_, token) =>
        {
            Assert.AreEqual(cancellation.Token, token);
            packageManager.Packages = [];
            cancellation.Cancel();
            return Task.CompletedTask;
        };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => packageManager.RegisterAsync(requestedLayout, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreEqual(requestedLayout, packageManager.Packages[0].InstalledPath);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task RegisterAsync_WhenCanceledDuringRecoveryQuery_CompletesReplacementBeforePropagatingCancellation(int canceledQuery)
    {
        using var cancellation = new CancellationTokenSource();
        string requestedLayout = GetLayoutDirectory("layout-b");
        int findCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.FindOverride = () =>
        {
            if (++findCalls == canceledQuery)
            {
                cancellation.Cancel();
            }

            return packageManager.Packages;
        };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => packageManager.RegisterAsync(requestedLayout, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreEqual(requestedLayout, packageManager.Packages[0].InstalledPath);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenCanceledWhileFindingMissingReplacement_ReportsLocationMismatch()
    {
        using var cancellation = new CancellationTokenSource();
        string requestedLayout = GetLayoutDirectory("layout-b");
        int findCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.FindOverride = () =>
        {
            if (++findCalls == 3)
            {
                packageManager.Packages = [];
                cancellation.Cancel();
            }

            return packageManager.Packages;
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(requestedLayout, cancellation.Token));

        Assert.Contains(requestedLayout, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsEmpty(packageManager.Packages);
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RegisterAsync_WhenQueryFails_DoesNotRelabelItAsDeploymentFailure(bool afterReplacement)
    {
        var failure = new InvalidOperationException("Could not query registered packages.");
        int findCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.FindOverride = () => ++findCalls == (afterReplacement ? 3 : 1)
            ? throw failure
            : packageManager.Packages;

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => packageManager.RegisterAsync(GetLayoutDirectory("layout-b"), TestContext.CancellationToken));

        Assert.AreSame(failure, exception);
        string[] expectedOperations = afterReplacement
            ? ["register", "find", $"remove:{PackageFullName}", "find", "register", "find"]
            : ["register", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RegisterAsync_WhenPostRemovalQueryFails_DoesNotWrapFailureOrRetryRegistration(bool cancelDuringQuery)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var failure = new IOException("Could not query packages after removal.");
        int findCalls = 0;
        var packageManager = new TestPackageManager
        {
            Packages = [new(PackageFullName, GetLayoutDirectory("layout-a"), isDevelopmentMode: true)],
        };
        packageManager.FindOverride = () =>
        {
            if (++findCalls == 2)
            {
                if (cancelDuringQuery)
                {
                    cancellation.Cancel();
                }

                throw failure;
            }

            return packageManager.Packages;
        };

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => packageManager.RegisterAsync(GetLayoutDirectory("layout-b"), cancellation.Token));

        Assert.AreSame(failure, exception);
        Assert.IsEmpty(packageManager.Packages, "Removal succeeded even though its subsequent verification failed.");
        string[] expectedOperations = ["register", "find", $"remove:{PackageFullName}", "find"];
        Assert.AreSequenceEqual(expectedOperations, packageManager.Operations);
    }

    private static string GetLayoutDirectory(string name)
        => Path.Combine(Path.GetTempPath(), nameof(PackageDeployerTests), name);

    private sealed class TestPackageManager
    {
        public IReadOnlyList<RegisteredPackageInfo> Packages { get; set; } = [];

        public List<string> Operations { get; } = [];

        public Func<CancellationToken, Task>? RegisterOverride { get; set; }

        public Func<IReadOnlyList<RegisteredPackageInfo>>? FindOverride { get; set; }

        public Func<string, CancellationToken, Task>? RemoveOverride { get; set; }

        public Task RegisterAsync(string layout, CancellationToken cancellationToken)
            => PackageDeployer.RegisterAsync(
                Path.Combine(layout, AppxManifestInfo.AppxManifestFileName),
                token =>
                {
                    Operations.Add("register");
                    if (RegisterOverride is not null)
                    {
                        return RegisterOverride(token);
                    }

                    // Model Windows retaining an existing same-version registration.
                    if (Packages.Count == 0)
                    {
                        Packages = [new(PackageFullName, layout, isDevelopmentMode: true)];
                    }

                    return Task.CompletedTask;
                },
                () =>
                {
                    Operations.Add("find");
                    return FindOverride?.Invoke() ?? Packages;
                },
                (packageFullName, token) =>
                {
                    Operations.Add($"remove:{packageFullName}");
                    if (RemoveOverride is not null)
                    {
                        return RemoveOverride(packageFullName, token);
                    }

                    Packages = Packages.Where(package => package.FullName != packageFullName).ToArray();
                    return Task.CompletedTask;
                },
                cancellationToken);
    }

    public TestContext TestContext { get; set; }
}

#endif

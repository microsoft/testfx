// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppTestHostLauncherTests
{
    private const string MicrosoftStorePublisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
    private const string MicrosoftStorePublisherId = "8wekyb3d8bbwe";

    /// <summary>A minimal manifest that makes a layout classify as packaged.</summary>
    private static readonly string PackagedManifestXml = BuildManifestXml("Contoso.MyTestApp", MicrosoftStorePublisher, "App");

    // A layout without an AppxManifest.xml can be started with a plain Process.Start. Enabling the
    // launcher would force the platform onto the test host controller (process restart) model and copy
    // the layout to a deployment directory — pure overhead here, and most visible for an unpackaged
    // WinUI test app that merely references this package.
    [TestMethod]
    public Task IsEnabledAsync_WithLooseLayout_IsDisabledSoTheDefaultLaunchPathIsKept()
        => AssertIsEnabledAsync(expected: false, manifestXml: null, mode: null);

    // A packaged (MSIX) layout genuinely cannot be started with Process.Start, so the launcher takes
    // over without the user having to configure anything.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_WithPackagedLayout_IsEnabled()
        => AssertIsEnabledAsync(expected: true, PackagedManifestXml, mode: null);

    // 'always' is how a consumer opts a non-packaged layout into deploy-and-launch.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_WithLooseLayoutAndAlwaysMode_IsEnabled()
        => AssertIsEnabledAsync(expected: true, manifestXml: null, mode: "always");

    // 'never' is the escape hatch: it keeps the launcher out of the way even for a packaged layout.
    [TestMethod]
    public Task IsEnabledAsync_WithPackagedLayoutAndNeverMode_IsDisabled()
        => AssertIsEnabledAsync(expected: false, PackagedManifestXml, mode: "never");

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_ModeIsCaseAndWhitespaceInsensitive()
        => AssertIsEnabledAsync(expected: true, manifestXml: null, mode: "  AlWaYs  ");

    // A typo in an environment variable must not do anything beyond falling back to the default (probe
    // the layout), and must never fail the run. Asserting against a packaged layout is what proves the
    // probe actually ran: expecting false on a loose layout would be indistinguishable from an
    // unrecognized mode short-circuiting straight to disabled.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_WithUnrecognizedMode_FallsBackToProbingTheLayout()
        => AssertIsEnabledAsync(expected: true, PackagedManifestXml, mode: "alwyas");

    [TestMethod]
    public Task IsEnabledAsync_WithUnrecognizedModeAndLooseLayout_IsDisabled()
        => AssertIsEnabledAsync(expected: false, manifestXml: null, mode: "alwyas");

    // The layout probe walks up from the app directory because Application/@Executable may point into a
    // subdirectory. Ancestor manifests are accepted only when they describe the app directory, so an
    // unrelated manifest far above (a shared build root, a CI staging directory) must not classify an
    // ordinary test app as packaged and take over its run.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_WithAncestorManifestDeclaringDeepExecutable_IsEnabled()
    {
        const int AppSubdirectoryDepth = 4;
        return AssertIsEnabledForManifestAsync(
            BuildManifestXml(
                "Contoso.MyTestApp",
                MicrosoftStorePublisher,
                applicationId: "App",
                executable: GetNestedExecutablePath(AppSubdirectoryDepth, "MyTestApp.exe")),
            expected: true,
            appSubdirectoryDepth: AppSubdirectoryDepth);
    }

    [TestMethod]
    public Task IsEnabledAsync_WithStrayAncestorManifest_IsDisabled()
        => AssertIsEnabledForManifestAsync(
            BuildManifestXml(
                "Contoso.MyOtherApp",
                MicrosoftStorePublisher,
                applicationId: "App",
                executable: "other\\MyTestApp.exe"),
            expected: false,
            appSubdirectoryDepth: 4);

    [TestMethod]
    public Task IsEnabledAsync_WithMalformedAncestorManifest_IsDisabled()
        => AssertIsEnabledForManifestAsync("not xml", expected: false, appSubdirectoryDepth: 4);

    // Regression test: the launcher passes AppContext.BaseDirectory, which ends with a directory
    // separator, while Path.GetDirectoryName never returns one. Comparing them unnormalized silently
    // failed to attribute any ancestor manifest, so enablement never fired in production even though
    // the separator-free paths these tests build kept passing.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps are a Windows-only scenario; the launcher is unconditionally disabled elsewhere.")]
    public Task IsEnabledAsync_WithTrailingSeparatorOnAppDirectory_IsEnabled()
    {
        const int AppSubdirectoryDepth = 4;
        return RunInTemporaryLayoutAsync(
            BuildManifestXml(
                "Contoso.MyTestApp",
                MicrosoftStorePublisher,
                applicationId: "App",
                executable: GetNestedExecutablePath(AppSubdirectoryDepth, "MyTestApp.exe")),
            async (_, appDirectory) =>
            {
                var launcher = new PackagedAppTestHostLauncher(appDirectory + Path.DirectorySeparatorChar, static _ => null);

                Assert.IsTrue(await launcher.IsEnabledAsync());
            },
            AppSubdirectoryDepth);
    }

    // Packaged Windows apps are a Windows-only concept, so neither a packaged layout nor an explicit
    // 'always' may register the launcher elsewhere: that would force every non-Windows run onto the
    // controller host for a launcher that cannot work there.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "This asserts the non-Windows behavior.")]
    public Task IsEnabledAsync_OnNonWindows_IsDisabledEvenForAPackagedLayoutAndAlwaysMode()
        => AssertIsEnabledAsync(expected: false, PackagedManifestXml, mode: "always");

    [TestMethod]
    public async Task LaunchTestHostAsync_WithPackagedLayout_ThrowsWithApplicationUserModelId()
    {
        InvalidOperationException exception = await LaunchInLayoutContainingManifestAsync(applicationId: "App");

        // The error must stay actionable: it carries the AUMID activation would use, so a reader knows
        // exactly which packaged app could not be launched.
        Assert.Contains($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!App", exception.Message);
    }

    [TestMethod]
    public async Task LaunchTestHostAsync_WithPackagedLayoutWithoutApplication_ThrowsWithPackageFamilyName()
    {
        InvalidOperationException exception = await LaunchInLayoutContainingManifestAsync(applicationId: null);

        // With no Application declared there is no AUMID, so the message falls back to the package
        // family name rather than an empty identity.
        Assert.Contains($"Contoso.MyTestApp_{MicrosoftStorePublisherId}", exception.Message);
    }

    [TestMethod]
    public async Task LaunchTestHostAsync_WithMultipleApplications_ReportsTheOneMatchingTheExecutable()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" Executable="First.exe" />
                <Application Id="Second" Executable="MyTestApp.exe" />
              </Applications>
            </Package>
            """;

        InvalidOperationException exception = await LaunchInLayoutContainingManifestAsync(ManifestXml, testHostFileName: "MyTestApp.exe");

        // The reported identity must be the app whose Executable matches the requested test host, not
        // simply the first application declared in the manifest.
        Assert.Contains($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!Second", exception.Message);
    }

    [TestMethod]
    public async Task LaunchTestHostAsync_WithAncestorManifestDeclaringDeepExecutable_ThrowsWithApplicationUserModelId()
    {
        const int AppSubdirectoryDepth = 4;
        InvalidOperationException exception = await LaunchInLayoutContainingManifestAsync(
            BuildManifestXml(
                "Contoso.MyTestApp",
                MicrosoftStorePublisher,
                applicationId: "App",
                executable: GetNestedExecutablePath(AppSubdirectoryDepth, "MyTestApp.exe")),
            testHostFileName: "MyTestApp.exe",
            appSubdirectoryDepth: AppSubdirectoryDepth);

        Assert.Contains($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!App", exception.Message);
    }

    [TestMethod]
    public void CreateActivationArguments_WithAppContainerApplication_UsesOpaquePayload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PackagedAppTestHostLauncherTests", Guid.NewGuid().ToString("N"));
        try
        {
            string[] expected = ["--filter", "two words", string.Empty];
            var application = new AppxApplicationInfo("App", "App.exe", "Contoso!App", usesLaunchActivationArguments: true);

            PackagedAppActivationData activation = PackagedAppTestHostLauncher.CreateActivationArguments(application, expected, directory);
            string[] actual = PackagedAppActivationArguments.Read(activation.Arguments, directory);

            Assert.IsNull(activation.PayloadPath);
            Assert.HasCount(expected.Length, actual);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], $"Argument {i} differs.");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CreateActivationArguments_WithFullTrustApplication_UsesWindowsArgvQuoting()
    {
        var application = new AppxApplicationInfo("App", "App.exe", "Contoso!App", usesLaunchActivationArguments: false);

        PackagedAppActivationData activation = PackagedAppTestHostLauncher.CreateActivationArguments(
            application,
            ["--filter", "two words", string.Empty],
            Path.GetTempPath());

        Assert.IsNull(activation.PayloadPath);
        Assert.AreEqual("--filter \"two words\" \"\"", activation.Arguments);
    }

    [TestMethod]
    public void RedirectAppContainerFileSystemOptions_WithExplicitDirectories_ReplacesTheirValues()
    {
        string scratchDirectory = Path.GetFullPath("appcontainer-scratch");

        IReadOnlyList<string> actual = RedirectAppContainerFileSystemOptions(
            ["--results-directory", "controller-results", "--diagnostic", "--diagnostic-output-directory", "controller-diagnostics"],
            scratchDirectory,
            removeMSBuildNode: false);

        Assert.AreSequenceEqual(
            ["--results-directory", scratchDirectory, "--diagnostic", "--diagnostic-output-directory", scratchDirectory],
            actual);
    }

    [TestMethod]
    public void RedirectAppContainerFileSystemOptions_WithoutExplicitDirectories_AddsRequiredScratchDirectories()
    {
        string scratchDirectory = Path.GetFullPath("appcontainer-scratch");

        IReadOnlyList<string> actual = RedirectAppContainerFileSystemOptions(
            ["--diagnostic", "--filter", "two words"],
            scratchDirectory,
            removeMSBuildNode: false);

        Assert.AreSequenceEqual(
            [
                "--diagnostic",
                "--filter",
                "two words",
                "--results-directory",
                scratchDirectory,
                "--diagnostic-output-directory",
                scratchDirectory,
            ],
            actual);
    }

    [TestMethod]
    public void RedirectAppContainerFileSystemOptions_ForRetry_RemovesMSBuildNode()
    {
        string scratchDirectory = Path.GetFullPath("appcontainer-scratch");

        IReadOnlyList<string> actual = RedirectAppContainerFileSystemOptions(
            ["--internal-msbuild-node", "msbuild-pipe", "--internal-retry-pipename", "retry-pipe"],
            scratchDirectory,
            removeMSBuildNode: true);

        Assert.AreSequenceEqual(
            [
                "--internal-retry-pipename",
                "retry-pipe",
                "--results-directory",
                scratchDirectory,
            ],
            actual);
    }

    [TestMethod]
    public void GetControllerPath_WithRelativePath_UsesLaunchWorkingDirectory()
    {
        string workingDirectory = Path.GetFullPath("controller-working-directory");
#pragma warning disable TPEXP // TestHostLaunchContext is experimental.
        var context = new TestHostLaunchContext(
            Path.Combine(workingDirectory, "testhost.exe"),
            [],
            new Dictionary<string, string?>(),
            workingDirectory);
#pragma warning restore TPEXP

        string actual = GetControllerPath("TestResults", context);

        Assert.AreEqual(Path.Combine(workingDirectory, "TestResults"), actual);
    }

    [TestMethod]
    public void IsAppxRecipeAlreadyMaterialized_ManifestComesFromLayout_ReturnsTrue()
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(PackagedAppTestHostLauncherTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifestPath = Path.Combine(directory, "AppxManifest.xml");
            File.WriteAllText(manifestPath, "<Package />");
            var recipe = XDocument.Parse($"""
                <Project>
                  <AppXManifest Include="{manifestPath}">
                    <PackagePath>AppxManifest.xml</PackagePath>
                  </AppXManifest>
                </Project>
                """);

            Assert.IsTrue(IsAppxRecipeAlreadyMaterialized(recipe, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void IsAppxRecipeAlreadyMaterialized_ManifestComesFromCoreStagingDirectory_ReturnsFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(PackagedAppTestHostLauncherTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "AppxManifest.xml"), "<Package />");
            string stagedManifestPath = Path.Combine(directory, "Core", "AppxManifest.xml");
            var recipe = XDocument.Parse($"""
                <Project>
                  <AppXManifest Include="{stagedManifestPath}">
                    <PackagePath>AppxManifest.xml</PackagePath>
                  </AppXManifest>
                </Project>
                """);

            Assert.IsFalse(IsAppxRecipeAlreadyMaterialized(recipe, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MaterializeAppxRecipeLayout_WithMultipleApplications_ReturnsRequestedPackagedExecutable()
    {
        string root = Path.Combine(Path.GetTempPath(), nameof(PackagedAppTestHostLauncherTests), Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(root, "Source");
        string aliasDirectory = Path.Combine(root, "Alias");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(aliasDirectory);
        try
        {
            string manifestPath = Path.Combine(root, "AppxManifest.xml");
            File.WriteAllText(
                manifestPath,
                BuildManifestXmlWithApplications(
                    "Contoso.MyTestApp",
                    MicrosoftStorePublisher,
                    """
                    <Applications>
                      <Application Id="First" Executable="Apps\First.exe" />
                      <Application Id="Requested" Executable="Apps\Requested.exe" />
                    </Applications>
                    """));
            string firstExecutablePath = Path.Combine(root, "First.exe");
            string requestedExecutablePath = Path.Combine(sourceDirectory, "Requested.exe");
            string aliasedExecutablePath = Path.Combine(aliasDirectory, "Requested.exe");
            File.WriteAllText(firstExecutablePath, "first");
            File.WriteAllText(requestedExecutablePath, "requested");
            File.WriteAllText(aliasedExecutablePath, "alias");

            string recipePath = Path.Combine(sourceDirectory, "App.build.appxrecipe");
            File.WriteAllText(
                recipePath,
                $"""
                <Project>
                  <AppXManifest Include="{manifestPath}">
                    <PackagePath>AppxManifest.xml</PackagePath>
                  </AppXManifest>
                  <AppxPackagedFile Include="{firstExecutablePath}">
                    <PackagePath>Apps\First.exe</PackagePath>
                  </AppxPackagedFile>
                  <AppxPackagedFile Include="{aliasedExecutablePath}">
                    <PackagePath>Apps\Requested.exe</PackagePath>
                  </AppxPackagedFile>
                </Project>
                """);

            string materializedExecutablePath = MaterializeAppxRecipeLayout(
                requestedExecutablePath,
                out string? actualRecipePath,
                path => string.Equals(path, aliasedExecutablePath, StringComparison.OrdinalIgnoreCase)
                    ? requestedExecutablePath
                    : Path.GetFullPath(path));

            string layoutDirectory = Path.Combine(sourceDirectory, "_MtpPackageLayout");
            Assert.AreEqual(Path.Combine(layoutDirectory, "Apps", "Requested.exe"), materializedExecutablePath);
            Assert.AreEqual(recipePath, actualRecipePath);
            Assert.AreEqual("requested", File.ReadAllText(materializedExecutablePath));
            Assert.AreEqual(
                "Requested",
                AppxManifestInfo.ReadFromManifest(Path.Combine(layoutDirectory, AppxManifestInfo.AppxManifestFileName))
                    .ResolveApplication(layoutDirectory, materializedExecutablePath)?
                    .Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MaterializeAppxRecipeLayout_WithClassicUwpEntrypoint_ReturnsManifestBootstrapExecutable()
    {
        string root = Path.Combine(Path.GetTempPath(), nameof(PackagedAppTestHostLauncherTests), Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(root, "Source");
        Directory.CreateDirectory(sourceDirectory);
        try
        {
            string manifestPath = Path.Combine(root, "AppxManifest.xml");
            File.WriteAllText(
                manifestPath,
                BuildManifestXml(
                    "Contoso.MyTestApp",
                    MicrosoftStorePublisher,
                    applicationId: "App",
                    executable: "CUwp.exe"));
            string requestedExecutablePath = Path.Combine(sourceDirectory, "CUwp.exe");
            string bootstrapExecutablePath = Path.Combine(root, "Core", "CUwp.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(bootstrapExecutablePath)!);
            File.WriteAllText(requestedExecutablePath, "managed entrypoint");
            File.WriteAllText(bootstrapExecutablePath, "native bootstrap");

            File.WriteAllText(
                Path.Combine(sourceDirectory, "App.build.appxrecipe"),
                $"""
                <Project>
                  <AppXManifest Include="{manifestPath}">
                    <PackagePath>AppxManifest.xml</PackagePath>
                  </AppXManifest>
                  <AppxPackagedFile Include="{requestedExecutablePath}">
                    <PackagePath>entrypoint\CUwp.exe</PackagePath>
                  </AppxPackagedFile>
                  <AppxPackagedFile Include="{bootstrapExecutablePath}">
                    <PackagePath>CUwp.exe</PackagePath>
                  </AppxPackagedFile>
                </Project>
                """);

            string materializedExecutablePath = MaterializeAppxRecipeLayout(requestedExecutablePath, out _);

            string layoutDirectory = Path.Combine(sourceDirectory, "_MtpPackageLayout");
            Assert.AreEqual(Path.Combine(layoutDirectory, "CUwp.exe"), materializedExecutablePath);
            Assert.AreEqual("native bootstrap", File.ReadAllText(materializedExecutablePath));
            Assert.AreEqual(
                "managed entrypoint",
                File.ReadAllText(Path.Combine(layoutDirectory, "entrypoint", "CUwp.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Task<InvalidOperationException> LaunchInLayoutContainingManifestAsync(string? applicationId)
        => LaunchInLayoutContainingManifestAsync(
            BuildManifestXml("Contoso.MyTestApp", MicrosoftStorePublisher, applicationId),
            testHostFileName: "MyTestApp.exe");

    private static async Task<InvalidOperationException> LaunchInLayoutContainingManifestAsync(
        string manifestXml,
        string testHostFileName,
        int appSubdirectoryDepth = 0)
    {
        InvalidOperationException? exception = null;

        await RunInTemporaryLayoutAsync(manifestXml, async (_, appDirectory) =>
        {
            var launcher = new PackagedAppTestHostLauncher();

            // The executable does not need to exist: the packaged-layout check happens before any launch.
            string fakeTestHost = Path.Combine(appDirectory, testHostFileName);
#pragma warning disable TPEXP // TestHostLaunchContext is experimental.
            var context = new TestHostLaunchContext(fakeTestHost, [], new Dictionary<string, string?>(), workingDirectory: null);
            exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => launcher.LaunchTestHostAsync(context, CancellationToken.None));
#pragma warning restore TPEXP
        },
        appSubdirectoryDepth);

        return exception!;
    }

    /// <summary>
    /// Asserts the launcher's enablement decision for a layout that does (or does not) contain an
    /// <c>AppxManifest.xml</c>, under an explicit <see cref="PackagedAppTestHostLauncher.LauncherModeEnvironmentVariable"/>
    /// value. The environment is stubbed rather than mutated so the assertion never depends on — nor
    /// leaks into — the ambient environment of a parallel test run.
    /// </summary>
    private static Task AssertIsEnabledAsync(bool expected, string? manifestXml, string? mode)
        => RunInTemporaryLayoutAsync(manifestXml, async (_, appDirectory) =>
        {
            var launcher = new PackagedAppTestHostLauncher(
                appDirectory,
                name => name == PackagedAppTestHostLauncher.LauncherModeEnvironmentVariable ? mode : null);

            Assert.AreEqual(expected, await launcher.IsEnabledAsync());
        });

    [TestMethod]
    public void GetConnectBackEnvironment_IncludesReporterRecoveryTransport()
    {
#pragma warning disable TPEXP // TestHostLaunchContext is experimental.
        var context = new TestHostLaunchContext(
            "testhost.exe",
            [],
            new Dictionary<string, string?>
            {
                ["TESTINGPLATFORM_CTRFREPORT_JOURNAL"] = "ctrf.jsonl",
                ["TESTINGPLATFORM_HTMLREPORT_JOURNAL"] = "html.jsonl",
                ["TESTINGPLATFORM_JUNITREPORT_JOURNAL"] = "junit.jsonl",
                ["TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"] = "retry.txt",
                ["TESTINGPLATFORM_SECRET"] = "must-not-flow",
            },
            workingDirectory: null);
#pragma warning restore TPEXP

        var environment =
            PackagedAppTestHostLauncher.GetConnectBackEnvironment(context).ToDictionary();

        Assert.AreEqual("ctrf.jsonl", environment["TESTINGPLATFORM_CTRFREPORT_JOURNAL"]);
        Assert.AreEqual("html.jsonl", environment["TESTINGPLATFORM_HTMLREPORT_JOURNAL"]);
        Assert.AreEqual("junit.jsonl", environment["TESTINGPLATFORM_JUNITREPORT_JOURNAL"]);
        Assert.AreEqual("retry.txt", environment["TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"]);
        Assert.IsFalse(environment.ContainsKey("TESTINGPLATFORM_SECRET"));
    }

    /// <summary>
    /// Places a manifest at the layout root and the app <paramref name="appSubdirectoryDepth"/>
    /// directory levels below it, then asserts whether the launcher's upward probe still finds it.
    /// </summary>
    private static Task AssertIsEnabledForManifestAsync(string manifestXml, bool expected, int appSubdirectoryDepth)
        => RunInTemporaryLayoutAsync(manifestXml, async (_, appDirectory) =>
        {
            var launcher = new PackagedAppTestHostLauncher(appDirectory, static _ => null);

            Assert.AreEqual(expected, await launcher.IsEnabledAsync());
        },
        appSubdirectoryDepth);

    private static IReadOnlyList<string> RedirectAppContainerFileSystemOptions(
        IReadOnlyList<string> arguments,
        string scratchDirectory,
        bool removeMSBuildNode)
        => (IReadOnlyList<string>)typeof(PackagedAppTestHostLauncher)
            .GetMethod(
                "RedirectAppContainerFileSystemOptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [arguments, scratchDirectory, removeMSBuildNode])!;

    private static string GetControllerPath(string path, TestHostLaunchContext context)
        => (string)typeof(PackagedAppTestHostLauncher)
            .GetMethod(
                "GetControllerPath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [path, context])!;

    private static bool IsAppxRecipeAlreadyMaterialized(XDocument recipe, string sourceDirectory)
        => (bool)typeof(PackagedAppTestHostLauncher)
            .GetMethod(
                "IsAppxRecipeAlreadyMaterialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [recipe, sourceDirectory])!;

    private static string MaterializeAppxRecipeLayout(
        string targetFileName,
        out string? appxRecipePath,
        Func<string, string>? resolveFinalPath = null)
    {
        object?[] arguments = [targetFileName, null, resolveFinalPath];
        string materializedTargetFileName = (string)typeof(PackagedAppTestHostLauncher)
            .GetMethod(
                "MaterializeAppxRecipeLayout",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, arguments)!;
        appxRecipePath = (string?)arguments[1];
        return materializedTargetFileName;
    }

    /// <summary>
    /// Runs <paramref name="action"/> against a throw-away layout, optionally containing an
    /// <c>AppxManifest.xml</c> at its root so the layout classifies as packaged. The action receives the
    /// layout root and the app directory, which sit <paramref name="appSubdirectoryDepth"/> levels
    /// apart so the upward manifest probe can be exercised. Passing the directory explicitly (rather
    /// than relying on the test run's own output directory) keeps these tests independent of where the
    /// test host happens to run from.
    /// </summary>
    private static async Task RunInTemporaryLayoutAsync(string? manifestXml, Func<string, string, Task> action, int appSubdirectoryDepth = 0)
    {
        string root = Path.Combine(Path.GetTempPath(), "PackagedAppTestHostLauncherTests", Guid.NewGuid().ToString("N"));
        string appDirectory = appSubdirectoryDepth == 0
            ? root
            : Path.Combine(root, Path.Combine([.. Enumerable.Repeat("nested", appSubdirectoryDepth)]));
        Directory.CreateDirectory(appDirectory);
        try
        {
            if (manifestXml is not null)
            {
                File.WriteAllText(Path.Combine(root, AppxManifestInfo.AppxManifestFileName), manifestXml);
            }

            await action(root, appDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string BuildManifestXml(string name, string publisher, string? applicationId)
        => BuildManifestXml(name, publisher, applicationId, executable: null);

    private static string BuildManifestXmlWithApplications(string name, string publisher, string applications)
        => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="{name}" Publisher="{publisher}" Version="1.0.0.0" />
              {applications}
            </Package>
            """;

    private static string BuildManifestXml(string name, string publisher, string? applicationId, string? executable)
    {
        string executableAttribute = executable is null ? string.Empty : $" Executable=\"{executable}\"";
        string applications = applicationId is null
            ? string.Empty
            : $"""
                 <Applications>
                   <Application Id="{applicationId}"{executableAttribute} />
                 </Applications>
               """;

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="{name}" Publisher="{publisher}" Version="1.0.0.0" />
            {applications}
            </Package>
            """;
    }

    private static string GetNestedExecutablePath(int depth, string fileName)
        => string.Join("\\", Enumerable.Repeat("nested", depth).Append(fileName));
}

#endif

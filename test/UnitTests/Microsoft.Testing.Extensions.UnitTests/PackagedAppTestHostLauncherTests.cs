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

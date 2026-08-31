// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers which identity — if any — the launcher asks the platform to authorize on the test host controller
/// pipe. Every case that must <em>not</em> widen the pipe DACL is asserted explicitly, because a false
/// positive here silently adds an identity to a security boundary that otherwise only names the current
/// user.
/// </summary>
[TestClass]
public sealed class PackagedAppPipeAuthorizationTests
{
    private const string MicrosoftStorePublisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    /// <summary>The AppContainer SID of <c>Contoso.MyTestApp_8wekyb3d8bbwe</c>.</summary>
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    // The motivating case: a UWP / AppContainer-configured packaged app cannot reach a pipe whose DACL only
    // names the user, so its package SID — and only its package SID — is requested.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithAppContainerPackage_AuthorizesOnlyThatPackage()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null),
            mode: null,
            expected: [ContosoPackageSid]);

    // A packaged full-trust desktop host runs with an ordinary token and already reaches the pipe through
    // the platform's current-user protection, so it must get nothing extra.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithRunFullTrustCapability_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: true, entryPoint: null),
            mode: null,
            expected: []);

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithFullTrustEntryPoint_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: "Windows.FullTrustApplication"),
            mode: null,
            expected: []);

    /// <summary>
    /// A <c>packagedClassicApp</c> whose <c>TrustLevel</c> is <c>appContainer</c> receives its arguments as
    /// ordinary process <c>argv</c> — so it needs no activation bootstrap — but it still runs sandboxed, so
    /// its restricted token cannot reach the controller connection without the package SID being authorized.
    /// Conflating "receives launch-activation arguments" with "runs in an AppContainer" would leave exactly
    /// this shape unable to connect.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithClassicAppInAnAppContainer_StillAuthorizesThePackage()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null, trustLevel: "appContainer", runtimeBehavior: "packagedClassicApp"),
            mode: null,
            expected: [ContosoPackageSid]);

    /// <summary>
    /// A <c>windowsApp</c> normally uses launch activation, but an explicit <c>mediumIL</c> trust level is
    /// authoritative about the token: the process is not an AppContainer and already reaches the pipe
    /// through the platform's current-user protection.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithWindowsAppAtMediumIntegrity_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null, trustLevel: "mediumIL", runtimeBehavior: "windowsApp"),
            mode: null,
            expected: []);

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithMixedPackageAndFullTrustHostSelected_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildMixedTrustManifestXml(),
            mode: null,
            expected: [],
            testHostFileName: "DesktopTests.exe");

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithMixedPackageAndAppContainerHostSelected_AuthorizesThePackage()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildMixedTrustManifestXml(),
            mode: null,
            expected: [ContosoPackageSid],
            testHostFileName: "UwpTests.exe");

    // A loose (non-packaged) layout has no package identity at all; it is launched as an ordinary process.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept; the launcher unconditionally authorizes nothing elsewhere, which OnNonWindows_AuthorizesNothingEvenWithAlwaysMode covers.")]
    public Task WithLooseLayout_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(manifestXml: null, mode: null, expected: []);

    // A manifest we cannot parse must never lead to a widened DACL; the launch path reports the parse
    // failure with its own error.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept; the launcher unconditionally authorizes nothing elsewhere, which OnNonWindows_AuthorizesNothingEvenWithAlwaysMode covers.")]
    public Task WithMalformedManifest_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync("not xml", mode: null, expected: []);

    // The escape hatch must be able to switch the grant off entirely.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept; the launcher unconditionally authorizes nothing elsewhere, which OnNonWindows_AuthorizesNothingEvenWithAlwaysMode covers.")]
    public Task WithNeverMode_AuthorizesNothing()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null),
            mode: "NeVeR",
            expected: []);

    // ... and on, for a manifest whose AppContainer signals we would otherwise read as full trust.
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithAlwaysMode_AuthorizesThePackageEvenForAFullTrustManifest()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: true, entryPoint: "Windows.FullTrustApplication"),
            mode: "always",
            expected: [ContosoPackageSid]);

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept; the launcher unconditionally authorizes nothing elsewhere, which OnNonWindows_AuthorizesNothingEvenWithAlwaysMode covers.")]
    public Task WithUnrecognizedMode_FallsBackToProbingTheManifest()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: true, entryPoint: null),
            mode: "alwyas",
            expected: []);

    // AppContainers, package SIDs and pipe DACLs do not exist elsewhere, so nothing is ever requested.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "This asserts the non-Windows behavior.")]
    public Task OnNonWindows_AuthorizesNothingEvenWithAlwaysMode()
        => AssertAuthorizedSecurityIdentifiersAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null),
            mode: "always",
            expected: []);

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainers are a Windows-only concept.")]
    public Task WithCancellation_Throws()
        => RunInTemporaryLayoutAsync(
            BuildManifestXml(runFullTrust: false, entryPoint: null),
            async appDirectory =>
            {
                var launcher = new PackagedAppTestHostLauncher(appDirectory, static _ => null);
                using var cancellationTokenSource = new CancellationTokenSource();
                await cancellationTokenSource.CancelAsync();

#pragma warning disable TPEXP // ITestHostControllerConnectionAuthorizer is experimental.
                await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                    () => launcher.GetAuthorizedSecurityIdentitiesAsync(
                        Path.Combine(appDirectory, "MyTestApp.exe"),
                        cancellationTokenSource.Token));
#pragma warning restore TPEXP
            });

    private static Task AssertAuthorizedSecurityIdentifiersAsync(
        string? manifestXml,
        string? mode,
        string[] expected,
        string testHostFileName = "MyTestApp.exe")
        => RunInTemporaryLayoutAsync(manifestXml, async appDirectory =>
        {
            var launcher = new PackagedAppTestHostLauncher(
                appDirectory,
                name => name == PackagedAppTestHostLauncher.PipeAuthorizationModeEnvironmentVariable ? mode : null);

#pragma warning disable TPEXP // ITestHostControllerConnectionAuthorizer is experimental.
            IReadOnlyList<string> actual = await launcher.GetAuthorizedSecurityIdentitiesAsync(
                Path.Combine(appDirectory, testHostFileName),
                CancellationToken.None);
#pragma warning restore TPEXP

            Assert.AreSequenceEqual(expected, actual);
        });

    private static async Task RunInTemporaryLayoutAsync(string? manifestXml, Func<string, Task> action)
    {
        string appDirectory = Path.Combine(Path.GetTempPath(), "PackagedAppPipeAuthorizationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(appDirectory);
        try
        {
            if (manifestXml is not null)
            {
                File.WriteAllText(Path.Combine(appDirectory, AppxManifestInfo.AppxManifestFileName), manifestXml);
            }

            await action(appDirectory);
        }
        finally
        {
            Directory.Delete(appDirectory, recursive: true);
        }
    }

    private static string BuildManifestXml(bool runFullTrust, string? entryPoint)
        => BuildManifestXml(runFullTrust, entryPoint, trustLevel: null, runtimeBehavior: null);

    private static string BuildManifestXml(bool runFullTrust, string? entryPoint, string? trustLevel, string? runtimeBehavior)
    {
        // The AppContainer classification lives in AppxManifestInfo (shared with activation-argument
        // delivery), so these manifests exercise the same signals it reads.
        string entryPointAttribute = entryPoint is null ? string.Empty : $" EntryPoint=\"{entryPoint}\"";
        string trustLevelAttribute = trustLevel is null ? string.Empty : $" uap10:TrustLevel=\"{trustLevel}\"";
        string runtimeBehaviorAttribute = runtimeBehavior is null ? string.Empty : $" uap10:RuntimeBehavior=\"{runtimeBehavior}\"";
        string capabilities = runFullTrust
            ? """
                <Capabilities>
                  <rescap:Capability Name="runFullTrust" />
                </Capabilities>
              """
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10">
              <Identity Name="Contoso.MyTestApp" Publisher="{MicrosoftStorePublisher}" Version="1.0.0.0" />
              <Applications>
                <Application Id="App" Executable="MyTestApp.exe"{entryPointAttribute}{trustLevelAttribute}{runtimeBehaviorAttribute} />
              </Applications>
            {capabilities}
            </Package>
            """;
    }

    private static string BuildMixedTrustManifestXml()
        => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10">
              <Identity Name="Contoso.MyTestApp" Publisher="{MicrosoftStorePublisher}" Version="1.0.0.0" />
              <Applications>
                <Application Id="Desktop" Executable="DesktopTests.exe" EntryPoint="Windows.FullTrustApplication" />
                <Application Id="Uwp" Executable="UwpTests.exe" uap10:RuntimeBehavior="windowsApp" uap10:TrustLevel="appContainer" />
              </Applications>
            </Package>
            """;
}

#endif

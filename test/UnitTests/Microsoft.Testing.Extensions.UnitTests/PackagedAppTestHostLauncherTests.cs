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

    [TestMethod]
    public async Task IsEnabledAsync_IsEnabledOnlyOnWindows()
    {
        var launcher = new PackagedAppTestHostLauncher();

        // Packaged Windows apps are Windows-only. On other operating systems the launcher must report
        // itself disabled so it is never registered and the platform is not forced onto the controller
        // (deploy-and-launch) host. This assertion runs on both Windows and non-Windows CI legs.
        Assert.AreEqual(OperatingSystem.IsWindows(), await launcher.IsEnabledAsync());
    }

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

    private static Task<InvalidOperationException> LaunchInLayoutContainingManifestAsync(string? applicationId)
        => LaunchInLayoutContainingManifestAsync(
            BuildManifestXml("Contoso.MyTestApp", MicrosoftStorePublisher, applicationId),
            testHostFileName: "MyTestApp.exe");

    private static async Task<InvalidOperationException> LaunchInLayoutContainingManifestAsync(string manifestXml, string testHostFileName)
    {
        string directory = Path.Combine(Path.GetTempPath(), "PackagedAppTestHostLauncherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, AppxManifestInfo.AppxManifestFileName),
                manifestXml);

            var launcher = new PackagedAppTestHostLauncher();

            // The executable does not need to exist: the packaged-layout check happens before any launch.
            string fakeTestHost = Path.Combine(directory, testHostFileName);
#pragma warning disable TPEXP // TestHostLaunchContext is experimental.
            var context = new TestHostLaunchContext(fakeTestHost, [], new Dictionary<string, string?>(), workingDirectory: null);
            return await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => launcher.LaunchTestHostAsync(context, CancellationToken.None));
#pragma warning restore TPEXP
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string BuildManifestXml(string name, string publisher, string? applicationId)
    {
        string applications = applicationId is null
            ? string.Empty
            : $"""
                 <Applications>
                   <Application Id="{applicationId}" />
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
}

#endif

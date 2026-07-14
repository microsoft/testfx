// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AppxManifestInfoTests
{
    // The publisher of Microsoft Store apps, whose well-known publisher id is "8wekyb3d8bbwe".
    private const string MicrosoftStorePublisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
    private const string MicrosoftStorePublisherId = "8wekyb3d8bbwe";

    [TestMethod]
    public void ComputePublisherId_ForMicrosoftStorePublisher_MatchesWellKnownHash()
        => Assert.AreEqual(MicrosoftStorePublisherId, AppxManifestInfo.ComputePublisherId(MicrosoftStorePublisher));

    [TestMethod]
    public void ComputePublisherId_IsThirteenCharacters()
        => Assert.HasCount(13, AppxManifestInfo.ComputePublisherId("CN=Contoso"));

    [TestMethod]
    public void ComputePublisherId_IsStableForSameInput()
        => Assert.AreEqual(
            AppxManifestInfo.ComputePublisherId("CN=Contoso"),
            AppxManifestInfo.ComputePublisherId("CN=Contoso"));

    [TestMethod]
    public void ComputePublisherId_IsCaseSensitive()
        => Assert.AreNotEqual(
            AppxManifestInfo.ComputePublisherId("CN=Contoso"),
            AppxManifestInfo.ComputePublisherId("CN=contoso"));

    [TestMethod]
    public void ReadFromManifest_ComputesPackageFamilyNameAndAppUserModelId()
    {
        AppxManifestInfo info = ReadManifest(
            name: "Contoso.MyTestApp",
            publisher: MicrosoftStorePublisher,
            applicationId: "App");

        Assert.AreEqual("Contoso.MyTestApp", info.PackageName);
        Assert.AreEqual(MicrosoftStorePublisher, info.Publisher);
        Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}", info.PackageFamilyName);

        AppxApplicationInfo application = Assert.ContainsSingle(info.Applications);
        Assert.AreEqual("App", application.Id);
        Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!App", application.AppUserModelId);
    }

    [TestMethod]
    public void ReadFromManifest_WithoutApplication_LeavesApplicationsEmpty()
    {
        AppxManifestInfo info = ReadManifest(
            name: "Contoso.MyTestApp",
            publisher: MicrosoftStorePublisher,
            applicationId: null);

        Assert.IsEmpty(info.Applications);
        Assert.IsNull(info.ResolveApplication("MyTestApp.exe"));
        Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}", info.PackageFamilyName);
    }

    [TestMethod]
    public void ReadFromManifest_IgnoresSchemaNamespaceVersion()
    {
        // A different foundation-namespace revision must still parse (we match by local name).
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10/2020">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="App" />
              </Applications>
            </Package>
            """;

        AppxManifestInfo info = ReadManifest(ManifestXml);

        Assert.AreEqual("Contoso.MyTestApp", info.PackageName);
        Assert.AreEqual("App", Assert.ContainsSingle(info.Applications).Id);
    }

    [TestMethod]
    public void ReadFromManifest_ParsesAllApplicationsInManifestOrder()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" Executable="First.exe" />
                <Application Id="Second" Executable="Second.exe" />
              </Applications>
            </Package>
            """;

        AppxManifestInfo info = ReadManifest(ManifestXml);

        Assert.HasCount(2, info.Applications);
        Assert.AreEqual("First", info.Applications[0].Id);
        Assert.AreEqual("First.exe", info.Applications[0].Executable);
        Assert.AreEqual("Second", info.Applications[1].Id);
        Assert.AreEqual("Second.exe", info.Applications[1].Executable);
    }

    [TestMethod]
    public void ResolveApplication_WithSingleApplication_ReturnsItRegardlessOfExecutable()
    {
        AppxManifestInfo info = ReadManifest(
            name: "Contoso.MyTestApp",
            publisher: MicrosoftStorePublisher,
            applicationId: "App");

        // A single application is unambiguous, so it is returned even when the executable does not match.
        Assert.AreEqual("App", info.ResolveApplication("does-not-matter.exe")?.Id);
    }

    [TestMethod]
    public void ResolveApplication_WithMultipleApplications_SelectsTheOneMatchingTheExecutable()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" Executable="First.exe" />
                <Application Id="Second" Executable="Second.exe" />
              </Applications>
            </Package>
            """;

        AppxManifestInfo info = ReadManifest(ManifestXml);

        // The executable disambiguates: the second application is selected rather than defaulting to the first.
        Assert.AreEqual("Second", info.ResolveApplication("Second.exe")?.Id);
    }

    [TestMethod]
    public void ResolveApplication_WithMultipleApplicationsAndNoMatch_Throws()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" Executable="First.exe" />
                <Application Id="Second" Executable="Second.exe" />
              </Applications>
            </Package>
            """;

        AppxManifestInfo info = ReadManifest(ManifestXml);

        // An ambiguous request must be rejected instead of silently defaulting to the first application.
        Assert.ThrowsExactly<InvalidOperationException>(() => info.ResolveApplication("Unknown.exe"));
    }

    [TestMethod]
    public void ReadFromManifest_WithoutIdentity_Throws()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Applications>
                <Application Id="App" />
              </Applications>
            </Package>
            """;

        Assert.ThrowsExactly<InvalidOperationException>(() => ReadManifest(ManifestXml));
    }

    [TestMethod]
    public void ReadFromManifest_WithoutPublisher_Throws()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Version="1.0.0.0" />
            </Package>
            """;

        Assert.ThrowsExactly<InvalidOperationException>(() => ReadManifest(ManifestXml));
    }

    [TestMethod]
    public void GetManifestPath_WithoutManifest_ReturnsNull()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.IsNull(AppxManifestInfo.GetManifestPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GetManifestPath_WithManifest_ReturnsPathWithoutParsing()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifestPath = Path.Combine(directory, AppxManifestInfo.AppxManifestFileName);

            // Intentionally not a valid manifest: GetManifestPath must be a pure existence probe that
            // never parses, so an unparsable file must still be reported.
            File.WriteAllText(manifestPath, "not xml");

            Assert.AreEqual(manifestPath, AppxManifestInfo.GetManifestPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void FindManifestPath_WhenManifestIsInAnAncestorDirectory_ReturnsTheNearestManifest()
    {
        // Model a valid MSIX layout where the manifest sits at the package root but the executable
        // lives in a subdirectory (Application/@Executable = "bin\host.exe").
        string root = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"));
        string executableDirectory = Path.Combine(root, "bin");
        Directory.CreateDirectory(executableDirectory);
        try
        {
            string manifestPath = Path.Combine(root, AppxManifestInfo.AppxManifestFileName);
            File.WriteAllText(manifestPath, "not xml");

            // GetManifestPath only probes the executable's own directory and must miss the ancestor
            // manifest, whereas FindManifestPath walks up and locates it.
            Assert.IsNull(AppxManifestInfo.GetManifestPath(executableDirectory));
            Assert.AreEqual(manifestPath, AppxManifestInfo.FindManifestPath(executableDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FindManifestPath_WithoutAnyManifest_ReturnsNull()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"), "bin");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.IsNull(AppxManifestInfo.FindManifestPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveApplication_MatchesExecutableDeclaredWithASubdirectoryPath()
    {
        // A valid MSIX manifest can declare Executable with a package-relative subdirectory path; the
        // platform still asks to launch the bare executable file name, so resolution must match on it.
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" Executable="bin\First.exe" />
                <Application Id="Second" Executable="bin\Second.exe" />
              </Applications>
            </Package>
            """;

        AppxManifestInfo info = ReadManifest(ManifestXml);

        Assert.AreEqual("Second", info.ResolveApplication("Second.exe")?.Id);
    }

    private static AppxManifestInfo ReadManifest(string name, string publisher, string? applicationId)
        => ReadManifest(BuildManifestXml(name, publisher, applicationId));

    private static AppxManifestInfo ReadManifest(string manifestXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(manifestXml));
        return AppxManifestInfo.ReadFromManifest(stream);
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

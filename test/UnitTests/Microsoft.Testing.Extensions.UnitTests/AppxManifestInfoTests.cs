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
        Assert.AreEqual("App", info.ApplicationId);
        Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}", info.PackageFamilyName);
        Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!App", info.AppUserModelId);
    }

    [TestMethod]
    public void ReadFromManifest_WithoutApplication_LeavesAppUserModelIdNull()
    {
        AppxManifestInfo info = ReadManifest(
            name: "Contoso.MyTestApp",
            publisher: MicrosoftStorePublisher,
            applicationId: null);

        Assert.IsNull(info.ApplicationId);
        Assert.IsNull(info.AppUserModelId);
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
        Assert.AreEqual("App", info.ApplicationId);
    }

    [TestMethod]
    public void ReadFromManifest_UsesFirstApplicationId()
    {
        const string ManifestXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
              <Applications>
                <Application Id="First" />
                <Application Id="Second" />
              </Applications>
            </Package>
            """;

        Assert.AreEqual("First", ReadManifest(ManifestXml).ApplicationId);
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
    public void TryReadFromLayout_WithoutManifest_ReturnsFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.IsFalse(AppxManifestInfo.TryReadFromLayout(directory, out AppxManifestInfo? info));
            Assert.IsNull(info);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TryReadFromLayout_WithManifest_ReturnsParsedInfo()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AppxManifestInfoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, AppxManifestInfo.AppxManifestFileName),
                BuildManifestXml("Contoso.MyTestApp", MicrosoftStorePublisher, "App"));

            Assert.IsTrue(AppxManifestInfo.TryReadFromLayout(directory, out AppxManifestInfo? info));
            Assert.IsNotNull(info);
            Assert.AreEqual($"Contoso.MyTestApp_{MicrosoftStorePublisherId}!App", info.AppUserModelId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Covers the violation branches of <see cref="PackageMetadataInspector"/> that the produced packages never
/// reach, using synthetic nuspec documents.
/// </summary>
/// <remarks>
/// <see cref="PackageMetadataCompletenessTests"/> only proves that today's packages are clean, so on its own it
/// cannot tell a working detector from one that never reports anything. These tests pin the detection itself,
/// including a correct multi-paragraph description that must NOT be reported so the indentation check cannot
/// drift into false positives.
/// </remarks>
[TestClass]
public sealed class PackageMetadataInspectorTests
{
    private const string PackageFileName = "PackageMetadataInspectorTests.1.0.0.nupkg";
    private const string RealDescription = "Package metadata inspector test package. Microsoft Testing Platform is a lightweight and portable test runner for .NET.";
    private static readonly IReadOnlySet<string> CompleteArchiveEntries = new HashSet<string>(StringComparer.Ordinal)
    {
        "PACKAGE.md",
        "icon.png",
    };

    [TestMethod]
    public void InspectPackage_WithIndentedDescriptionLine_ReturnsViolation()
    {
        XDocument nuspec = LoadNuspecWithDescription($"First line{Environment.NewLine}  indented continuation");

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, CompleteArchiveEntries);

        Assert.HasCount(1, violations);
        Assert.Contains("description contains lines indented", violations[0]);
    }

    [TestMethod]
    public void InspectPackage_WithPlaceholderDescription_ReturnsViolation()
    {
        XDocument nuspec = CreateNuspec(description: PackageMetadataInspector.NuGetPlaceholderDescription);

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, CompleteArchiveEntries);

        Assert.HasCount(1, violations);
        Assert.Contains("ships NuGet's 'Package Description' placeholder", violations[0]);
    }

    [TestMethod]
    public void InspectPackage_WithWhitespaceDescription_ReturnsViolation()
    {
        XDocument nuspec = CreateNuspec(description: "   ");

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, CompleteArchiveEntries);

        Assert.HasCount(1, violations);
        Assert.Contains("has no <description>", violations[0]);
    }

    [TestMethod]
    public void InspectPackage_WithUnindentedMultiParagraphDescription_ReturnsNoViolation()
    {
        const string Description = """
MSTest.TestFramework exposes the MSTest attributes, assertions and TestContext APIs used by test projects.

Supported platforms:
- .NET 8.0+
- .NET Framework 4.6.2+

MSTest is a fully supported, open source and cross-platform test framework for .NET.
""";
        XDocument nuspec = CreateNuspec(description: Description);

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, CompleteArchiveEntries);

        Assert.IsEmpty(violations);
    }

    [TestMethod]
    [DataRow("readme")]
    [DataRow("icon")]
    public void InspectPackage_WithMissingMetadataElement_ReturnsViolation(string metadataName)
    {
        XDocument nuspec = CreateNuspec(
            readme: metadataName == "readme" ? null : "PACKAGE.md",
            icon: metadataName == "icon" ? null : "icon.png");

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, CompleteArchiveEntries);

        Assert.HasCount(1, violations);
        Assert.Contains($"has no <{metadataName}>", violations[0]);
    }

    [TestMethod]
    [DataRow("readme")]
    [DataRow("icon")]
    public void InspectPackage_WithDeclaredMetadataFileMissingFromArchive_ReturnsViolation(string metadataName)
    {
        XDocument nuspec = CreateNuspec();
        IReadOnlySet<string> archiveEntries = new HashSet<string>(
            metadataName == "readme" ? ["icon.png"] : ["PACKAGE.md"],
            StringComparer.Ordinal);

        IReadOnlyList<string> violations = PackageMetadataInspector.InspectPackage(PackageFileName, nuspec, archiveEntries);

        Assert.HasCount(1, violations);
        Assert.Contains($"declares <{metadataName}>", violations[0]);
        Assert.Contains("but does not contain that file", violations[0]);
    }

    private static XDocument CreateNuspec(string? description = RealDescription, string? readme = "PACKAGE.md", string? icon = "icon.png")
    {
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        XElement metadata = new(ns + "metadata");
        AddElementIfNotNull(metadata, ns + "id", "PackageMetadataInspectorTests");
        AddElementIfNotNull(metadata, ns + "version", "1.0.0");
        AddElementIfNotNull(metadata, ns + "description", description);
        AddElementIfNotNull(metadata, ns + "readme", readme);
        AddElementIfNotNull(metadata, ns + "icon", icon);

        return new XDocument(new XElement(ns + "package", metadata));
    }

    private static XDocument LoadNuspecWithDescription(string description)
    {
        string nuspec = $"""
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>PackageMetadataInspectorTests</id>
    <version>1.0.0</version>
    <description>{description}</description>
    <readme>PACKAGE.md</readme>
    <icon>icon.png</icon>
  </metadata>
</package>
""";
        using StringReader reader = new(nuspec);
        return XDocument.Load(reader);
    }

    private static void AddElementIfNotNull(XElement parent, XName name, string? value)
    {
        if (value is not null)
        {
            parent.Add(new XElement(name, value));
        }
    }
}

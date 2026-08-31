// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.DynamicExtensions;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DynamicExtensionManifestParserTests
{
    // Deliberately digit-free: assertions below check that an entry *index* appears in error messages, which
    // would be satisfied by accident if the path itself contained digits (a real temp path usually does).
    private static readonly string ManifestPath =
        Path.Combine($"{Path.DirectorySeparatorChar}manifests", "contoso.testingplatformextensions.json");

    [TestMethod]
    public void Parse_WithFullyPopulatedEntry_ReadsEveryProperty()
    {
        const string Content = """
            {
              "extensions": [
                {
                  "id": "8E680F4D-E423-415A-9566-855439363BC0",
                  "displayName": "Contoso reporting",
                  "assemblyPath": "extensions/Contoso.dll",
                  "typeFullName": "Contoso.TestingPlatformBuilderHook",
                  "enabled": false
                }
              ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.AreEqual(ManifestPath, manifest.ManifestPath);
        Assert.HasCount(1, manifest.Extensions);

        DynamicExtensionEntry entry = manifest.Extensions[0];
        Assert.AreEqual("8E680F4D-E423-415A-9566-855439363BC0", entry.Id);
        Assert.AreEqual("Contoso reporting", entry.DisplayName);
        Assert.AreEqual("extensions/Contoso.dll", entry.AssemblyPath);
        Assert.AreEqual("Contoso.TestingPlatformBuilderHook", entry.TypeFullName);
        Assert.AreEqual(0, entry.Index);
        Assert.AreEqual(ManifestPath, entry.ManifestPath);
        Assert.IsFalse(entry.IsEnabled);
    }

    [TestMethod]
    public void Parse_WithOnlyRequiredProperties_AppliesDocumentedDefaults()
    {
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "Contoso.dll", "typeFullName": "Contoso.Hook" }
              ]
            }
            """;

        DynamicExtensionEntry entry = DynamicExtensionManifestParser.Parse(ManifestPath, Content).Extensions[0];

        Assert.IsTrue(entry.IsEnabled, "'enabled' must default to true.");
        Assert.AreEqual("Contoso.Hook", entry.DisplayName, "'displayName' must default to the type full name.");
        Assert.AreEqual($"{entry.ResolvedAssemblyPath}|Contoso.Hook", entry.Id, "'id' must default to the resolved path and type name.");
    }

    [TestMethod]
    public void Parse_WithRelativeAssemblyPath_ResolvesAgainstManifestDirectory()
    {
        const string Content = """
            { "extensions": [ { "assemblyPath": "nested/Contoso.dll", "typeFullName": "Contoso.Hook" } ] }
            """;

        DynamicExtensionEntry entry = DynamicExtensionManifestParser.Parse(ManifestPath, Content).Extensions[0];

        string expected = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ManifestPath)!, "nested/Contoso.dll"));
        Assert.AreEqual(expected, entry.ResolvedAssemblyPath);
        Assert.AreEqual("nested/Contoso.dll", entry.AssemblyPath, "The path as authored must be preserved for diagnostics.");
    }

    [TestMethod]
    public void Parse_WithAbsoluteAssemblyPath_KeepsItAbsolute()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "elsewhere", "Contoso.dll");
        string content = $$"""
            { "extensions": [ { "assemblyPath": {{ToJsonString(absolute)}}, "typeFullName": "Contoso.Hook" } ] }
            """;

        DynamicExtensionEntry entry = DynamicExtensionManifestParser.Parse(ManifestPath, content).Extensions[0];

        Assert.AreEqual(Path.GetFullPath(absolute), entry.ResolvedAssemblyPath);
    }

    [TestMethod]
    public void Parse_WithEmptyExtensionsArray_ReturnsNoEntries()
    {
        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, """{ "extensions": [] }""");

        Assert.IsEmpty(manifest.Extensions);
        Assert.IsEmpty(manifest.UnknownProperties);
    }

    [TestMethod]
    public void Parse_PreservesDeclarationOrder()
    {
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "A.dll", "typeFullName": "A.Hook" },
                { "assemblyPath": "B.dll", "typeFullName": "B.Hook" },
                { "assemblyPath": "C.dll", "typeFullName": "C.Hook" }
              ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.AreSequenceEqual(
            ["A.Hook", "B.Hook", "C.Hook"],
            manifest.Extensions.Select(e => e.TypeFullName).ToArray());
        Assert.AreSequenceEqual([0, 1, 2], manifest.Extensions.Select(e => e.Index).ToArray());
    }

    [TestMethod]
    public void Parse_WithUnknownRootProperty_IgnoresItButReportsIt()
    {
        const string Content = """
            {
              "$schema": "https://example.com/schema.json",
              "extensions": [ { "assemblyPath": "Contoso.dll", "typeFullName": "Contoso.Hook" } ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.HasCount(1, manifest.Extensions);
        Assert.AreSequenceEqual(["$schema"], manifest.UnknownProperties.ToArray());
    }

    [TestMethod]
    public void Parse_WithUnknownEntryProperty_DoesNotFailButReportsIt()
    {
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "Contoso.dll", "typeFullName": "Contoso.Hook", "futureOption": 42 }
              ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.HasCount(1, manifest.Extensions);
        Assert.AreSequenceEqual(["extensions[0].futureOption"], manifest.UnknownProperties.ToArray());
    }

#if NETCOREAPP
    [TestMethod]
    public void Parse_WithDuplicateExtensionsProperty_Throws()
    {
        // Last-wins would drop policyA entirely, which is the silent degradation this feature exists to
        // prevent: the author deployed two blocks and would get one, with nothing said about it.
        const string Content = """
            {
              "extensions": [ { "assemblyPath": "A.dll", "typeFullName": "Contoso.A" } ],
              "extensions": [ { "assemblyPath": "B.dll", "typeFullName": "Contoso.B" } ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, Content));

        Assert.Contains(DynamicExtensionConstants.ExtensionsPropertyName, ex.Message);
    }

    [TestMethod]
    public void Parse_WithDuplicateEntryProperty_Throws()
    {
        // Same reasoning one level down: silently taking B.dll over A.dll would load an extension the author
        // did not think they were declaring.
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "A.dll", "assemblyPath": "B.dll", "typeFullName": "Contoso.Hook" }
              ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, Content));

        Assert.Contains(DynamicExtensionConstants.AssemblyPathPropertyName, ex.Message);
    }

    [TestMethod]
    public void Parse_WithDuplicateUnknownEntryProperty_DoesNotThrow()
    {
        // Only recognized properties are rejected. A repeated unknown property cannot change what the platform
        // loads, so failing on it would punish forward-compatible manifests for no benefit.
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "A.dll", "typeFullName": "Contoso.Hook", "futureOption": 1, "futureOption": 2 }
              ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.HasCount(1, manifest.Extensions);
    }
#endif

    [TestMethod]
    public void Parse_WithMisspelledEnabledProperty_ReportsItSoTheTypoIsDiscoverable()
    {
        // A silent typo here is dangerous: the author believes the extension is off, but it runs.
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "Contoso.dll", "typeFullName": "Contoso.Hook", "enabeld": false }
              ]
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.IsTrue(manifest.Extensions[0].IsEnabled);
        Assert.AreSequenceEqual(["extensions[0].enabeld"], manifest.UnknownProperties.ToArray());
    }

    [TestMethod]
    public void Parse_WithCommentsAndTrailingCommas_Succeeds()
    {
        const string Content = """
            {
              // Contoso policy extensions.
              "extensions": [
                { "assemblyPath": "Contoso.dll", "typeFullName": "Contoso.Hook", },
              ],
            }
            """;

        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, Content);

        Assert.HasCount(1, manifest.Extensions);
    }

    [TestMethod]
    public void Parse_WithTrailingContentAfterTheRootObject_Throws()
    {
        // The netstandard2.0 reader is built on Jsonite, which stops as soon as it has the root value and never
        // checks for end of input. Without an explicit check this would be accepted on .NET Framework and
        // rejected on .NET, so the parity is asserted here rather than assumed.
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, """{ "extensions": [] } garbage"""));

        Assert.Contains(ManifestPath, ex.Message);
    }

    [TestMethod]
    public void Parse_WithTrailingWhitespaceAfterTheRootObject_Succeeds()
    {
        DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(ManifestPath, "{ \"extensions\": [] }  \r\n\t ");

        Assert.IsEmpty(manifest.Extensions);
    }

    [TestMethod]
    public void Parse_WithMalformedJson_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, "{ this is not json"));

        Assert.Contains(ManifestPath, ex.Message);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("\"a string\"")]
    [DataRow("42")]
    [DataRow("null")]
    public void Parse_WithNonObjectRoot_Throws(string content)
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, content));

        Assert.Contains(ManifestPath, ex.Message);
    }

    [TestMethod]
    public void Parse_WithoutExtensionsProperty_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, """{ "somethingElse": true }"""));

        Assert.Contains("extensions", ex.Message);
    }

    [TestMethod]
    public void Parse_WithNonArrayExtensionsProperty_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, """{ "extensions": { "assemblyPath": "a.dll" } }"""));

        Assert.Contains("extensions", ex.Message);
    }

    [TestMethod]
    public void Parse_WithNonObjectEntry_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, """{ "extensions": [ "Contoso.dll" ] }"""));

        Assert.Contains(ManifestPath, ex.Message);
    }

    [TestMethod]
    [DataRow("""{ "extensions": [ { "typeFullName": "Contoso.Hook" } ] }""", "assemblyPath")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "Contoso.dll" } ] }""", "typeFullName")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "", "typeFullName": "Contoso.Hook" } ] }""", "assemblyPath")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "  ", "typeFullName": "Contoso.Hook" } ] }""", "assemblyPath")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "Contoso.dll", "typeFullName": " " } ] }""", "typeFullName")]
    public void Parse_WithMissingOrBlankRequiredProperty_Throws(string content, string expectedPropertyName)
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, content));

        Assert.Contains(expectedPropertyName, ex.Message);
    }

    [TestMethod]
    [DataRow("""{ "extensions": [ { "assemblyPath": 1, "typeFullName": "Contoso.Hook" } ] }""", "assemblyPath")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "a.dll", "typeFullName": true } ] }""", "typeFullName")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "a.dll", "typeFullName": "H", "id": 3 } ] }""", "id")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "a.dll", "typeFullName": "H", "displayName": [] } ] }""", "displayName")]
    [DataRow("""{ "extensions": [ { "assemblyPath": "a.dll", "typeFullName": "H", "enabled": "yes" } ] }""", "enabled")]
    public void Parse_WithWrongPropertyType_Throws(string content, string expectedPropertyName)
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, content));

        Assert.Contains(expectedPropertyName, ex.Message);
    }

    [TestMethod]
    public void Parse_ReportsTheIndexOfTheOffendingEntry()
    {
        const string Content = """
            {
              "extensions": [
                { "assemblyPath": "A.dll", "typeFullName": "A.Hook" },
                { "assemblyPath": "B.dll" }
              ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => DynamicExtensionManifestParser.Parse(ManifestPath, Content));

        // ManifestPath and the entry data are digit-free, so the only "1" that can appear is the entry index.
        Assert.Contains("1", ex.Message, "The message must point at the second entry.");
        Assert.DoesNotContain("0", ex.Message, "The message must not point at the first entry.");
        Assert.Contains("typeFullName", ex.Message);
    }

    private static string ToJsonString(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

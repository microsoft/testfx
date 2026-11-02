// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.Services;

[TestClass]
public sealed class ArtifactNamingHelperTests
{
    [TestMethod]
    public void ResolveTemplate_WithReplacements_ReplacesCorrectly()
    {
        string template = "{pname}_{pid}_{asm}.dmp";
        var replacements = new Dictionary<string, string>
        {
            ["pname"] = "test-process",
            ["pid"] = "12345",
            ["asm"] = "TestAssembly",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("test-process_12345_TestAssembly.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_LiteralTextAndPlaceholders_ReplacesOnlyPlaceholders()
    {
        string template = "{pname}_{pid}_custom.dmp";
        var replacements = new Dictionary<string, string>
        {
            ["pname"] = "custom-process",
            ["pid"] = "99999",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("custom-process_99999_custom.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_EmptyReplacementValue_ReplacesWithEmptyString()
    {
        string template = "{asm}_{pname}.dmp";
        var replacements = new Dictionary<string, string>
        {
            ["asm"] = string.Empty,
            ["pname"] = "test-process",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("_test-process.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithUnknownPlaceholder_KeepsPlaceholderAsIs()
    {
        string template = "{unknown-field}_{pname}";
        var replacements = new Dictionary<string, string>
        {
            ["pname"] = "test-process",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("{unknown-field}_test-process", result);
    }

    [TestMethod]
    public void ResolveTemplate_ComplexTemplate_ReplacesAllKnownPlaceholders()
    {
        string template = "{asm}/{pname}_{pid}_{tfm}_{time}.dmp";
        var replacements = new Dictionary<string, string>
        {
            ["asm"] = "TestAssembly",
            ["pname"] = "test-process",
            ["pid"] = "12345",
            ["tfm"] = "net9.0",
            ["time"] = "2025-09-22_13-49-34.0000000",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("TestAssembly/test-process_12345_net9.0_2025-09-22_13-49-34.0000000.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseSensitive_DoesNotMatchDifferentCase()
    {
        string template = "{PName}_{PID}";
        var replacements = new Dictionary<string, string>
        {
            ["pname"] = "test-process",
            ["pid"] = "12345",
        };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        // Case-sensitive: {PName} and {PID} don't match lowercase keys, so they are preserved as-is.
        Assert.AreEqual("{PName}_{PID}", result);
    }

    [TestMethod]
    public void ResolveTemplate_NullReplacements_ReturnsTemplateUnchanged()
    {
        string template = "{pname}_{pid}.dmp";

        string result = ArtifactNamingHelper.ResolveTemplate(template, null);

        Assert.AreEqual("{pname}_{pid}.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_EmptyReplacements_ReturnsTemplateUnchanged()
    {
        string template = "{pname}_{pid}.dmp";
        var replacements = new Dictionary<string, string>();

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("{pname}_{pid}.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_RepeatedPlaceholder_ReplacesAllOccurrences()
    {
        string template = "{pname}_{pname}.dmp";
        var replacements = new Dictionary<string, string> { ["pname"] = "test-process" };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("test-process_test-process.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_NoPlaceholders_ReturnsTemplateUnchanged()
    {
        string template = "simple.dmp";
        var replacements = new Dictionary<string, string> { ["pname"] = "test-process" };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("simple.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseInsensitiveDictionary_EnforcesOrdinalComparison()
    {
        var ciDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PNAME"] = "process",
        };

        // After ordinal normalization, key is stored as "PNAME".
        // {pname} looks up "pname" which does not match "PNAME" ordinally, so placeholder is preserved.
        string result = ArtifactNamingHelper.ResolveTemplate("{pname}", ciDict);

        Assert.AreEqual("{pname}", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseInsensitiveDictionaryWithMatchingKey_Resolves()
    {
        var ciDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pname"] = "process",
        };

        string result = ArtifactNamingHelper.ResolveTemplate("{pname}", ciDict);

        Assert.AreEqual("process", result);
    }

    [TestMethod]
    public void ResolveTemplate_EmptyPlaceholder_IsPreservedAsIs()
    {
        string template = "{}_{pname}.dmp";
        var replacements = new Dictionary<string, string> { ["pname"] = "myproc" };

        string result = ArtifactNamingHelper.ResolveTemplate(template, replacements);

        Assert.AreEqual("{}_myproc.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_NullTemplate_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => ArtifactNamingHelper.ResolveTemplate(null!));

    [TestMethod]
    public void ResolveTemplate_EmptyTemplate_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => ArtifactNamingHelper.ResolveTemplate(string.Empty));

    [TestMethod]
    public void ResolveTemplate_WhitespaceOnlyTemplate_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => ArtifactNamingHelper.ResolveTemplate("   "));

    [TestMethod]
    public void GetStandardReplacements_ReturnsExpectedKeys()
    {
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements("myproc", "42", new DateTimeOffset(2025, 9, 22, 13, 49, 34, TimeSpan.Zero));

        Assert.AreEqual("myproc", replacements["pname"]);
        Assert.AreEqual("42", replacements["pid"]);
        Assert.AreEqual("2025-09-22_13-49-34.0000000", replacements["time"]);

        // asm, tfm and arch are runtime-dependent, just verify they're present and non-empty.
        Assert.IsTrue(replacements.ContainsKey("asm"), "Expected 'asm' key in standard replacements");
        Assert.IsTrue(replacements.ContainsKey("tfm"), "Expected 'tfm' key in standard replacements");
        Assert.IsTrue(replacements.ContainsKey("arch"), "Expected 'arch' key in standard replacements");
        Assert.AreNotEqual(string.Empty, replacements["asm"], "Expected 'asm' to have a non-empty value");
        Assert.AreNotEqual(string.Empty, replacements["tfm"], "Expected 'tfm' to have a non-empty value");
        Assert.AreNotEqual(string.Empty, replacements["arch"], "Expected 'arch' to have a non-empty value");
    }

    [TestMethod]
    public void GetStandardReplacements_ArchKey_MatchesProcessArchitecture()
    {
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements("p", "1", DateTimeOffset.UtcNow);

        string expected = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        Assert.AreEqual(expected, replacements["arch"]);
    }
}

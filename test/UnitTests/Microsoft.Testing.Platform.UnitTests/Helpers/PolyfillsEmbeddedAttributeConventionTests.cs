// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PolyfillsEmbeddedAttributeConventionTests
{
    private static readonly Regex TypeDeclarationRegex = new(
        @"(?ms)(?<attributes>(?:\s*\[[^\]]+\]\s*)*)(?:internal|public|private|protected)\s+(?:sealed\s+|partial\s+|abstract\s+|static\s+|readonly\s+)*(?:class|struct|interface|enum)\s+(?<typeName>\w+Attribute)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmbeddedAttributeRegex = new(
        @"\[\s*(?:global::)?(?:Microsoft\.CodeAnalysis\.)?Embedded(?:Attribute)?(?:\s*\(|\s*\])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [TestMethod]
    public void PolyfillAttributeTypes_AreDecoratedWithEmbeddedAttribute()
    {
        string? repositoryRoot = FindRepositoryRoot();
        Assert.IsNotNull(repositoryRoot, "Unable to locate repository root from test execution directory.");

        string polyfillsDirectory = Path.Combine(repositoryRoot, "src", "Polyfills");

        foreach (string filePath in Directory.EnumerateFiles(polyfillsDirectory, "*.cs"))
        {
            if (Path.GetFileName(filePath).Equals("EmbeddedAttribute.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(filePath);
            MatchCollection typeDeclarations = TypeDeclarationRegex.Matches(source);

            if (typeDeclarations.Count == 0)
            {
                continue;
            }

            foreach (Match declaration in typeDeclarations)
            {
                string typeName = declaration.Groups["typeName"].Value;
                string attributes = declaration.Groups["attributes"].Value;

                Assert.IsTrue(
                    EmbeddedAttributeRegex.IsMatch(attributes),
                    $"Type '{typeName}' in '{filePath}' must be decorated with [Embedded].");
            }
        }
    }

    private static string? FindRepositoryRoot()
    {
        DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
        while (currentDirectory is not null &&
               !Directory.Exists(Path.Combine(currentDirectory.FullName, "src", "Polyfills")))
        {
            currentDirectory = currentDirectory.Parent;
        }

        return currentDirectory?.FullName;
    }
}

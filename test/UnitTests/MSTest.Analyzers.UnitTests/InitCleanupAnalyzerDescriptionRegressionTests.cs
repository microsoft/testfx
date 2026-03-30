// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

/// <summary>
/// Regression tests for:
/// - PR #3391 / Issue #3323: Analyzer descriptions for init/cleanup should document
///   [TestClass] and abstract requirements.
/// - PR #4224 / Issue #4209: Analyzers package should support both C# and VB.NET.
/// </summary>
[TestClass]
public sealed class InitCleanupAnalyzerDescriptionRegressionTests
{
    #region PR #3391 — Analyzer descriptions must document [TestClass] and type-level requirements

    [TestMethod]
    public void TestInitializeAnalyzer_DescriptionContainsTestClassRequirement()
    {
        DiagnosticDescriptor rule = TestInitializeShouldBeValidAnalyzer.Rule;
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: TestInitialize description should mention [TestClass] requirement for sealed classes.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: TestInitialize description should document type-level requirements.");
        StringAssert.Contains(description, "abstract",
            "PR #3391: TestInitialize description should mention 'abstract' constraint.");
    }

    [TestMethod]
    public void TestCleanupAnalyzer_DescriptionContainsTestClassRequirement()
    {
        DiagnosticDescriptor rule = TestCleanupShouldBeValidAnalyzer.Rule;
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: TestCleanup description should mention [TestClass] requirement for sealed classes.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: TestCleanup description should document type-level requirements.");
        StringAssert.Contains(description, "abstract",
            "PR #3391: TestCleanup description should mention 'abstract' constraint.");
    }

    [TestMethod]
    public void ClassInitializeAnalyzer_DescriptionContainsTestClassAndAbstractRequirements()
    {
        DiagnosticDescriptor rule = GetSingleRule<ClassInitializeShouldBeValidAnalyzer>();
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: ClassInitialize description should mention [TestClass] requirement for sealed classes.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: ClassInitialize description should document type-level requirements.");
        StringAssert.Contains(description, "abstract",
            "PR #3391: ClassInitialize description should mention 'abstract' class requirements for InheritanceBehavior.");
    }

    [TestMethod]
    public void ClassCleanupAnalyzer_DescriptionContainsTestClassAndAbstractRequirements()
    {
        DiagnosticDescriptor rule = GetSingleRule<ClassCleanupShouldBeValidAnalyzer>();
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: ClassCleanup description should mention [TestClass] requirement for sealed classes.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: ClassCleanup description should document type-level requirements.");
        StringAssert.Contains(description, "abstract",
            "PR #3391: ClassCleanup description should mention 'abstract' class requirements for InheritanceBehavior.");
    }

    [TestMethod]
    public void AssemblyInitializeAnalyzer_DescriptionContainsTestClassRequirement()
    {
        DiagnosticDescriptor rule = GetSingleRule<AssemblyInitializeShouldBeValidAnalyzer>();
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: AssemblyInitialize description should mention [TestClass] requirement.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: AssemblyInitialize description should document type-level requirements.");
    }

    [TestMethod]
    public void AssemblyCleanupAnalyzer_DescriptionContainsTestClassRequirement()
    {
        DiagnosticDescriptor rule = GetSingleRule<AssemblyCleanupShouldBeValidAnalyzer>();
        string description = rule.Description.ToString(CultureInfo.InvariantCulture);

        StringAssert.Contains(description, "[TestClass]",
            "PR #3391: AssemblyCleanup description should mention [TestClass] requirement.");
        StringAssert.Contains(description, "The type declaring these methods should also respect the following rules",
            "PR #3391: AssemblyCleanup description should document type-level requirements.");
    }

    #endregion

    #region PR #4224 — Analyzers should support both C# and VB.NET

    [TestMethod]
    public void TestInitializeAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<TestInitializeShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void TestCleanupAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<TestCleanupShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void ClassInitializeAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<ClassInitializeShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void ClassCleanupAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<ClassCleanupShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void AssemblyInitializeAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<AssemblyInitializeShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void AssemblyCleanupAnalyzer_SupportsBothCSharpAndVisualBasic()
        => AssertAnalyzerSupportsLanguages<AssemblyCleanupShouldBeValidAnalyzer>(LanguageNames.CSharp, LanguageNames.VisualBasic);

    [TestMethod]
    public void AnalyzerPackage_CsprojIncludesVbAnalyzerPath()
    {
        // PR #4224: The MSTest.Analyzers.Package.csproj must include both
        // analyzers/dotnet/cs and analyzers/dotnet/vb paths so VB.NET projects
        // can load the analyzers.
        string repoRoot = FindRepoRoot();
        string csprojPath = Path.Combine(repoRoot, "src", "Analyzers",
            "MSTest.Analyzers.Package", "MSTest.Analyzers.Package.csproj");

        Assert.IsTrue(File.Exists(csprojPath), $"Package csproj not found at: {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();
        IEnumerable<XElement> packageFiles = doc.Descendants(ns + "TfmSpecificPackageFile");

        bool hasCsAnalyzer = packageFiles.Any(e =>
            e.Attribute("PackagePath")?.Value.Contains("analyzers/dotnet/cs") == true &&
            e.Attribute("Include")?.Value.Contains("MSTest.Analyzers.dll") == true);

        bool hasVbAnalyzer = packageFiles.Any(e =>
            e.Attribute("PackagePath")?.Value.Contains("analyzers/dotnet/vb") == true &&
            e.Attribute("Include")?.Value.Contains("MSTest.Analyzers.dll") == true);

        Assert.IsTrue(hasCsAnalyzer, "Package should include MSTest.Analyzers.dll for C# (analyzers/dotnet/cs).");
        Assert.IsTrue(
            hasVbAnalyzer,
            "PR #4224: Package should include MSTest.Analyzers.dll for VB.NET (analyzers/dotnet/vb).");
    }

    #endregion

    #region Helpers

    private static DiagnosticDescriptor GetSingleRule<TAnalyzer>()
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var analyzer = new TAnalyzer();
        Assert.AreEqual(1, analyzer.SupportedDiagnostics.Length,
            $"Expected exactly one rule on {typeof(TAnalyzer).Name}.");
        return analyzer.SupportedDiagnostics[0];
    }

    private static void AssertAnalyzerSupportsLanguages<TAnalyzer>(params string[] expectedLanguages)
        where TAnalyzer : DiagnosticAnalyzer
    {
        DiagnosticAnalyzerAttribute? attr = typeof(TAnalyzer)
            .GetCustomAttribute<DiagnosticAnalyzerAttribute>();

        Assert.IsNotNull(
            attr,
            $"{typeof(TAnalyzer).Name} should have a [DiagnosticAnalyzer] attribute.");

        foreach (string lang in expectedLanguages)
        {
            CollectionAssert.Contains(attr.Languages.ToList(), lang,
                $"PR #4224: {typeof(TAnalyzer).Name} should support {lang}.");
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, "testfx.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: walk up from current directory
        dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, "testfx.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate repository root.");
        return string.Empty; // unreachable
    }

    #endregion
}

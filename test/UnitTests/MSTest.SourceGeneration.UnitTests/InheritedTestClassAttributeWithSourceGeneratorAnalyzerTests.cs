// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Analyzers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class InheritedTestClassAttributeWithSourceGeneratorAnalyzerTests
{
    private const string MSTestStub = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
            public class TestClassAttribute : System.Attribute {}
        }
        """;

    [TestMethod]
    public async Task NoDiagnostic_WhenTestClassIsAppliedDirectly()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class MyTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenNoTestClassAttributeAtAll()
    {
        const string source = """
            namespace Sample
            {
                public class BaseTests {}
                public class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Diagnostic_WhenTestClassIsInheritedFromDirectBase()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                public class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle();
        Diagnostic diagnostic = diagnostics[0];
        diagnostic.Id.Should().Be(InheritedTestClassAttributeWithSourceGeneratorAnalyzer.DiagnosticId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Contain("DerivedTests").And.Contain("BaseTests");
    }

    [TestMethod]
    public async Task Diagnostic_WhenTestClassIsInheritedFromGrandparent()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class GrandparentTests {}

                public class ParentTests : GrandparentTests {}

                public class DerivedTests : ParentTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        // Both ParentTests and DerivedTests are missing the direct attribute.
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).Should().AllSatisfy(m => m.Should().Contain("GrandparentTests"));
    }

    [TestMethod]
    public async Task NoDiagnostic_OnAbstractClass()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public abstract class BaseTests {}

                public abstract class AbstractDerived : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NoDiagnostic_OnStaticClass()
    {
        // A static class cannot derive from a class (CS0713). Roslyn keeps the declared base type's name
        // but exposes none of its attributes, so the analyzer must stay silent on such erroneous code.
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                public static class StaticDerived : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenDerivedClassAlsoAppliesTestClassDirectly()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                [TestClass]
                public class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NoDiagnostic_OnGeneratedCode()
    {
        const string baseSource = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}
            }
            """;

        const string generatedSource = """
            // <auto-generated />
            namespace Sample
            {
                public class GeneratedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(baseSource, generatedSource);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Diagnostic_IsReportedAtTheDeclarationOfTheDerivedClass()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                public class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Location location = diagnostics.Should().ContainSingle().Which.Location;
        location.IsInSource.Should().BeTrue();
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("DerivedTests");
    }

    [TestMethod]
    public async Task Diagnostic_IsReportedOnceForPartialClass_AtItsFirstDeclaration()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                public partial class DerivedTests : BaseTests {}

                public partial class DerivedTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Location location = diagnostics.Should().ContainSingle().Which.Location;
        location.SourceSpan.Start.Should().Be(source.IndexOf("DerivedTests", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SupportedDiagnostics_ExposesTheMSTEST0069Rule()
    {
        var analyzer = new InheritedTestClassAttributeWithSourceGeneratorAnalyzer();

        DiagnosticDescriptor rule = analyzer.SupportedDiagnostics.Should().ContainSingle().Which;

        rule.Id.Should().Be(InheritedTestClassAttributeWithSourceGeneratorAnalyzer.DiagnosticId).And.Be("MSTEST0069");
        rule.Title.ToString(CultureInfo.InvariantCulture).Should().Be("Inherited [TestClass] is ignored by the MSTest source generator");
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be(
            "Class '{0}' inherits the [TestClass] attribute from base class '{1}'. The MSTest source generator only discovers types that declare [TestClass] directly; apply [TestClass] to '{0}' so it is discovered when source-generated discovery is active.");
        rule.Category.Should().Be("Usage");
        rule.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        rule.IsEnabledByDefault.Should().BeTrue();
        rule.Description.ToString(CultureInfo.InvariantCulture).Should().Be(
            "The MSTest source generator uses ForAttributeWithMetadataName to enumerate test classes, which does not follow inheritance. A class that relies on inheriting [TestClass] from a base type will be silently skipped from discovery in source-generation mode. Apply [TestClass] directly to the derived class to keep it discoverable.");
        rule.HelpLinkUri.Should().Be("https://aka.ms/mstest/analyzers");
    }

    [TestMethod]
    public async Task Analyzer_SupportsConcurrentExecution()
    {
        var analyzer = new InheritedTestClassAttributeWithSourceGeneratorAnalyzer();
        CompilationWithAnalyzers withAnalyzers = CreateCompilation("namespace Sample { public class Tests {} }", includeMSTestStub: true).WithAnalyzers([analyzer]);
        await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        AnalyzerTelemetryInfo telemetry = await withAnalyzers.GetAnalyzerTelemetryInfoAsync(analyzer, CancellationToken.None);

        telemetry.Concurrent.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true, 1)]
    [DataRow(false, 0)]
    public async Task Analyzer_RegistersSymbolAnalysis_OnlyWhenTestClassAttributeIsAvailable(bool includeMSTestStub, int expectedSymbolActions)
    {
        var analyzer = new InheritedTestClassAttributeWithSourceGeneratorAnalyzer();
        CompilationWithAnalyzers withAnalyzers = CreateCompilation("namespace Sample { public class Tests {} }", includeMSTestStub).WithAnalyzers([analyzer]);
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        AnalyzerTelemetryInfo telemetry = await withAnalyzers.GetAnalyzerTelemetryInfoAsync(analyzer, CancellationToken.None);

        diagnostics.Should().BeEmpty();
        telemetry.SymbolActionsCount.Should().Be(expectedSymbolActions);
    }

    [TestMethod]
    public async Task Diagnostic_WhenSubclassOfTestClassAttributeIsInherited()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                public sealed class MyTestClassAttribute : TestClassAttribute {}

                [MyTestClass]
                public class BaseTests {}

                public class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("DerivedTests").And.Contain("BaseTests");
    }

    [TestMethod]
    public async Task NoDiagnostic_OnOpenGenericClass()
    {
        // The source generator skips open generic types, so warning on them would be misleading
        // — applying [TestClass] directly still wouldn't make them discoverable.
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                public class DerivedTests<T> : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NoDiagnostic_OnFileLocalClass()
    {
        // The source generator skips file-local types, so warning on them would be misleading.
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                file class DerivedTests : BaseTests {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    // Returns every diagnostic the analyzer produced, including AD0001 analyzer-exception diagnostics.
    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(params string[] sources)
    {
        var analyzer = new InheritedTestClassAttributeWithSourceGeneratorAnalyzer();
        CompilationWithAnalyzers withAnalyzers = CreateCompilation(sources, includeMSTestStub: true).WithAnalyzers([analyzer]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(string source, bool includeMSTestStub)
        => CreateCompilation([source], includeMSTestStub);

    private static CSharpCompilation CreateCompilation(string[] sources, bool includeMSTestStub)
    {
        IEnumerable<string> allSources = includeMSTestStub ? sources.Prepend(MSTestStub) : sources;

        MetadataReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        ];

        return CSharpCompilation.Create(
            "TestSample",
            allSources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}

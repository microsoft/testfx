// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Sample
            {
                [TestClass]
                public class BaseTests {}

                // Static class can't actually derive from a normal class, but exercise the
                // static filter via a self-attributed static type to ensure we don't flag it.
                public static class StaticHelper {}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
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

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        SyntaxTree[] trees =
        [
            CSharpSyntaxTree.ParseText(MSTestStub),
            CSharpSyntaxTree.ParseText(source),
        ];

        MetadataReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        ];

        var compilation = CSharpCompilation.Create(
            "TestSample",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new InheritedTestClassAttributeWithSourceGeneratorAnalyzer();
        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer]);
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id == InheritedTestClassAttributeWithSourceGeneratorAnalyzer.DiagnosticId)
            .ToImmutableArray();
    }
}

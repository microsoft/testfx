// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UnitTests.PublicTestClassSuppressorTests.WarnForPublicTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class PublicTestClassSuppressorTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PublicTestClass_DiagnosticIsSuppressed(bool discoverInternals)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{(discoverInternals ? "[assembly: DiscoverInternals]" : string.Empty)}}

            [TestClass]
            public class {|#0:Tests|}
            {
                [TestMethod]
                public void Test() { }
            }

            public class {|#1:Helper|} { }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(false),
                VerifyCS.Diagnostic().WithLocation(1).WithIsSuppressed(false),
            },
        }.RunAsync();

        await new TestWithSuppressor
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(true),
                VerifyCS.Diagnostic().WithLocation(1).WithIsSuppressed(false),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task DerivedTestClassAttribute_DiagnosticIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Custom = CustomTestClassAttribute;

            internal class CustomTestClassAttribute : TestClassAttribute { }

            [Custom]
            public class {|#0:Tests|} { }
            """;

        await new TestWithSuppressor
        {
            TestCode = code,
            ExpectedDiagnostics = { VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(true) },
        }.RunAsync();
    }

    [TestMethod]
    public async Task PartialTestClass_AttributeOnAnotherDeclaration_DiagnosticIsSuppressed()
        => await new TestWithSuppressor
        {
            TestState =
            {
                Sources =
                {
                    "public partial class {|#0:Tests|} { }",
                    "[Microsoft.VisualStudio.TestTools.UnitTesting.TestClass] public partial class Tests { }",
                },
            },
            ExpectedDiagnostics = { VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(true) },
        }.RunAsync();

    [TestMethod]
    public async Task NestedTypes_OnlyAnnotatedTestClassIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:Tests|}
            {
                [TestClass]
                public class {|#1:NestedTests|} { }

                public class {|#2:Helper|} { }
            }
            """;

        await new TestWithSuppressor
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(true),
                VerifyCS.Diagnostic().WithLocation(1).WithIsSuppressed(true),
                VerifyCS.Diagnostic().WithLocation(2).WithIsSuppressed(false),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task UnrelatedTypes_DiagnosticsAreNotSuppressed()
    {
        string code = """
            using System;

            internal class TestClassAttribute : Attribute { }

            [TestClass]
            public class {|#0:NotAnMSTestClass|} { }

            public class {|#1:Helper|} { }

            public class {|#2:Derived|} : Helper { }
            """;

        await new TestWithSuppressor
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(false),
                VerifyCS.Diagnostic().WithLocation(1).WithIsSuppressed(false),
                VerifyCS.Diagnostic().WithLocation(2).WithIsSuppressed(false),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task MissingMSTestReference_DiagnosticIsNotSuppressed()
    {
        var test = new TestWithSuppressor
        {
            TestCode = "public class {|#0:Helper|} { }",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            ExpectedDiagnostics = { VerifyCS.Diagnostic().WithLocation(0).WithIsSuppressed(false) },
        };
        test.TestState.AdditionalReferences.Clear();
        await test.RunAsync();
    }

    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1041:Compiler extensions should be implemented in assemblies targeting netstandard2.0", Justification = "For suppression test only.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class WarnForPublicTypes : DiagnosticAnalyzer
    {
        [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1029:Do not use reserved diagnostic IDs", Justification = "Emulates CA1515 for suppression tests.")]
        [SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking", Justification = "For suppression test only.")]
        public static readonly DiagnosticDescriptor Rule = new("CA1515", "Title", "Message", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(
                symbolContext =>
                {
                    if (symbolContext.Symbol is INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public } type)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0]));
                    }
                },
                SymbolKind.NamedType);
        }
    }

    private sealed class TestWithSuppressor : VerifyCS.Test
    {
        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            foreach (DiagnosticAnalyzer analyzer in base.GetDiagnosticAnalyzers())
            {
                yield return analyzer;
            }

            yield return new PublicTestClassSuppressor();
        }
    }
}

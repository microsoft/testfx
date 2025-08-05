// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UnitTests.UseAsyncSuffixTestFixtureMethodSuppressorTests.WarnForMissingAsyncSuffix,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class UseAsyncSuffixTestFixtureMethodSuppressorTests
{
    [TestMethod]
    public async Task AsyncTestFixtureMethodsWithoutSuffix_DiagnosticIsSuppressed()
    {
        string code = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    [AssemblyInitialize]
    public static async Task {|#0:AssemblyInitialize|}() { }

    [AssemblyCleanup]
    public static async Task {|#1:AssemblyCleanup|}() { }

    [ClassInitialize]
    public static async Task {|#2:ClassInitialize|}() { }

    [ClassCleanup]
    public static async Task {|#3:ClassCleanup|}() { }

    [TestInitialize]
    public async Task {|#4:TestInitialize|}() { }

    [TestCleanup]
    public async Task {|#5:TestCleanup|}() { }
}
";

        // Verify issues Are reported
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(0)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(1)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(2)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(3)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(4)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(5)
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(1)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(2)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(3)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(4)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(5)
                    .WithIsSuppressed(true),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task GlobalTestMethodsWithoutAsyncSuffix_DiagnosticIsSuppressed()
    {
        string code = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public static class SomeClass
{
    [GlobalTestInitialize]
    public static async Task {|#0:GlobalTestInitialize|}(TestContext context) { }

    [GlobalTestCleanup]
    public static async Task {|#1:GlobalTestCleanup|}(TestContext context) { }
}
";

        // Verify issues Are reported
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(0)
                    .WithIsSuppressed(false),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(1)
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                VerifyCS.Diagnostic(WarnForMissingAsyncSuffix.Rule)
                    .WithLocation(1)
                    .WithIsSuppressed(true),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task AsyncTestMethodWithSuffix_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SomeClass
            {
                [AssemblyInitialize]
                public static async Task AssemblyInitializeAsync() { }

                [AssemblyCleanup]
                public static async Task AssemblyCleanupAsync() { }

                [ClassInitialize]
                public static async Task ClassInitializeAsync() { }

                [ClassCleanup]
                public static async Task ClassCleanupAsync() { }

                [TestInitialize]
                public async Task TestInitializeAsync() { }

                [TestCleanup]
                public async Task TestCleanupAsync() { }

                [GlobalTestInitialize]
                public static async Task GlobalTestInitializeAsync(TestContext context) { }

                [GlobalTestCleanup]
                public static async Task GlobalTestCleanupAsync(TestContext context) { }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
        }.RunAsync();
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1041:Compiler extensions should be implemented in assemblies targeting netstandard2.0", Justification = "For suppression test only.")]
    public class WarnForMissingAsyncSuffix : DiagnosticAnalyzer
    {
        [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1017:DiagnosticId for analyzers must be a non-null constant.", Justification = "For suppression test only.")]
        public static readonly DiagnosticDescriptor Rule = new(UseAsyncSuffixTestFixtureMethodSuppressor.Rule.SuppressedDiagnosticId, "Title", "Message", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (method.Name.EndsWith("Async", StringComparison.Ordinal))
            {
                return;
            }

            if (method.ReturnType.MetadataName != "Task")
            {
                // Not asynchronous (incomplete checking is sufficient for this test)
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations[0]));
        }
    }

    internal sealed class TestWithSuppressor : VerifyCS.Test
    {
        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            foreach (DiagnosticAnalyzer analyzer in base.GetDiagnosticAnalyzers())
            {
                yield return analyzer;
            }

            yield return new UseAsyncSuffixTestFixtureMethodSuppressor();
        }
    }
}

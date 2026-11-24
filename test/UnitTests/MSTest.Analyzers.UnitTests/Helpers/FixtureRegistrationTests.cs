// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class FixtureRegistrationTests
{
    [TestMethod]
    [DataRow(false, true, true, true, false, DisplayName = "MissingFixtureAttribute")]
    [DataRow(true, false, true, false, false, DisplayName = "MissingTestClass")]
    [DataRow(true, true, false, true, false, DisplayName = "RequiredContextMissing")]
    [DataRow(true, true, false, false, true, DisplayName = "OptionalContextMissing")]
    [DataRow(true, true, true, true, true, DisplayName = "CompleteContract")]
    public async Task NonGenericRegistration_RequiresOnlyTheRequestedSymbols(
        bool includeFixture, bool includeClass, bool includeContext, bool requireContext, bool expectedCallback)
    {
        string source = $$"""
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                {{(includeFixture ? "public class TestInitializeAttribute : System.Attribute { }" : string.Empty)}}
                {{(includeClass ? "public class TestClassAttribute : System.Attribute { }" : string.Empty)}}
                {{(includeContext ? "public class TestContext { }" : string.Empty)}}
            }
            public class Tests
            {
                {{(includeFixture ? "[Microsoft.VisualStudio.TestTools.UnitTesting.TestInitialize]" : string.Empty)}}
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        var analyzer = new FixtureCallbackAnalyzer(requireContext);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer);
        Assert.HasCount(expectedCallback ? 1 : 0, diagnostics);
        Assert.HasCount(expectedCallback ? 1 : 0, analyzer.SymbolSets);
        if (expectedCallback)
        {
            AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "TEST001", DiagnosticSeverity.Warning,
                source, "Check", "Registered Check");
            object symbols = Assert.ContainsSingle(analyzer.SymbolSets);
            Assert.AreEqual("TestInitializeAttribute", ProductReflection.Get<INamedTypeSymbol>(symbols, "FixtureAttributeSymbol").Name);
            Assert.AreEqual("TestClassAttribute", ProductReflection.Get<INamedTypeSymbol>(symbols, "TestClassAttributeSymbol").Name);
            Assert.AreEqual("System.Threading.Tasks.Task", ProductReflection.Get<INamedTypeSymbol>(symbols, "TaskSymbol").ToDisplayString());
            Assert.AreEqual("System.Threading.Tasks.ValueTask", ProductReflection.Get<INamedTypeSymbol>(symbols, "ValueTaskSymbol").ToDisplayString());
            Assert.AreEqual(includeContext ? "TestContext" : null, ProductReflection.Get<INamedTypeSymbol?>(symbols, "TestContextSymbol")?.Name);
            Assert.IsNull(ProductReflection.Get<INamedTypeSymbol?>(symbols, "InheritanceBehaviorSymbol"));
            Assert.IsFalse(ProductReflection.Get<bool>(symbols, "CanDiscoverInternals"));
        }
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references", Justification = "Test-only analyzer exercising the shipping registration helper.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "Test-only analyzer exercising the shipping registration helper.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1041:Compiler extensions should be implemented in assemblies targeting netstandard2.0", Justification = "Test-only analyzer exercising the shipping registration helper.")]
    private sealed class FixtureCallbackAnalyzer(bool requireContext) : DiagnosticAnalyzer
    {
        [SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking", Justification = "This descriptor is only a test callback, not a shipping rule.")]
        private static readonly DiagnosticDescriptor Rule = new("TEST001", "Fixture callback", "Registered {0}", "Contract", DiagnosticSeverity.Warning, true);

        internal ConcurrentBag<object> SymbolSets { get; } = [];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            Type helper = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.FixtureMethodAnalyzerHelper");
            Type symbols = helper.GetNestedType("FixtureMethodSymbols", ProductReflection.AllMembers)!;
            Type actionType = typeof(Action<,>).MakeGenericType(typeof(SymbolAnalysisContext), symbols);
            MethodInfo callback = GetType().GetMethod(nameof(OnSymbol), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(symbols);
            var action = Delegate.CreateDelegate(actionType, this, callback);
            ProductReflection.Call(helper, null, "RegisterFixtureMethodSymbolAction",
                [typeof(AnalysisContext), typeof(string), actionType, typeof(bool)],
                context, "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute", action, requireContext);
        }

        private void OnSymbol<TSymbols>(SymbolAnalysisContext context, TSymbols symbols)
        {
            if (context.Symbol.Name == "Check")
            {
                SymbolSets.Add(symbols!);
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Symbol.Locations.Single(), context.Symbol.Name));
            }
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.CodeFixes;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CodeFixRegistrationContinuationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LegacyFixers_StaticImports_RetainDiagnosticWithoutUnsupportedFix(bool collection)
    {
        string type = collection ? "CollectionAssert" : "StringAssert";
        string invocation = collection ? "Contains(new[] { 17, 29 }, 17)" : "Contains(\"cart\", \"ar\")";
        string source = $$"""
            using static Microsoft.VisualStudio.TestTools.UnitTesting.{{type}};
            public class Tests
            {
                public void Check() { {{invocation}}; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        DiagnosticAnalyzer analyzer = collection ? new CollectionAssertToAssertAnalyzer() : new StringAssertToAssertAnalyzer();
        CodeFixProvider fixer = collection ? new CollectionAssertToAssertFixer() : new StringAssertToAssertFixer();
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer);
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], collection ? "MSTEST0068" : "MSTEST0046", DiagnosticSeverity.Info,
            source, invocation, $"Use 'Assert.Contains' instead of '{type}.Contains'");
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], fixer));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    [DataRow("String", "ProperAssertMethodNameKey", false)]
    [DataRow("String", "ProperAssertMethodNameKey", true)]
    [DataRow("Collection", "ProperAssertMethodNameKey", false)]
    [DataRow("Collection", "ProperAssertMethodNameKey", true)]
    [DataRow("Collection", "FixKindKey", false)]
    [DataRow("Collection", "FixKindKey", true)]
    public async Task LegacyFixers_IncompleteDiagnosticMetadata_OfferNoAction(string scenario, string property, bool explicitNull)
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, scenario);
        Assert.IsTrue(original.Properties.TryGetValue(property, out string? originalValue));
        Assert.IsNotNull(originalValue);
        ImmutableDictionary<string, string?> properties = explicitNull
            ? original.Properties.SetItem(property, null)
            : original.Properties.Remove(property);
        Diagnostic incomplete = CopyDiagnostic(original, properties: properties);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, incomplete, fixer));
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!);
    }

    [TestMethod]
    [DataRow("String")]
    [DataRow("Collection")]
    [DataRow("Boolean")]
    [DataRow("Description")]
    [DataRow("DataRow")]
    [DataRow("Async")]
    [DataRow("Timeout")]
    [DataRow("OS")]
    [DataRow("Proper")]
    [DataRow("ContainsKey")]
    public async Task Fixers_NonmatchingSyntax_OfferNoAction(string scenario)
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, scenario);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        Location unrelatedLocation = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First().GetLocation();
        Diagnostic stale = CopyDiagnostic(original, location: unrelatedLocation);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, stale, fixer));
        string source = (await document.GetTextAsync()).ToString();
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source, scenario == "ContainsKey" ? ["CS1503"] : []);
    }

    [TestMethod]
    public async Task NegatedBoolean_StaticImport_DoesNotRewriteInvocation()
    {
        const string source = """
            using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
            public class Tests
            {
                public void Check(bool flag) { IsTrue(!flag); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DoNotNegateBooleanAssertionAnalyzer());
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0023", DiagnosticSeverity.Info, source, "IsTrue(!flag)",
            "Do not negate boolean assertions, instead use the opposite assertion");
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], new DoNotNegateBooleanAssertionFixer());
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]), source);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NegatedBoolean_IncompleteDiagnosticMetadata_OffersNoAction(bool explicitNull)
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Boolean");
        Diagnostic incomplete = CopyDiagnostic(original, properties: explicitNull
            ? original.Properties.SetItem("ProperAssertMethodNameKey", null)
            : original.Properties.Remove("ProperAssertMethodNameKey"));
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, incomplete, fixer));
    }

    [TestMethod]
    public async Task NegatedBoolean_StaleCondition_DoesNotPartiallyRewrite()
    {
        using var workspace = new AdhocWorkspace();
        (Document originalDocument, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Boolean");
        string currentSource = (await originalDocument.GetTextAsync()).ToString().Replace("!flag", "(flag)");
        Document current = originalDocument.WithText(Microsoft.CodeAnalysis.Text.SourceText.From(currentSource));
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(current, original, fixer);
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(current, actions[0]), currentSource);
    }

    [TestMethod]
    public async Task NegatedBoolean_StaleArgumentLocation_OffersNoAction()
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Boolean");
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        Diagnostic stale = CopyDiagnostic(original, additionalLocations: [root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().GetLocation()]);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, stale, fixer));
    }

    [TestMethod]
    public async Task Description_StaleDiagnostic_DoesNotChangeUnrelatedAttributes()
    {
        using var workspace = new AdhocWorkspace();
        (Document originalDocument, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Description");
        string currentSource = (await originalDocument.GetTextAsync()).ToString()
            .Replace("System.ComponentModel.Description(\"system\")", "Description(\"keep MSTest description\")");
        Document current = originalDocument.WithText(Microsoft.CodeAnalysis.Text.SourceText.From(currentSource));
        SyntaxNode root = (await current.GetSyntaxRootAsync())!;
        Diagnostic stale = CopyDiagnostic(original, location: root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Identifier.GetLocation());
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(current, stale, fixer);
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(current, actions[0]), currentSource);
    }

    [TestMethod]
    [DataRow("IsNegatedKey", null)]
    [DataRow("OSPlatformKey", null)]
    [DataRow("IsNegatedKey", "not-a-boolean")]
    public async Task OSCondition_InvalidDiagnosticMetadata_OffersNoAction(string property, string? value)
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "OS");
        Assert.IsTrue(original.Properties.ContainsKey(property));
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(
            document, CopyDiagnostic(original, properties: original.Properties.SetItem(property, value)), fixer));
    }

    [TestMethod]
    [DataRow("Assert.IsTrue(item == null, \"context\")", 0)]
    [DataRow("Assert.IsTrue(item == null, \"context\")", 1)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\"), \"context\")", 0)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\"), \"context\")", 1)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\"), \"context\")", 2)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", 0)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", 1)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", 2)]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", 3)]
    [DataRow("Assert.AreEqual(false, flag, \"context\")", 0)]
    [DataRow("Assert.AreEqual(0, values.Count, \"context\")", 0)]
    [DataRow("Assert.AreEqual(0, values.Count, \"context\")", 1)]
    [DataRow("Assert.AreEqual(0, values.Count, \"context\")", 2)]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 0)]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 1)]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 2)]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 3)]
    public async Task ProperAssert_StaleOperandLocations_DoNotPartiallyRewriteCalls(string invocation, int staleLocationIndex)
    {
        string source = CodeFixContinuationTests.ProperAssertSource(invocation);
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer());
        Assert.HasCount(1, diagnostics);
        Assert.AreEqual("MSTEST0037", diagnostics[0].Id);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        ImmutableArray<Location> locations = diagnostics[0].AdditionalLocations.ToImmutableArray()
            .SetItem(staleLocationIndex, root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().GetLocation());
        Diagnostic stale = CopyDiagnostic(diagnostics[0], additionalLocations: locations);
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, stale, new UseProperAssertMethodsFixer());
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]), source);
    }

    [TestMethod]
    [DataRow("CodeFixModeKey", null)]
    [DataRow("ProperAssertMethodNameKey", null)]
    [DataRow("CodeFixModeKey", "FutureRewriteMode")]
    public async Task ProperAssert_UnsupportedDiagnosticMetadata_OffersNoAction(string property, string? value)
    {
        using var workspace = new AdhocWorkspace();
        (Document document, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Proper");
        Diagnostic stale = CopyDiagnostic(original, properties: original.Properties.SetItem(property, value));
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, stale, fixer));
    }

    [TestMethod]
    public async Task AsyncAssertion_StaleAction_DoesNotInventBlockedTask()
    {
        using var workspace = new AdhocWorkspace();
        (Document originalDocument, Diagnostic original, CodeFixProvider fixer) = await CreateScenarioAsync(workspace, "Async");
        const string currentSource = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check()
                {
                    Assert.ThrowsExactly<Exception>(async () => { await Task.Yield(); ThrowNow(); });
                }
                private void ThrowNow() => throw new Exception();
            }
            """;
        Document current = originalDocument.WithText(Microsoft.CodeAnalysis.Text.SourceText.From(currentSource));
        SyntaxNode root = (await current.GetSyntaxRootAsync())!;
        Location location = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(invocation => invocation.Expression.ToString().StartsWith("Assert.", StringComparison.Ordinal)).GetLocation();
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(current, CopyDiagnostic(original, location: location), fixer);
        Assert.HasCount(1, actions);
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public async Task Check()
                {
                    await Assert.ThrowsExactlyAsync<Exception>(async () => { await Task.Yield(); ThrowNow(); });
                }
                private void ThrowNow() => throw new Exception();
            }
            """;
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(current, actions[0]), expected);
    }

    [TestMethod]
    public async Task FreshDocument_RealDiagnostic_ProducesCompilableFix()
    {
        using var originalWorkspace = new AdhocWorkspace();
        (Document original, Diagnostic diagnostic, CodeFixProvider fixer) = await CreateScenarioAsync(originalWorkspace, "DataRow");
        string source = (await original.GetTextAsync()).ToString();
        using var freshWorkspace = new AdhocWorkspace();
        Document fresh = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(freshWorkspace, source);

        // Do not request a tree/model before the public provider: this is the cold document path.
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(fresh, diagnostic, fixer);
        Assert.HasCount(1, actions);
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(fresh, actions[0]);
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                [DataRow(17)]
                public void Check(int number) { }
            }
            """;
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected);
    }

    private static Diagnostic CopyDiagnostic(
        Diagnostic original,
        Location? location = null,
        ImmutableArray<Location>? additionalLocations = null,
        ImmutableDictionary<string, string?>? properties = null)
        => Diagnostic.Create(original.Descriptor, location ?? original.Location,
            additionalLocations: additionalLocations ?? original.AdditionalLocations.ToImmutableArray(),
            properties: properties ?? original.Properties);

    private static async Task<(Document Document, Diagnostic Diagnostic, CodeFixProvider Fixer)> CreateScenarioAsync(AdhocWorkspace workspace, string scenario)
    {
        (string Source, DiagnosticAnalyzer? Analyzer, CodeFixProvider Fixer) setup = scenario switch
        {
            "String" => (
                "using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { public void Check() { StringAssert.Contains(\"cart\", \"ar\"); } }",
                new StringAssertToAssertAnalyzer(), new StringAssertToAssertFixer()),
            "Collection" => (
                "using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { public void Check() { CollectionAssert.Contains(new[] { 17, 29 }, 17); } }",
                new CollectionAssertToAssertAnalyzer(), new CollectionAssertToAssertFixer()),
            "Boolean" => (CodeFixContinuationTests.BooleanSource("Assert.IsTrue(!flag)"),
                new DoNotNegateBooleanAssertionAnalyzer(), new DoNotNegateBooleanAssertionFixer()),
            "Description" => (
                "using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { [TestMethod, System.ComponentModel.Description(\"system\")] public void Check() { } }",
                new DoNotUseSystemDescriptionAttributeAnalyzer(), new DoNotUseSystemDescriptionAttributeFixer()),
            "DataRow" => (
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    [TestMethod]
                    [DataRow(17)]
                    [DataRow(17)]
                    public void Check(int number) { }
                }
                """,
                new DuplicateDataRowAnalyzer(), new DuplicateDataRowFixer()),
            "Async" => (
                "using System; using System.Threading.Tasks; using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { [TestMethod] public void Check() { Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult()); } }",
                new PreferAsyncAssertionAnalyzer(), new PreferAsyncAssertionFixer()),
            "Timeout" => (
                "using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { [Timeout(17)] public void Check() { } }",
                new UseCooperativeCancellationForTimeoutAnalyzer(), new UseCooperativeCancellationForTimeoutFixer()),
            "OS" => (CodeFixContinuationTests.OSSource("OSCondition(OperatingSystems.Linux)", "if (!OperatingSystem.IsWindows()) { return; }"),
                new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer(), new UseOSConditionAttributeInsteadOfRuntimeCheckFixer()),
            "Proper" => (CodeFixContinuationTests.ProperAssertSource("Assert.IsTrue(item == null, \"context\")"),
                new UseProperAssertMethodsAnalyzer(), new UseProperAssertMethodsFixer()),
            "ContainsKey" => (
                "using System.Collections.Generic; public class Tests { public void Check(IDictionary<string, object> properties) { properties.Contains(\"Key\"); } }",
                null, new TestContextPropertiesContainsKeyFixer()),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, setup.Source);
        if (setup.Analyzer is null)
        {
            Compilation compilation = (await document.Project.GetCompilationAsync())!;
            AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation, "CS1503");
            return (document, compilation.GetDiagnostics().Single(diagnostic => diagnostic.Id == "CS1503"), setup.Fixer);
        }

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, setup.Analyzer);
        Assert.HasCount(1, diagnostics);
        return (document, diagnostics[0], setup.Fixer);
    }

    [TestMethod]
    [DataRow("AreSame", "AreEqual")]
    [DataRow("AreNotSame", "AreNotEqual")]
    public async Task ValueTypeAssertion_NullReplacementMetadata_FailsBeforeRegisteringAnUnsafeFix(string originalMethod, string replacement)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(int first, int second)
                {
                    Assert.{{originalMethod}}(first, second, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertAreSameWithValueTypesAnalyzer());
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0038", DiagnosticSeverity.Warning, source,
            $"Assert.{originalMethod}(first, second, \"context\")", $"Use '{replacement}' instead of '{originalMethod}' when comparing value types");
        Assert.AreEqual(replacement, diagnostics[0].Properties["ReplacementKey"]);
        var fixer = new AvoidAssertAreSameWithValueTypesFixer();
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], fixer);
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(
            await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]), source.Replace($"Assert.{originalMethod}(", $"Assert.{replacement}("));

        Diagnostic invalid = CopyDiagnostic(diagnostics[0], properties: diagnostics[0].Properties.SetItem("ReplacementKey", null));
        var unsafeActions = new List<CodeAction>();
        Exception exception = await Assert.ThrowsAsync<Exception>(() => fixer.RegisterCodeFixesAsync(
            new CodeFixContext(document, invalid, (action, _) => unsafeActions.Add(action), CancellationToken.None)));
        Assert.AreEqual("System.Diagnostics.UnreachableException", exception.GetType().FullName);
        StringAssert.StartsWith(exception.Message, "This program location is thought to be unreachable. File='");
        StringAssert.EndsWith(exception.Message.Replace('\\', '/'), "/src/Analyzers/MSTest.Analyzers.CodeFixes/AvoidAssertAreSameWithValueTypesFixer.cs' Line=43");
        Assert.IsEmpty(unsafeActions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }
}

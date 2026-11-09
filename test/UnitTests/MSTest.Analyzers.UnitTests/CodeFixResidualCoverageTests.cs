// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using MSTest.Analyzers.CodeFixes;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CodeFixResidualCoverageTests
{
    [TestMethod]
    public async Task Timeout_NoArgumentListNameTie_OffersNoActionAndRetainsMissingDurationError()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Timeout]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        AttributeSyntax attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
        Assert.IsNull(attribute.ArgumentList);
        Assert.AreEqual(attribute.Span, attribute.Name.Span);
        Assert.AreSame(attribute.Name, root.FindNode(attribute.Span, getInnermostNodeForTie: true));
        var fixer = new UseCooperativeCancellationForTimeoutFixer();
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, DiagnosticAt(fixer, attribute), fixer));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source, "CS7036");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "OneLineAsyncMethod")]
    [DataRow(true, DisplayName = "OneLineAsyncLocalFunction")]
    public async Task AsyncVoid_OneLineFileWithTypeAlias_UsesFallbackLineEndingAndKeepsAlias(bool localFunction)
    {
        const string method = """public async void Check() { Assert.IsTrue(true, "context"); }""";
        const string local = """public void Check() { async void Local() { Assert.IsTrue(true, "context"); } Local(); }""";
        string source = $$"""using Numbers = int[]; using Microsoft.VisualStudio.TestTools.UnitTesting; public class Tests { {{(localFunction ? local : method)}} }""";
        string expected = localFunction
            ? """
              using Numbers = int[];
              using System.Threading.Tasks;
              using Microsoft.VisualStudio.TestTools.UnitTesting;
              public class Tests
              {
                  public void Check()
                  {
                      async Task Local() { Assert.IsTrue(true, "context"); }
                      Local();
                  }
              }
              """
            : """
              using Numbers = int[];
              using System.Threading.Tasks;
              using Microsoft.VisualStudio.TestTools.UnitTesting;
              public class Tests
              {
                  public async Task Check() { Assert.IsTrue(true, "context"); }
              }
              """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(node => node.Expression.ToString() == "Assert.IsTrue");
        var fixer = new AvoidUsingAssertsInAsyncVoidContextFixer();
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, invocation), fixer);
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation change = Assert.ContainsSingle(operations.OfType<ApplyChangesOperation>());
        Document unformatted = change.ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (CompilationUnitSyntax)(await unformatted.GetSyntaxRootAsync())!;
        UsingDirectiveSyntax taskImport = changedRoot.Usings.Single(node => node.Name?.ToString() == "System.Threading.Tasks");
        Assert.AreEqual(Environment.NewLine, taskImport.GetTrailingTrivia().Single(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)).ToFullString());
        Assert.AreEqual("Numbers", changedRoot.Usings[0].Alias!.Name.Identifier.ValueText);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(unformatted, expected);
    }

    [TestMethod]
    public async Task ConditionalAccess_ReturnValue_RewritesOperandWithoutInventingAStatementGuard()
    {
        const string source = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public string Check(object? value)
                {
                    return Assert.IsInstanceOfType<string>(value?.ToString());
                }
            }
            """;
        const string expected = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public string Check(object? value)
                {
                    return Assert.IsInstanceOfType<string>(value.ToString());
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var fixer = new AssertionArgsShouldAvoidConditionalAccessFixer();
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(node => node.Expression is MemberAccessExpressionSyntax);
        ConditionalAccessExpressionSyntax conditional = root.DescendantNodes().OfType<ConditionalAccessExpressionSyntax>().Single();
        Diagnostic diagnostic = DiagnosticAt(fixer, invocation, additionalLocations: [conditional.GetLocation()]);
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
    [DataRow("counter = 17;", false, DisplayName = "AssignmentIsNotNullGuard")]
    [DataRow("((System.Action)callback)();", false, DisplayName = "DelegateInvocationIsNotNullGuard")]
    [DataRow("IsNotNull();", false, DisplayName = "ArgumentlessNameIsNotNullGuard")]
    [DataRow("Assert.IsNotNull(other);", false, DisplayName = "OtherReceiverIsNotNullGuard")]
    [DataRow("Assert.IsNotNull(text);", true, DisplayName = "MatchingPrecedingNullGuard")]
    public async Task ConditionalAccess_PrecedingStatements_OnlyReuseMatchingNonemptyNullGuard(string preceding, bool alreadyGuarded)
    {
        string source = ConditionalSource(preceding, string.Empty, "text?.Length");
        string expected = ConditionalSource(preceding, alreadyGuarded ? string.Empty : "Assert.IsNotNull(text);", "text.Length");
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new AssertionArgsShouldAvoidConditionalAccessAnalyzer(), enableDisabledRules: true);
        Diagnostic diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("MSTEST0026", diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.AreEqual(
            "Assert.AreEqual(3, text?.Length)",
            source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        var fixer = new AssertionArgsShouldAvoidConditionalAccessFixer();
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "OrderedStaticGenericAssertion")]
    [DataRow(true, DisplayName = "EquivalentStaticGenericAssertion")]
    public async Task CollectionEquality_StaticImport_PreservesMessageAndChosenGenericMode(bool equivalent)
    {
        const string source = """
            using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
            public class Tests
            {
                public void Check(int[] first, int[] second)
                {
                    AreEqual<int[]>(actual: second, expected: first, message: "context");
                }
            }
            """;
        string expected = equivalent
            ? """
              using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
              public class Tests
              {
                  public void Check(int[] first, int[] second)
                  {
                      AreEquivalent<int[]>(first, second, "context");
                  }
              }
              """
            : """
              using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
              public class Tests
              {
                  public void Check(int[] first, int[] second)
                  {
                      AreSequenceEqual(first, second, "context");
                  }
              }
              """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertAreEqualOnCollectionsAnalyzer());
        Diagnostic diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("MSTEST0065", diagnostic.Id);
        Assert.AreEqual(
            "AreEqual<int[]>(actual: second, expected: first, message: \"context\")",
            source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        var fixer = new AvoidAssertAreEqualOnCollectionsFixer();
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, fixer);
        Assert.HasCount(2, actions);
        CollectionAssert.AreEqual(
            new[] { $"{nameof(AvoidAssertAreEqualOnCollectionsFixer)}.Ordered", $"{nameof(AvoidAssertAreEqualOnCollectionsFixer)}.Equivalent" },
            actions.Select(action => action.EquivalenceKey).ToArray());
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(
            await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[equivalent ? 1 : 0]), expected);
    }

    [TestMethod]
    [DataRow("Compare()", DisplayName = "BothArgumentsImplicit")]
    [DataRow("Compare(17)", DisplayName = "ActualArgumentImplicit")]
    public async Task CollectionEquality_StaleInvocationWithImplicitOperands_OffersNoAction(string invocation)
    {
        string source = $$"""
            public class Tests
            {
                public void Check() { {{invocation}}; }
                private void Compare(int expected = 17, int actual = 29) { }
            }
            """;
        await VerifyNoActionAsync(source, new AvoidAssertAreEqualOnCollectionsFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single());
    }

    [TestMethod]
    public async Task CollectionEquality_ExplicitComparer_DoesNotOfferEitherIncompatibleReplacement()
    {
        const string source = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(int[] first, int[] second)
                {
                    Assert.AreEqual(first, second, EqualityComparer<int[]>.Default, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Diagnostic diagnostic = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertAreEqualOnCollectionsAnalyzer()));
        Assert.AreEqual("MSTEST0065", diagnostic.Id);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, new AvoidAssertAreEqualOnCollectionsFixer()));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task CollectionMigration_NamespaceAlias_PreservesQualifierAndSwapsOnlyOperands()
    {
        const string source = """
            using Testing = Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check()
                {
                    Testing::CollectionAssert.Contains(new[] { 17, 29 }, 17, "context");
                }
            }
            """;
        const string expected = """
            using Testing = Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check()
                {
                    Testing::Assert.Contains(17, new[] { 17, 29 }, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Diagnostic diagnostic = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new CollectionAssertToAssertAnalyzer()));
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "MSTEST0068", DiagnosticSeverity.Info, source,
            "Testing::CollectionAssert.Contains(new[] { 17, 29 }, 17, \"context\")", "Use 'Assert.Contains' instead of 'CollectionAssert.Contains'");
        var fixer = new CollectionAssertToAssertFixer();
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
    [DataRow("Other<int>(value, \"context\")", DisplayName = "UnqualifiedUnrelatedCall")]
    [DataRow("Assert.AreEqual(17, 29)", DisplayName = "WrongAssertionMethod")]
    [DataRow("Assert.IsInstanceOfType<int>(value, \"context\")", DisplayName = "StaleLiteralArgument")]
    [DataRow("TestingAssert.IsInstanceOfType<int>(value, \"context\")", DisplayName = "AliasDoesNotMatchAssertSyntax")]
    public async Task OutParameter_StaleArgumentOnUnsupportedCall_OffersNoAction(string invocation)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using TestingAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
            public class Tests
            {
                public void Check(object value) { {{invocation}}; }
                private static T Other<T>(object value, string message) => default(T);
            }
            """;
        await VerifyNoActionAsync(source, new AvoidOutParameterOnAssertIsInstanceOfTypeFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single().ArgumentList.Arguments.Last());
    }

    [TestMethod]
    public async Task OutParameter_DeclarationInNestedInvocation_RetainsCompilerErrorWithoutPartialRewrite()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(object value)
                {
                    int result = Assert.IsInstanceOfType<int>(value, out int typed);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation, "CS1615");
        Diagnostic diagnostic = compilation.GetDiagnostics().Single(item => item.Id == "CS1615");
        Assert.AreEqual("int typed", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        var fixer = new AvoidOutParameterOnAssertIsInstanceOfTypeFixer();
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), source, "CS1615");
    }

    [TestMethod]
    public async Task NegatedBoolean_ConditionBelongingToAnotherCall_DoesNotPartiallyRenameAssertion()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(bool flag, bool other)
                {
                    Keep(!other);
                    Assert.IsTrue(!flag, "context");
                }
                private void Keep(bool value) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax[] invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var fixer = new DoNotNegateBooleanAssertionFixer();
        Diagnostic diagnostic = DiagnosticAt(fixer, invocations[1],
            Properties(("ProperAssertMethodNameKey", "IsFalse")),
            [invocations[0].ArgumentList.Arguments[0].GetLocation()]);
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), source);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "MissingDynamicDataEnum")]
    [DataRow(true, DisplayName = "MissingTestMethodAttribute")]
    public async Task AttributeFixers_MissingFrameworkMetadata_DoNotChangeValidAttributes(bool duplicateTestMethod)
    {
        const string source = """
            using System;
            public class Tests
            {
                [Obsolete("keep")]
                public void Check() { }
            }
            """;
        CodeFixProvider fixer = duplicateTestMethod ? new DuplicateTestMethodAttributeFixer() : new AvoidExplicitDynamicDataSourceTypeFixer();
        await VerifySyntheticFixAsync(source, source, fixer,
            root => duplicateTestMethod
                ? root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single()
                : root.DescendantNodes().OfType<AttributeSyntax>().Single(),
            includeMSTest: false);
    }

    [TestMethod]
    public async Task Description_MissingBclDescriptionType_DoesNotRewriteOtherAttributes()
    {
        const string source = """
            using System;
            public class Tests
            {
                [Obsolete("keep")]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document original = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await original.Project.GetCompilationAsync())!;
        INamedTypeSymbol description = compilation.GetTypeByMetadataName("System.ComponentModel.DescriptionAttribute")!;
        Assert.IsNotNull(description);
        MetadataReference reference = compilation.GetMetadataReference(description.ContainingAssembly)!;
        Assert.IsNotNull(reference);
        Document document = original.Project.RemoveMetadataReference(reference).GetDocument(original.Id)!;
        Assert.IsNull((await document.Project.GetCompilationAsync())!.GetTypeByMetadataName("System.ComponentModel.DescriptionAttribute"));
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var fixer = new DoNotUseSystemDescriptionAttributeFixer();
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single()), fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), source);
    }

    [TestMethod]
    public async Task DuplicateTestMethod_OnlyOneTestAttribute_DoesNotRemoveItOrOtherMetadata()
    {
        const string source = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Obsolete("keep"), TestMethod]
                public void Check() { }
            }
            """;
        await VerifySyntheticFixAsync(source, source, new DuplicateTestMethodAttributeFixer(),
            root => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single());
    }

    [TestMethod]
    [DataRow("Constructor", DisplayName = "ExistingExpressionBodiedConstructor")]
    [DataRow("Initialize", DisplayName = "ExistingExpressionBodiedInitialize")]
    [DataRow("Dispose", DisplayName = "ExistingExpressionBodiedDispose")]
    public async Task FixtureConversions_ExistingExpressionBody_RetainBothBodiesAndExposeCompilerError(string scenario)
    {
        (string Source, string Expected, CodeFixProvider Fixer, string Target) scenarioData = scenario switch
        {
            "Constructor" => (
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() => marker = 29;
                    [TestInitialize]
                    public void Setup() { marker = 17; }
                }
                """,
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() { marker = 17; } => marker = 29;
                }
                """,
                new PreferConstructorOverTestInitializeFixer(), "Setup"),
            "Initialize" => (
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() { marker = 17; }
                    [TestInitialize]
                    public void Setup() => marker = 29;
                }
                """,
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    [TestInitialize]
                    public void Setup() { marker = 17; } => marker = 29;
                }
                """,
                new PreferTestInitializeOverConstructorFixer(), "Tests"),
            "Dispose" => (
                """
                using System;
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests : IDisposable
                {
                    private int marker;
                    public void Dispose() => marker = 29;
                    [TestCleanup]
                    public void Cleanup() { marker = 17; }
                }
                """,
                """
                using System;
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests : IDisposable
                {
                    private int marker;
                    public void Dispose() { marker = 17; } => marker = 29;
                }
                """,
                new PreferDisposeOverTestCleanupFixer(), "Cleanup"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        (string? source, string? expected, CodeFixProvider? fixer, string? target) = scenarioData;
        await VerifySyntheticFixAsync(source, expected, fixer,
            root => target == "Tests"
                ? root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single()
                : root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == target),
            fixedCompilerErrors: ["CS8057"], validateSerializedSource: true);
    }

    [TestMethod]
    public async Task DisposeConversion_NamespaceLevelMethod_DoesNotAddAttributesToInvalidContainer()
    {
        const string source = """
            namespace Fixtures
            {
                public void Dispose() { }
            }
            """;
        await VerifySyntheticFixAsync(source, source, new PreferTestCleanupOverDisposeFixer(),
            root => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(),
            originalCompilerErrors: ["CS0116"], fixedCompilerErrors: ["CS0116"]);
    }

    [TestMethod]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\"), \"context\")", 0, DisplayName = "AddArgumentTupleCondition")]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", 0, DisplayName = "AddTwoArgumentsTupleCondition")]
    [DataRow("Assert.AreEqual(false, flag, \"context\")", 0, DisplayName = "RemoveArgumentTupleOperand")]
    [DataRow("Assert.AreEqual(0, values.Count, \"context\")", 0, DisplayName = "ReplaceCountTupleOperand")]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 0, DisplayName = "ContainsSingleTupleOperand")]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", 1, DisplayName = "ContainsSingleForeignReplacement")]
    public async Task ProperAssert_TupleOperandLocations_DoNotPartiallyRewriteCalls(string invocation, int staleIndex)
    {
        string source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(bool flag, List<string> values)
                {
                    var unrelated = (17, 29);
                    {{invocation}};
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Diagnostic original = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer()));
        Assert.AreEqual("MSTEST0037", original.Id);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        ArgumentSyntax tupleArgument = root.DescendantNodes().OfType<TupleExpressionSyntax>().Single().Arguments[0];
        ImmutableArray<Location> locations = original.AdditionalLocations.ToImmutableArray().SetItem(staleIndex, tupleArgument.GetLocation());
        var stale = Diagnostic.Create(original.Descriptor, original.Location, additionalLocations: locations, properties: original.Properties);
        var fixer = new UseProperAssertMethodsFixer();
        CodeAction action = await GetSingleActionAsync(document, stale, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), source);
    }

    [TestMethod]
    public async Task ProperAssert_ReversedCountOperands_InsertsCollectionAfterReplacedPredicate()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(List<string> values)
                {
                    Assert.AreEqual(values.Count(value => value == "needle"), 1, "context");
                }
            }
            """;
        const string expected = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(List<string> values)
                {
                    Assert.ContainsSingle(value => value == "needle", values, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax assertion = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        InvocationExpressionSyntax count = assertion.ArgumentList.Arguments[0].Expression as InvocationExpressionSyntax
            ?? throw new InvalidOperationException();
        var fixer = new UseProperAssertMethodsFixer();
        Diagnostic diagnostic = DiagnosticAt(fixer, assertion,
            Properties(("CodeFixModeKey", "CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument"), ("ProperAssertMethodNameKey", "ContainsSingle")),
            [
                assertion.ArgumentList.Arguments[1].GetLocation(),
                assertion.ArgumentList.Arguments[0].GetLocation(),
                count.ArgumentList.Arguments[0].Expression.GetLocation(),
                ((MemberAccessExpressionSyntax)count.Expression).Expression.GetLocation(),
            ]);
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
#if DEBUG
    [DoNotParallelize]
#endif
    [DataRow(false, DisplayName = "ProperAssertParenthesizedDelegate")]
    [DataRow(true, DisplayName = "ValueTypeAssertParenthesizedDelegate")]
    public async Task AssertionRename_StaleParenthesizedDelegate_OffersNoUnsafeAction(bool valueType)
    {
        const string source = """
            using System;
            public class Tests
            {
                public void Check(Action callback) { ((Action)callback)(); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        CodeFixProvider fixer = valueType ? new AvoidAssertAreSameWithValueTypesFixer() : new UseProperAssertMethodsFixer();
        Diagnostic diagnostic = DiagnosticAt(fixer, invocation,
            Properties(("ReplacementKey", "AreEqual"), ("CodeFixModeKey", "CodeFixModeSimple"), ("ProperAssertMethodNameKey", "IsNull")));
#if DEBUG
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, fixer));
#if DEBUG
        Assert.AreEqual(
            "Is this an interesting scenario where we are unable to retrieve SimpleNameSyntax corresponding to the assert method? SyntaxNode type: '((Action)callback)', Text: '((Action)callback)'.",
            Assert.ContainsSingle(assertions));
#endif
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task OSCondition_StaleLocationInsideMethodWithoutIf_OffersNoAction()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check() { Assert.AreEqual(17, 17); }
            }
            """;
        await VerifyNoActionAsync(source, new UseOSConditionAttributeInsteadOfRuntimeCheckFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(),
            Properties(("IsNegatedKey", "True"), ("OSPlatformKey", "Windows")));
    }

    [TestMethod]
    [DataRow("OSCondition(OperatingSystems.Linux)", "OperatingSystem.IsWindows()", "False", "OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)", DisplayName = "IncludeThenExclude")]
    [DataRow("OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)", "!OperatingSystem.IsWindows()", "True", "OSCondition(OperatingSystems.Windows)", DisplayName = "ExcludeThenInclude")]
    public async Task OSCondition_MismatchedExistingMode_AddsSeparateAttributeAndRetainsDuplicateError(
        string existingAttribute, string condition, string negated, string addedAttribute)
    {
        string source = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                [{{existingAttribute}}]
                public void Check()
                {
                    if ({{condition}}) { return; }
                    Assert.AreEqual(17, GetValue());
                }
                private int GetValue() => 17;
            }
            """;
        string expected = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                [{{existingAttribute}}]
                [{{addedAttribute}}]
                public void Check()
                {
                    Assert.AreEqual(17, GetValue());
                }
                private int GetValue() => 17;
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new UseOSConditionAttributeInsteadOfRuntimeCheckFixer(),
            root => root.DescendantNodes().OfType<IfStatementSyntax>().Single(),
            Properties(("IsNegatedKey", negated), ("OSPlatformKey", "Windows")),
            fixedCompilerErrors: ["CS0579"]);
    }

    [TestMethod]
    public async Task OSPlatformConsistency_MethodUnderClassCondition_OffersNoConflictingMethodAttribute()
    {
        const string source = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [OSCondition(OperatingSystems.Linux)]
            public class Tests
            {
                [SupportedOSPlatform("windows")]
                public void Check() { }
            }
            """;
        await VerifyNoActionAsync(source, new OSPlatformAttributesShouldBeConsistentFixer(),
            root => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(),
            Properties(("ConditionModeKey", "Include"), ("OperatingSystemsKey", "Windows")));
    }

    [TestMethod]
    public async Task DisplayName_StaleInvocationArgument_ThrowsBeforeChangingSource()
    {
        const string source = """
            public class Tests
            {
                public void Check() { Keep("context"); }
                private void Keep(string text) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var fixer = new TestMethodAttributeShouldSetDisplayNameCorrectlyFixer();
        ArgumentSyntax argument = root.DescendantNodes().OfType<ArgumentSyntax>().Single();
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, argument), fixer);
        Exception exception = await Assert.ThrowsAsync<Exception>(() => action.GetOperationsAsync(CancellationToken.None));
        Assert.AreEqual("System.Diagnostics.UnreachableException", exception.GetType().FullName);
        StringAssert.StartsWith(exception.Message, "This program location is thought to be unreachable. File='");
        StringAssert.EndsWith(exception.Message.Replace('\\', '/'), "/src/Analyzers/MSTest.Analyzers.CodeFixes/TestMethodAttributeShouldSetDisplayNameCorrectlyFixer.cs' Line=81");
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    [DataRow("callerFilePath", "callerFilePath, callerLineNumber", DisplayName = "CanonicalFileForwarding")]
    [DataRow("file", "file", DisplayName = "RenamedFileForwardingIsNotGuessed")]
    public async Task SourceInformation_ExistingFileParameter_AddsLineWithoutGuessingRenamedBaseArgument(string parameter, string expectedArguments)
    {
        string source = $$"""
            using System.Runtime.CompilerServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute
            {
                public CustomTestMethodAttribute([CallerFilePath] string {{parameter}} = "") : base({{parameter}}) { }
            }
            """;
        string expected = $$"""
            using System.Runtime.CompilerServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute
            {
                public CustomTestMethodAttribute([CallerFilePath] string {{parameter}} = "", [CallerLineNumber] int callerLineNumber = -1) : base({{expectedArguments}}) { }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new TestMethodAttributeShouldPropagateSourceInformationFixer(),
            root => root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single());
    }

    [TestMethod]
    [DataRow("get;", DisplayName = "MissingSetterOnAbstractProperty")]
    [DataRow("set;", DisplayName = "MissingGetterOnAbstractProperty")]
    public async Task TestContext_AbstractAccessorList_AddsMissingAccessorAndPreservesAbstractContract(string accessor)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public abstract class Tests
            {
                protected internal abstract TestContext Context { {{accessor}} }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public abstract class Tests
            {
                abstract public TestContext TestContext { get; set; }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new TestContextShouldBeValidFixer(),
            root => root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single());
    }

    [TestMethod]
    [DataRow("Missing", DisplayName = "MissingScopeDefaultsToMethod")]
    [DataRow("Null", DisplayName = "NullScopeDefaultsToMethod")]
    [DataRow("Unknown", DisplayName = "UnknownScopeUsesMethod")]
    [DataRow("class", DisplayName = "ExplicitClassScope")]
    public async Task ResourceLock_DiagnosticScopeMetadata_RoutesAttributeWithoutExecutingMutation(string scope)
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                [TestMethod]
                public void Check() { }
            }
            """;
        string expected = scope == "class"
            ? """
              using Microsoft.VisualStudio.TestTools.UnitTesting;
              [TestClass]
              [ResourceLock(WellKnownResources.EnvironmentVariables)]
              public class Tests
              {
                  [TestMethod]
                  public void Check() { }
              }
              """
            : """
              using Microsoft.VisualStudio.TestTools.UnitTesting;
              [TestClass]
              public class Tests
              {
                  [TestMethod]
                  [ResourceLock(WellKnownResources.EnvironmentVariables)]
                  public void Check() { }
              }
              """;
        ImmutableDictionary<string, string?> properties = Properties(("ResourceMember", "EnvironmentVariables"));
        if (scope != "Missing")
        {
            properties = properties.Add("FixScope", scope == "Null" ? null : scope);
        }

        await VerifySyntheticFixAsync(source, expected, new AddResourceLockFixer(),
            root => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(), properties);
    }

    [TestMethod]
    public async Task AddTestClass_LocalAttributeNameCollision_UsesFrameworkQualifiedName()
    {
        const string source = """
            namespace Scope
            {
                public class TestClassAttribute { }
                public class Tests { }
            }
            """;
        const string expected = """
            namespace Scope
            {
                public class TestClassAttribute { }
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                public class Tests { }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new AddTestClassFixer(),
            root => root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(node => node.Identifier.ValueText == "Tests"));
    }

    [TestMethod]
    public async Task AsyncAssertion_TopLevelStatement_AddsAwaitWithoutRequiringMethodDeclaration()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult(), "context");
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            await Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask, "context");
            """;
        await VerifySyntheticFixAsync(source, expected, new PreferAsyncAssertionFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().First(),
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true));
    }

    [TestMethod]
    public async Task AsyncAssertion_StaleUnboundCall_RetainsCompilerErrorAndLeavesBlockedArgumentIntact()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check()
                {
                    Assert.Missing(() => Task.CompletedTask.GetAwaiter().GetResult());
                }
            }
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public async Task Check()
                {
                    await Assert.MissingAsync(() => Task.CompletedTask.GetAwaiter().GetResult());
                }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new PreferAsyncAssertionFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().First(),
            originalCompilerErrors: ["CS0117"], fixedCompilerErrors: ["CS0117"]);
    }

    [TestMethod]
    public async Task AsyncAssertion_UnawaitableReturnFollowedByStatements_RefusesUnsafeAsyncConversion()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                private readonly object gate = new object();
                public Task Check()
                {
                    lock (gate) { return Task.CompletedTask; }
                    Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult());
                    return Task.CompletedTask;
                }
            }
            """;
        await VerifyNoActionAsync(source, new PreferAsyncAssertionFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().First(node => node.Expression.ToString().StartsWith("Assert.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AsyncAssertion_BareEarlyReturnAndUnchangedNestedBlock_PreservesControlFlow()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public Task Check(bool early)
                {
                    if (early) return;
                    { Keep(); }
                    Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult());
                    return Task.CompletedTask;
                }
                private void Keep() { }
            }
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public async Task Check(bool early)
                {
                    if (early) return;
                    { Keep(); }
                    await Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask);
                    await Task.CompletedTask;
                }
                private void Keep() { }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new PreferAsyncAssertionFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().First(node => node.Expression.ToString().StartsWith("Assert.", StringComparison.Ordinal)),
            originalCompilerErrors: ["CS0126"]);
    }

    [TestMethod]
    public async Task ContainsKey_UnrelatedOverloadResolutionFailure_DoesNotRenameUserMethod()
    {
        const string source = """
            public class Receiver
            {
                public void Contains(int value) { }
            }
            public class Tests
            {
                public void Check(Receiver receiver) { receiver.Contains("key"); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation, "CS1503");
        Diagnostic diagnostic = compilation.GetDiagnostics().Single(item => item.Id == "CS1503");
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "CS1503", DiagnosticSeverity.Error, source, "\"key\"",
            "Argument 1: cannot convert from 'string' to 'int'");
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, new TestContextPropertiesContainsKeyFixer()));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source, "CS1503");
    }

    [TestMethod]
    public async Task IgnoreJustification_ArgumentLocation_FindsContainingAttributeAndPreservesOtherMetadata()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Ignore(""), TestMethod]
                public void Check() { }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Ignore("TODO: explain why this is ignored"), TestMethod]
                public void Check() { }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new IgnoreShouldHaveJustificationFixer(),
            root => root.DescendantNodes().OfType<AttributeArgumentSyntax>().Single());
    }

    [TestMethod]
    [DataRow("in", "", DisplayName = "NestedInArgument")]
    [DataRow("ref", "", DisplayName = "NestedRefArgument")]
    [DataRow("out", "number = 29;", DisplayName = "NestedOutArgument")]
    public async Task AssertFail_NestedByReferenceArgument_DoesNotRemoveObservableInteraction(string modifier, string assignment)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check()
                {
                    int number = 17;
                    Assert.IsTrue(false && Probe({{modifier}} number), "context");
                }
                private bool Probe({{modifier}} int number) { {{assignment}} return number == 29; }
            }
            """;
        await VerifyNoActionAsync(source, new PreferAssertFailOverAlwaysFalseConditionsFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().First());
    }

    private static string ConditionalSource(string preceding, string guard, string actual) => $$"""
        #nullable enable
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        public class Tests
        {
            private int counter;
            private System.Action callback = () => { };
            public void Check(string? text, string? other)
            {
                int unrelated = 29;
                {{preceding}}
                {{guard}}
                Assert.AreEqual(3, {{actual}});
            }
            private void IsNotNull() { }
        }
        """;

    internal static ImmutableDictionary<string, string?> Properties(params (string Key, string? Value)[] properties)
        => properties.ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

    internal static Diagnostic DiagnosticAt(
        CodeFixProvider fixer,
        SyntaxNode node,
        ImmutableDictionary<string, string?>? properties = null,
        IEnumerable<Location>? additionalLocations = null)
    {
        var descriptor = new DiagnosticDescriptor(fixer.FixableDiagnosticIds[0], "Stale diagnostic", "Stale diagnostic", "Test", DiagnosticSeverity.Warning, true);
        return Diagnostic.Create(descriptor, node.GetLocation(), additionalLocations, properties);
    }

    private static async Task<CodeAction> GetSingleActionAsync(Document document, Diagnostic diagnostic, CodeFixProvider fixer)
        => Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, fixer));

    private static async Task VerifySyntheticFixAsync(
        string source,
        string expected,
        CodeFixProvider fixer,
        Func<SyntaxNode, SyntaxNode> selectNode,
        ImmutableDictionary<string, string?>? properties = null,
        bool includeMSTest = true,
        string[]? originalCompilerErrors = null,
        string[]? fixedCompilerErrors = null,
        CSharpCompilationOptions? options = null,
        bool validateSerializedSource = false)
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest, options: options);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!, originalCompilerErrors ?? []);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, selectNode(root), properties), fixer);
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, action);
        if (validateSerializedSource)
        {
            // Factory-created combinations can lack parser diagnostics. Validate the text that
            // would be written back to the editor, not just the transient construction tree.
            SyntaxTree reparsed = CSharpSyntaxTree.ParseText(await changed.GetTextAsync(), (CSharpParseOptions)changed.Project.ParseOptions!);
            changed = changed.WithSyntaxRoot(await reparsed.GetRootAsync());
        }

        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected, fixedCompilerErrors ?? []);
    }

    private static async Task VerifyNoActionAsync(
        string source,
        CodeFixProvider fixer,
        Func<SyntaxNode, SyntaxNode> selectNode,
        ImmutableDictionary<string, string?>? properties = null)
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, DiagnosticAt(fixer, selectNode(root), properties), fixer));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task CollectionEquality_VarargsInvocationWithoutFixedOperands_OffersNoAction()
    {
        const string source = """
            public class Tests
            {
                public void Check() { Compare(__arglist(17, 29)); }
                private void Compare(__arglist) { }
            }
            """;
        await VerifyNoActionAsync(source, new AvoidAssertAreEqualOnCollectionsFixer(),
            root => root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(invocation => invocation.Expression.ToString() == "Compare"));
    }

    [TestMethod]
    public async Task CollectionMigration_StaleFactoryReceiver_UsesFallbackAssertQualifier()
    {
        const string originalSource = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check() { CollectionAssert.Contains(new[] { 17, 29 }, 17, "context"); }
            }
            """;
        const string source = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Receiver
            {
                public void Contains(ICollection collection, object element, string message) { }
            }
            public class Tests
            {
                public void Check() { CreateReceiver().Contains(new[] { 17, 29 }, 17, "context"); }
                private Receiver CreateReceiver() => new Receiver();
            }
            """;
        const string expected = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Receiver
            {
                public void Contains(ICollection collection, object element, string message) { }
            }
            public class Tests
            {
                public void Check() { Assert.Contains(17, new[] { 17, 29 }, "context"); }
                private Receiver CreateReceiver() => new Receiver();
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document original = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, originalSource);
        Diagnostic prior = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(original, new CollectionAssertToAssertAnalyzer()));
        AnalyzerContinuationTestHelpers.AssertDiagnostic(prior, "MSTEST0068", DiagnosticSeverity.Info, originalSource,
            "CollectionAssert.Contains(new[] { 17, 29 }, 17, \"context\")", "Use 'Assert.Contains' instead of 'CollectionAssert.Contains'");
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .First(node => node.Expression is MemberAccessExpressionSyntax);
        var stale = Diagnostic.Create(prior.Descriptor, invocation.GetLocation(), properties: prior.Properties);
        var fixer = new CollectionAssertToAssertFixer();
        CodeAction action = await GetSingleActionAsync(document, stale, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
    public async Task DuplicateTestMethod_UnresolvedOtherAttribute_RetainsErrorsAndSingleTestAttribute()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [MissingAttribute, TestMethod]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic[] errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.IsNotEmpty(errors);
        Assert.IsTrue(errors.All(diagnostic => diagnostic.Id == "CS0246"));
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        AttributeSyntax missing = root.DescendantNodes().OfType<AttributeSyntax>().First();
        SymbolInfo symbol = (await document.GetSemanticModelAsync())!.GetSymbolInfo(missing);
        Assert.IsNull(symbol.Symbol);
        Assert.IsEmpty(symbol.CandidateSymbols);
        var fixer = new DuplicateTestMethodAttributeFixer();
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single()), fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), source,
            errors.Select(diagnostic => diagnostic.Id).ToArray());
    }

    [TestMethod]
    public async Task DisposeConversion_InheritedDisposableBase_AddsDirectInterfaceAndPreservesBase()
    {
        const string source = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Base : IDisposable
            {
                public void Dispose() { }
            }
            public class Tests : Base
            {
                [TestCleanup]
                public void Cleanup() { Assert.AreEqual(17, 29, "context"); }
            }
            """;
        const string expected = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Base : IDisposable
            {
                public void Dispose() { }
            }
            public class Tests : Base, IDisposable
            {
                public void Dispose() { Assert.AreEqual(17, 29, "context"); }
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new PreferDisposeOverTestCleanupFixer(),
            root => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(node => node.Identifier.ValueText == "Cleanup"));
    }

    [TestMethod]
    public async Task OSPlatformConsistency_GeneratedPartialDeclaration_CannotOverwriteTheSourceDocument()
    {
        const string source = """
            public partial class Tests { }
            """;
        using var workspace = new AdhocWorkspace();
        Document original = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Document document = original.Project.AddAnalyzerReference(new GeneratedConditionReference(new GeneratedCondition().AsSourceGenerator())).GetDocument(original.Id)!;
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("Tests")!;
        AttributeData condition = Assert.ContainsSingle(type.GetAttributes());
        Assert.AreEqual("Microsoft.VisualStudio.TestTools.UnitTesting.OSConditionAttribute", condition.AttributeClass!.ToDisplayString());
        SyntaxReference reference = condition.ApplicationSyntaxReference!;
        Assert.IsNotNull(reference);
        Assert.IsInstanceOfType<SourceGeneratedDocument>(document.Project.Solution.GetDocument(reference.SyntaxTree));
        Assert.HasCount(1, await document.Project.GetSourceGeneratedDocumentsAsync(CancellationToken.None));
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var fixer = new OSPlatformAttributesShouldBeConsistentFixer();
        Diagnostic diagnostic = DiagnosticAt(fixer, root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(),
            Properties(("ConditionModeKey", "Include"), ("OperatingSystemsKey", "Windows")));
        CodeAction action = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, fixer));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => action.GetOperationsAsync(CancellationToken.None));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    private sealed class GeneratedCondition : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
            => context.RegisterPostInitializationOutput(output => output.AddSource(
                "Conditions.g.cs",
                Microsoft.CodeAnalysis.Text.SourceText.From(
                    """
                    [global::Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(global::Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Linux)]
                    public partial class Tests { }
                    """, Encoding.UTF8)));
    }

    private sealed class GeneratedConditionReference(ISourceGenerator generator) : AnalyzerReference
    {
        public override string FullPath => string.Empty;

        public override object Id { get; } = new object();

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language)
            => language == LanguageNames.CSharp ? [generator] : [];

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => [generator];
    }

    [TestMethod]
    public async Task TestContext_ExpressionBodiedProperty_ExposesRetainedExpressionBodyCompilerError()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                private TestContext Context => null;
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public TestContext TestContext { get; set; } => null;
            }
            """;
        await VerifySyntheticFixAsync(source, expected, new TestContextShouldBeValidFixer(),
            root => root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(),
            fixedCompilerErrors: ["CS8057"], validateSerializedSource: true);
    }

    [TestMethod]
    [DataRow("TestMethod", DisplayName = "AttributeWithoutArguments")]
    [DataRow("TestMethod()", DisplayName = "AttributeWithEmptyArguments")]
    [DataRow("TestMethod(\"Check\")", DisplayName = "PositionalArgumentIsNotDisplayNameProperty")]
    public async Task RedundantDisplayName_AttributeWithoutNamedDisplayName_OffersNoAction(string attribute)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [{{attribute}}]
                public void Check() { }
            }
            """;
        await VerifyNoActionAsync(source, new RedundantTestMethodDisplayNameFixer(),
            root => root.DescendantNodes().OfType<AttributeSyntax>().Single());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "InferredNullAssertion")]
    [DataRow(true, DisplayName = "ExplicitGenericNullAssertion")]
    public async Task ProperAssert_IsNotNull_UsesNonGenericOverload(bool generic)
    {
        string typeArguments = generic ? "<string>" : string.Empty;
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(string text)
                {
                    Assert.AreNotEqual{{typeArguments}}(null, text, "context");
                }
            }
            """;
        string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check(string text)
                {
                    Assert.IsNotNull(text, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Diagnostic diagnostic = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer()));
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "MSTEST0037", DiagnosticSeverity.Info, source,
            $"Assert.AreNotEqual{typeArguments}(null, text, \"context\")", "Use 'Assert.IsNotNull' instead of 'Assert.AreNotEqual'");
        Assert.AreEqual("CodeFixModeRemoveArgument", diagnostic.Properties["CodeFixModeKey"]);
        var fixer = new UseProperAssertMethodsFixer();
        CodeAction action = await GetSingleActionAsync(document, diagnostic, fixer);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(await AnalyzerContinuationTestHelpers.ApplyAsync(document, action), expected);
    }

    [TestMethod]
    public async Task ContainsKey_NullLiteralHasNoStringType_OffersNoActionAndRetainsCompilerError()
    {
        const string source = """
            using System.Collections.Generic;
            public class Tests
            {
                public void Check(IDictionary<string, object> properties) { properties.Contains(null); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation, "CS1503");
        Diagnostic diagnostic = compilation.GetDiagnostics().Single(item => item.Id == "CS1503");
        Assert.AreEqual("null", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        Assert.IsNull((await document.GetSemanticModelAsync())!.GetTypeInfo(root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single()).Type);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, new TestContextPropertiesContainsKeyFixer()));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source, "CS1503");
    }

    [TestMethod]
    [DataRow("Constructor", DisplayName = "ExpressionBodiedInitializeHasNoBlockToMerge")]
    [DataRow("Initialize", DisplayName = "ExpressionBodiedConstructorHasNoBlockToMerge")]
    [DataRow("Dispose", DisplayName = "ExpressionBodiedCleanupHasNoBlockToMerge")]
    public async Task FixtureConversions_ExpressionBodiedOrigin_DoesNotMergeAbsentBlockStatements(string scenario)
    {
        (string Source, string Expected, CodeFixProvider Fixer, string Target) scenarioData = scenario switch
        {
            "Constructor" => (
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() { marker = 29; }
                    [TestInitialize]
                    public void Setup() => marker = 17;
                }
                """,
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() { marker = 29; }
                }
                """,
                new PreferConstructorOverTestInitializeFixer(), "Setup"),
            "Initialize" => (
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    public Tests() => marker = 17;
                    [TestInitialize]
                    public void Setup() { marker = 29; }
                }
                """,
                """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests
                {
                    private int marker;
                    [TestInitialize]
                    public void Setup() { marker = 29; }
                }
                """,
                new PreferTestInitializeOverConstructorFixer(), "Tests"),
            "Dispose" => (
                """
                using System;
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests : IDisposable
                {
                    private int marker;
                    public void Dispose() { marker = 29; }
                    [TestCleanup]
                    public void Cleanup() => marker = 17;
                }
                """,
                """
                using System;
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public class Tests : IDisposable
                {
                    private int marker;
                    public void Dispose() { marker = 29; }
                }
                """,
                new PreferDisposeOverTestCleanupFixer(), "Cleanup"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        (string? source, string? expected, CodeFixProvider? fixer, string? target) = scenarioData;
        await VerifySyntheticFixAsync(source, expected, fixer,
            root => target == "Tests"
                ? root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single()
                : root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == target));
    }

    [TestMethod]
    public async Task DisposeConversion_StaleMethodWithoutBaseList_ExposesEmptyBaseClauseInSerializedSource()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Dispose() { Assert.AreEqual(17, 29, "context"); }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests :
            {
                [TestCleanup]
                public void TestCleanup() { Assert.AreEqual(17, 29, "context"); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var fixer = new PreferTestCleanupOverDisposeFixer();
        CodeAction action = await GetSingleActionAsync(document, DiagnosticAt(fixer, root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single()), fixer);
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, action);
        ClassDeclarationSyntax type = ((await changed.GetSyntaxRootAsync())!).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Assert.IsNotNull(type.BaseList);

        // The factory-created empty base list has no parser diagnostics attached. Reparse the
        // serialized code as a consumer would, rather than mistaking that AST for valid C#.
        SyntaxTree reparsed = CSharpSyntaxTree.ParseText((await changed.GetTextAsync()).ToString(), (CSharpParseOptions)changed.Project.ParseOptions!);
        Document consumer = changed.WithSyntaxRoot(await reparsed.GetRootAsync());
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(consumer, expected, "CS1031");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "OtherResolvedAttributeBeforeInitialize")]
    [DataRow(true, DisplayName = "UnresolvedConstructorBeforeInitialize")]
    public async Task InitializeConversion_OtherAttributeBinding_DoesNotHideExistingInitializeMethod(bool invalidConstructor)
    {
        string obsoleteArgument = invalidConstructor ? "17" : "\"keep\"";
        string source = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                private int marker;
                public Tests() { marker = 17; }
                [Obsolete({{obsoleteArgument}}), TestInitialize]
                public void Setup() { marker = 29; }
            }
            """;
        string expected = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                private int marker;
                [Obsolete({{obsoleteArgument}}), TestInitialize]
                public void Setup() { marker = 29; marker = 17; }
            }
            """;
        string[] compilerErrors = invalidConstructor ? ["CS1503"] : [];
        await VerifySyntheticFixAsync(source, expected, new PreferTestInitializeOverConstructorFixer(),
            root => root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single(),
            originalCompilerErrors: compilerErrors, fixedCompilerErrors: compilerErrors);
    }
}

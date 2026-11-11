// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.CodeFixes;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CodeFixResidualHelperTests
{
    [TestMethod]
    public async Task Timeout_NoArgumentListHelper_AddsExplicitPropertyWithoutInventingDuration()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Timeout]
                public void Check() { }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Timeout(CooperativeCancellation = true)]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!, "CS7036");
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        AttributeSyntax attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
        Document changed = await (Task<Document>)ProductReflection.Call(
            typeof(UseCooperativeCancellationForTimeoutFixer), null, "AddCooperativeCancellationAsync",
            [typeof(Document), typeof(AttributeSyntax), typeof(CancellationToken)], document, attribute, CancellationToken.None)!;
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected, "CS7036");
    }

    [TestMethod]
    public async Task AsyncVoid_MissingTaskMetadata_DoesNotGuessThatAnImportCanRepairRuntime()
    {
        const string source = """namespace Empty { }""";
        using var workspace = new AdhocWorkspace();
        Document original = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Document document = original.Project.WithMetadataReferences([]).GetDocument(original.Id)!;
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Assert.IsNull(compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"));
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        bool inScope = await (Task<bool>)ProductReflection.Call(
            typeof(AvoidUsingAssertsInAsyncVoidContextFixer), null, "IsTaskInScopeAsync",
            [typeof(Document), typeof(SyntaxNode), typeof(CancellationToken)], document, root, CancellationToken.None)!;
        Assert.IsTrue(inScope, "A missing runtime Task type must not cause a speculative using insertion.");
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SharedGuardHelperRejectsForeignGuard")]
    [DataRow(true, DisplayName = "OSGuardHelperRejectsForeignGuard")]
    public async Task GuardReplacement_GuardFromAnotherMethod_ThrowsBeforeChangingEitherMethod(bool osHelper)
    {
        const string source = """
            public class Tests
            {
                public void Guarded(bool skip)
                {
                    if (skip) { return; }
                    Keep();
                }
                public void Unrelated() { Keep(); }
                private void Keep() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(node => node.Identifier.ValueText == "Unrelated");
        IfStatementSyntax foreignGuard = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        Task<Document> operation = osHelper
            ? (Task<Document>)ProductReflection.Call(
                typeof(UseOSConditionAttributeInsteadOfRuntimeCheckFixer), null, "AddOSConditionAttributeAsync",
                [typeof(Document), typeof(MethodDeclarationSyntax), typeof(IfStatementSyntax), typeof(string), typeof(bool), typeof(CancellationToken)],
                document, method, foreignGuard, "Windows", true, CancellationToken.None)!
            : (Task<Document>)ProductReflection.Call(
                ProductReflection.Type(codeFix: true, "MSTest.Analyzers.Helpers.SkipGuardCodeFixHelper"), null, "ReplaceGuardWithAttributeAsync",
                [typeof(Document), typeof(MethodDeclarationSyntax), typeof(IfStatementSyntax), typeof(string), typeof(string[]), typeof(CancellationToken), typeof(bool)],
                document, method, foreignGuard, "OSConditionAttribute", new[] { "OperatingSystems.Windows" }, CancellationToken.None, true)!;
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => operation);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task SkipGuard_LiteralArgumentAndFollowingComment_PreservesExecutableAssertion()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check(bool skip)
                {
                    if (skip) { return; }

                    // Retain the assertion.
                    Assert.AreEqual(17, 29, "context");
                }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                [global::Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute("maintenance")]
                public void Check(bool skip)
                {
                    // Retain the assertion.
                    Assert.AreEqual(17, 29, "context");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Document changed = await (Task<Document>)ProductReflection.Call(
            ProductReflection.Type(codeFix: true, "MSTest.Analyzers.Helpers.SkipGuardCodeFixHelper"), null, "ReplaceGuardWithAttributeAsync",
            [typeof(Document), typeof(MethodDeclarationSyntax), typeof(IfStatementSyntax), typeof(string), typeof(string[]), typeof(CancellationToken), typeof(bool)],
            document, method, method.Body!.Statements.OfType<IfStatementSyntax>().Single(), "IgnoreAttribute",
            new[] { "\"maintenance\"" }, CancellationToken.None, false)!;
        Assert.Contains("// Retain the assertion.\n", (await changed.GetTextAsync()).ToString().Replace("\r\n", "\n"));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected);
    }

    [TestMethod]
    public async Task FixtureSignature_ExpressionHasNoDeclaredMethod_DoesNotRewriteSurroundingDeclaration()
    {
        const string source = """
            public class Tests
            {
                private int Check(int input) { return input + 17; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        LiteralExpressionSyntax expression = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        Document changed = await (Task<Document>)ProductReflection.Call(
            ProductReflection.Type(codeFix: true, "MSTest.Analyzers.Helpers.FixtureMethodFixer"), null, "FixSignatureAsync",
            [typeof(Document), typeof(SyntaxNode), typeof(SyntaxNode), typeof(bool), typeof(bool), typeof(CancellationToken)],
            document, root, expression, true, true, CancellationToken.None)!;
        Assert.AreSame(document, changed);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, source);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "CollectionStaticImportHelper")]
    [DataRow(true, DisplayName = "StringStaticImportHelper")]
    public async Task LegacyMigration_StaticImportHelper_DoesNotInventMemberAccess(bool stringAssertion)
    {
        string source = stringAssertion
            ? """
              using static Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
              public class Tests
              {
                  public void Check() { Contains("cart", "ar", "context"); }
              }
              """
            : """
              using static Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
              public class Tests
              {
                  public void Check() { Contains(new[] { 17, 29 }, 17, "context"); }
              }
              """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, useMSTestV4: true);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        Task<Document> operation = stringAssertion
            ? (Task<Document>)ProductReflection.Call(
                typeof(StringAssertToAssertFixer), null, "FixStringAssertAsync",
                [typeof(Document), typeof(InvocationExpressionSyntax), typeof(string), typeof(CancellationToken)],
                document, invocation, "Contains", CancellationToken.None)!
            : (Task<Document>)ProductReflection.Call(
                typeof(CollectionAssertToAssertFixer), null, "FixCollectionAssertAsync",
                [typeof(Document), typeof(InvocationExpressionSyntax), typeof(string), typeof(string), typeof(CancellationToken)],
                document, invocation, "Contains", "FixKindSwapTwoArgs", CancellationToken.None)!;
        Document changed = await operation;
        Assert.AreSame(document, changed);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, source);
    }

    [TestMethod]
    public async Task CollectionMigration_NullFixKindHelper_DoesNotGuessArgumentTransformation()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check() { CollectionAssert.Contains(new[] { 17, 29 }, 17, "context"); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, useMSTestV4: true);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        Document changed = await (Task<Document>)ProductReflection.Call(
            typeof(CollectionAssertToAssertFixer), null, "FixAssertAsync",
            [typeof(Document), typeof(InvocationExpressionSyntax), typeof(string), typeof(string), typeof(CancellationToken)],
            document, root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(), "Contains", null, CancellationToken.None)!;
        Assert.AreSame(document, changed);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, source);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "OrderedRenameKeepsUnsupportedCallable")]
    [DataRow(true, DisplayName = "EquivalentRenameKeepsUnsupportedCallable")]
    public async Task CollectionEquality_UnsupportedCallableHelper_PreservesParenthesizedDelegate(bool preserveGeneric)
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
        var expression = (ExpressionSyntax)ProductReflection.Call(
            typeof(AvoidAssertAreEqualOnCollectionsFixer), null, "ReplaceMethodName",
            [typeof(ExpressionSyntax), typeof(string), typeof(bool)], invocation.Expression, "AreEquivalent", preserveGeneric)!;
        Assert.AreSame(invocation.Expression, expression);
        Assert.AreEqual("((Action)callback)", expression.ToString());
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task AsyncAssertion_UnsupportedCallableHelper_DoesNotAppendSuffixToDelegateExpression()
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
        var changed = (InvocationExpressionSyntax)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "ReplaceAssertMethodName", [typeof(InvocationExpressionSyntax)], invocation)!;
        Assert.AreSame(invocation, changed);
        Assert.AreEqual("((Action)callback)()", changed.ToString());
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task AsyncAssertion_InvocationWithoutActionParameter_ReturnsFalseAndMinusOne()
    {
        const string source = """
            using System;
            public class Tests
            {
                public int Check() { return Math.Abs(-17); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        object?[] arguments =
        [
            root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(),
            (await document.GetSemanticModelAsync())!,
            CancellationToken.None,
            29,
        ];
        bool found = (bool)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "TryGetActionArgumentIndex",
            [typeof(InvocationExpressionSyntax), typeof(SemanticModel), typeof(CancellationToken), typeof(int).MakeByRefType()],
            arguments)!;
        Assert.IsFalse(found);
        Assert.AreEqual(-1, arguments[3]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    [DataRow("() => ThrowNow()", DisplayName = "SynchronousExpressionLambda")]
    [DataRow("() => { ThrowNow(); }", DisplayName = "SynchronousBlockLambda")]
    [DataRow("delegate { ThrowNow(); }", DisplayName = "SynchronousAnonymousMethod")]
    [DataRow("new Action(ThrowNow)", DisplayName = "DelegateCreationWithoutBlockedTask")]
    [DataRow("(Action)(() => ThrowNow())", DisplayName = "CastSynchronousLambda")]
    [DataRow("(() => ThrowNow())", DisplayName = "ParenthesizedSynchronousLambda")]
    public async Task AsyncAssertion_SynchronousActionHelper_DoesNotInventTaskExpression(string expression)
    {
        string source = $$"""
            using System;
            public class Tests
            {
                public void Check() { Action action = {{expression}}; }
                private void ThrowNow() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        ExpressionSyntax action = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single().Initializer!.Value;
        object?[] arguments = [action, SyntaxFactory.IdentifierName("sentinel")];
        bool replaced = (bool)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "TryReplaceActionExpression",
            [typeof(ExpressionSyntax), typeof(ExpressionSyntax).MakeByRefType()], arguments)!;
        Assert.IsFalse(replaced);
        Assert.IsNull(arguments[1]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task AsyncAssertion_AwaitedDelegateReceiver_ParenthesizesBeforeInvokingResult()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            public class Tests
            {
                public async Task<int> Check(Func<Func<int, int>> factory, Func<Task<Func<int, int>>> asyncFactory)
                {
                    return factory()(17);
                }
            }
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            public class Tests
            {
                public async Task<int> Check(Func<Func<int, int>> factory, Func<Task<Func<int, int>>> asyncFactory)
                {
                    return (await asyncFactory())(17);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax original = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(node => node.Expression is IdentifierNameSyntax);
        var replacement = (ExpressionSyntax)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "CreateAwaitExpression",
            [typeof(InvocationExpressionSyntax), typeof(InvocationExpressionSyntax)],
            original, (InvocationExpressionSyntax)SyntaxFactory.ParseExpression("asyncFactory()"))!;
        Assert.AreEqual(SyntaxKind.ParenthesizedExpression, replacement.Kind());
        Document changed = document.WithSyntaxRoot(root.ReplaceNode(original, replacement));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected);
    }

    [TestMethod]
    public async Task OSCondition_ReplaceAbsentAttributeHelper_DoesNotAccidentallyAddAttribute()
    {
        const string source = """
            public class Tests
            {
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = (MethodDeclarationSyntax)ProductReflection.Call(
            typeof(UseOSConditionAttributeInsteadOfRuntimeCheckFixer), null, "ReplaceExistingAttribute",
            [typeof(MethodDeclarationSyntax), typeof(AttributeSyntax)],
            method, SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("OSCondition")))!;
        Assert.AreSame(method, changed);
        Assert.IsEmpty(changed.AttributeLists);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task ContainsKey_MissingCollectionMetadata_ReturnsNoExpectedCandidate()
    {
        const string source = """namespace Empty { }""";
        using var workspace = new AdhocWorkspace();
        Document original = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Document document = original.Project.WithMetadataReferences([]).GetDocument(original.Id)!;
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Assert.IsNull(compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2"));
        object? candidate = ProductReflection.Call(
            typeof(TestContextPropertiesContainsKeyFixer), null, "GetExpectedContainsMethodCandidate", [typeof(Compilation)], compilation);
        Assert.IsNull(candidate);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task TestClassAttribute_MissingMSTestMetadata_IsNotConsideredInScope()
    {
        const string source = """
            public class Tests { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        Assert.IsNull(model.Compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute"));
        bool inScope = (bool)ProductReflection.Call(
            typeof(AddTestClassFixer), null, "IsTestClassAttributeInScope", [typeof(SemanticModel), typeof(int)], model, 0)!;
        Assert.IsFalse(inScope);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task CancellationTokenParameter_ExistingParameter_AppendsContextAndPreservesBody()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public static void Initialize(int seed) { Assert.AreEqual(17, seed, "context"); }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public static void Initialize(int seed, TestContext testContext) { Assert.AreEqual(17, seed, "context"); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = (MethodDeclarationSyntax)ProductReflection.Call(
            typeof(FlowTestContextCancellationTokenFixer), null, "AddTestContextParameterToMethod", [typeof(MethodDeclarationSyntax)], method)!;
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document.WithSyntaxRoot(root.ReplaceNode(method, changed)), expected);
    }

    [TestMethod]
    public async Task CollectionMigration_VarargsCannotBeOrderedByFixedParameterOrdinal_ReturnsFalseAndNull()
    {
        const string source = """
            public class Tests
            {
                public void Check() { Compare(__arglist(17, 29)); }
                private void Compare(__arglist) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        var invocation = (Microsoft.CodeAnalysis.Operations.IInvocationOperation)model.GetOperation(
            root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node => node.Expression.ToString() == "Compare"))!;
        Assert.IsTrue(invocation.Arguments.Any(argument => argument.Parameter is null));
        object?[] arguments = [invocation, new[] { SyntaxFactory.Argument(SyntaxFactory.IdentifierName("sentinel")) }];
        bool ordered = (bool)ProductReflection.Call(
            typeof(CollectionAssertToAssertFixer), null, "TryGetArgumentsByOrdinal",
            [typeof(Microsoft.CodeAnalysis.Operations.IInvocationOperation), typeof(ArgumentSyntax[]).MakeByRefType()], arguments)!;
        Assert.IsFalse(ordered);
        Assert.IsNull(arguments[1]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    public async Task AsyncAssertion_ParameterizedAnonymousMethodHelper_DoesNotDropDelegateParameters()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            public class Tests
            {
                public void Check()
                {
                    Action<int> action = delegate (int value) { Task.FromResult(value).GetAwaiter().GetResult(); };
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        AnonymousMethodExpressionSyntax action = root.DescendantNodes().OfType<AnonymousMethodExpressionSyntax>().Single();
        object?[] arguments = [action, SyntaxFactory.IdentifierName("sentinel")];
        bool replaced = (bool)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "TryReplaceActionExpression",
            [typeof(ExpressionSyntax), typeof(ExpressionSyntax).MakeByRefType()], arguments)!;
        Assert.IsFalse(replaced);
        Assert.IsNull(arguments[1]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }

    [TestMethod]
    [DataRow("Task", "Task.CompletedTask", true, DisplayName = "NongenericTask")]
    [DataRow("ValueTask", "default(ValueTask)", true, DisplayName = "NongenericValueTask")]
    [DataRow("Task<int>", "Task.FromResult(17)", false, DisplayName = "GenericTaskIsNotVoidLike")]
    [DataRow("ValueTask<int>", "new ValueTask<int>(29)", false, DisplayName = "GenericValueTaskIsNotVoidLike")]
    [DataRow("int", "17", false, DisplayName = "SynchronousReturnType")]
    public async Task AsyncAssertion_ReturnTypeHelper_RecognizesOnlyNongenericTaskContracts(string returnType, string expression, bool expected)
    {
        string source = $$"""
            using System.Threading.Tasks;
            public class Tests
            {
                public {{returnType}} Check() { return {{expression}}; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        bool voidLike = (bool)ProductReflection.Call(
            typeof(PreferAsyncAssertionFixer), null, "IsTaskOrValueTaskReturnType",
            [typeof(MethodDeclarationSyntax), typeof(SemanticModel), typeof(CancellationToken)],
            root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(), (await document.GetSemanticModelAsync())!, CancellationToken.None)!;
        Assert.AreEqual(expected, voidLike);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source);
    }
}

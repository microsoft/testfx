// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for CS0115: Transform 'Execute' override to 'ExecuteAsync' when overriding TestMethodAttribute.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseExecuteAsyncOverrideFixer))]
[Shared]
public sealed class UseExecuteAsyncOverrideFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create("CS0115");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        SyntaxToken identifierToken = root.FindToken(context.Span.Start);

        if (identifierToken.Parent is MethodDeclarationSyntax methodDeclarationSyntax &&
            IsExecuteMethodOverride(methodDeclarationSyntax))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.TransformExecuteToExecuteAsyncFix,
                    createChangedDocument: ct => TransformExecuteToExecuteAsyncAsync(context.Document, root, methodDeclarationSyntax),
                    equivalenceKey: nameof(UseExecuteAsyncOverrideFixer)),
                context.Diagnostics);
        }
    }

    private static bool IsExecuteMethodOverride(MethodDeclarationSyntax methodDeclaration)
    {
        // Check if method is named "Execute" and has override modifier
        if (methodDeclaration.Identifier.ValueText != "Execute" ||
            !methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword) ||
            !methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return false;
        }

        // Check if it has the expected signature, return type must be TestResult[]
        if (methodDeclaration.ReturnType is not ArrayTypeSyntax arrayType ||
            GetRightmostName(arrayType.ElementType) is not IdentifierNameSyntax { Identifier.ValueText: "TestResult" })
        {
            return false;
        }

        // It should have exactly one parameter.
        if (methodDeclaration.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        // The parameter should be of type ITestMethod
        ParameterSyntax parameter = methodDeclaration.ParameterList.Parameters[0];
        if (GetRightmostName(parameter.Type) is not IdentifierNameSyntax { Identifier.ValueText: "ITestMethod" })
        {
            return false;
        }

        // We passed all the checks.
        // The method signature is:
        // public override TestResult[] Execute(ITestMethod)
        return true;
    }

    private static SimpleNameSyntax? GetRightmostName(TypeSyntax? node)
        => node switch
        {
            QualifiedNameSyntax qualified when qualified.Right != null => qualified.Right,
            SimpleNameSyntax simple => simple,
            AliasQualifiedNameSyntax aliasQualifiedName when aliasQualifiedName.Name != null => aliasQualifiedName.Name,
            _ => null,
        };

    private static Task<Document> TransformExecuteToExecuteAsyncAsync(Document document, SyntaxNode root, MethodDeclarationSyntax methodDeclaration)
    {
        // Change method name from Execute to ExecuteAsync
        MethodDeclarationSyntax newMethod = methodDeclaration
            .WithIdentifier(SyntaxFactory.Identifier("ExecuteAsync").WithTriviaFrom(methodDeclaration.Identifier))
            .WithReturnType(WrapTypeWithGenericTask(methodDeclaration.ReturnType));

        // Transform the method body to wrap return statements with Task.FromResult
        if (methodDeclaration.Body is not null)
        {
            BlockSyntax newBody = TransformMethodBody(methodDeclaration.Body);
            newMethod = newMethod.WithBody(newBody);
        }
        else if (methodDeclaration.ExpressionBody is not null)
        {
            // Handle expression body members
            ArrowExpressionClauseSyntax newExpressionBody = TransformExpressionBody(methodDeclaration.ExpressionBody);
            newMethod = newMethod.WithExpressionBody(newExpressionBody);
        }

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(methodDeclaration, newMethod)));
    }

    private static GenericNameSyntax WrapTypeWithGenericTask(TypeSyntax type)
        => SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("Task"),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(type))).WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation, new SyntaxAnnotation("SymbolId", "System.Threading.Tasks.Task"));

    private static BlockSyntax TransformMethodBody(BlockSyntax body)
    {
        // Transform all return statements to return Task.FromResult<TestResult[]>(...)
        var transformer = new ReturnStatementTransformer();
        return (BlockSyntax)transformer.Visit(body)!;
    }

    private static ArrowExpressionClauseSyntax TransformExpressionBody(ArrowExpressionClauseSyntax expressionBody)
    {
        ExpressionSyntax taskFromResultExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Task"),
                    SyntaxFactory.IdentifierName("FromResult")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(expressionBody.Expression))));

        return expressionBody.WithExpression(taskFromResultExpression);
    }

    private sealed class ReturnStatementTransformer : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression is null)
            {
                // Error scenario. We don't expect a return statement without an expression (return;)
                return node;
            }

            ExpressionSyntax taskFromResultExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Task"),
                        SyntaxFactory.IdentifierName("FromResult")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(node.Expression))));

            return node.WithExpression(taskFromResultExpression);
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            => node;

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            => node;
    }
}

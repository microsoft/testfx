// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidExpectedExceptionAttributeFixer))]
[Shared]
public sealed class AvoidExpectedExceptionAttributeFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidExpectedExceptionAttributeRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        bool allowDerivedTypes = diagnostic.Properties.ContainsKey(AvoidExpectedExceptionAttributeAnalyzer.AllowDerivedTypesKey);

        // Find the method declaration identified by the diagnostic.
        MethodDeclarationSyntax methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();
        SemanticModel semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute, out INamedTypeSymbol? expectedExceptionAttributeSymbol))
        {
            return;
        }

        IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
        if (methodSymbol is null)
        {
            return;
        }

        AttributeData? attribute = methodSymbol.GetAttributes().FirstOrDefault(
            attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, expectedExceptionAttributeSymbol));

        if (attribute?.ApplicationSyntaxReference is not { } syntaxRef)
        {
            return;
        }

        if (await syntaxRef.GetSyntaxAsync(context.CancellationToken).ConfigureAwait(false) is not { } attributeSyntax)
        {
            return;
        }

        TypedConstant exceptionTypeArgument = attribute.ConstructorArguments.Where(a => a.Kind == TypedConstantKind.Type).FirstOrDefault();
        if (exceptionTypeArgument.Value is not ITypeSymbol exceptionTypeSymbol)
        {
            return;
        }

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseAssertThrowsExceptionOnLastStatementFix,
                createChangedDocument: c => WrapLastStatementWithAssertThrowsExceptionAsync(context.Document, methodDeclaration, attributeSyntax, exceptionTypeSymbol, allowDerivedTypes, c),
                equivalenceKey: nameof(AvoidExpectedExceptionAttributeFixer)),
            diagnostic);
    }

    private static (SyntaxNode ExpressionOrStatement, SyntaxNode NodeToReplace)? TryGetExpressionOfInterestAndNodeToFromBlockSyntax(BlockSyntax? block)
    {
        if (block is null)
        {
            return null;
        }

        for (int i = block.Statements.Count - 1; i >= 0; i--)
        {
            StatementSyntax statement = block.Statements[i];

            if (statement is LockStatementSyntax lockStatement)
            {
                if (lockStatement.Statement is BlockSyntax lockBlock)
                {
                    if (TryGetExpressionOfInterestAndNodeToFromBlockSyntax(lockBlock) is { } resultFromLock)
                    {
                        return resultFromLock;
                    }

                    continue;
                }

                statement = lockStatement.Statement;
            }

            if (statement is LocalFunctionStatementSyntax or EmptyStatementSyntax)
            {
                continue;
            }
            else if (statement is BlockSyntax nestedBlock)
            {
                if (TryGetExpressionOfInterestAndNodeToFromBlockSyntax(nestedBlock) is { } expressionFromNestedBlock)
                {
                    return expressionFromNestedBlock;
                }

                // The BlockSyntax doesn't have any meaningful statements/expressions.
                // Ignore it.
                continue;
            }
            else if (statement is ExpressionStatementSyntax expressionStatement)
            {
                return (expressionStatement.Expression, statement);
            }
            else if (statement is LocalDeclarationStatementSyntax localDeclarationStatementSyntax &&
                localDeclarationStatementSyntax.Declaration.Variables.Count == 1 &&
                localDeclarationStatementSyntax.Declaration.Variables[0].Initializer is { } initializer)
            {
                return (initializer.Value, statement);
            }

            return (statement, statement);
        }

        return null;
    }

    private static (SyntaxNode ExpressionOrStatement, SyntaxNode NodeToReplace)? TryGetExpressionOfInterestAndNodeToFromExpressionBody(MethodDeclarationSyntax method)
        => method.ExpressionBody is null ? null : (method.ExpressionBody.Expression, method.ExpressionBody.Expression);

    private static async Task<Document> WrapLastStatementWithAssertThrowsExceptionAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNode attributeSyntax,
        ITypeSymbol exceptionTypeSymbol,
        bool allowDerivedTypes,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.RemoveNode(attributeSyntax);

        (SyntaxNode ExpressionOrStatement, SyntaxNode NodeToReplace)? expressionAndNodeToReplace = TryGetExpressionOfInterestAndNodeToFromBlockSyntax(methodDeclaration.Body)
            ?? TryGetExpressionOfInterestAndNodeToFromExpressionBody(methodDeclaration);

        if (expressionAndNodeToReplace is null)
        {
            return editor.GetChangedDocument();
        }

        SyntaxGenerator generator = editor.Generator;
        SyntaxNode expressionToUseInLambda = expressionAndNodeToReplace.Value.ExpressionOrStatement;

        expressionToUseInLambda = expressionToUseInLambda switch
        {
            ThrowStatementSyntax { Expression: not null } throwStatement => generator.ThrowExpression(throwStatement.Expression),
            // This is the case when the last statement of the method body is a loop for example (e.g, for, foreach, while, do while).
            // It can also happen for using statement, or switch statement.
            // In that case, we need to wrap in a block syntax (i.e, curly braces)
            StatementSyntax expressionToUseAsStatement => SyntaxFactory.Block(expressionToUseAsStatement.WithoutTrivia()).NormalizeWhitespace(),
            _ => expressionToUseInLambda.WithoutTrivia(),
        };

        SyntaxNode newLambdaExpression = generator.VoidReturningLambdaExpression(expressionToUseInLambda);

        bool containsAsyncCode = newLambdaExpression.DescendantNodesAndSelf().Any(n => n is AwaitExpressionSyntax);
        if (containsAsyncCode)
        {
            newLambdaExpression = ((LambdaExpressionSyntax)newLambdaExpression).WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        SyntaxNode newStatement = generator.InvocationExpression(
                generator.MemberAccessExpression(
                    generator.IdentifierName("Assert"),
                    generator.GenericName(
                        (containsAsyncCode, allowDerivedTypes) switch
                        {
                            (false, false) => "ThrowsExactly",
                            (false, true) => "Throws",
                            (true, false) => "ThrowsExactlyAsync",
                            (true, true) => "ThrowsAsync",
                        }, [exceptionTypeSymbol])),
                newLambdaExpression);

        if (containsAsyncCode)
        {
            newStatement = generator.AwaitExpression(newStatement);
        }

        if (methodDeclaration.Body is not null)
        {
            // For block bodies, we need to wrap the invocation (or the await expression) in expression statement. Otherwise, we shouldn't.
            newStatement = generator.ExpressionStatement(newStatement);
        }

        editor.ReplaceNode(expressionAndNodeToReplace.Value.NodeToReplace, newStatement.WithTriviaFrom(expressionAndNodeToReplace.Value.NodeToReplace));
        return editor.GetChangedDocument();
    }
}

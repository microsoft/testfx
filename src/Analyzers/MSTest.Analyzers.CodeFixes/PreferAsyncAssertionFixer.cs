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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="PreferAsyncAssertionAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferAsyncAssertionFixer))]
[Shared]
public sealed class PreferAsyncAssertionFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferAsyncAssertionRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode diagnosticNode = root.FindNode(context.Span);

        if (diagnosticNode.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault() is not { } invocationExpression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseAsyncAssertionFix,
                createChangedDocument: ct => UseAsyncAssertionAsync(context.Document, invocationExpression, ct),
                equivalenceKey: nameof(PreferAsyncAssertionFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> UseAsyncAssertionAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        InvocationExpressionSyntax newInvocationExpression = ReplaceAssertMethodName(invocationExpression);
        if (newInvocationExpression.ArgumentList.Arguments.Count > 0 &&
            TryReplaceLambda(newInvocationExpression.ArgumentList.Arguments[0], out ArgumentSyntax? newArgument))
        {
            newInvocationExpression = newInvocationExpression.WithArgumentList(
                newInvocationExpression.ArgumentList.WithArguments(newInvocationExpression.ArgumentList.Arguments.Replace(newInvocationExpression.ArgumentList.Arguments[0], newArgument)));
        }

        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(newInvocationExpression.WithoutLeadingTrivia())
            .WithLeadingTrivia(invocationExpression.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (invocationExpression.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } methodDeclaration)
        {
            MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.ReplaceNode(invocationExpression, awaitExpression);
            editor.ReplaceNode(methodDeclaration, AddAsyncModifierAndTaskReturnType(newMethodDeclaration));
        }
        else
        {
            editor.ReplaceNode(invocationExpression, awaitExpression);
        }

        return editor.GetChangedDocument();
    }

    private static InvocationExpressionSyntax ReplaceAssertMethodName(InvocationExpressionSyntax invocationExpression)
    {
        if (invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return invocationExpression;
        }

        SimpleNameSyntax asyncName = memberAccessExpression.Name switch
        {
            GenericNameSyntax genericName => genericName.WithIdentifier(SyntaxFactory.Identifier(
                genericName.Identifier.LeadingTrivia,
                genericName.Identifier.ValueText + "Async",
                genericName.Identifier.TrailingTrivia)),
            IdentifierNameSyntax identifierName => identifierName.WithIdentifier(SyntaxFactory.Identifier(
                identifierName.Identifier.LeadingTrivia,
                identifierName.Identifier.ValueText + "Async",
                identifierName.Identifier.TrailingTrivia)),
            _ => memberAccessExpression.Name,
        };

        return invocationExpression.WithExpression(memberAccessExpression.WithName(asyncName));
    }

    private static bool TryReplaceLambda(ArgumentSyntax argument, [NotNullWhen(returnValue: true)] out ArgumentSyntax? newArgument)
    {
        if (argument.Expression is not LambdaExpressionSyntax lambdaExpression ||
            !TryGetBlockedTaskExpressionFromLambda(lambdaExpression, out ExpressionSyntax? asyncExpression))
        {
            newArgument = null;
            return false;
        }

        LambdaExpressionSyntax newLambdaExpression = lambdaExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.WithBody(asyncExpression.WithTriviaFrom(lambdaExpression.Body)),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.WithBody(asyncExpression.WithTriviaFrom(lambdaExpression.Body)),
            _ => lambdaExpression,
        };

        newArgument = argument.WithExpression(newLambdaExpression);
        return true;
    }

    private static MethodDeclarationSyntax AddAsyncModifierAndTaskReturnType(MethodDeclarationSyntax methodDeclaration)
    {
        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;

        if (!newMethodDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AsyncKeyword)))
        {
            newMethodDeclaration = newMethodDeclaration.WithModifiers(newMethodDeclaration.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword)));
        }

        if (newMethodDeclaration.ReturnType.IsVoid())
        {
            newMethodDeclaration = newMethodDeclaration.WithReturnType(SyntaxFactory.IdentifierName("Task").WithTriviaFrom(newMethodDeclaration.ReturnType));
        }

        return newMethodDeclaration.WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static bool TryGetBlockedTaskExpressionFromLambda(ExpressionSyntax expression, [NotNullWhen(returnValue: true)] out ExpressionSyntax? asyncExpression)
    {
        if (WalkDownParentheses(expression) is not LambdaExpressionSyntax lambdaExpression)
        {
            asyncExpression = null;
            return false;
        }

        if (lambdaExpression.Body is ExpressionSyntax expressionBody)
        {
            return TryGetBlockedTaskExpression(expressionBody, out asyncExpression);
        }

        if (lambdaExpression.Body is BlockSyntax blockSyntax &&
            blockSyntax.Statements.Count == 1 &&
            blockSyntax.Statements[0] is ExpressionStatementSyntax expressionStatement)
        {
            return TryGetBlockedTaskExpression(expressionStatement.Expression, out asyncExpression);
        }

        asyncExpression = null;
        return false;
    }

    private static bool TryGetBlockedTaskExpression(ExpressionSyntax expression, [NotNullWhen(returnValue: true)] out ExpressionSyntax? asyncExpression)
    {
        ExpressionSyntax currentExpression = WalkDownParentheses(expression);
        if (currentExpression is InvocationExpressionSyntax getResultInvocation &&
            getResultInvocation.ArgumentList.Arguments.Count == 0 &&
            getResultInvocation.Expression is MemberAccessExpressionSyntax getResultMemberAccess &&
            getResultMemberAccess.Name.Identifier.ValueText == "GetResult" &&
            WalkDownParentheses(getResultMemberAccess.Expression) is InvocationExpressionSyntax getAwaiterInvocation &&
            getAwaiterInvocation.ArgumentList.Arguments.Count == 0 &&
            getAwaiterInvocation.Expression is MemberAccessExpressionSyntax getAwaiterMemberAccess &&
            getAwaiterMemberAccess.Name.Identifier.ValueText == "GetAwaiter")
        {
            asyncExpression = WalkDownParentheses(getAwaiterMemberAccess.Expression);
            return true;
        }

        asyncExpression = null;
        return false;
    }

    private static ExpressionSyntax WalkDownParentheses(ExpressionSyntax expression)
    {
        ExpressionSyntax currentExpression = expression;
        while (currentExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            currentExpression = parenthesizedExpression.Expression;
        }

        return currentExpression;
    }
}

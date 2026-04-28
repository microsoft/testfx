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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="AvoidUsingAssertsInAsyncVoidContextAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidUsingAssertsInAsyncVoidContextFixer))]
[Shared]
public sealed class AvoidUsingAssertsInAsyncVoidContextFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidUsingAssertsInAsyncVoidContextRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Walk up the ancestors to find the nearest async void method or local function.
        foreach (SyntaxNode ancestor in diagnosticNode.AncestorsAndSelf())
        {
            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                if (methodDeclaration.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                    methodDeclaration.ReturnType.IsVoid())
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: CodeFixResources.AvoidUsingAssertsInAsyncVoidContextFix,
                            createChangedDocument: ct => ChangeReturnTypeToTaskAsync(context.Document, methodDeclaration, ct),
                            equivalenceKey: nameof(AvoidUsingAssertsInAsyncVoidContextFixer)),
                        diagnostic);
                }

                break;
            }

            if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                if (localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                    localFunction.ReturnType.IsVoid())
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: CodeFixResources.AvoidUsingAssertsInAsyncVoidContextFix,
                            createChangedDocument: ct => ChangeReturnTypeToTaskAsync(context.Document, localFunction, ct),
                            equivalenceKey: nameof(AvoidUsingAssertsInAsyncVoidContextFixer)),
                        diagnostic);
                }

                break;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax)
            {
                // For lambdas/anonymous functions, we don't provide a fix since changing to Task
                // would require changing the delegate type as well.
                break;
            }
        }
    }

    private static async Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.WithReturnType(
            SyntaxFactory.IdentifierName("Task").WithTriviaFrom(methodDeclaration.ReturnType));
        editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
        return editor.GetChangedDocument();
    }

    private static async Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        LocalFunctionStatementSyntax localFunction,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        LocalFunctionStatementSyntax newLocalFunction = localFunction.WithReturnType(
            SyntaxFactory.IdentifierName("Task").WithTriviaFrom(localFunction.ReturnType));
        editor.ReplaceNode(localFunction, newLocalFunction);
        return editor.GetChangedDocument();
    }
}

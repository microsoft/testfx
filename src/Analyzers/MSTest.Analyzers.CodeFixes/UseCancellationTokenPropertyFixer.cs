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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UseCancellationTokenPropertyAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCancellationTokenPropertyFixer))]
[Shared]
public sealed class UseCancellationTokenPropertyFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseCancellationTokenPropertyRuleId);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        // The node here is a the node accessing CancellationTokenSource property on testContext.
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return;
        }

        // We are looking for testContext.CancellationTokenSource.Token.
        // We already have testContext.CancellationTokenSource, so we get the parent, and check if it's accessing 'Token'.
        if (memberAccessExpression.Parent is not MemberAccessExpressionSyntax parentMemberAccess ||
            parentMemberAccess.Name is not IdentifierNameSyntax { Identifier.ValueText: "Token" })
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseCancellationTokenPropertyFix,
                createChangedDocument: c =>
                {
                    // Replace testContext.CancellationTokenSource.Token with testContext.CancellationToken
                    MemberAccessExpressionSyntax newExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        memberAccessExpression.Expression, // testContext part
                        SyntaxFactory.IdentifierName("CancellationToken"));

                    return Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(parentMemberAccess, newExpression)));
                },
                equivalenceKey: nameof(UseCancellationTokenPropertyFixer)),
            diagnostic);
    }
}

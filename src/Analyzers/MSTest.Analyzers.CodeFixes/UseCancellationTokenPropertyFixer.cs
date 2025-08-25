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

        // Find the member access expression identified by the diagnostic
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return;
        }

        // Verify this is the pattern we expect: testContext.CancellationTokenSource.Token
        if (memberAccessExpression.Expression is not MemberAccessExpressionSyntax parentMemberAccess ||
            memberAccessExpression.Name.Identifier.ValueText != "Token" ||
            parentMemberAccess.Name.Identifier.ValueText != "CancellationTokenSource")
        {
            return;
        }

        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseCancellationTokenPropertyFix,
                createChangedDocument: c =>
                {
                    // Replace testContext.CancellationTokenSource.Token with testContext.CancellationToken
                    MemberAccessExpressionSyntax newExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        parentMemberAccess.Expression, // testContext part
                        SyntaxFactory.IdentifierName("CancellationToken"));

                    return Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(memberAccessExpression, newExpression)));
                },
                equivalenceKey: nameof(UseCancellationTokenPropertyFixer)),
            diagnostic);
    }
}

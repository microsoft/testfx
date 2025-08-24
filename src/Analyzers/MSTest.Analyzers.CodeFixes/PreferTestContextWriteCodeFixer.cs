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

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Code fixer for <see cref="PreferTestContextWriteAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferTestContextWriteCodeFixer))]
[Shared]
public sealed class PreferTestContextWriteCodeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferTestContextWriteRuleId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return;
        }

        // Register a code fix that will invoke the fix operation.
        string title = CodeFixResources.PreferTestContextWriteTitle;
        var action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => FixPreferTestContextWriteAsync(context.Document, invocationExpressionSyntax, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> FixPreferTestContextWriteAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        CancellationToken cancellationToken)
    {
        // Check if the invocation expression has a member access expression
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Create new TestContext.WriteLine call
        // Replace Console/Trace/Debug.Write* with TestContext.WriteLine
        MemberAccessExpressionSyntax newMemberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("TestContext"),
            SyntaxFactory.IdentifierName("WriteLine"));

        InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithExpression(newMemberAccess);

        // If original method was Write (not WriteLine), we need to preserve the arguments
        // but WriteLine always adds a newline, so we keep the same arguments
        
        editor.ReplaceNode(invocationExpr, newInvocationExpr);
        return editor.GetChangedDocument();
    }
}
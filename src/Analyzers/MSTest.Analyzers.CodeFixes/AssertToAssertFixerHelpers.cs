// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Shared helpers for code fixers that migrate legacy MSTest assert types to <c>Assert</c>.
/// </summary>
internal static class AssertToAssertFixerHelpers
{
    /// <summary>
    /// Runs the common diagnostic-property/invocation-shape validation and, when applicable, registers a
    /// <c>CodeAction</c> that delegates to <paramref name="fixAssertAsync"/>.
    /// </summary>
    /// <param name="context">The code-fix context.</param>
    /// <param name="properAssertMethodNamePropertyKey">Diagnostic property key holding the replacement <c>Assert</c> method name.</param>
    /// <param name="codeActionTitleFormat">Localized format string used to build the code action title.</param>
    /// <param name="fixKindPropertyKey">Optional diagnostic property key holding an additional fix discriminator. When <see langword="null"/>, no <c>fixKind</c> is read.</param>
    /// <param name="fixAssertAsync">Callback that rewrites the invocation.</param>
    internal static async Task RegisterCodeFixAsync(
        CodeFixContext context,
        string properAssertMethodNamePropertyKey,
        string codeActionTitleFormat,
        string? fixKindPropertyKey,
        Func<Document, InvocationExpressionSyntax, string, string?, CancellationToken, Task<Document>> fixAssertAsync)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(properAssertMethodNamePropertyKey, out string? properAssertMethodName)
            || properAssertMethodName is null)
        {
            return;
        }

        string? fixKind = null;
        if (fixKindPropertyKey is not null
            && (!diagnostic.Properties.TryGetValue(fixKindPropertyKey, out fixKind) || fixKind is null))
        {
            return;
        }

        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

        // We only know how to rewrite `<expr>.<member>(...)`-shaped invocations. `using static` and similar
        // shapes fall through without a fix; the diagnostic still surfaces so the user can migrate manually.
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax)
        {
            return;
        }

        string title = string.Format(CultureInfo.InvariantCulture, codeActionTitleFormat, properAssertMethodName);
        var action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => fixAssertAsync(context.Document, invocationExpr, properAssertMethodName, fixKind, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

    /// <summary>
    /// Replaces an invocation node in the document root.
    /// </summary>
    internal static async Task<Document> ReplaceInvocationAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        InvocationExpressionSyntax newInvocationExpr,
        CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root.ReplaceNode(invocationExpr, newInvocationExpr));
    }
}

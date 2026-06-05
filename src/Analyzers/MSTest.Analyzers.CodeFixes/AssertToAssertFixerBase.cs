// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Shared scaffold for <c>*Assert</c>-to-<c>Assert</c> code fixers.
/// </summary>
public abstract class AssertToAssertFixerBase : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(ProperAssertMethodNamePropertyKey, out string? properAssertMethodName)
            || properAssertMethodName is null)
        {
            return;
        }

        string? fixKind = null;
        if (FixKindPropertyKey is string fixKindPropertyKey
            && (!diagnostic.Properties.TryGetValue(fixKindPropertyKey, out fixKind) || fixKind is null))
        {
            return;
        }

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

        string title = string.Format(CultureInfo.InvariantCulture, CodeActionTitle, properAssertMethodName);
        var action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => FixAssertAsync(context.Document, invocationExpr, properAssertMethodName, fixKind, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

    /// <summary>
    /// Gets the diagnostic property key containing the replacement <c>Assert</c> method name.
    /// </summary>
    protected abstract string ProperAssertMethodNamePropertyKey { get; }

    /// <summary>
    /// Gets the localized code action title format string.
    /// </summary>
    protected abstract string CodeActionTitle { get; }

    /// <summary>
    /// Gets the optional diagnostic property key containing an additional fix discriminator.
    /// </summary>
    protected virtual string? FixKindPropertyKey => null;

    /// <summary>
    /// Builds the fixed document for the discovered assert invocation.
    /// </summary>
    protected abstract Task<Document> FixAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        string? fixKind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an invocation node in the document root.
    /// </summary>
    protected static async Task<Document> ReplaceInvocationAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        InvocationExpressionSyntax newInvocationExpr,
        CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root.ReplaceNode(invocationExpr, newInvocationExpr));
    }
}

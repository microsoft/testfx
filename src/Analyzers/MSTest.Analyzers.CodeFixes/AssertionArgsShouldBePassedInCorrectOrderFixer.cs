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
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertionArgsShouldBePassedInCorrectOrderFixer))]
[Shared]
public sealed class AssertionArgsShouldBePassedInCorrectOrderFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssertionArgsShouldBePassedInCorrectOrderRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root.FindNode(diagnosticSpan) is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

        if (context.Diagnostics.Any(d => !d.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey)))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.FixAssertionArgsOrder,
                    ct => SwapArgumentsAsync(context.Document, root, invocationExpr, ct),
                    nameof(AssertionArgsShouldBePassedInCorrectOrderFixer)),
                context.Diagnostics);
        }
    }

    private async Task<Document> SwapArgumentsAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentListSyntax argumentList = invocationExpr.ArgumentList;
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;

        SeparatedSyntaxList<ArgumentSyntax> newArgumentList = SyntaxFactory.SeparatedList<ArgumentSyntax>(new SyntaxNodeOrToken[]
        {
            arguments[1],
            SyntaxFactory.Token(SyntaxKind.CommaToken),
            arguments[0],
        });

        InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithArgumentList(SyntaxFactory.ArgumentList(newArgumentList));
        SyntaxNode newRoot = root.ReplaceNode(invocationExpr, newInvocationExpr);

        return document.WithSyntaxRoot(newRoot);
    }
}

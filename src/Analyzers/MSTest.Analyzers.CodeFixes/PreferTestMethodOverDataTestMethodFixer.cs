// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferTestMethodOverDataTestMethodFixer))]
[Shared]
public sealed class PreferTestMethodOverDataTestMethodFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferTestMethodOverDataTestMethodRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode? diagnosticNode = root?.FindNode(diagnostic.Location.SourceSpan);
            if (diagnosticNode is not AttributeSyntax attributeSyntax)
            {
                continue;
            }

            // Replace DataTestMethod with TestMethod
            var action = CodeAction.Create(
                title: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle,
                createChangedDocument: c => Task.FromResult(ReplaceDataTestMethod(context.Document, root!, attributeSyntax)),
                equivalenceKey: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle);

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    private static Document ReplaceDataTestMethod(Document document, SyntaxNode root, AttributeSyntax attributeSyntax)
    {
        var newAttribute = attributeSyntax.WithName(SyntaxFactory.IdentifierName("TestMethod"));
        var newRoot = root.ReplaceNode(attributeSyntax, newAttribute);

        return document.WithSyntaxRoot(newRoot);
    }
}
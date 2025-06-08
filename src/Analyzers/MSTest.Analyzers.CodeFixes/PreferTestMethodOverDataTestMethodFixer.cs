// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = nameof(PreferTestMethodOverDataTestMethodFixer))]
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
            if (diagnosticNode is null)
            {
                continue;
            }

            if (context.Document.Project.Language == LanguageNames.CSharp)
            {
                RegisterCSharpCodeFixesAsync(context, root!, diagnosticNode);
            }
            else if (context.Document.Project.Language == LanguageNames.VisualBasic)
            {
                RegisterVisualBasicCodeFixesAsync(context, root!, diagnosticNode);
            }
        }
    }

    private static Task RegisterCSharpCodeFixesAsync(CodeFixContext context, SyntaxNode root, SyntaxNode diagnosticNode)
    {
        if (diagnosticNode is not Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax attributeSyntax)
        {
            return Task.CompletedTask;
        }

        // Replace DataTestMethod with TestMethod
        var action = CodeAction.Create(
            title: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle,
            createChangedDocument: c => ReplaceDataTestMethodAsync(context.Document, root, attributeSyntax, c),
            equivalenceKey: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle);

        context.RegisterCodeFix(action, context.Diagnostics);
        return Task.CompletedTask;
    }

    private static Task RegisterVisualBasicCodeFixesAsync(CodeFixContext context, SyntaxNode root, SyntaxNode diagnosticNode)
    {
        if (diagnosticNode is not Microsoft.CodeAnalysis.VisualBasic.Syntax.AttributeSyntax attributeSyntax)
        {
            return Task.CompletedTask;
        }

        // Replace DataTestMethod with TestMethod
        var action = CodeAction.Create(
            title: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle,
            createChangedDocument: c => ReplaceDataTestMethodAsync(context.Document, root, attributeSyntax, c),
            equivalenceKey: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle);

        context.RegisterCodeFix(action, context.Diagnostics);
        return Task.CompletedTask;
    }

    private static Task<Document> ReplaceDataTestMethodAsync(Document document, SyntaxNode root, SyntaxNode attributeSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode newRoot;

        if (document.Project.Language == LanguageNames.CSharp)
        {
            var csAttributeSyntax = (Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax)attributeSyntax;
            var newAttribute = csAttributeSyntax.WithName(SyntaxFactory.IdentifierName("TestMethod"));
            newRoot = root.ReplaceNode(csAttributeSyntax, newAttribute);
        }
        else
        {
            var vbAttributeSyntax = (Microsoft.CodeAnalysis.VisualBasic.Syntax.AttributeSyntax)attributeSyntax;
            var newAttribute = vbAttributeSyntax.WithName(Microsoft.CodeAnalysis.VisualBasic.SyntaxFactory.IdentifierName("TestMethod"));
            newRoot = root.ReplaceNode(vbAttributeSyntax, newAttribute);
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
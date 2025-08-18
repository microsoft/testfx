// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for CS0103 compiler error when the identifier is "ClassCleanupBehavior".
/// This fixer removes the attribute argument containing the invalid identifier.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveClassCleanupBehaviorArgumentFixer))]
[Shared]
public sealed class RemoveClassCleanupBehaviorArgumentFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create("CS0103");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            // Only handle CS0103 errors for "ClassCleanupBehavior"
            if (!diagnostic.GetMessage().Contains("ClassCleanupBehavior"))
            {
                continue;
            }

            // Find the syntax node at the diagnostic location
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);

            // Check if this identifier is within an AttributeArgument
            AttributeArgumentSyntax? attributeArgument = node.FirstAncestorOrSelf<AttributeArgumentSyntax>();
            if (attributeArgument is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.RemoveClassCleanupBehaviorArgumentFix,
                        createChangedDocument: c => RemoveAttributeArgumentAsync(context.Document, attributeArgument, c),
                        equivalenceKey: nameof(RemoveClassCleanupBehaviorArgumentFixer)),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> RemoveAttributeArgumentAsync(Document document, AttributeArgumentSyntax attributeArgument, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        // Find the parent AttributeArgumentListSyntax
        if (attributeArgument.Parent is not AttributeArgumentListSyntax argumentList)
        {
            return document;
        }

        // If this is the only argument, remove the entire argument list
        if (argumentList.Arguments.Count == 1)
        {
            SyntaxNode newRoot = root.RemoveNode(argumentList, SyntaxRemoveOptions.KeepNoTrivia);
            return document.WithSyntaxRoot(newRoot);
        }

        // Otherwise, just remove this specific argument
        AttributeArgumentListSyntax newArgumentList = argumentList.RemoveNode(attributeArgument, SyntaxRemoveOptions.KeepNoTrivia);
        if (newArgumentList is not null)
        {
            SyntaxNode newRoot = root.ReplaceNode(argumentList, newArgumentList);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}

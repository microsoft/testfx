// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for MSTEST0058 to remove one of the conflicting parallelization attributes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveConflictingParallelizationAttributeFixer))]
[Shared]
public sealed class RemoveConflictingParallelizationAttributeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.DoNotUseParallelizeAndDoNotParallelizeTogetherRuleId);

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
            // Find the attribute syntax node at the diagnostic location
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
            
            if (node is not AttributeSyntax attributeSyntax)
            {
                continue;
            }

            // Determine which attribute this is
            string? attributeName = attributeSyntax.Name.ToString();
            if (attributeName is null)
            {
                continue;
            }

            // Normalize the attribute name (handle both short and full names)
            bool isParallelizeAttribute = attributeName.Contains("Parallelize") && !attributeName.Contains("DoNotParallelize");
            bool isDoNotParallelizeAttribute = attributeName.Contains("DoNotParallelize");

            if (isParallelizeAttribute)
            {
                // This is [Parallelize] - offer to remove it
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.RemoveParallelizeAttributeFix,
                        createChangedDocument: c => RemoveAttributeAsync(context.Document, attributeSyntax, c),
                        equivalenceKey: nameof(RemoveConflictingParallelizationAttributeFixer) + "_RemoveParallelize"),
                    diagnostic);
            }
            else if (isDoNotParallelizeAttribute)
            {
                // This is [DoNotParallelize] - offer to remove it
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.RemoveDoNotParallelizeAttributeFix,
                        createChangedDocument: c => RemoveAttributeAsync(context.Document, attributeSyntax, c),
                        equivalenceKey: nameof(RemoveConflictingParallelizationAttributeFixer) + "_RemoveDoNotParallelize"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> RemoveAttributeAsync(Document document, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        // Find the AttributeListSyntax that contains this attribute
        if (attributeSyntax.Parent is not AttributeListSyntax attributeList)
        {
            return document;
        }

        SyntaxNode nodeToRemove;
        
        // If this is the only attribute in the list, remove the entire attribute list
        if (attributeList.Attributes.Count == 1)
        {
            nodeToRemove = attributeList;
        }
        else
        {
            // Otherwise, just remove this specific attribute
            nodeToRemove = attributeSyntax;
        }

        SyntaxNode newRoot = root.RemoveNode(nodeToRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
        return document.WithSyntaxRoot(newRoot);
    }
}

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
/// Code fixer for <see cref="DuplicateDataRowAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuplicateDataRowFixer))]
[Shared]
public sealed class DuplicateDataRowFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.DuplicateDataRowRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);
        AttributeSyntax? attributeSyntax = diagnosticNode.FirstAncestorOrSelf<AttributeSyntax>();
        if (attributeSyntax is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.RemoveDuplicateDataRowFix,
                createChangedDocument: ct => RemoveDuplicateDataRowAsync(context.Document, attributeSyntax, ct),
                equivalenceKey: nameof(DuplicateDataRowFixer)),
            diagnostic);
    }

    private static async Task<Document> RemoveDuplicateDataRowAsync(Document document, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (attributeSyntax.Parent is not AttributeListSyntax attributeList)
        {
            return document;
        }

        SyntaxNode newRoot;
        if (attributeList.Attributes.Count == 1)
        {
            // Remove the entire attribute list if this is the only attribute in it
            newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.AddElasticMarker)!;
        }
        else
        {
            // Remove only this attribute from the list
            AttributeListSyntax newAttributeList = attributeList.RemoveNode(attributeSyntax, SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.AddElasticMarker)!;
            newRoot = root.ReplaceNode(attributeList, newAttributeList);
        }

        return document.WithSyntaxRoot(newRoot);
    }
}

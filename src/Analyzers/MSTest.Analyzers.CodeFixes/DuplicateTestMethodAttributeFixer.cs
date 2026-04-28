// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="DuplicateTestMethodAttributeAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuplicateTestMethodAttributeFixer))]
[Shared]
public sealed class DuplicateTestMethodAttributeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.DuplicateTestMethodAttributeRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken identifierToken = root.FindToken(diagnosticSpan.Start);
        if (identifierToken.Parent is null)
        {
            return;
        }

        MethodDeclarationSyntax? methodDeclaration = identifierToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.RemoveDuplicateTestMethodAttributeFix,
                createChangedDocument: c => RemoveDuplicateAttributesAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(DuplicateTestMethodAttributeFixer)),
            diagnostic);
    }

    private static async Task<Document> RemoveDuplicateAttributesAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
            out INamedTypeSymbol? testMethodAttributeSymbol))
        {
            return document;
        }

        var nodesToRemove = new List<SyntaxNode>();
        bool foundFirst = false;

        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            var duplicatesInList = new List<AttributeSyntax>();

            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(attribute, cancellationToken);
                ISymbol? attributeSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                INamedTypeSymbol? attributeTypeSymbol = attributeSymbol?.ContainingType;

                if (attributeTypeSymbol is not null && attributeTypeSymbol.Inherits(testMethodAttributeSymbol))
                {
                    if (!foundFirst)
                    {
                        foundFirst = true;
                    }
                    else
                    {
                        duplicatesInList.Add(attribute);
                    }
                }
            }

            if (duplicatesInList.Count == 0)
            {
                continue;
            }

            // If every attribute in this list is a duplicate, remove the whole list
            if (duplicatesInList.Count == attributeList.Attributes.Count)
            {
                nodesToRemove.Add(attributeList);
            }
            else
            {
                nodesToRemove.AddRange(duplicatesInList);
            }
        }

        if (nodesToRemove.Count == 0)
        {
            return document;
        }

        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot = root.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia)!;

        return document.WithSyntaxRoot(newRoot);
    }
}

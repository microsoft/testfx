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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="DoNotUseSystemDescriptionAttributeAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DoNotUseSystemDescriptionAttributeFixer))]
[Shared]
public sealed class DoNotUseSystemDescriptionAttributeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.DoNotUseSystemDescriptionAttributeRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxToken syntaxToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        MethodDeclarationSyntax? methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseMSTestDescriptionAttributeInsteadFix,
                createChangedDocument: c => ReplaceWithMSTestDescriptionAttributeAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(DoNotUseSystemDescriptionAttributeFixer)),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithMSTestDescriptionAttributeAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        INamedTypeSymbol? systemDescriptionAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemDescriptionAttribute);

        if (systemDescriptionAttributeSymbol is null)
        {
            return document;
        }

        AttributeSyntax? systemDescriptionAttribute = null;

        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol { ContainingType: { } containingType }
                    && SymbolEqualityComparer.Default.Equals(containingType, systemDescriptionAttributeSymbol))
                {
                    systemDescriptionAttribute = attribute;
                    break;
                }
            }

            if (systemDescriptionAttribute is not null)
            {
                break;
            }
        }

        if (systemDescriptionAttribute is null)
        {
            return document;
        }

        // Replace the System.ComponentModel.Description attribute name with the MSTest Description attribute name.
        // Since the MSTest namespace (Microsoft.VisualStudio.TestTools.UnitTesting) is already in scope,
        // we can use just the simple name "Description" which will resolve to MSTest's DescriptionAttribute.
        AttributeSyntax newAttribute = systemDescriptionAttribute.WithName(
            SyntaxFactory.IdentifierName("Description")
                .WithTriviaFrom(systemDescriptionAttribute.Name));

        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        return document.WithSyntaxRoot(root.ReplaceNode(systemDescriptionAttribute, newAttribute));
    }
}

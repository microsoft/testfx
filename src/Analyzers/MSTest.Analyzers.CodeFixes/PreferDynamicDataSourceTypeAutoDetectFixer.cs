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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="PreferDynamicDataSourceTypeAutoDetectAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferDynamicDataSourceTypeAutoDetectFixer))]
[Shared]
public sealed class PreferDynamicDataSourceTypeAutoDetectFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferDynamicDataSourceTypeAutoDetectRuleId);

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

        if (root.FindNode(diagnosticSpan) is not AttributeSyntax attributeSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.RemoveDynamicDataSourceTypeFix,
                ct => RemoveDynamicDataSourceTypeAsync(context.Document, attributeSyntax, ct),
                nameof(PreferDynamicDataSourceTypeAutoDetectFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> RemoveDynamicDataSourceTypeAsync(Document document, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol))
        {
            return document;
        }

        AttributeArgumentListSyntax? argumentList = attributeSyntax.ArgumentList;
        if (argumentList is null)
        {
            return document;
        }

        // Find and remove the DynamicDataSourceType argument
        List<AttributeArgumentSyntax> newArguments = [];
        
        foreach (AttributeArgumentSyntax argument in argumentList.Arguments)
        {
            // Check if this argument is a DynamicDataSourceType
            bool isDynamicDataSourceTypeArgument = false;

            if (argument.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Handle DynamicDataSourceType.Property or DynamicDataSourceType.Method
                TypeInfo typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken);
                if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dynamicDataSourceTypeSymbol))
                {
                    isDynamicDataSourceTypeArgument = true;
                }
            }
            else if (argument.Expression is IdentifierNameSyntax identifierName)
            {
                // Handle cases where the enum is used without full qualification
                TypeInfo typeInfo = semanticModel.GetTypeInfo(identifierName, cancellationToken);
                if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dynamicDataSourceTypeSymbol))
                {
                    isDynamicDataSourceTypeArgument = true;
                }
            }

            // Only keep arguments that are not DynamicDataSourceType
            if (!isDynamicDataSourceTypeArgument)
            {
                newArguments.Add(argument);
            }
        }

        // Create new attribute with remaining arguments
        AttributeSyntax newAttribute;
        if (newArguments.Count == 0)
        {
            // If no arguments remain, remove the argument list entirely
            newAttribute = attributeSyntax.WithArgumentList(null);
        }
        else
        {
            // Create new argument list with remaining arguments
            var newArgumentList = SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(newArguments));
            newAttribute = attributeSyntax.WithArgumentList(newArgumentList);
        }

        editor.ReplaceNode(attributeSyntax, newAttribute);

        return editor.GetChangedDocument();
    }
}
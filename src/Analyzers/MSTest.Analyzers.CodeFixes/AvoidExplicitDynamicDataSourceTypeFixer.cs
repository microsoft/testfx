// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

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
/// Code fixer for <see cref="AvoidExplicitDynamicDataSourceTypeAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidExplicitDynamicDataSourceTypeFixer))]
[Shared]
public sealed class AvoidExplicitDynamicDataSourceTypeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidExplicitDynamicDataSourceTypeRuleId);

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

        if (root.FindNode(diagnosticSpan) is not AttributeSyntax attributeSyntax ||
            attributeSyntax.ArgumentList is not AttributeArgumentListSyntax argumentList)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.RemoveDynamicDataSourceTypeFix,
                ct => RemoveDynamicDataSourceTypeAsync(context.Document, argumentList, ct),
                nameof(AvoidExplicitDynamicDataSourceTypeFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> RemoveDynamicDataSourceTypeAsync(Document document, AttributeArgumentListSyntax argumentList, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol))
        {
            return document;
        }

        foreach (AttributeArgumentSyntax argument in argumentList.Arguments)
        {
            Microsoft.CodeAnalysis.TypeInfo typeInfo = semanticModel.GetTypeInfo(argument.Expression, cancellationToken);
            if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dynamicDataSourceTypeSymbol))
            {
                editor.ReplaceNode(argumentList, argumentList.WithArguments(argumentList.Arguments.Remove(argument)));
                break;
            }
        }

        return editor.GetChangedDocument();
    }
}

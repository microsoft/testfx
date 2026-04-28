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
                title: CodeFixResources.UseTestMethodDisplayNameInsteadOfDescriptionAttributeFix,
                createChangedDocument: c => ReplaceDescriptionAttributeAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(DoNotUseSystemDescriptionAttributeFixer)),
            diagnostic);
    }

    private static async Task<Document> ReplaceDescriptionAttributeAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        INamedTypeSymbol? testMethodAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
        INamedTypeSymbol? descriptionAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemDescriptionAttribute);

        if (testMethodAttributeSymbol is null || descriptionAttributeSymbol is null)
        {
            return document;
        }

        AttributeSyntax? descriptionAttribute = null;
        AttributeSyntax? testMethodAttribute = null;

        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol { ContainingType: { } containingType })
                {
                    if (SymbolEqualityComparer.Default.Equals(containingType, descriptionAttributeSymbol))
                    {
                        descriptionAttribute = attribute;
                    }
                    else if (IsOrInheritsFrom(containingType, testMethodAttributeSymbol))
                    {
                        testMethodAttribute = attribute;
                    }
                }
            }
        }

        if (descriptionAttribute is null || testMethodAttribute is null)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Add DisplayName = "text" to the [TestMethod] attribute (only if it doesn't already have DisplayName)
        bool hasDisplayName = testMethodAttribute.ArgumentList?.Arguments.Any(
            a => a.NameEquals?.Name.Identifier.ValueText == "DisplayName") == true;

        if (!hasDisplayName && descriptionAttribute.ArgumentList?.Arguments.Count > 0)
        {
            ExpressionSyntax descriptionExpression = descriptionAttribute.ArgumentList.Arguments[0].Expression;

            AttributeArgumentSyntax displayNameArg = SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("DisplayName")),
                nameColon: null,
                descriptionExpression);

            AttributeSyntax newTestMethodAttribute = testMethodAttribute.ArgumentList is null
                ? testMethodAttribute.WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(displayNameArg)))
                : testMethodAttribute.WithArgumentList(
                    testMethodAttribute.ArgumentList.AddArguments(displayNameArg));

            editor.ReplaceNode(testMethodAttribute, newTestMethodAttribute);
        }

        // Remove the [Description] attribute
        if (descriptionAttribute.Parent is AttributeListSyntax containingAttributeList)
        {
            if (containingAttributeList.Attributes.Count == 1)
            {
                // Remove the entire attribute list
                editor.RemoveNode(containingAttributeList);
            }
            else
            {
                // Remove just the attribute from the list
                editor.ReplaceNode(
                    containingAttributeList,
                    containingAttributeList.RemoveNode(descriptionAttribute, SyntaxRemoveOptions.KeepLeadingTrivia)!);
            }
        }

        return editor.GetChangedDocument();
    }

    private static bool IsOrInheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        INamedTypeSymbol? current = type;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}

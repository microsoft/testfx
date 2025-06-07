// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

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
                await RegisterCSharpCodeFixesAsync(context, root!, diagnosticNode).ConfigureAwait(false);
            }
            else if (context.Document.Project.Language == LanguageNames.VisualBasic)
            {
                await RegisterVisualBasicCodeFixesAsync(context, root!, diagnosticNode).ConfigureAwait(false);
            }
        }
    }

    private static async Task RegisterCSharpCodeFixesAsync(CodeFixContext context, SyntaxNode root, SyntaxNode diagnosticNode)
    {
        if (diagnosticNode is not Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax attributeSyntax)
        {
            return;
        }

        // Check if the method also has TestMethod attribute
        var methodDeclaration = attributeSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        bool hasTestMethodAttribute = false;
        Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax? testMethodAttribute = null;

        foreach (var attributeList in methodDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol attributeSymbol)
                {
                    string attributeTypeName = attributeSymbol.ContainingType.ToDisplayString();
                    if (attributeTypeName == WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute)
                    {
                        hasTestMethodAttribute = true;
                        testMethodAttribute = attribute;
                        break;
                    }
                }
            }
            if (hasTestMethodAttribute)
            {
                break;
            }
        }

        if (hasTestMethodAttribute)
        {
            // Both attributes exist - remove DataTestMethod
            var action = CodeAction.Create(
                title: CodeFixResources.RemoveDataTestMethodAttributeTitle,
                createChangedDocument: c => RemoveAttributeAsync(context.Document, root, attributeSyntax, c),
                equivalenceKey: CodeFixResources.RemoveDataTestMethodAttributeTitle);

            context.RegisterCodeFix(action, context.Diagnostics);
        }
        else
        {
            // Only DataTestMethod exists - replace with TestMethod
            var action = CodeAction.Create(
                title: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle,
                createChangedDocument: c => ReplaceDataTestMethodAsync(context.Document, root, attributeSyntax, c),
                equivalenceKey: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle);

            context.RegisterCodeFix(action, context.Diagnostics);
        }
    }

    private static async Task RegisterVisualBasicCodeFixesAsync(CodeFixContext context, SyntaxNode root, SyntaxNode diagnosticNode)
    {
        if (diagnosticNode is not Microsoft.CodeAnalysis.VisualBasic.Syntax.AttributeSyntax attributeSyntax)
        {
            return;
        }

        // Check if the method also has TestMethod attribute
        var methodDeclaration = attributeSyntax.FirstAncestorOrSelf<MethodStatementSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        bool hasTestMethodAttribute = false;

        foreach (var attributeList in methodDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol attributeSymbol)
                {
                    string attributeTypeName = attributeSymbol.ContainingType.ToDisplayString();
                    if (attributeTypeName == WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute)
                    {
                        hasTestMethodAttribute = true;
                        break;
                    }
                }
            }
            if (hasTestMethodAttribute)
            {
                break;
            }
        }

        if (hasTestMethodAttribute)
        {
            // Both attributes exist - remove DataTestMethod
            var action = CodeAction.Create(
                title: CodeFixResources.RemoveDataTestMethodAttributeTitle,
                createChangedDocument: c => RemoveAttributeAsync(context.Document, root, attributeSyntax, c),
                equivalenceKey: CodeFixResources.RemoveDataTestMethodAttributeTitle);

            context.RegisterCodeFix(action, context.Diagnostics);
        }
        else
        {
            // Only DataTestMethod exists - replace with TestMethod
            var action = CodeAction.Create(
                title: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle,
                createChangedDocument: c => ReplaceDataTestMethodAsync(context.Document, root, attributeSyntax, c),
                equivalenceKey: CodeFixResources.ReplaceDataTestMethodWithTestMethodTitle);

            context.RegisterCodeFix(action, context.Diagnostics);
        }
    }

    private static Task<Document> RemoveAttributeAsync(Document document, SyntaxNode root, SyntaxNode attributeSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode newRoot;

        if (document.Project.Language == LanguageNames.CSharp)
        {
            var csAttributeSyntax = (Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax)attributeSyntax;
            var attributeList = csAttributeSyntax.Parent as Microsoft.CodeAnalysis.CSharp.Syntax.AttributeListSyntax;

            if (attributeList?.Attributes.Count == 1)
            {
                // Remove the entire attribute list if it only contains this attribute
                newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia);
            }
            else
            {
                // Remove only this attribute from the list
                newRoot = root.RemoveNode(csAttributeSyntax, SyntaxRemoveOptions.KeepNoTrivia);
            }
        }
        else
        {
            var vbAttributeSyntax = (Microsoft.CodeAnalysis.VisualBasic.Syntax.AttributeSyntax)attributeSyntax;
            var attributeList = vbAttributeSyntax.Parent as Microsoft.CodeAnalysis.VisualBasic.Syntax.AttributeListSyntax;

            if (attributeList?.Attributes.Count == 1)
            {
                // Remove the entire attribute list if it only contains this attribute
                newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia);
            }
            else
            {
                // Remove only this attribute from the list
                newRoot = root.RemoveNode(vbAttributeSyntax, SyntaxRemoveOptions.KeepNoTrivia);
            }
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot!));
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
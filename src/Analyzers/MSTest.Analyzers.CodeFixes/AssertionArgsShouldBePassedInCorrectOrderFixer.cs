// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertionArgsShouldBePassedInCorrectOrderFixer))]
[Shared]
public sealed class AssertionArgsShouldBePassedInCorrectOrderFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssertionArgsShouldBePassedInCorrectOrderRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode node = root.FindNode(context.Span);
        if (node == null)
        {
            return;
        }

        if (context.Diagnostics.Any(d => !d.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey)))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.FixAssertionArgsOrder,
                    ct => FixAssertionArgsOrderAsync(context.Document, root, node, isParameterLess: false, shouldBeStatic: true, ct),
                    nameof(AssertionArgsShouldBePassedInCorrectOrderFixer)),
                context.Diagnostics);
        }
    }

    public static async Task<Solution> FixAssertionArgsOrderAsync(Document document, SyntaxNode root, SyntaxNode node,
        bool isParameterLess, bool shouldBeStatic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document.Project.Solution;
        }

        var methodSymbol = (IMethodSymbol?)semanticModel.GetDeclaredSymbol(node, cancellationToken);
        if (methodSymbol is null)
        {
            return document.Project.Solution;
        }

        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);
        var syntaxGenerator = SyntaxGenerator.GetGenerator(document);

        SyntaxNode fixedMethodDeclarationNode = syntaxGenerator.MethodDeclaration(
            methodSymbol.Name,
            GetParameters(syntaxGenerator, isParameterLess, wellKnownTypeProvider),
            typeParameters: null,
            GetReturnType(syntaxGenerator, methodSymbol, wellKnownTypeProvider),
            Accessibility.Public,
            GetModifiers(methodSymbol, shouldBeStatic),
            GetStatements(node, syntaxGenerator));

        // Copy the attributes from the old method to the new method.
        fixedMethodDeclarationNode = syntaxGenerator.AddAttributes(fixedMethodDeclarationNode, syntaxGenerator.GetAttributes(node));

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclarationNode)).Project.Solution;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodFixer
{
    private const SyntaxNode? VoidReturnTypeNode = null;

    public static async Task<Solution> FixSignatureAsync(Document document, SyntaxNode root, SyntaxNode node,
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

    private static IEnumerable<SyntaxNode> GetStatements(SyntaxNode node, SyntaxGenerator syntaxGenerator)
        => syntaxGenerator.GetStatements(node)
            .Where(x => !x.IsKind(SyntaxKind.ReturnStatement) && !x.IsKind(SyntaxKind.YieldReturnStatement));

    private static DeclarationModifiers GetModifiers(IMethodSymbol methodSymbol, bool shouldBeStatic)
    {
        DeclarationModifiers newModifiers = methodSymbol.IsAsync
            ? DeclarationModifiers.Async
            : DeclarationModifiers.None;

        return newModifiers.WithIsStatic(shouldBeStatic);
    }

    private static SyntaxNode? GetReturnType(SyntaxGenerator syntaxGenerator, IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1)))
        {
            return syntaxGenerator.IdentifierName("ValueTask");
        }

        if (methodSymbol.IsAsync
            || SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1)))
        {
            return syntaxGenerator.IdentifierName("Task");
        }

        // For all other cases return void.
        return VoidReturnTypeNode;
    }

    private static IEnumerable<SyntaxNode> GetParameters(SyntaxGenerator syntaxGenerator, bool isParameterLess,
        WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (isParameterLess
            || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext,
                out INamedTypeSymbol? testContextTypeSymbol))
        {
            return Enumerable.Empty<SyntaxNode>();
        }

        SyntaxNode testContextType = syntaxGenerator.TypeExpression(testContextTypeSymbol);
        SyntaxNode testContextParameter = syntaxGenerator.ParameterDeclaration("testContext", testContextType);
        return new[] { testContextParameter };
    }
}

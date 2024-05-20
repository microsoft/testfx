// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodFixer
{
    private const SyntaxNode? VoidReturnTypeNode = null;

    public static async Task<Solution> FixSignatureAsync(Document document, SyntaxNode root, SyntaxNode node,
        FixtureMethodSignatureChanges fixesToApply, CancellationToken cancellationToken)
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
            GetParameters(fixesToApply, syntaxGenerator, wellKnownTypeProvider),
            typeParameters: null,
            GetReturnType(fixesToApply, syntaxGenerator, methodSymbol, wellKnownTypeProvider),
            GetAccessibility(fixesToApply, methodSymbol),
            GetModifiers(fixesToApply, methodSymbol),
            syntaxGenerator.GetStatements(node));

        // Copy the attributes from the old method to the new method.
        fixedMethodDeclarationNode = syntaxGenerator.AddAttributes(fixedMethodDeclarationNode, syntaxGenerator.GetAttributes(node));

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclarationNode)).Project.Solution;
    }

    private static DeclarationModifiers GetModifiers(FixtureMethodSignatureChanges fixesToApply, IMethodSymbol methodSymbol)
    {
        var currentModifiers = DeclarationModifiers.From(methodSymbol);

        return fixesToApply.HasFlag(FixtureMethodSignatureChanges.MakeStatic)
            ? currentModifiers.WithIsStatic(true)
            : fixesToApply.HasFlag(FixtureMethodSignatureChanges.RemoveStatic)
                ? currentModifiers.WithIsStatic(false)
                : currentModifiers;
    }

    private static Accessibility GetAccessibility(FixtureMethodSignatureChanges fixesToApply, IMethodSymbol methodSymbol)
        => fixesToApply.HasFlag(FixtureMethodSignatureChanges.MakePublic)
            ? Accessibility.Public
            : methodSymbol.DeclaredAccessibility;

    private static SyntaxNode? GetReturnType(FixtureMethodSignatureChanges fixesToApply, SyntaxGenerator syntaxGenerator,
        IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (fixesToApply.HasFlag(FixtureMethodSignatureChanges.FixAsyncVoid))
        {
            return syntaxGenerator.IdentifierName("Task");
        }

        if (fixesToApply.HasFlag(FixtureMethodSignatureChanges.FixReturnType))
        {
            if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1)))
            {
                return syntaxGenerator.IdentifierName("Task");
            }

            if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1)))
            {
                return syntaxGenerator.IdentifierName("ValueTask");
            }
        }

        return VoidReturnTypeNode;
    }

    private static IEnumerable<SyntaxNode> GetParameters(FixtureMethodSignatureChanges fixesToApply, SyntaxGenerator syntaxGenerator,
        WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (!fixesToApply.HasFlag(FixtureMethodSignatureChanges.AddTestContextParameter)
            || wellKnownTypeProvider is null
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

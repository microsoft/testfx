// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodFixer
{
    public static async Task<Document> FixSignatureAsync(Document document, SyntaxNode root, SyntaxNode node,
        bool isParameterLess, bool shouldBeStatic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        var methodSymbol = (IMethodSymbol?)semanticModel.GetDeclaredSymbol(node, cancellationToken);
        if (methodSymbol is null)
        {
            return document;
        }

        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);
        var syntaxGenerator = SyntaxGenerator.GetGenerator(document);

        SyntaxNode fixedMethodDeclarationNode = node;
        fixedMethodDeclarationNode = syntaxGenerator.WithAccessibility(fixedMethodDeclarationNode, Accessibility.Public);
        fixedMethodDeclarationNode = syntaxGenerator.WithTypeParameters(fixedMethodDeclarationNode);
        fixedMethodDeclarationNode = UpdateModifiers(syntaxGenerator, fixedMethodDeclarationNode, shouldBeStatic);

        fixedMethodDeclarationNode = ((MethodDeclarationSyntax)fixedMethodDeclarationNode)
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(
                        GetParameters(syntaxGenerator, isParameterLess, wellKnownTypeProvider))))
            .WithReturnType(GetReturnType(syntaxGenerator, methodSymbol, wellKnownTypeProvider));

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclarationNode));
    }

    private static SyntaxNode UpdateModifiers(SyntaxGenerator generator, SyntaxNode declaration, bool shouldBeStatic)
    {
        DeclarationModifiers oldModifiers = generator.GetModifiers(declaration);
        DeclarationModifiers newModifiers = oldModifiers.WithIsStatic(shouldBeStatic).WithIsAbstract(false);

        declaration = generator.WithModifiers(declaration, newModifiers);
        if (oldModifiers.IsAbstract)
        {
            // This will remove the semicolon from the method declaration, and replace it with braces.
            declaration = generator.WithStatements(declaration, []);
        }

        return declaration;
    }

    private static TypeSyntax GetReturnType(SyntaxGenerator syntaxGenerator, IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1)))
        {
            return (TypeSyntax)syntaxGenerator.IdentifierName("ValueTask");
        }

        if (methodSymbol.IsAsync
            || SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1)))
        {
            return (TypeSyntax)syntaxGenerator.IdentifierName("Task");
        }

        // For all other cases return void.
        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static IEnumerable<SyntaxNode> GetParameters(SyntaxGenerator syntaxGenerator, bool isParameterLess,
        WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (isParameterLess
            || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext,
                out INamedTypeSymbol? testContextTypeSymbol))
        {
            return [];
        }

        SyntaxNode testContextType = syntaxGenerator.TypeExpression(testContextTypeSymbol);
        SyntaxNode testContextParameter = syntaxGenerator.ParameterDeclaration("testContext", testContextType);
        return [testContextParameter];
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodFixer
{
    public static async Task<Solution> FixSignatureAsync(Document document, SyntaxNode root, SyntaxNode node,
        bool isParameterLess, bool shouldBeStatic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Cast to MethodDeclarationSyntax to preserve method body and trivia
        if (node is not MethodDeclarationSyntax methodDeclaration)
        {
            return document.Project.Solution;
        }

        SemanticModel? semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        var methodSymbol = (IMethodSymbol?)semanticModel.GetDeclaredSymbol(node, cancellationToken);
        if (methodSymbol is null)
        {
            return document.Project.Solution;
        }

        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);

        // Start with the original method declaration to preserve trivia and body
        MethodDeclarationSyntax fixedMethodDeclaration = methodDeclaration;

        // Update parameters
        ParameterListSyntax newParameterList = GetParameterList(isParameterLess, wellKnownTypeProvider);
        fixedMethodDeclaration = fixedMethodDeclaration.WithParameterList(newParameterList);

        // Update return type
        TypeSyntax? newReturnType = GetReturnType(methodSymbol, wellKnownTypeProvider);
        if (newReturnType is not null)
        {
            fixedMethodDeclaration = fixedMethodDeclaration.WithReturnType(newReturnType);
        }

        // Update modifiers (accessibility and static)
        SyntaxTokenList newModifiers = GetModifiers(methodDeclaration, methodSymbol, shouldBeStatic);
        fixedMethodDeclaration = fixedMethodDeclaration.WithModifiers(newModifiers);

        // Remove type parameters if any
        if (fixedMethodDeclaration.TypeParameterList is not null)
        {
            fixedMethodDeclaration = fixedMethodDeclaration.WithTypeParameterList(null);
        }

        // If the method is abstract (no body), add an empty body
        if (fixedMethodDeclaration.Body is null)
        {
            fixedMethodDeclaration = fixedMethodDeclaration
                .WithBody(SyntaxFactory.Block())
                .WithSemicolonToken(default);
        }
        else
        {
            // Remove return and yield return statements from body if needed
            SyntaxList<StatementSyntax> statements = fixedMethodDeclaration.Body.Statements;
            IEnumerable<StatementSyntax> filteredStatements = statements
                .Where(x => !x.IsKind(SyntaxKind.ReturnStatement) && !x.IsKind(SyntaxKind.YieldReturnStatement));

            if (statements.Count != filteredStatements.Count())
            {
                fixedMethodDeclaration = fixedMethodDeclaration.WithBody(
                    fixedMethodDeclaration.Body.WithStatements(SyntaxFactory.List(filteredStatements)));
            }
        }

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclaration)).Project.Solution;
    }

    private static SyntaxTokenList GetModifiers(MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, bool shouldBeStatic)
    {
        // Start with existing modifiers
        SyntaxTokenList modifiers = methodDeclaration.Modifiers;

        // Remove all accessibility modifiers and abstract modifier
        modifiers = SyntaxFactory.TokenList(modifiers.Where(m =>
            !m.IsKind(SyntaxKind.PublicKeyword) &&
            !m.IsKind(SyntaxKind.PrivateKeyword) &&
            !m.IsKind(SyntaxKind.ProtectedKeyword) &&
            !m.IsKind(SyntaxKind.InternalKeyword) &&
            !m.IsKind(SyntaxKind.AbstractKeyword)));

        // Handle static modifier
        bool hasStaticModifier = modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        if (shouldBeStatic && !hasStaticModifier)
        {
            modifiers = modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        }
        else if (!shouldBeStatic && hasStaticModifier)
        {
            modifiers = SyntaxFactory.TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword)));
        }

        // Add public at the beginning (before static if present)
        modifiers = modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        return modifiers;
    }

    private static TypeSyntax? GetReturnType(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1)))
        {
            return SyntaxFactory.IdentifierName("ValueTask");
        }

        if (methodSymbol.IsAsync
            || SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1)))
        {
            return SyntaxFactory.IdentifierName("Task");
        }

        // For void, return a predefined void type
        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static ParameterListSyntax GetParameterList(bool isParameterLess, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (isParameterLess
            || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext,
                out INamedTypeSymbol? testContextTypeSymbol))
        {
            return SyntaxFactory.ParameterList();
        }

        // Use simple name "TestContext" instead of fully qualified name
        TypeSyntax testContextType = SyntaxFactory.IdentifierName("TestContext");
        ParameterSyntax testContextParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("testContext"))
            .WithType(testContextType);

        return SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(testContextParameter));
    }
}

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
        MethodDeclarationSyntax fixedMethodDeclaration = methodDeclaration
            .WithParameterList(GetParameterList(isParameterLess, wellKnownTypeProvider))
            .WithReturnType(GetReturnType(methodSymbol, wellKnownTypeProvider))
            .WithModifiers(GetModifiers(methodDeclaration, shouldBeStatic));

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
        else if (fixedMethodDeclaration.Body.Statements.Any(s => s.IsKind(SyntaxKind.ReturnStatement) || s.IsKind(SyntaxKind.YieldReturnStatement)))
        {
            // Remove return and yield return statements from body
            SyntaxList<StatementSyntax> filteredStatements = SyntaxFactory.List(
                fixedMethodDeclaration.Body.Statements
                    .Where(s => !s.IsKind(SyntaxKind.ReturnStatement) && !s.IsKind(SyntaxKind.YieldReturnStatement)));

            fixedMethodDeclaration = fixedMethodDeclaration.WithBody(
                fixedMethodDeclaration.Body.WithStatements(filteredStatements));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclaration)).Project.Solution;
    }

    private static SyntaxTokenList GetModifiers(MethodDeclarationSyntax methodDeclaration, bool shouldBeStatic)
    {
        // Remove all accessibility modifiers, abstract modifier, and static modifier
        SyntaxTokenList modifiers = SyntaxFactory.TokenList(
            methodDeclaration.Modifiers.Where(m =>
                !m.IsKind(SyntaxKind.PublicKeyword) &&
                !m.IsKind(SyntaxKind.PrivateKeyword) &&
                !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                !m.IsKind(SyntaxKind.InternalKeyword) &&
                !m.IsKind(SyntaxKind.AbstractKeyword) &&
                !m.IsKind(SyntaxKind.StaticKeyword)));

        // Build new modifier list: public [static] <remaining modifiers>
        if (shouldBeStatic)
        {
            return SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddRange(modifiers);
        }

        // Non-static method
        return SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddRange(modifiers);
    }

    private static TypeSyntax GetReturnType(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
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

        // Default to void
        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static ParameterListSyntax GetParameterList(bool isParameterLess, WellKnownTypeProvider wellKnownTypeProvider)
    {
        if (isParameterLess
            || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext,
                out _))
        {
            return SyntaxFactory.ParameterList();
        }

        ParameterSyntax testContextParameter = SyntaxFactory
            .Parameter(SyntaxFactory.Identifier("testContext"))
            .WithType(SyntaxFactory.IdentifierName("TestContext"));

        return SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(testContextParameter));
    }
}

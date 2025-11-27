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

        if (node is not MethodDeclarationSyntax methodDeclaration)
        {
            return document.Project.Solution;
        }

        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (semanticModel.GetDeclaredSymbol(node, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return document.Project.Solution;
        }

        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);

        MethodDeclarationSyntax fixedMethodDeclaration = methodDeclaration
            .WithParameterList(GetParameterList(isParameterLess, wellKnownTypeProvider))
            .WithReturnType(GetReturnType(methodSymbol, wellKnownTypeProvider))
            .WithModifiers(GetModifiers(methodDeclaration, shouldBeStatic))
            .WithTypeParameterList(null);

        if (fixedMethodDeclaration.Body is null)
        {
            fixedMethodDeclaration = fixedMethodDeclaration
                .WithBody(SyntaxFactory.Block())
                .WithSemicolonToken(default);
        }
        else
        {
            SyntaxList<StatementSyntax> statements = fixedMethodDeclaration.Body.Statements;
            IEnumerable<StatementSyntax> filteredStatements = statements
                .Where(s => !s.IsKind(SyntaxKind.ReturnStatement) && !s.IsKind(SyntaxKind.YieldReturnStatement));

            if (statements.Count != filteredStatements.Count())
            {
                fixedMethodDeclaration = fixedMethodDeclaration.WithBody(
                    fixedMethodDeclaration.Body.WithStatements(SyntaxFactory.List(filteredStatements)));
            }
        }

        return document.WithSyntaxRoot(root.ReplaceNode(node, fixedMethodDeclaration)).Project.Solution;
    }

    private static SyntaxTokenList GetModifiers(MethodDeclarationSyntax methodDeclaration, bool shouldBeStatic)
    {
        SyntaxTokenList modifiers = SyntaxFactory.TokenList(
            methodDeclaration.Modifiers.Where(m =>
                !m.IsKind(SyntaxKind.PublicKeyword) &&
                !m.IsKind(SyntaxKind.PrivateKeyword) &&
                !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                !m.IsKind(SyntaxKind.InternalKeyword) &&
                !m.IsKind(SyntaxKind.AbstractKeyword) &&
                !m.IsKind(SyntaxKind.StaticKeyword)));

        SyntaxTokenList result = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
        if (shouldBeStatic)
        {
            result = result.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        }

        return result.AddRange(modifiers);
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

        ParameterSyntax parameter = SyntaxFactory
            .Parameter(SyntaxFactory.Identifier("testContext"))
            .WithType(SyntaxFactory.IdentifierName("TestContext"));

        return SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameter));
    }
}

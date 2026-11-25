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
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="TestMethodShouldBeValidAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestMethodShouldBeValidFixer))]
[Shared]
public sealed class TestMethodShouldBeValidFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => [DiagnosticIds.TestMethodShouldBeValidRuleId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        if (diagnostic.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey))
        {
            return;
        }

        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        // Find the method declaration identified by the diagnostic.
        if (syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } methodDeclaration)
        {
            return;
        }

        SemanticModel semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol method)
        {
            return;
        }

        if (method.IsStatic || method.IsAbstract || method.IsVirtual || method.IsOverride || (method.IsAsync && method.ReturnsVoid))
        {
            return;
        }

        if (methodDeclaration.TypeParameterList is not null)
        {
            bool allInferable = method.TypeParameters.All(typeParameter =>
                method.Parameters.Any(parameter => TestMethodShouldBeValidAnalyzer.IsOrHasTypeParameter(parameter.Type, typeParameter)));
            if (!allInferable)
            {
                return;
            }
        }

        INamedTypeSymbol? taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
        INamedTypeSymbol? valueTaskSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
        if (!method.ReturnsVoid
            && !SymbolEqualityComparer.Default.Equals(method.ReturnType, taskSymbol)
            && !SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskSymbol))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.TestMethodShouldBeValidFix,
                createChangedSolution: c => FixTestMethodAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(TestMethodShouldBeValidFixer)),
            diagnostic);
    }

    private static async Task<Solution> FixTestMethodAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;

        // Ensure the method has public visibility.
        SyntaxToken publicModifier = newMethodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword));
        if (publicModifier == default)
        {
            IEnumerable<SyntaxToken> modifiersWithoutAccessModifiers = newMethodDeclaration.Modifiers.Where(m =>
                !m.IsKind(SyntaxKind.PrivateKeyword) &&
                !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                !m.IsKind(SyntaxKind.InternalKeyword));

            newMethodDeclaration = newMethodDeclaration.WithModifiers(
                new SyntaxTokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddRange(modifiersWithoutAccessModifiers));
        }

        // Apply changes.
        editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
        Document newDocument = editor.GetChangedDocument();
        return newDocument.Project.Solution;
    }
}

internal static class TypeSyntaxExtensions
{
    public static bool IsVoid(this TypeSyntax typeSyntax) => typeSyntax is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
}

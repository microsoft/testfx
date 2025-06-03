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
/// Code fixer for <see cref="TestClassShouldBeValidAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = nameof(TestClassShouldBeValidFixer))]
[Shared]
public sealed class TestClassShouldBeValidFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.TestClassShouldBeValidRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        TypeDeclarationSyntax declaration = syntaxToken.Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.TestClassShouldBeValidFix,
                createChangedDocument: c => FixClassDeclarationAsync(context.Document, declaration, c),
                equivalenceKey: nameof(TestClassShouldBeValidFixer)),
            diagnostic);
    }

    private static async Task<Document> FixClassDeclarationAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get the SemanticModel and Compilation
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        bool canDiscoverInternals = semanticModel.Compilation.CanDiscoverInternals();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Remove the static modifier if it exists
        SyntaxTokenList modifiers = SyntaxFactory.TokenList(
            typeDeclaration.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.StaticKeyword)));

        if (!typeDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            // Determine the visibility modifier
            SyntaxToken visibilityModifier = canDiscoverInternals
                ? SyntaxFactory.Token(SyntaxKind.InternalKeyword)
                : SyntaxFactory.Token(SyntaxKind.PublicKeyword);

            modifiers = SyntaxFactory.TokenList(
                modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.PrivateKeyword) && !modifier.IsKind(SyntaxKind.InternalKeyword))).Add(visibilityModifier);
        }

        // Create a new class declaration with the updated modifiers.
        TypeDeclarationSyntax newTypeDeclaration = typeDeclaration.WithModifiers(modifiers);
        editor.ReplaceNode(typeDeclaration, newTypeDeclaration);
        SyntaxNode newRoot = editor.GetChangedRoot();

        return document.WithSyntaxRoot(newRoot);
    }
}

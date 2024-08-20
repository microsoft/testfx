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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestContextShouldBeValidFixer))]
[Shared]
public sealed class TestContextShouldBeValidFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.TestContextShouldBeValidRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

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

        MemberDeclarationSyntax declaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.TestContextShouldBeValidFix,
                createChangedDocument: c => FixMemberDeclarationAsync(context.Document, declaration, c),
                equivalenceKey: nameof(TestContextShouldBeValidFixer)),
            diagnostic);
    }

    public static async Task<Document> FixMemberDeclarationAsync(Document document, MemberDeclarationSyntax memberDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get the SemanticModel and Compilation
        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("SemanticModel cannot be null.");
        bool canDiscoverInternals = semanticModel.Compilation.CanDiscoverInternals();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Remove the static and readonly modifiers if it exists
        SyntaxTokenList modifiers = SyntaxFactory.TokenList(
            memberDeclaration.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.StaticKeyword) && !modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));

        if (!memberDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            // Determine the visibility modifier
            SyntaxToken visibilityModifier = canDiscoverInternals
                ? SyntaxFactory.Token(SyntaxKind.InternalKeyword)
                : SyntaxFactory.Token(SyntaxKind.PublicKeyword);

            modifiers = SyntaxFactory.TokenList(
                modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.PrivateKeyword) && !modifier.IsKind(SyntaxKind.InternalKeyword) && !modifier.IsKind(SyntaxKind.ProtectedKeyword))).Add(visibilityModifier);
        }

        MemberDeclarationSyntax newMemberDeclaration = memberDeclaration.WithModifiers(modifiers);

        if (newMemberDeclaration is FieldDeclarationSyntax fieldDeclarationSyntax)
        {
            newMemberDeclaration = ConvertFieldToProperty(fieldDeclarationSyntax);
        }
        else
        {
            // ensure that the property has setter and getter
            var propertyDeclaration = (PropertyDeclarationSyntax)newMemberDeclaration;
            SyntaxList<AccessorDeclarationSyntax> accessors = propertyDeclaration.AccessorList?.Accessors ?? default;

            AccessorDeclarationSyntax getAccessor = accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration)
                ?? SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            AccessorDeclarationSyntax setAccessor = accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration)
                ?? SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            newMemberDeclaration = propertyDeclaration.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[] { getAccessor, setAccessor })));
        }

        // Create a new member declaration with the updated modifiers.
        editor.ReplaceNode(memberDeclaration, newMemberDeclaration);
        SyntaxNode newRoot = editor.GetChangedRoot();

        return document.WithSyntaxRoot(newRoot);
    }

    private static PropertyDeclarationSyntax ConvertFieldToProperty(FieldDeclarationSyntax fieldDeclaration)
    {
        TypeSyntax type = fieldDeclaration.Declaration.Type;
        VariableDeclaratorSyntax variableDeclarator = fieldDeclaration.Declaration.Variables.First();

        // Create the property declaration
        PropertyDeclarationSyntax propertyDeclaration = SyntaxFactory.PropertyDeclaration(type, variableDeclarator.Identifier.Text)
            .WithModifiers(SyntaxFactory.TokenList(fieldDeclaration.Modifiers))
            .WithAccessorList(SyntaxFactory.AccessorList(
                SyntaxFactory.List(new[]
                {
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                })));

        return propertyDeclaration;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PublicMethodShouldBeTestMethodFixer))]
[Shared]
public sealed class PublicMethodShouldBeTestMethodFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIds.PublicMethodShouldBeTestMethodRuleId);

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

        // Find the method declaration identified by the diagnostic.
        MethodDeclarationSyntax methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AddTestMethodAttributeFix,
                createChangedDocument: c => AddTestMethodAttributeAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(PublicMethodShouldBeTestMethodFixer) + "_AddTestMethod"),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.ChangeMethodAccessibilityToPrivateFix,
                createChangedDocument: c => ChangeMethodVisibilityAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(PublicMethodShouldBeTestMethodFixer) + "_ChangeVisibility"),
            diagnostic);
    }

    private static async Task<Document> AddTestMethodAttributeAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        SyntaxNode testMethodAttribute = editor.Generator.Attribute("TestMethod");

        // Add the TestMethod attribute to the method.
        editor.AddAttribute(methodDeclaration, testMethodAttribute);

        // Apply the changes and return the updated document.
        return editor.GetChangedDocument();
    }

    private static async Task<Document> ChangeMethodVisibilityAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SyntaxNode updatedMethodDeclaration = editor.Generator.WithAccessibility(methodDeclaration, Accessibility.Private);

        editor.ReplaceNode(methodDeclaration, updatedMethodDeclaration);
        return editor.GetChangedDocument();
    }
}

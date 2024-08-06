﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PublicTypeShouldBeTestClassFixer))]
[Shared]
public sealed class PublicTypeShouldBeTestClassFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PublicTypeShouldBeTestClassRuleId);

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

        // Find the type declaration identified by the diagnostic.
        TypeDeclarationSyntax declaration = syntaxToken.Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add [TestClass]",
                createChangedDocument: c => AddTestClassAttributeAsync(context.Document, declaration, c),
                equivalenceKey: nameof(PublicTypeShouldBeTestClassFixer)),
            diagnostic);
    }

    private static async Task<Document> AddTestClassAttributeAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeSyntax testClassAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("TestClass"));
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testClassAttribute));

        TypeDeclarationSyntax newTypeDecl = typeDecl.AddAttributeLists(attributeList);
        editor.ReplaceNode(typeDecl, newTypeDecl);

        SyntaxNode newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;

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
/// Code fixer for <see cref="PublicTypeShouldBeTestClassAnalyzer"/> and <see cref="TypeContainingTestMethodShouldBeATestClassAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTestClassFixer))]
[Shared]
public sealed class AddTestClassFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(
            DiagnosticIds.PublicTypeShouldBeTestClassRuleId,
            DiagnosticIds.TypeContainingTestMethodShouldBeATestClassRuleId);

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

        // Find the type declaration identified by the diagnostic.
        TypeDeclarationSyntax declaration = syntaxToken.Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

        // For structs and record structs, we need to change them to classes/record classes since [TestClass] cannot be applied to structs
        if (declaration is StructDeclarationSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.ChangeStructToClassAndAddTestClassFix,
                    createChangedDocument: c => ChangeStructToClassAndAddTestClassAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_ChangeStructToClass_{diagnostic.Id}"),
                diagnostic);
        }
        else if (declaration is RecordDeclarationSyntax recordDeclaration && IsRecordStruct(recordDeclaration))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.ChangeStructToClassAndAddTestClassFix,
                    createChangedDocument: c => ChangeRecordStructToRecordClassAndAddTestClassAttributeAsync(context.Document, recordDeclaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_ChangeRecordStructToClass_{diagnostic.Id}"),
                diagnostic);
        }
        else
        {
            // For classes and record classes, just add the [TestClass] attribute
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.AddTestClassFix,
                    createChangedDocument: c => AddTestClassAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_{diagnostic.Id}"),
                diagnostic);
        }
    }

    private static async Task<Document> AddTestClassAttributeAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeSyntax testClassAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("TestClass"));
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testClassAttribute));

        TypeDeclarationSyntax newTypeDeclaration = typeDeclaration.AddAttributeLists(attributeList);
        editor.ReplaceNode(typeDeclaration, newTypeDeclaration);

        SyntaxNode newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ChangeStructToClassAndAddTestClassAttributeAsync(Document document, TypeDeclarationSyntax structDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Create the [TestClass] attribute
        AttributeSyntax testClassAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("TestClass"));
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testClassAttribute));

        // Convert struct to class
        ClassDeclarationSyntax classDeclaration = SyntaxFactory.ClassDeclaration(structDeclaration.Identifier)
            .WithModifiers(structDeclaration.Modifiers)
            .WithTypeParameterList(structDeclaration.TypeParameterList)
            .WithConstraintClauses(structDeclaration.ConstraintClauses)
            .WithBaseList(structDeclaration.BaseList)
            .WithMembers(structDeclaration.Members)
            .WithAttributeLists(structDeclaration.AttributeLists.Add(attributeList))
            .WithLeadingTrivia(structDeclaration.GetLeadingTrivia())
            .WithTrailingTrivia(structDeclaration.GetTrailingTrivia());

        editor.ReplaceNode(structDeclaration, classDeclaration);

        SyntaxNode newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsRecordStruct(RecordDeclarationSyntax recordDeclaration)
    {
        // Check if the record has the 'struct' keyword
        return recordDeclaration.Modifiers.Any(SyntaxKind.StructKeyword);
    }

    private static async Task<Document> ChangeRecordStructToRecordClassAndAddTestClassAttributeAsync(Document document, RecordDeclarationSyntax recordStructDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Create the [TestClass] attribute
        AttributeSyntax testClassAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("TestClass"));
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testClassAttribute));

        // Convert record struct to record class by removing the 'struct' keyword and optionally adding 'class' keyword
        SyntaxTokenList newModifiers = SyntaxFactory.TokenList(
            recordStructDeclaration.Modifiers
                .Where(modifier => !modifier.IsKind(SyntaxKind.StructKeyword)));

        // Optionally add 'class' keyword if it's not already implicit
        bool hasClassKeyword = newModifiers.Any(SyntaxKind.ClassKeyword);
        if (!hasClassKeyword)
        {
            // For explicit record class syntax, add the 'class' keyword after other modifiers
            // Find the position after access modifiers but before 'record'
            int recordIndex = -1;
            for (int i = 0; i < newModifiers.Count; i++)
            {
                if (newModifiers[i].IsKind(SyntaxKind.RecordKeyword))
                {
                    recordIndex = i;
                    break;
                }
            }

            if (recordIndex >= 0)
            {
                SyntaxToken classToken = SyntaxFactory.Token(SyntaxKind.ClassKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space);
                newModifiers = newModifiers.Insert(recordIndex + 1, classToken);
            }
        }

        RecordDeclarationSyntax recordClassDeclaration = recordStructDeclaration
            .WithModifiers(newModifiers)
            .WithAttributeLists(recordStructDeclaration.AttributeLists.Add(attributeList));

        editor.ReplaceNode(recordStructDeclaration, recordClassDeclaration);

        SyntaxNode newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }
}

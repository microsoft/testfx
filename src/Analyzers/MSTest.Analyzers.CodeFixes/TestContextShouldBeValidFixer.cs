// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fix for <see cref="TestContextShouldBeValidAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = nameof(TestContextShouldBeValidFixer))]
[Shared]
public sealed class TestContextShouldBeValidFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.TestContextShouldBeValidRuleId);

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

        SyntaxNode declaration = syntaxToken.Parent.AncestorsAndSelf().FirstOrDefault(node => 
            SyntaxFacts.IsFieldDeclaration(node) || SyntaxFacts.IsPropertyDeclaration(node));
        
        if (declaration is null)
        {
            return;
        }

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.TestContextShouldBeValidFix,
                createChangedDocument: c => FixMemberDeclarationAsync(context.Document, declaration, c),
                equivalenceKey: nameof(TestContextShouldBeValidFixer)),
            diagnostic);
    }

    private static async Task<Document> FixMemberDeclarationAsync(Document document, SyntaxNode memberDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SyntaxGenerator generator = editor.Generator;

        if (SyntaxFacts.IsFieldDeclaration(memberDeclaration))
        {
            // Convert field to property
            SyntaxNode newProperty = ConvertFieldToProperty(generator, memberDeclaration);
            editor.ReplaceNode(memberDeclaration, newProperty);
        }
        else if (SyntaxFacts.IsPropertyDeclaration(memberDeclaration))
        {
            // Fix existing property
            SyntaxNode newProperty = FixProperty(generator, memberDeclaration);
            editor.ReplaceNode(memberDeclaration, newProperty);
        }

        return editor.GetChangedDocument();
    }

    private static SyntaxNode ConvertFieldToProperty(SyntaxGenerator generator, SyntaxNode fieldDeclaration)
    {
        var modifiers = generator.GetModifiers(fieldDeclaration);
        var type = generator.GetType(fieldDeclaration);
        
        // Remove static and readonly modifiers, add public if needed
        var newModifiers = DeclarationModifiers.None;
        if ((modifiers & DeclarationModifiers.Static) == 0)
        {
            newModifiers |= DeclarationModifiers.Public;
        }

        // Create auto-property with get and set
        return generator.PropertyDeclaration(
            TestContextShouldBeValidAnalyzer.TestContextPropertyName,
            type,
            accessibility: Accessibility.Public,
            getAccessorStatements: null, // Auto-property
            setAccessorStatements: null); // Auto-property
    }

    private static SyntaxNode FixProperty(SyntaxGenerator generator, SyntaxNode propertyDeclaration)
    {
        var modifiers = generator.GetModifiers(propertyDeclaration);
        var type = generator.GetType(propertyDeclaration);
        
        // Remove static modifier, ensure public accessibility
        var newModifiers = DeclarationModifiers.Public;

        // Ensure property has both getter and setter
        return generator.PropertyDeclaration(
            TestContextShouldBeValidAnalyzer.TestContextPropertyName,
            type,
            accessibility: Accessibility.Public,
            getAccessorStatements: null, // Auto-property
            setAccessorStatements: null); // Auto-property
    }
}

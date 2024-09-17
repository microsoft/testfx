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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassInitializeShouldBeValidFixer))]
[Shared]
public sealed class PreferTestCleanupOverDisposeFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferTestCleanupOverDisposeRuleId);

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
                CodeFixResources.ReplaceWithTestCleanuFix,
                c => ReplaceDisposeWithTestCleanupAsync(context.Document, methodDeclaration, c),
                nameof(TestMethodShouldBeValidCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ReplaceDisposeWithTestCleanupAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        if (methodDeclaration.Parent is not TypeDeclarationSyntax newParent)
        {
            return editor.OriginalDocument;
        }

        TypeDeclarationSyntax parentClass = newParent;

        // Create a new method with the same body but named "TestCleanup"
        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration
            .WithIdentifier(SyntaxFactory.Identifier("TestCleanup"))
            .WithAttributeLists(SyntaxFactory.SingletonList(CreateTestCleanupAttribute()));

        newParent = newParent.ReplaceNode(methodDeclaration, newMethodDeclaration);

        var newBaseTypes = newParent.BaseList?.Types
            .Where(type => !IsDisposableInterface(type))
            .ToList();

        if (newBaseTypes?.Count != 0)
        {
            // If other interfaces remain, replace the base list with updated interfaces
            newParent = newParent.WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(newBaseTypes)));
        }
        else
        {
            // If no interfaces left, remove the base list entirely
            newParent = newParent.WithBaseList(null);
        }

        editor.ReplaceNode(parentClass, newParent);

        return editor.GetChangedDocument();
    }

    private static bool IsDisposableInterface(BaseTypeSyntax baseTypeSyntax)
    {
        string typeName = baseTypeSyntax.Type.ToString();
        return typeName is "IDisposable" or "IAsyncDisposable";
    }

    private static AttributeListSyntax CreateTestCleanupAttribute() =>
        // [TestCleanup] attribute
        SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("TestCleanup"))));
}

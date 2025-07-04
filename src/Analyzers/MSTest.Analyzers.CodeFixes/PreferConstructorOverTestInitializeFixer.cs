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
/// Code fixer for the PreferConstructorOverTestInitialize rule.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassInitializeShouldBeValidFixer))]
[Shared]
public sealed class PreferConstructorOverTestInitializeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferConstructorOverTestInitializeRuleId);

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

        // Find the method declaration identified by the diagnostic.
        MethodDeclarationSyntax? methodDeclaration = syntaxToken.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.ReplaceWithConstructorFix,
                c => ReplaceTestInitializeWithConstructorAsync(context.Document, methodDeclaration, c),
                nameof(PreferConstructorOverTestInitializeFixer)),
            diagnostic);
    }

    private static async Task<Document> ReplaceTestInitializeWithConstructorAsync(Document document, MethodDeclarationSyntax testInitializeMethod, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Find the class containing the method
        if (testInitializeMethod.Parent is ClassDeclarationSyntax containingClass)
        {
            ConstructorDeclarationSyntax? existingConstructor = containingClass.Members
                 .OfType<ConstructorDeclarationSyntax>()
                 .FirstOrDefault(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword));

            // Move the body of the TestInitialize method
            BlockSyntax? testInitializeBody = testInitializeMethod.Body;

            if (existingConstructor != null)
            {
                StatementSyntax[]? testInitializeStatements = testInitializeBody?.Statements.ToArray();
                ConstructorDeclarationSyntax newConstructor;

                // If a constructor already exists, append the body of the TestInitialize method to it
                if (existingConstructor.Body != null)
                {
                    BlockSyntax newConstructorBody = existingConstructor.Body.AddStatements(testInitializeStatements ?? []);
                    newConstructor = existingConstructor.WithBody(newConstructorBody);
                }
                else
                {
                    newConstructor = existingConstructor.WithBody(testInitializeBody);
                }

                editor.ReplaceNode(existingConstructor, newConstructor);
            }
            else
            {
                // Create a new constructor with the TestInitialize body if one doesn't exist
                ConstructorDeclarationSyntax constructor = SyntaxFactory.ConstructorDeclaration(containingClass.Identifier)
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(testInitializeBody);

                editor.AddMember(containingClass, constructor);
            }

            // Remove the TestInitialize method
            editor.RemoveNode(testInitializeMethod);
        }

        return editor.GetChangedDocument();
    }
}

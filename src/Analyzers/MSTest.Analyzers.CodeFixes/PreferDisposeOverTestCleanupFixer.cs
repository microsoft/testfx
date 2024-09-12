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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferDisposeOverTestCleanupFixer))]
[Shared]
public sealed class PreferDisposeOverTestCleanupFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferDisposeOverTestCleanupRuleId);

    public override FixAllProvider GetFixAllProvider()
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
        MethodDeclarationSyntax methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.ReplaceWithDisposeFix,
                c => ReplaceTestCleanupWithDisposeAsync(context.Document, methodDeclaration, c),
                nameof(PreferDisposeOverTestCleanupFixer)),
            diagnostic);
    }

    private static async Task<Document> ReplaceTestCleanupWithDisposeAsync(Document document, MethodDeclarationSyntax testCleanupMethod, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Find the class containing the method
        if (testCleanupMethod.Parent is ClassDeclarationSyntax containingClass)
        {
            MethodDeclarationSyntax? existingDisposeMethod = containingClass.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "Dispose" && !m.ParameterList.Parameters.Any());

            // Move the body of the TestCleanup method
            BlockSyntax? testCleanupBody = testCleanupMethod.Body;

            if (existingDisposeMethod != null)
            {
                StatementSyntax[]? testCleanupStatements = testCleanupBody?.Statements.ToArray();
                MethodDeclarationSyntax newDisposeMethod;

                // If a Dispose method already exists, append the body of the TestCleanup method to it
                if (existingDisposeMethod.Body != null)
                {
                    BlockSyntax newDisposeBody = existingDisposeMethod.Body.AddStatements(testCleanupStatements ?? Array.Empty<StatementSyntax>());
                    newDisposeMethod = existingDisposeMethod.WithBody(newDisposeBody);
                }
                else
                {
                    newDisposeMethod = existingDisposeMethod.WithBody(testCleanupBody);
                }

                editor.ReplaceNode(existingDisposeMethod, newDisposeMethod);
            }
            else
            {
                // Create a new Dispose method with the TestCleanup body if one doesn't exist
                MethodDeclarationSyntax disposeMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                        "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(testCleanupBody);

                editor.AddMember(containingClass, disposeMethod);
            }

            // Remove the TestCleanup method
            editor.RemoveNode(testCleanupMethod);
        }

        return editor.GetChangedDocument();
    }
}

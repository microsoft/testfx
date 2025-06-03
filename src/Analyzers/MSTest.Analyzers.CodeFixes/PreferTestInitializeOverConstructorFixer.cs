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
/// Code fixer for <see cref="PreferTestInitializeOverConstructorAnalyzer" />.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassInitializeShouldBeValidFixer))]
[Shared]
public sealed class PreferTestInitializeOverConstructorFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferTestInitializeOverConstructorRuleId);

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

        // Find the constructor declaration identified by the diagnostic.
        ConstructorDeclarationSyntax? constructorDeclaration = syntaxToken.Parent?.AncestorsAndSelf().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructorDeclaration == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.ReplaceWithTestInitializeFix,
                c => ReplaceConstructorWithTestInitializeAsync(context.Document, constructorDeclaration, c),
                nameof(PreferTestInitializeOverConstructorFixer)),
            diagnostic);
    }

    private static async Task<Document> ReplaceConstructorWithTestInitializeAsync(Document document, ConstructorDeclarationSyntax constructorDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        // Find the class containing the constructor
        if (constructorDeclaration.Parent is ClassDeclarationSyntax containingClass)
        {
            // Check if a TestInitialize method already exists
            INamedTypeSymbol? testInitializeAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
            MethodDeclarationSyntax? existingTestInitialize = containingClass.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists
                    .SelectMany(attrList => attrList.Attributes)
                    .Any(attr => semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol methodSymbol &&
                                 SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, testInitializeAttributeSymbol)));

            // Move the body of the constructor
            BlockSyntax? constructorBody = constructorDeclaration.Body;

            if (existingTestInitialize != null)
            {
                // If TestInitialize exists, append the constructor body to it
                StatementSyntax[]? constructorStatements = constructorBody?.Statements.ToArray();
                MethodDeclarationSyntax newTestInitialize;
                if (existingTestInitialize.Body != null)
                {
                    BlockSyntax newTestInitializeBody = existingTestInitialize.Body.AddStatements(constructorStatements ?? []);
                    newTestInitialize = existingTestInitialize.WithBody(newTestInitializeBody);
                }
                else
                {
                    newTestInitialize = existingTestInitialize.WithBody(constructorBody);
                }

                editor.ReplaceNode(existingTestInitialize, newTestInitialize);
            }
            else
            {
                // Create a new TestInitialize method with the constructor body
                MethodDeclarationSyntax testInitializeMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "TestInitialize")
                    .WithAttributeLists(
                        SyntaxFactory.SingletonList(
                            SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("TestInitialize"))))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(constructorBody);

                editor.AddMember(containingClass, testInitializeMethod);
            }

            // Remove the constructor
            editor.RemoveNode(constructorDeclaration);
        }

        return editor.GetChangedDocument();
    }
}

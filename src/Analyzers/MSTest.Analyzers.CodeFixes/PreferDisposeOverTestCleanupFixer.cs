// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for MSTest2017: Prefer using Dispose over TestCleanup.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferDisposeOverTestCleanupFixer))]
[Shared]
public sealed class PreferDisposeOverTestCleanupFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferDisposeOverTestCleanupRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);

        // Find the TestCleanup method declaration identified by the diagnostic.
        MethodDeclarationSyntax? testCleanupMethod = syntaxToken.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (testCleanupMethod == null ||
            !IsTestCleanupMethodValid(testCleanupMethod) ||
            testCleanupMethod.Parent is not TypeDeclarationSyntax containingType)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.ReplaceWithDisposeFix,
                c => AddDisposeAndBaseClassAsync(context.Document, testCleanupMethod, containingType, c),
                nameof(PreferDisposeOverTestCleanupFixer)),
            diagnostic);
    }

    // The fix will be only for void TestCleanup.
    // We can use DisposeAsync with other types but in that case we would also need to detect if the test is using multi-tfm as DisposeAsync is not available in netfx so we could only fix for netcore.
    private static bool IsTestCleanupMethodValid(MethodDeclarationSyntax methodDeclaration) =>
        // Check if the return type is void
        methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType &&
               predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);

    private static async Task<Solution> AddDisposeAndBaseClassAsync(
        Document document,
        MethodDeclarationSyntax testCleanupMethod,
        TypeDeclarationSyntax containingType,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("SemanticModel cannot be null.");

        SyntaxGenerator generator = editor.Generator;
        TypeDeclarationSyntax newParent = containingType;
        INamedTypeSymbol? iDisposableSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);

        // Move the code from TestCleanup to Dispose method
        // For partial classes, we need to check all parts of the type, not just the current partial declaration
        Document? disposeDocument = null;
        MethodDeclarationSyntax? existingDisposeMethod = null;
        if (semanticModel.GetDeclaredSymbol(containingType, cancellationToken) is INamedTypeSymbol typeSymbol)
        {
            // Find existing Dispose method in any part of the partial class
            IMethodSymbol? existingDisposeSymbol = typeSymbol.GetMembers("Dispose")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsDisposeImplementation(iDisposableSymbol));

            if (existingDisposeSymbol is not null)
            {
                // Find the syntax node for the existing Dispose method
                foreach (SyntaxReference syntaxRef in existingDisposeSymbol.DeclaringSyntaxReferences)
                {
                    if (await syntaxRef.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is MethodDeclarationSyntax methodSyntax)
                    {
                        existingDisposeMethod = methodSyntax;
                        // Find the document containing the Dispose method
                        foreach (Document projectDocument in document.Project.Documents)
                        {
                            SyntaxTree? docSyntaxTree = await projectDocument.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                            if (docSyntaxTree == syntaxRef.SyntaxTree)
                            {
                                disposeDocument = projectDocument;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        BlockSyntax? cleanupBody = testCleanupMethod.Body;
        Solution solution = document.Project.Solution;

        if (existingDisposeMethod is not null && disposeDocument is not null)
        {
            // Append the TestCleanup body to the existing Dispose method
            StatementSyntax[]? cleanupStatements = cleanupBody?.Statements.ToArray();
            MethodDeclarationSyntax newDisposeMethod;
            if (existingDisposeMethod.Body is not null)
            {
                BlockSyntax newDisposeBody = existingDisposeMethod.Body.AddStatements(cleanupStatements ?? []);
                newDisposeMethod = existingDisposeMethod.WithBody(newDisposeBody);
            }
            else
            {
                newDisposeMethod = existingDisposeMethod.WithBody(cleanupBody);
            }

            // Update the dispose document
            DocumentEditor disposeEditor = await DocumentEditor.CreateAsync(disposeDocument, cancellationToken).ConfigureAwait(false);
            disposeEditor.ReplaceNode(existingDisposeMethod, newDisposeMethod);
            solution = solution.WithDocumentSyntaxRoot(disposeDocument.Id, disposeEditor.GetChangedRoot());

            // Remove the TestCleanup method from the current document
            editor.RemoveNode(testCleanupMethod);
            solution = solution.WithDocumentSyntaxRoot(document.Id, editor.GetChangedRoot());
        }
        else
        {
            // Create a new Dispose method with the TestCleanup body
            var disposeMethod = (MethodDeclarationSyntax)generator.MethodDeclaration("Dispose", accessibility: Accessibility.Public);
            disposeMethod = disposeMethod.WithBody(cleanupBody);
            newParent = newParent.ReplaceNode(testCleanupMethod, disposeMethod);

            // Ensure the class implements IDisposable
            if (iDisposableSymbol != null && !ImplementsIDisposable(containingType, iDisposableSymbol, semanticModel))
            {
                newParent = (TypeDeclarationSyntax)generator.AddInterfaceType(newParent, generator.TypeExpression(iDisposableSymbol, addImport: true).WithAdditionalAnnotations(Simplifier.AddImportsAnnotation));
            }

            editor.ReplaceNode(containingType, newParent);
            solution = solution.WithDocumentSyntaxRoot(document.Id, editor.GetChangedRoot());
        }

        return solution;
    }

    private static bool ImplementsIDisposable(TypeDeclarationSyntax typeDeclaration, INamedTypeSymbol iDisposableSymbol, SemanticModel semanticModel)
        => typeDeclaration.BaseList?.Types
            .Any(t => semanticModel.GetSymbolInfo(t.Type).Symbol is INamedTypeSymbol typeSymbol &&
                      SymbolEqualityComparer.Default.Equals(typeSymbol, iDisposableSymbol)) == true;
}

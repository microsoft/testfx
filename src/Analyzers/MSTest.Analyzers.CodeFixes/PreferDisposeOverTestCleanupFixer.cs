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

        // Find the TestCleanup method declaration identified by the diagnostic.
        MethodDeclarationSyntax testCleanupMethod = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (testCleanupMethod == null || !IsTestCleanupMethodValid(testCleanupMethod))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.ReplaceWithDisposeFix,
                c => AddDisposeAndBaseClassAsync(context.Document, testCleanupMethod, c),
                nameof(PreferDisposeOverTestCleanupFixer)),
            diagnostic);
    }

    // The fix will be onle for void TestCleanup.
    // We can use DisposeAsync with other types but in that case we would also need to detect if the test is using multi-tfm as DisposeAsync is not available in netfx so we could only fix for netcore.
    private static bool IsTestCleanupMethodValid(MethodDeclarationSyntax methodDeclaration) =>
        // Check if the return type is void
        methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType &&
               predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);

    private static async Task<Document> AddDisposeAndBaseClassAsync(Document document, MethodDeclarationSyntax testCleanupMethod, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("SemanticModel cannot be null.");

        // Find the class containing the TestCleanup method
        if (testCleanupMethod.Parent is ClassDeclarationSyntax containingClass)
        {
            INamedTypeSymbol? iDisposableSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);
            INamedTypeSymbol? testCleanupAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);

            // Ensure the class implements IDisposable
            if (iDisposableSymbol != null && !ImplementsIDisposable(containingClass, semanticModel))
            {
                AddIDisposable(editor, containingClass);
            }

            // Move the code from TestCleanup to Dispose method
            MethodDeclarationSyntax? existingDisposeMethod = containingClass.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => semanticModel.GetDeclaredSymbol(m) is IMethodSymbol methodSymbol && methodSymbol.IsDisposeImplementation(iDisposableSymbol));

            BlockSyntax? cleanupBody = testCleanupMethod.Body;

            if (existingDisposeMethod != null)
            {
                // Append the TestCleanup body to the existing Dispose method
                StatementSyntax[]? cleanupStatements = cleanupBody?.Statements.ToArray();
                MethodDeclarationSyntax newDisposeMethod;
                if (existingDisposeMethod.Body != null)
                {
                    BlockSyntax newDisposeBody = existingDisposeMethod.Body.AddStatements(cleanupStatements ?? Array.Empty<StatementSyntax>());
                    newDisposeMethod = existingDisposeMethod.WithBody(newDisposeBody);
                }
                else
                {
                    newDisposeMethod = existingDisposeMethod.WithBody(cleanupBody);
                }

                editor.ReplaceNode(existingDisposeMethod, newDisposeMethod);
            }
            else
            {
                // Create a new Dispose method with the TestCleanup body
                MethodDeclarationSyntax disposeMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(cleanupBody);

                editor.AddMember(containingClass, disposeMethod);
            }

            // Remove the TestCleanup method
            editor.RemoveNode(testCleanupMethod);

            UsingDirectiveSyntax systemUsingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(WellKnownTypeNames.System));
            if (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is CompilationUnitSyntax root && root.Usings.All(u => !u.Name.IsEquivalentTo(systemUsingDirective.Name)))
            {
                editor.InsertBefore(root.Members.First(), systemUsingDirective);
            }
        }

        return editor.GetChangedDocument();
    }

    private static bool ImplementsIDisposable(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        INamedTypeSymbol? disposableSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);
        return classDeclaration.BaseList?.Types
            .Any(t => semanticModel.GetSymbolInfo(t.Type).Symbol is INamedTypeSymbol typeSymbol &&
                      SymbolEqualityComparer.Default.Equals(typeSymbol, disposableSymbol)) == true;
    }

    private static void AddIDisposable(DocumentEditor editor, ClassDeclarationSyntax classDeclaration)
    {
        SimpleBaseTypeSyntax disposableType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("IDisposable"));

        // If there is already a base list, add IDisposable to it, otherwise create a new one
        ClassDeclarationSyntax newClassDeclaration = classDeclaration.BaseList != null
            ? classDeclaration.WithBaseList(
                classDeclaration.BaseList.AddTypes(disposableType))
            : classDeclaration.AddBaseListTypes(disposableType);
        editor.ReplaceNode(classDeclaration, newClassDeclaration);
    }
}

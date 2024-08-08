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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestMethodShouldBeValidCodeFixProvider))]
[Shared]
public sealed class TestMethodShouldBeValidCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIds.TestMethodShouldBeValidRuleId);

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
                title: CodeFixResources.TestMethodShouldBeValidFix,
                createChangedSolution: c => FixTestMethodAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(TestMethodShouldBeValidCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Solution> FixTestMethodAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;

        // Remove static modifier if present.
        SyntaxToken staticModifier = newMethodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.StaticKeyword));
        if (staticModifier != default)
        {
            newMethodDeclaration = newMethodDeclaration.WithModifiers(newMethodDeclaration.Modifiers.Remove(staticModifier));
        }

        // Remove abstract modifier if present and ensure the method has a body.
        SyntaxToken abstractModifier = newMethodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.AbstractKeyword));
        if (abstractModifier != default)
        {
            newMethodDeclaration = newMethodDeclaration.WithModifiers(newMethodDeclaration.Modifiers.Remove(abstractModifier));
            if (newMethodDeclaration.Body == null)
            {
                newMethodDeclaration = newMethodDeclaration.WithBody(SyntaxFactory.Block()).WithSemicolonToken(default);
            }
        }

        // Remove type parameters to make the method non-generic.
        if (newMethodDeclaration.TypeParameterList != null)
        {
            newMethodDeclaration = newMethodDeclaration.WithTypeParameterList(null);
        }

        // Ensure the method has public visibility.
        SyntaxToken publicModifier = newMethodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword));
        if (publicModifier == default)
        {
            newMethodDeclaration = newMethodDeclaration.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        // Ensure the method returns void or Task/ValueTask.
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is not null)
        {
            Compilation compilation = semanticModel.Compilation;
            INamedTypeSymbol? taskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            INamedTypeSymbol? valueTaskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");

            if (newMethodDeclaration.ReturnType != null &&
                !newMethodDeclaration.ReturnType.IsVoid() &&
                (taskSymbol == null || !semanticModel.ClassifyConversion(newMethodDeclaration.ReturnType, taskSymbol).IsImplicit) &&
                (valueTaskSymbol == null || !semanticModel.ClassifyConversion(newMethodDeclaration.ReturnType, valueTaskSymbol).IsImplicit))
            {
                newMethodDeclaration = newMethodDeclaration.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)));

                // Remove any return statements in the method body.
                if (newMethodDeclaration.Body != null)
                {
                    SyntaxList<StatementSyntax> statements = newMethodDeclaration.Body.Statements;
                    IEnumerable<StatementSyntax> newStatements = statements.Where(s => s is not ReturnStatementSyntax);
                    newMethodDeclaration = newMethodDeclaration.WithBody(newMethodDeclaration.Body.WithStatements(SyntaxFactory.List(newStatements)));
                }
            }
        }

        // Ensure async methods do not return void.
        if (newMethodDeclaration.Modifiers.Any(SyntaxKind.AsyncKeyword) && newMethodDeclaration.ReturnType is not null && newMethodDeclaration.ReturnType.IsVoid())
        {
            SyntaxToken asyncModifier = newMethodDeclaration.Modifiers.First(m => m.IsKind(SyntaxKind.AsyncKeyword));
            newMethodDeclaration = newMethodDeclaration.WithModifiers(newMethodDeclaration.Modifiers.Remove(asyncModifier));
        }

        // Apply changes.
        editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
        Document newDocument = editor.GetChangedDocument();
        return newDocument.Project.Solution;
    }
}

internal static class TypeSyntaxExtensions
{
    public static bool IsVoid(this TypeSyntax typeSyntax) => typeSyntax is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
}

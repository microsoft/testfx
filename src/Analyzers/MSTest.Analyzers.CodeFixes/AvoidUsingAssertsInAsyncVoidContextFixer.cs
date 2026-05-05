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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="AvoidUsingAssertsInAsyncVoidContextAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidUsingAssertsInAsyncVoidContextFixer))]
[Shared]
public sealed class AvoidUsingAssertsInAsyncVoidContextFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidUsingAssertsInAsyncVoidContextRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Walk up the ancestors to find the nearest async void method or local function.
        foreach (SyntaxNode ancestor in diagnosticNode.AncestorsAndSelf())
        {
            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                if (methodDeclaration.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                    methodDeclaration.ReturnType.IsVoid() &&
                    !methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
                    !methodDeclaration.Modifiers.Any(SyntaxKind.VirtualKeyword) &&
                    methodDeclaration.ExplicitInterfaceSpecifier is null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: CodeFixResources.AvoidUsingAssertsInAsyncVoidContextFix,
                            createChangedDocument: ct => ChangeReturnTypeToTaskAsync(context.Document, methodDeclaration, ct),
                            equivalenceKey: nameof(AvoidUsingAssertsInAsyncVoidContextFixer)),
                        diagnostic);
                }

                break;
            }

            if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                if (localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                    localFunction.ReturnType.IsVoid())
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: CodeFixResources.AvoidUsingAssertsInAsyncVoidContextFix,
                            createChangedDocument: ct => ChangeReturnTypeToTaskAsync(context.Document, localFunction, ct),
                            equivalenceKey: nameof(AvoidUsingAssertsInAsyncVoidContextFixer)),
                        diagnostic);
                }

                break;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax anonymousFunction)
            {
                // Only stop at async lambdas/delegates — they represent the async void context.
                // For non-async lambdas, keep walking up to find the enclosing async void method/local function.
                if (anonymousFunction.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
                {
                    // For async lambdas/anonymous functions, we don't provide a fix since changing to Task
                    // would require changing the delegate type as well.
                    break;
                }
            }
        }
    }

    private static async Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.WithReturnType(
            SyntaxFactory.IdentifierName("Task").WithTriviaFrom(methodDeclaration.ReturnType));
        editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
        Document updatedDocument = editor.GetChangedDocument();

        return await EnsureSystemThreadingTasksImportAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        LocalFunctionStatementSyntax localFunction,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        LocalFunctionStatementSyntax newLocalFunction = localFunction.WithReturnType(
            SyntaxFactory.IdentifierName("Task").WithTriviaFrom(localFunction.ReturnType));
        editor.ReplaceNode(localFunction, newLocalFunction);
        Document updatedDocument = editor.GetChangedDocument();

        return await EnsureSystemThreadingTasksImportAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Document> EnsureSystemThreadingTasksImportAsync(Document document, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        // Check file-level usings and namespace-scoped usings for an existing System.Threading.Tasks import.
        if (HasSystemThreadingTasksUsing(compilationUnit.Usings))
        {
            return document;
        }

        foreach (NamespaceDeclarationSyntax ns in compilationUnit.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            if (HasSystemThreadingTasksUsing(ns.Usings))
            {
                return document;
            }
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks").WithLeadingTrivia(SyntaxFactory.Space))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        // Insert in correct alphabetical position: after System.* usings that sort before "System.Threading.Tasks"
        // and before any that sort after it or before any non-System usings.
        int insertionIndex = compilationUnit.Usings.Count; // default: append after all usings
        for (int i = 0; i < compilationUnit.Usings.Count; i++)
        {
            string? nameText = NormalizeUsingName(compilationUnit.Usings[i].Name?.ToString());
            if (nameText is null)
            {
                continue;
            }

            bool isSystemNamespace = IsSystemNamespace(nameText);
            if (!isSystemNamespace ||
                string.Compare(nameText, "System.Threading.Tasks", StringComparison.Ordinal) > 0)
            {
                insertionIndex = i;
                break;
            }
        }

        SyntaxList<UsingDirectiveSyntax> newUsings = compilationUnit.Usings.Insert(insertionIndex, usingDirective);
        return document.WithSyntaxRoot(compilationUnit.WithUsings(newUsings));
    }

    private static bool HasSystemThreadingTasksUsing(SyntaxList<UsingDirectiveSyntax> usings)
        => usings.Any(
            u => u.Alias is null &&
                 !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) &&
                 string.Equals(NormalizeUsingName(u.Name?.ToString()), "System.Threading.Tasks", StringComparison.Ordinal));

    private static bool IsSystemNamespace(string nameText)
        => string.Equals(nameText, "System", StringComparison.Ordinal) ||
           nameText.StartsWith("System.", StringComparison.Ordinal);

    private static string? NormalizeUsingName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        // Strip the "global::" qualifier if present so that "global::System.Threading.Tasks" is recognized.
        const string globalPrefix = "global::";
        return name.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? name[globalPrefix.Length..]
            : name;
    }
}

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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="AvoidUsingAssertsInAsyncVoidContextAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidUsingAssertsInAsyncVoidContextFixer))]
[Shared]
public sealed class AvoidUsingAssertsInAsyncVoidContextFixer : CodeFixProvider
{
    private const string SystemThreadingTasksNamespace = "System.Threading.Tasks";

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

    private static Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
        => ReplaceReturnTypeAsync(
            document,
            methodDeclaration,
            (node, newType) => ((MethodDeclarationSyntax)node).WithReturnType(newType),
            cancellationToken);

    private static Task<Document> ChangeReturnTypeToTaskAsync(
        Document document,
        LocalFunctionStatementSyntax localFunction,
        CancellationToken cancellationToken)
        => ReplaceReturnTypeAsync(
            document,
            localFunction,
            (node, newType) => ((LocalFunctionStatementSyntax)node).WithReturnType(newType),
            cancellationToken);

    private static async Task<Document> ReplaceReturnTypeAsync(
        Document document,
        SyntaxNode nodeToReplace,
        Func<SyntaxNode, TypeSyntax, SyntaxNode> withNewReturnType,
        CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        TypeSyntax originalReturnType = nodeToReplace switch
        {
            MethodDeclarationSyntax m => m.ReturnType,
            LocalFunctionStatementSyntax l => l.ReturnType,
            _ => throw new InvalidOperationException(),
        };

        // Determine whether 'Task' (System.Threading.Tasks.Task) is already in scope at the method's
        // location. This correctly handles file-scoped, namespace-scoped, global, and SDK-implicit usings.
        bool needsImport = !await IsTaskInScopeAsync(document, nodeToReplace, cancellationToken).ConfigureAwait(false);

        TypeSyntax newReturnType = SyntaxFactory.IdentifierName("Task").WithTriviaFrom(originalReturnType);
        SyntaxAnnotation methodMarker = new();
        SyntaxNode replacement = withNewReturnType(nodeToReplace, newReturnType).WithAdditionalAnnotations(methodMarker);
        SyntaxNode newRoot = root.ReplaceNode(nodeToReplace, replacement);

        if (needsImport && newRoot is CompilationUnitSyntax compilationUnit)
        {
            SyntaxNode newMethodNode = newRoot.GetAnnotatedNodes(methodMarker).First();
            newRoot = AddSystemThreadingTasksUsing(compilationUnit, newMethodNode);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<bool> IsTaskInScopeAsync(Document document, SyntaxNode nodeAtPosition, CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return false;
        }

        INamedTypeSymbol? taskSymbol = semanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        if (taskSymbol is null)
        {
            // Reference assembly missing — let the user deal with the resulting error rather than guessing.
            return true;
        }

        // Look up the unqualified name "Task" at the method/local function's position. If it resolves
        // to System.Threading.Tasks.Task, no extra import is needed (covers file-scoped, namespace-scoped,
        // global, and SDK-implicit usings, including 'using global::System.Threading.Tasks;' and
        // 'using System.Threading.Tasks;' inside the enclosing namespace).
        ImmutableArray<ISymbol> candidates = semanticModel.LookupNamespacesAndTypes(nodeAtPosition.SpanStart, name: "Task");
        return candidates.Any(c => SymbolEqualityComparer.Default.Equals(c, taskSymbol));
    }

    private static CompilationUnitSyntax AddSystemThreadingTasksUsing(CompilationUnitSyntax compilationUnit, SyntaxNode methodNode)
    {
        UsingDirectiveSyntax newUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(SystemThreadingTasksNamespace).WithLeadingTrivia(SyntaxFactory.Space))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        // Add the using to the smallest enclosing block-scoped namespace (preserving the file's existing
        // namespace-scoped style when applicable). For file-scoped namespaces (no NamespaceDeclarationSyntax
        // ancestor), fall back to file-scope insertion — that is the conventional location for usings
        // when a file uses 'namespace Foo;' style.
        NamespaceDeclarationSyntax? containingNs = methodNode.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (containingNs is not null)
        {
            SyntaxList<UsingDirectiveSyntax> updatedUsings = InsertAlphabetically(containingNs.Usings, newUsing);
            return compilationUnit.ReplaceNode(containingNs, containingNs.WithUsings(updatedUsings));
        }

        return compilationUnit.WithUsings(InsertAlphabetically(compilationUnit.Usings, newUsing));
    }

    private static SyntaxList<UsingDirectiveSyntax> InsertAlphabetically(SyntaxList<UsingDirectiveSyntax> existing, UsingDirectiveSyntax newUsing)
    {
        // Place 'System.Threading.Tasks' alphabetically among System.* usings, before any non-System usings.
        // C# 10+ same-file 'global using' directives must precede non-global usings, so always insert
        // after the global block.
        int insertionIndex = existing.Count;
        for (int i = 0; i < existing.Count; i++)
        {
            UsingDirectiveSyntax current = existing[i];
            if (IsGlobalUsing(current))
            {
                continue;
            }

            string? nameText = current.Name?.ToString();
            if (nameText is null)
            {
                continue;
            }

            bool isSystemNamespace = string.Equals(nameText, "System", StringComparison.Ordinal) ||
                                     nameText.StartsWith("System.", StringComparison.Ordinal);
            if (!isSystemNamespace ||
                string.Compare(nameText, SystemThreadingTasksNamespace, StringComparison.Ordinal) > 0)
            {
                insertionIndex = i;
                break;
            }
        }

        return existing.Insert(insertionIndex, newUsing);
    }

    private static bool IsGlobalUsing(UsingDirectiveSyntax usingDirective)
    {
        // 'global' is a contextual keyword introduced in C# 10. Detect it textually so the build-time
        // Roslyn 3.11 package does not need to expose UsingDirectiveSyntax.GlobalKeyword.
        SyntaxToken firstToken = usingDirective.GetFirstToken();
        return firstToken.Text == "global";
    }
}

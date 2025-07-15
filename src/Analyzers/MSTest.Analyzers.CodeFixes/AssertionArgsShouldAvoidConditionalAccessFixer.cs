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
/// Code fix for <see cref="AssertionArgsShouldAvoidConditionalAccessAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertionArgsShouldAvoidConditionalAccessFixer))]
[Shared]
public sealed class AssertionArgsShouldAvoidConditionalAccessFixer : CodeFixProvider
{
    /// <summary>
    /// The scenario that is complicating this code fix is if we have multiple diagnostics that are doing conditional access
    /// on the same expression. In that case, we need to ensure that we don't add multiple Assert.IsNotNull calls.
    /// The first idea was to iterate through the existing statements, and if we found Assert.IsNotNull with
    /// the relevant expression, we don't add it again. However, this approach works for iterative codefix application
    /// only, and doesn't work with the BatchFixAllProvider. The BatchFixAllProvider works by applying individual fixes
    /// completely in isolation, then merging the text changes.
    /// This means, every invocation of the code action will not see that Assert.IsNotNull was added by another.
    /// So, we provide our own FixAllProvider.
    /// This FixAllProvider will reuse the same DocumentEditor across all the code actions.
    /// </summary>
    private sealed class CustomFixAll : DocumentBasedFixAllProvider
    {
        protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
            Document currentDocument = document;
            foreach (Diagnostic diagnostic in diagnostics)
            {
                SyntaxNode assertInvocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                // We need to track the assert invocation so that the individual 'SingleFixCodeAction's can get the up-to-date node
                // from the most recent tree.
                // Having the most recent node is important for IsNullAssertAlreadyPresent to work properly.
                // We get the most recent node via editor.GetChangedRoot().GetCurrentNode(...)
                editor.TrackNode(assertInvocation);
            }

            foreach (Diagnostic diagnostic in diagnostics)
            {
                SyntaxNode conditionalAccess = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true);
                SyntaxNode assertInvocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                if (conditionalAccess is not ConditionalAccessExpressionSyntax conditionalAccessExpressionSyntax ||
                    assertInvocation is not InvocationExpressionSyntax invocationExpressionSyntax)
                {
                    continue;
                }

                var codeAction = new SingleFixCodeAction(currentDocument, conditionalAccessExpressionSyntax, invocationExpressionSyntax);
                currentDocument = codeAction.ApplyFix(editor);
            }

            return editor.GetChangedDocument();
        }
    }

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssertionArgsShouldAvoidConditionalAccessRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => new CustomFixAll();

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        SyntaxNode conditionalAccess = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true);
        SyntaxNode assertInvocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (conditionalAccess is not ConditionalAccessExpressionSyntax conditionalAccessExpressionSyntax ||
            assertInvocation is not InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            new SingleFixCodeAction(context.Document, conditionalAccessExpressionSyntax, invocationExpressionSyntax),
            diagnostic);
    }

    private sealed class SingleFixCodeAction : CodeAction
    {
        private readonly Document _document;
        private readonly ConditionalAccessExpressionSyntax _conditionalAccessExpressionSyntax;
        private readonly InvocationExpressionSyntax _invocationExpressionSyntax;

        public SingleFixCodeAction(Document document, ConditionalAccessExpressionSyntax conditionalAccessExpressionSyntax, InvocationExpressionSyntax invocationExpressionSyntax)
        {
            _document = document;
            _conditionalAccessExpressionSyntax = conditionalAccessExpressionSyntax;
            _invocationExpressionSyntax = invocationExpressionSyntax;
        }

        public override string Title { get; } = CodeFixResources.AssertionArgsShouldAvoidConditionalAccessFix;

        public override string? EquivalenceKey => nameof(AssertionArgsShouldAvoidConditionalAccessFixer);

        protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(_document, cancellationToken).ConfigureAwait(false);
            return ApplyFix(editor);
        }

        internal Document ApplyFix(DocumentEditor editor)
        {
            ExpressionSyntax expressionCheckedForNull = _conditionalAccessExpressionSyntax.Expression;
            bool isNullAssertAlreadyPresent = IsNullAssertAlreadyPresent(expressionCheckedForNull, editor.GetChangedRoot().GetCurrentNode(_invocationExpressionSyntax) ?? _invocationExpressionSyntax);

            // Easier than correctly reconstructing the syntax node manually, but not ideal.
            ExpressionSyntax parsedExpression = SyntaxFactory.ParseExpression($"{expressionCheckedForNull.ToFullString()}{_conditionalAccessExpressionSyntax.WhenNotNull}");
            parsedExpression = parsedExpression.WithTriviaFrom(_conditionalAccessExpressionSyntax);

            editor.ReplaceNode(_conditionalAccessExpressionSyntax, parsedExpression);

            if (!isNullAssertAlreadyPresent)
            {
                ExpressionStatementSyntax assertIsNotNull = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Assert"),
                            SyntaxFactory.IdentifierName("IsNotNull")))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(expressionCheckedForNull)))));
                if (_invocationExpressionSyntax.Parent is ExpressionStatementSyntax expressionStatement)
                {
                    editor.InsertBefore(expressionStatement, assertIsNotNull);
                }
                else if (_invocationExpressionSyntax.Parent is ArrowExpressionClauseSyntax arrowExpressionClauseSyntax)
                {
                    // The following types are where ArrowExpressionClause can appear.
                    // BaseMethodDeclarationSyntax: ConstructorDeclarationSyntax, ConversionOperatorDeclarationSyntax, DestructorDeclarationSyntax, MethodDeclarationSyntax, OperatorDeclarationSyntax
                    // AccessorDeclarationSyntax, IndexerDeclarationSyntax, PropertyDeclarationSyntax, LocalFunctionStatementSyntax
                    //
                    // PropertyDeclarationSyntax and IndexerDeclarationSyntax don't make sense so we won't handle it.
                    if (arrowExpressionClauseSyntax.Parent is BaseMethodDeclarationSyntax parentBaseMethod)
                    {
                        editor.ReplaceNode(
                            parentBaseMethod,
                            (node, _) =>
                            {
                                var parentBaseMethod = (BaseMethodDeclarationSyntax)node;
                                return parentBaseMethod
                                    .WithExpressionBody(null)
                                    .WithSemicolonToken(default)
                                    .WithBody(SyntaxFactory.Block(
                                        assertIsNotNull,
                                        SyntaxFactory.ExpressionStatement(parentBaseMethod.ExpressionBody!.Expression)));
                            });
                    }
                    else if (arrowExpressionClauseSyntax.Parent is AccessorDeclarationSyntax parentAccessor)
                    {
                        editor.ReplaceNode(
                            parentAccessor,
                            (node, _) =>
                            {
                                var parentAccessor = (AccessorDeclarationSyntax)node;
                                return parentAccessor
                                    .WithExpressionBody(null)
                                    .WithSemicolonToken(default)
                                    .WithBody(SyntaxFactory.Block(
                                        assertIsNotNull,
                                        SyntaxFactory.ExpressionStatement(parentAccessor.ExpressionBody!.Expression)));
                            });
                    }
                    else if (arrowExpressionClauseSyntax.Parent is LocalFunctionStatementSyntax parentLocalFunction)
                    {
                        editor.ReplaceNode(
                            parentLocalFunction,
                            (node, _) =>
                            {
                                var parentLocalFunction = (LocalFunctionStatementSyntax)node;
                                return parentLocalFunction
                                    .WithExpressionBody(null)
                                    .WithSemicolonToken(default)
                                    .WithBody(SyntaxFactory.Block(
                                        assertIsNotNull,
                                        SyntaxFactory.ExpressionStatement(parentLocalFunction.ExpressionBody!.Expression)));
                            });
                    }
                }
            }

            return editor.GetChangedDocument();
        }
    }

    private static bool IsNullAssertAlreadyPresent(SyntaxNode expressionCheckedForNull, InvocationExpressionSyntax invocationExpressionSyntax)
    {
        if (invocationExpressionSyntax.Parent?.Parent is not BlockSyntax blockSyntax)
        {
            return false;
        }

        foreach (StatementSyntax statement in blockSyntax.Statements)
        {
            if (statement is not ExpressionStatementSyntax expressionStatement)
            {
                continue;
            }

            // We expect Assert.IsNull to be present before the invocation expression in question.
            if (expressionStatement.Expression == invocationExpressionSyntax)
            {
                return false;
            }

            if (expressionStatement.Expression is InvocationExpressionSyntax invocation)
            {
                SimpleNameSyntax? methodName =
                    invocation.Expression as IdentifierNameSyntax ?? (invocation.Expression as MemberAccessExpressionSyntax)?.Name;
                if ((methodName?.Identifier.Value as string) == "IsNotNull" &&
                    invocation.ArgumentList.Arguments.Count > 0 &&
                    invocation.ArgumentList.Arguments[0].Expression.IsEquivalentTo(expressionCheckedForNull))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

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
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestMethodAttributeShouldSetDisplayNameCorrectlyFixer))]
[Shared]
public sealed class TestMethodAttributeShouldSetDisplayNameCorrectlyFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.TestMethodAttributeShouldSetDisplayNameCorrectlyRuleId);

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

        // Find the attribute syntax
        SyntaxNode? node = root.FindNode(diagnosticSpan);
        if (node is AttributeArgumentSyntax attributeArgumentSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.UseDisplayNamePropertyInsteadOfStringArgumentFix,
                    createChangedDocument: c => ConvertToDisplayNamePropertyAsync(context.Document, attributeArgumentSyntax, c),
                    equivalenceKey: nameof(TestMethodAttributeShouldSetDisplayNameCorrectlyFixer)),
                diagnostic);
        }
        else if (node is ArgumentSyntax argumentSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.UseDisplayNamePropertyInsteadOfStringArgumentFix,
                    createChangedDocument: c => ConvertToDisplayNamePropertyAsync(context.Document, argumentSyntax, c),
                    equivalenceKey: nameof(TestMethodAttributeShouldSetDisplayNameCorrectlyFixer)),
                diagnostic);
        }
    }

    private static async Task<Document> ConvertToDisplayNamePropertyAsync(Document document, AttributeArgumentSyntax attributeArgumentSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(
            root.ReplaceNode(
                attributeArgumentSyntax,
                attributeArgumentSyntax.WithNameEquals(SyntaxFactory.NameEquals("DisplayName"))));
    }

    private static async Task<Document> ConvertToDisplayNamePropertyAsync(Document document, ArgumentSyntax argumentSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (argumentSyntax.Parent is not ArgumentListSyntax argumentListSyntax ||
            argumentListSyntax.Parent is not BaseObjectCreationExpressionSyntax baseObjectCreationExpressionSyntax)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        BaseObjectCreationExpressionSyntax updatedObjectCreation = baseObjectCreationExpressionSyntax;
        ExpressionSyntax assignmentExpression = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("DisplayName"),
            argumentSyntax.Expression);

        if (updatedObjectCreation.Initializer is null)
        {
            // We don't have an initializer. We create one with a single expression.
            updatedObjectCreation = updatedObjectCreation.WithInitializer(
                SyntaxFactory.InitializerExpression(
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxFactory.SingletonSeparatedList(assignmentExpression)));
        }
        else
        {
            // We already have an initializer, we prepend the assignment expression to the existing expressions.
            updatedObjectCreation = updatedObjectCreation.WithInitializer(
                updatedObjectCreation.Initializer.WithExpressions(updatedObjectCreation.Initializer.Expressions.Insert(0, assignmentExpression)));
        }

        updatedObjectCreation = updatedObjectCreation.WithArgumentList(
            argumentListSyntax.WithArguments(argumentListSyntax.Arguments.Remove(argumentSyntax)));

        return document.WithSyntaxRoot(
            root.ReplaceNode(
                baseObjectCreationExpressionSyntax,
                updatedObjectCreation));
    }
}

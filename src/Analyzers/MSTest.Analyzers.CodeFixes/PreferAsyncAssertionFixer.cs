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
/// Code fixer for <see cref="PreferAsyncAssertionAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferAsyncAssertionFixer))]
[Shared]
public sealed partial class PreferAsyncAssertionFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = [DiagnosticIds.PreferAsyncAssertionRuleId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode diagnosticNode = root.FindNode(context.Span);

        if (diagnosticNode.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault() is not { } invocationExpression)
        {
            return;
        }

        if (invocationExpression.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } methodDeclaration &&
            ContainsReturnExpressionInUnawaitableContext(methodDeclaration))
        {
            SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is not null &&
                !methodDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.AsyncKeyword)) &&
                IsTaskOrValueTaskReturnType(methodDeclaration, semanticModel, context.CancellationToken))
            {
                return;
            }
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseAsyncAssertionFix,
                createChangedDocument: ct => UseAsyncAssertionAsync(context.Document, invocationExpression, ct),
                equivalenceKey: nameof(PreferAsyncAssertionFixer)),
            context.Diagnostics);
    }

    private static bool ContainsReturnExpressionInUnawaitableContext(MethodDeclarationSyntax methodDeclaration)
    {
        var walker = new UnawaitableReturnDetector();
        if (methodDeclaration.Body is { } body)
        {
            walker.Visit(body);
        }
        else if (methodDeclaration.ExpressionBody is { } expressionBody)
        {
            walker.Visit(expressionBody);
        }

        return walker.Found;
    }
}

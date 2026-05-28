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
/// Code fixer for <see cref="IgnoreShouldHaveJustificationAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IgnoreShouldHaveJustificationFixer))]
[Shared]
public sealed class IgnoreShouldHaveJustificationFixer : CodeFixProvider
{
    // Placeholder reason inserted by the code fix. Intentionally calls for action so it doesn't
    // get accidentally committed as-is.
    private const string PlaceholderJustification = "TODO: explain why this is ignored";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.IgnoreShouldHaveJustificationRuleId);

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

        SyntaxNode? node = root.FindNode(diagnosticSpan);
        AttributeSyntax? attributeSyntax = node as AttributeSyntax ?? node?.FirstAncestorOrSelf<AttributeSyntax>();
        if (attributeSyntax is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AddIgnoreJustificationFix,
                createChangedDocument: c => AddJustificationAsync(context.Document, attributeSyntax, c),
                equivalenceKey: nameof(IgnoreShouldHaveJustificationFixer)),
            diagnostic);
    }

    private static async Task<Document> AddJustificationAsync(Document document, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        LiteralExpressionSyntax justificationLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(PlaceholderJustification));

        AttributeArgumentListSyntax existingArgumentList = attributeSyntax.ArgumentList ?? SyntaxFactory.AttributeArgumentList();

        AttributeArgumentListSyntax newArgumentList;
        if (existingArgumentList.Arguments.Count == 0)
        {
            // [Ignore] or [Ignore()] - add a positional message argument.
            newArgumentList = existingArgumentList.WithArguments(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(justificationLiteral)));
        }
        else
        {
            // Replace the first argument's value with the placeholder, preserving any
            // named-argument syntax. This handles both [Ignore("")] (positional) and
            // [Ignore(IgnoreMessage = "")] (named) - in either case we keep the existing
            // form and only swap the empty/null literal for the placeholder string.
            AttributeArgumentSyntax firstArgument = existingArgumentList.Arguments[0];
            AttributeArgumentSyntax updatedFirstArgument = firstArgument.WithExpression(justificationLiteral);
            newArgumentList = existingArgumentList.WithArguments(
                existingArgumentList.Arguments.Replace(firstArgument, updatedFirstArgument));
        }

        AttributeSyntax newAttribute = attributeSyntax.WithArgumentList(newArgumentList);
        return document.WithSyntaxRoot(root.ReplaceNode(attributeSyntax, newAttribute));
    }
}

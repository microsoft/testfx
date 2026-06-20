// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="RedundantTestMethodDisplayNameAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantTestMethodDisplayNameFixer))]
[Shared]
public sealed class RedundantTestMethodDisplayNameFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.RedundantTestMethodDisplayNameRuleId);

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

        SyntaxNode node = root.FindNode(diagnosticSpan);
        AttributeSyntax? attributeSyntax = node.FirstAncestorOrSelf<AttributeSyntax>();
        AttributeArgumentSyntax? displayNameArgument = attributeSyntax?.ArgumentList?.Arguments
            .FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.Text == "DisplayName");
        if (displayNameArgument is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.RemoveRedundantTestMethodDisplayNameFix,
                createChangedDocument: c => RemoveDisplayNameArgumentAsync(context.Document, displayNameArgument, c),
                equivalenceKey: nameof(RedundantTestMethodDisplayNameFixer)),
            diagnostic);
    }

    private static async Task<Document> RemoveDisplayNameArgumentAsync(Document document, AttributeArgumentSyntax displayNameArgument, CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (displayNameArgument.Parent is not AttributeArgumentListSyntax argumentList)
        {
            return document;
        }

        // If 'DisplayName' is the only argument, remove the whole argument list so we don't leave empty parentheses.
        if (argumentList.Arguments.Count == 1)
        {
            SyntaxNode newRoot = root.RemoveNode(argumentList, SyntaxRemoveOptions.KeepNoTrivia)!;
            return document.WithSyntaxRoot(newRoot);
        }

        AttributeArgumentListSyntax? newArgumentList = argumentList.RemoveNode(displayNameArgument, SyntaxRemoveOptions.KeepNoTrivia);
        if (newArgumentList is not null)
        {
            SyntaxNode newRoot = root.ReplaceNode(argumentList, newArgumentList);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}

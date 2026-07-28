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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fix for <see cref="AvoidOutRefTestMethodParametersAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidOutRefTestMethodParametersFixer))]
[Shared]
public sealed class AvoidOutRefTestMethodParametersFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIds.AvoidOutRefTestMethodParametersRuleId);

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

        // The diagnostic is reported on the method, so we can directly get the method declaration
        if (root.FindNode(diagnosticSpan) is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AvoidOutRefTestMethodParametersFix,
                createChangedDocument: c => RemoveOutRefModifiersAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(AvoidOutRefTestMethodParametersFixer)),
            diagnostic);
    }

    private static async Task<Document> RemoveOutRefModifiersAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        foreach (ParameterSyntax parameter in methodDeclaration.ParameterList.Parameters)
        {
            SyntaxToken[] removedModifiers = parameter.Modifiers
                .Where(modifier => modifier.IsKind(SyntaxKind.OutKeyword)
                    || modifier.IsKind(SyntaxKind.RefKeyword)
                    || modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
                .ToArray();
            if (!removedModifiers.Any(modifier => modifier.IsKind(SyntaxKind.OutKeyword) || modifier.IsKind(SyntaxKind.RefKeyword)))
            {
                continue;
            }

            SyntaxTokenList filteredModifiers = SyntaxFactory.TokenList(
                parameter.Modifiers.Except(removedModifiers));

            ParameterSyntax updatedParameter = parameter.WithModifiers(filteredModifiers);
            SyntaxTrivia[] removedTrivia = removedModifiers
                .SelectMany(modifier => modifier.LeadingTrivia.Concat(modifier.TrailingTrivia))
                .ToArray();
            SyntaxTrivia[] preservedTrivia = removedTrivia
                .SkipWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                .Reverse()
                .SkipWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                .Reverse()
                .ToArray();
            IEnumerable<SyntaxTrivia> typeLeadingTrivia = preservedTrivia.Length > 0
                && !preservedTrivia[^1].IsKind(SyntaxKind.EndOfLineTrivia)
                    ? preservedTrivia.Append(SyntaxFactory.Space)
                    : preservedTrivia;

            updatedParameter = parameter.Type is { } parameterType
                && preservedTrivia.Any(trivia => !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    ? updatedParameter.WithType(parameterType.WithLeadingTrivia(typeLeadingTrivia.Concat(parameterType.GetLeadingTrivia())))
                    : updatedParameter.WithLeadingTrivia(parameter.GetLeadingTrivia());

            editor.ReplaceNode(
                parameter,
                updatedParameter.WithAdditionalAnnotations(Formatter.Annotation));
        }

        return editor.GetChangedDocument();
    }
}

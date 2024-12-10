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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferAssertFailOverAlwaysFalseConditionsFixer))]
[Shared]
public sealed class PreferAssertFailOverAlwaysFalseConditionsFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferAssertFailOverAlwaysFalseConditionsRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root.FindNode(diagnosticSpan) is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

        if (context.Diagnostics.Any(d => !d.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey)))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.ReplaceWithFailAssertionFix,
                    ct => UseAssertFailAsync(context.Document, invocationExpr, diagnostic.AdditionalLocations, ct),
                    nameof(PreferAssertFailOverAlwaysFalseConditionsFixer)),
                context.Diagnostics);
        }
    }

    private static async Task<Document> UseAssertFailAsync(Document document, InvocationExpressionSyntax invocationExpr, IReadOnlyList<Location> additionalLocations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SyntaxGenerator generator = editor.Generator;

        var newInvocationExpr = (InvocationExpressionSyntax)generator.InvocationExpression(
            generator.MemberAccessExpression(generator.IdentifierName("Assert"), "Fail"));

        if (additionalLocations.Count >= 1)
        {
            IEnumerable<ArgumentSyntax> arguments = additionalLocations.Select(location => (ArgumentSyntax)invocationExpr.FindNode(location.SourceSpan));
            newInvocationExpr = newInvocationExpr.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
        }

        editor.ReplaceNode(invocationExpr, newInvocationExpr);

        return editor.GetChangedDocument();
    }
}

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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertionArgsShouldBePassedInCorrectOrderFixer))]
[Shared]
public sealed class AssertionArgsShouldBePassedInCorrectOrderFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssertionArgsShouldBePassedInCorrectOrderRuleId);

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
                    CodeFixResources.FixAssertionArgsOrder,
                    ct => SwapArgumentsAsync(context.Document, invocationExpr, ct),
                    nameof(AssertionArgsShouldBePassedInCorrectOrderFixer)),
                context.Diagnostics);
        }
    }

    private static async Task<Document> SwapArgumentsAsync(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpr.ArgumentList.Arguments;

        ArgumentSyntax expectedArg = arguments.FirstOrDefault(arg => IsExpectedArgument(arg));
        ArgumentSyntax actualArg = arguments.FirstOrDefault(arg => IsActualArgument(arg));

        // Handle positional arguments if named arguments are not found
        if (expectedArg == null || actualArg == null)
        {
            expectedArg = arguments[0];
            actualArg = arguments[1];
        }

        var newArguments = arguments.ToList();
        int expectedIndex = arguments.IndexOf(expectedArg);
        int actualIndex = arguments.IndexOf(actualArg);

        ArgumentSyntax tmpExpectedArg = expectedArg;
        newArguments[expectedIndex] = expectedArg.WithExpression(actualArg.Expression);
        newArguments[actualIndex] = actualArg.WithExpression(tmpExpectedArg.Expression);

        InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArguments)));
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot = root.ReplaceNode(invocationExpr, newInvocationExpr);

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsActualArgument(ArgumentSyntax argument) => string.Equals(argument.NameColon?.Name.Identifier.Text, "actual", StringComparison.Ordinal);

    private static bool IsExpectedArgument(ArgumentSyntax argument) => string.Equals(argument.NameColon?.Name.Identifier.Text, "expected", StringComparison.Ordinal)
                                                                       || string.Equals(argument.NameColon?.Name.Identifier.Text, "notExpected", StringComparison.Ordinal);
}

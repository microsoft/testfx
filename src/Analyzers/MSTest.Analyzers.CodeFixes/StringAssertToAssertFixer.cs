// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Code fixer for <see cref="StringAssertToAssertAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringAssertToAssertFixer))]
[Shared]
public sealed class StringAssertToAssertFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.StringAssertToAssertRuleId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => AssertToAssertFixerHelpers.RegisterCodeFixAsync(
            context,
            StringAssertToAssertAnalyzer.ProperAssertMethodNameKey,
            CodeFixResources.StringAssertToAssertTitle,
            fixKindPropertyKey: null,
            FixAssertAsync);

    private static Task<Document> FixAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        string? fixKind,
        CancellationToken cancellationToken)
        => FixStringAssertAsync(document, invocationExpr, properAssertMethodName, cancellationToken);

    private static async Task<Document> FixStringAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        CancellationToken cancellationToken)
    {
        // Check if the invocation expression has a member access expression
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return document;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpr.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return document;
        }

        // Create new argument list with swapped first two arguments
        // We keep the existing separators in case there is trivia attached to them.
        var newArguments = arguments.GetWithSeparators().ToList();
        // NOTE: Index 1 has the "separator" (comma) between the first and second arguments.
        (newArguments[0], newArguments[2]) = (newArguments[2], newArguments[0]);

        ArgumentListSyntax newArgumentList = invocationExpr.ArgumentList.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(newArguments));
        InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithArgumentList(newArgumentList);

        // Replace StringAssert with Assert in the member access expression
        // Change StringAssert.MethodName to Assert.ProperMethodName
        MemberAccessExpressionSyntax newMemberAccess = memberAccessExpr.WithExpression(SyntaxFactory.IdentifierName("Assert"))
            .WithName(SyntaxFactory.IdentifierName(properAssertMethodName));
        newInvocationExpr = newInvocationExpr.WithExpression(newMemberAccess);

        // Preserve leading trivia (including empty lines) from the original invocation
        newInvocationExpr = newInvocationExpr.WithLeadingTrivia(invocationExpr.GetLeadingTrivia());

        return await AssertToAssertFixerHelpers.ReplaceInvocationAsync(document, invocationExpr, newInvocationExpr, cancellationToken).ConfigureAwait(false);
    }
}

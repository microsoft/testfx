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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(StringAssertToAssertAnalyzer.ProperAssertMethodNameKey, out string? properAssertMethodName)
            || properAssertMethodName == null)
        {
            return;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return;
        }

        // Register a code fix that will invoke the fix operation.
        string title = string.Format(CultureInfo.InvariantCulture, CodeFixResources.StringAssertToAssertTitle, properAssertMethodName);
        var action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => FixStringAssertAsync(context.Document, invocationExpressionSyntax, properAssertMethodName, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

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

        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        // Create new argument list with swapped first two arguments
        // We keep the existing separators in case there is trivia attached to them.
        var newArguments = arguments.GetWithSeparators().ToList();
        // NOTE: Index 1 has the "separator" (comma) between the first and second arguments.
        (newArguments[0], newArguments[2]) = (newArguments[2], newArguments[0]);

        // StringAssert has overloads with parameter types (string, string, string, StringComparison)
        // In Assert class, these are (string, string, StringComparison, string)
        // So we need to do the swap.
        if (arguments.Count >= 4)
        {
            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel.GetSymbolInfo(invocationExpr, cancellationToken).Symbol is IMethodSymbol stringAssertMethod &&
                stringAssertMethod.Parameters.Length >= 4 &&
                stringAssertMethod.Parameters[2].Type.SpecialType == SpecialType.System_String &&
                stringAssertMethod.Parameters[3].Type.Name == "StringComparison")
            {
                (newArguments[4], newArguments[6]) = (newArguments[6], newArguments[4]);
            }
        }

        ArgumentListSyntax newArgumentList = invocationExpr.ArgumentList.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(newArguments));
        InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithArgumentList(newArgumentList);

        // Replace StringAssert with Assert in the member access expression
        // Change StringAssert.MethodName to Assert.ProperMethodName
        MemberAccessExpressionSyntax newMemberAccess = memberAccessExpr.WithExpression(SyntaxFactory.IdentifierName("Assert"))
            .WithName(SyntaxFactory.IdentifierName(properAssertMethodName));
        newInvocationExpr = newInvocationExpr.WithExpression(newMemberAccess);

        // Preserve leading trivia (including empty lines) from the original invocation
        newInvocationExpr = newInvocationExpr.WithLeadingTrivia(invocationExpr.GetLeadingTrivia());

        return document.WithSyntaxRoot(root.ReplaceNode(invocationExpr, newInvocationExpr));
    }
}

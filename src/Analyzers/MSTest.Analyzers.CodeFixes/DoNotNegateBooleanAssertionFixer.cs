// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fix for MSTEST0023: Do not negate boolean assertions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DoNotNegateBooleanAssertionFixer))]
[Shared]
public sealed class DoNotNegateBooleanAssertionFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.DoNotNegateBooleanAssertionRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        // Get the proper assert method name from diagnostic properties
        if (!diagnostic.Properties.TryGetValue(DoNotNegateBooleanAssertionAnalyzer.ProperAssertMethodNameKey, out string? properAssertMethodName) ||
            properAssertMethodName is null)
        {
            return;
        }

        // Retrieve the condition argument from the additional locations provided by the analyzer
        if (diagnostic.AdditionalLocations.Count == 0)
        {
            return;
        }

        // Find the argument node that corresponds to the additional location
        SyntaxNode? argumentNode = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan);

        // Walk up to find the ArgumentSyntax if we got a child node
        while (argumentNode is not null and not ArgumentSyntax)
        {
            argumentNode = argumentNode.Parent;
        }

        if (argumentNode is not ArgumentSyntax conditionArgument)
        {
            return;
        }

        // Find this argument in the invocation's argument list
        int conditionArgumentIndex = -1;
        for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
        {
            if (invocation.ArgumentList.Arguments[i].Span == conditionArgument.Span)
            {
                conditionArgumentIndex = i;
                break;
            }
        }

        if (conditionArgumentIndex == -1)
        {
            return;
        }

        // Get the actual argument from the invocation
        conditionArgument = invocation.ArgumentList.Arguments[conditionArgumentIndex];

        string title = string.Format(CultureInfo.InvariantCulture, Resources.DoNotNegateBooleanAssertionFix, properAssertMethodName);

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => FixNegatedAssertionAsync(context.Document, invocation, conditionArgument, cancellationToken),
                equivalenceKey: nameof(DoNotNegateBooleanAssertionFixer)),
            diagnostic);
    }

    private static async Task<Document> FixNegatedAssertionAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ArgumentSyntax conditionArgument,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Get the member access expression (Assert.IsTrue or Assert.IsFalse)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        ExpressionSyntax argumentExpression = conditionArgument.Expression;

        // Start with the CURRENT method name
        string currentMethodName = memberAccess.Name.Identifier.ValueText;
        ExpressionSyntax currentExpression = argumentExpression;

        // Iteratively remove negations one at a time
        while (true)
        {
            ExpressionSyntax unnegatedExpression = RemoveNegation(currentExpression);

            // If no negation was removed, we're done
            if (unnegatedExpression == currentExpression)
            {
                break;
            }

            // A negation was removed, so flip the method name
            currentMethodName = currentMethodName == "IsTrue" ? "IsFalse" : "IsTrue";
            currentExpression = unnegatedExpression;
        }

        // If nothing changed, return the original document
        if (currentExpression == argumentExpression && currentMethodName == memberAccess.Name.Identifier.ValueText)
        {
            return document;
        }

        // Create the new method name identifier
        IdentifierNameSyntax newMethodName = SyntaxFactory.IdentifierName(currentMethodName);

        // Create the new member access expression
        MemberAccessExpressionSyntax newMemberAccess = memberAccess.WithName(newMethodName);

        // Create the new argument with the unnegated expression
        ArgumentSyntax newArgument = conditionArgument.WithExpression(currentExpression);

        // Find the index of the condition argument in the invocation's argument list
        int conditionArgumentIndex = -1;
        for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
        {
            if (invocation.ArgumentList.Arguments[i].Span == conditionArgument.Span)
            {
                conditionArgumentIndex = i;
                break;
            }
        }

        if (conditionArgumentIndex == -1)
        {
            return document;
        }

        // Replace the condition argument in the arguments list
        SeparatedSyntaxList<ArgumentSyntax> newArguments = invocation.ArgumentList.Arguments.Replace(
            invocation.ArgumentList.Arguments[conditionArgumentIndex],
            newArgument);
        ArgumentListSyntax newArgumentList = invocation.ArgumentList.WithArguments(newArguments);

        // Create the new invocation expression
        InvocationExpressionSyntax newInvocation = invocation
            .WithExpression(newMemberAccess)
            .WithArgumentList(newArgumentList);

        // Replace the old invocation with the new one
        editor.ReplaceNode(invocation, newInvocation);

        return editor.GetChangedDocument();
    }

    private static ExpressionSyntax RemoveNegation(ExpressionSyntax expression)
    {
        // Handle parenthesized expressions - unwrap to find the negation
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            // Check if what's inside is a negation
            if (parenthesized.Expression is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } innerPrefixUnary)
            {
                // Remove the parentheses and the negation, return the operand (unwrapped)
                return UnwrapParentheses(innerPrefixUnary.Operand);
            }

            // Recursively check inside parentheses
            ExpressionSyntax inner = RemoveNegation(parenthesized.Expression);
            if (inner != parenthesized.Expression)
            {
                // We removed a negation from inside - return without the outer parentheses (unwrapped)
                return UnwrapParentheses(inner);
            }

            // No negation inside, return as-is
            return expression;
        }

        // Handle logical not operator (!) - remove ONLY this one negation
        if (expression is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } prefixUnary)
        {
            // Return the operand directly (unwrapped)
            return UnwrapParentheses(prefixUnary.Operand);
        }

        return expression;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        // Recursively unwrap all unnecessary parentheses
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

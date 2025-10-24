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
        => FixAll.Instance;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

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

        string title = string.Format(System.Globalization.CultureInfo.InvariantCulture, Resources.DoNotNegateBooleanAssertionFix, properAssertMethodName);

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => FixNegatedAssertionAsync(context.Document, invocation, properAssertMethodName, cancellationToken),
                equivalenceKey: nameof(DoNotNegateBooleanAssertionFixer)),
            diagnostic);
    }

    private static async Task<Document> FixNegatedAssertionAsync(
    Document document,
    InvocationExpressionSyntax invocation,
    string properAssertMethodName,
    CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Get the member access expression (Assert.IsTrue or Assert.IsFalse)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        // Get the argument list
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return document;
        }

        // Find the 'condition' argument (should be the first one or named 'condition')
        ArgumentSyntax? conditionArgument = null;
        int conditionArgumentIndex = -1;

        for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
        {
            ArgumentSyntax arg = invocation.ArgumentList.Arguments[i];
            if (arg.NameColon?.Name.Identifier.ValueText == "condition" || (arg.NameColon == null && i == 0))
            {
                conditionArgument = arg;
                conditionArgumentIndex = i;
                break;
            }
        }

        if (conditionArgument == null || conditionArgumentIndex == -1)
        {
            return document;
        }

        ExpressionSyntax argumentExpression = conditionArgument.Expression;

        // Remove ALL negations (for double negation in one step)
        ExpressionSyntax unnegatedExpression = RemoveAllNegations(argumentExpression);

        if (unnegatedExpression == argumentExpression)
        {
            // No negation found, nothing to fix
            return document;
        }

        // Always swap the method name
        IdentifierNameSyntax newMethodName = SyntaxFactory.IdentifierName(properAssertMethodName);
        MemberAccessExpressionSyntax newMemberAccess = memberAccess.WithName(newMethodName);
        ArgumentSyntax newArgument = conditionArgument.WithExpression(unnegatedExpression);

        SeparatedSyntaxList<ArgumentSyntax> newArguments = invocation.ArgumentList.Arguments.Replace(
            invocation.ArgumentList.Arguments[conditionArgumentIndex],
            newArgument);
        ArgumentListSyntax newArgumentList = invocation.ArgumentList.WithArguments(newArguments);

        InvocationExpressionSyntax newInvocation = invocation
            .WithExpression(newMemberAccess)
            .WithArgumentList(newArgumentList);

        editor.ReplaceNode(invocation, newInvocation);
        return editor.GetChangedDocument();
    }

    private static ExpressionSyntax RemoveAllNegations(ExpressionSyntax expression)
    {
        // Walk down parentheses and negations until we find the core expression
        ExpressionSyntax current = expression;

        while (true)
        {
            // Remove parentheses
            if (current is ParenthesizedExpressionSyntax parenthesized)
            {
                current = parenthesized.Expression;
                continue;
            }

            // Remove negation
            if (current is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } prefixUnary)
            {
                current = prefixUnary.Operand;
                continue;
            }

            // No more parentheses or negations
            break;
        }

        return current;
    }

    private sealed class FixAll : DocumentBasedFixAllProvider
    {
        public static readonly FixAll Instance = new();

        protected override async Task<Document?> FixAllAsync(
            FixAllContext fixAllContext,
            Document document,
            ImmutableArray<Diagnostic> diagnostics)
        {
            SyntaxNode? root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            // Collect all invocations that need to be replaced
            var nodesToReplace = new Dictionary<InvocationExpressionSyntax, InvocationExpressionSyntax>();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                if (node is not InvocationExpressionSyntax invocation)
                {
                    continue;
                }

                if (!diagnostic.Properties.TryGetValue(DoNotNegateBooleanAssertionAnalyzer.ProperAssertMethodNameKey, out string? properAssertMethodName) ||
                    properAssertMethodName is null)
                {
                    continue;
                }

                // Get the member access expression
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                if (invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                ArgumentSyntax? conditionArgument = null;
                int conditionArgumentIndex = -1;

                for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
                {
                    ArgumentSyntax arg = invocation.ArgumentList.Arguments[i];
                    if (arg.NameColon?.Name.Identifier.ValueText == "condition" || (arg.NameColon == null && i == 0))
                    {
                        conditionArgument = arg;
                        conditionArgumentIndex = i;
                        break;
                    }
                }

                if (conditionArgument == null || conditionArgumentIndex == -1)
                {
                    continue;
                }

                ExpressionSyntax argumentExpression = conditionArgument.Expression;
                ExpressionSyntax unnegatedExpression = RemoveAllNegations(argumentExpression);

                if (unnegatedExpression == argumentExpression)
                {
                    continue;
                }

                // Create the new method name identifier
                IdentifierNameSyntax newMethodName = SyntaxFactory.IdentifierName(properAssertMethodName);
                MemberAccessExpressionSyntax newMemberAccess = memberAccess.WithName(newMethodName);
                ArgumentSyntax newArgument = conditionArgument.WithExpression(unnegatedExpression);

                SeparatedSyntaxList<ArgumentSyntax> newArguments = invocation.ArgumentList.Arguments.Replace(
                    invocation.ArgumentList.Arguments[conditionArgumentIndex],
                    newArgument);
                ArgumentListSyntax newArgumentList = invocation.ArgumentList.WithArguments(newArguments);

                InvocationExpressionSyntax newInvocation = invocation
                    .WithExpression(newMemberAccess)
                    .WithArgumentList(newArgumentList);

                nodesToReplace[invocation] = newInvocation;
            }

            // Apply all replacements at once
            if (nodesToReplace.Count > 0)
            {
                root = root.ReplaceNodes(
                    nodesToReplace.Keys,
                    (originalNode, _) => nodesToReplace[originalNode]);

                return document.WithSyntaxRoot(root);
            }

            return document;
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsFixer
{
    private static async Task<Document> FixAssertMethodForAddArgumentModeAsync(Document document, Location conditionLocation, Location expectedLocation, Location actualLocation, SyntaxNode root, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        SyntaxNode conditionExpression = root.FindNode(conditionLocation.SourceSpan, getInnermostNodeForTie: true);
        ArgumentSyntax? conditionNode = conditionExpression.FirstAncestorOrSelf<ArgumentSyntax>();
        if (conditionNode?.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        if (root.FindNode(expectedLocation.SourceSpan) is not { } expectedNode
            || expectedNode is not ArgumentSyntax and not ExpressionSyntax)
        {
            return document;
        }

        if (root.FindNode(actualLocation.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax actualNode)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        GenericNameSyntax? explicitPredicateName = (conditionExpression as InvocationExpressionSyntax)?.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
            GenericNameSyntax name => name,
            _ => null,
        };
        if (properAssertMethodName is "ContainsOnly" or "Contains" or "DoesNotContain" &&
            explicitPredicateName is not null &&
            editor.SemanticModel.GetOperation(conditionExpression, cancellationToken) is IInvocationOperation { TargetMethod.Name: "All" or "Any", Arguments.Length: 2 })
        {
            editor.ReplaceNode(simpleNameSyntax, explicitPredicateName.WithIdentifier(SyntaxFactory.Identifier(properAssertMethodName)).WithTriviaFrom(simpleNameSyntax));
        }
        else
        {
            FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);
        }

        ArgumentListSyntax newArgumentList = argumentList;
        ExpressionSyntax expectedExpression = expectedNode switch
        {
            ArgumentSyntax argument => argument.Expression,
            ExpressionSyntax expression => expression,
            _ => throw new InvalidOperationException($"Unexpected node type for expected argument: {expectedNode.GetType()}"),
        };

        // Preserve the leading trivia from the original condition argument, remove trailing trivia
        ArgumentSyntax newExpectedArgument = SyntaxFactory.Argument(
            expectedExpression
                .WithLeadingTrivia(conditionNode.GetLeadingTrivia())
                .WithoutTrailingTrivia());

        int conditionIndex = argumentList.Arguments.IndexOf(conditionNode);

        // Build the new arguments list explicitly to preserve trivia correctly
        var newArguments = new List<ArgumentSyntax>();
        var newSeparators = new List<SyntaxToken>();

        // Add the new expected argument (replacing condition)
        newArguments.Add(newExpectedArgument);

        // Add separator after expected argument with just a standard comma
        newSeparators.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));

        // Add the new actual argument with a space as leading trivia (standard formatting after comma)
        ArgumentSyntax newActualArgument = SyntaxFactory.Argument(
            actualNode
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .WithLeadingTrivia(SyntaxFactory.Space));
        newArguments.Add(newActualArgument);

        // Add remaining arguments (e.g., message) with their original separators and trivia
        var originalSeparators = argumentList.Arguments.GetSeparators().ToList();
        for (int i = 0; i < argumentList.Arguments.Count; i++)
        {
            if (i == conditionIndex)
            {
                continue;
            }

            newSeparators.Add(i > 0 ? originalSeparators[i - 1] : SyntaxFactory.Token(SyntaxKind.CommaToken));
            newArguments.Add(argumentList.Arguments[i]);
        }

        newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments, newSeparators));

        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForAddTwoArgumentsModeAsync(Document document, Location conditionLocation, Location firstArgLocation, Location secondArgLocation, Location thirdArgLocation, SyntaxNode root, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        // Handle Contains with comparer: Assert.IsTrue(enumerable.Contains(item, comparer)) -> Assert.Contains(item, enumerable, comparer)
        if (root.FindNode(conditionLocation.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not { } conditionNode)
        {
            return document;
        }

        if (conditionNode.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        // FindNode may return ArgumentSyntax (outermost for tied spans) when the expression
        // is a direct child of an ArgumentSyntax in the inner invocation. Extract the expression.
        if (!TryGetExpressionFromNode(root, firstArgLocation, out ExpressionSyntax? firstArgNode))
        {
            return document;
        }

        if (!TryGetExpressionFromNode(root, secondArgLocation, out ExpressionSyntax? secondArgNode))
        {
            return document;
        }

        if (!TryGetExpressionFromNode(root, thirdArgLocation, out ExpressionSyntax? thirdArgNode))
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);

        int conditionIndex = argumentList.Arguments.IndexOf(conditionNode);
        if (conditionIndex != 0)
        {
            return document;
        }

        // Build the new arguments list: (firstArg, secondArg, thirdArg, ...remaining)
        var newArguments = new List<ArgumentSyntax>();
        var newSeparators = new List<SyntaxToken>();

        newArguments.Add(SyntaxFactory.Argument(
            firstArgNode
                .WithLeadingTrivia(conditionNode.GetLeadingTrivia())
                .WithoutTrailingTrivia()));

        newSeparators.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
        newArguments.Add(SyntaxFactory.Argument(
            secondArgNode
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .WithLeadingTrivia(SyntaxFactory.Space)));

        newSeparators.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
        newArguments.Add(SyntaxFactory.Argument(
            thirdArgNode
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .WithLeadingTrivia(SyntaxFactory.Space)));

        // Add remaining arguments (e.g., message) with their original separators and trivia
        var originalSeparators = argumentList.Arguments.GetSeparators().ToList();
        for (int i = conditionIndex + 1; i < argumentList.Arguments.Count; i++)
        {
            if (i - 1 < originalSeparators.Count)
            {
                newSeparators.Add(originalSeparators[i - 1]);
            }

            newArguments.Add(argumentList.Arguments[i]);
        }

        ArgumentListSyntax newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments, newSeparators));
        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    // FindNode may return an ArgumentSyntax when the location span coincides with
    // the span of an argument in the inner invocation (e.g. Contains(item, comparer)).
    // We handle this by extracting the expression from the ArgumentSyntax.
    private static bool TryGetExpressionFromNode(SyntaxNode root, Location location, [NotNullWhen(true)] out ExpressionSyntax? expression)
    {
        SyntaxNode? node = root.FindNode(location.SourceSpan);
        expression = node switch
        {
            ArgumentSyntax argument => argument.Expression,
            ExpressionSyntax expr => expr,
            _ => null,
        };

        return expression is not null;
    }
}

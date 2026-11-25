// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsFixer
{
    private static async Task<Document> FixAssertMethodForSimpleModeAsync(Document document, IReadOnlyList<Location> additionalLocations, SyntaxNode root, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);

        for (int i = 0; i < additionalLocations.Count; i += 2)
        {
            SyntaxNode condition = root.FindNode(additionalLocations[i].SourceSpan, getInnermostNodeForTie: true);
            if (condition.FirstAncestorOrSelf<ArgumentSyntax>() is not { } conditionNodeToBeReplaced ||
                (conditionNodeToBeReplaced.NameColon is not null && properAssertMethodName is not ("IsPositive" or "IsNegative")))
            {
                return document;
            }

            if (root.FindNode(additionalLocations[i + 1].SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax replacementExpressionNode)
            {
                return document;
            }

            // Preserve the leading trivia from the original argument, remove any trailing trivia from the replacement
            ArgumentSyntax newArgument = SyntaxFactory.Argument(
                replacementExpressionNode
                    .WithLeadingTrivia(conditionNodeToBeReplaced.GetLeadingTrivia())
                    .WithoutTrailingTrivia());
            if (conditionNodeToBeReplaced.NameColon is { } nameColon)
            {
                newArgument = newArgument.WithNameColon(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("value")).WithTriviaFrom(nameColon));
            }

            editor.ReplaceNode(conditionNodeToBeReplaced, newArgument);
        }

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForSimpleWithTypeArgumentModeAsync(Document document, Location conditionLocation, Location valueLocation, Location? typeCheckLocation, SyntaxNode root, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        SyntaxNode conditionExpression = root.FindNode(conditionLocation.SourceSpan, getInnermostNodeForTie: true);

        // When the type check is nested in the condition (e.g. 'collection.All(x => x is T)'), it is reported as a separate location.
        SyntaxNode typeCheckExpression = typeCheckLocation is null
            ? conditionExpression
            : root.FindNode(typeCheckLocation.SourceSpan, getInnermostNodeForTie: true);
        if (conditionExpression.FirstAncestorOrSelf<ArgumentSyntax>() is not { } conditionNode ||
            conditionNode.Parent is not ArgumentListSyntax argumentList ||
            root.FindNode(valueLocation.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax valueExpression ||
            GetCheckedType(typeCheckExpression) is not { } checkedType)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        if (properAssertMethodName == "IsInstanceOfType")
        {
            FixPositiveTypeAssertion();
        }
        else
        {
            editor.ReplaceNode(
                simpleNameSyntax,
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(properAssertMethodName),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(checkedType.WithoutTrivia())))
                .WithTriviaFrom(simpleNameSyntax));

            ArgumentSyntax valueArgument = CreateValueArgument(conditionNode, valueExpression);
            if (conditionNode.NameColon is not null)
            {
                valueArgument = valueArgument.WithNameColon(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(properAssertMethodName == "AreAllOfType" ? "collection" : "value"))
                        .WithTriviaFrom(conditionNode.NameColon));
            }

            editor.ReplaceNode(conditionNode, valueArgument);
        }

        return editor.GetChangedDocument();

        void FixPositiveTypeAssertion()
        {
            bool hasNamedArguments = argumentList.Arguments.Any(argument => argument.NameColon is not null);
            int conditionIndex = argumentList.Arguments.IndexOf(conditionNode);
            if (conditionIndex < 0)
            {
                return;
            }

            editor.ReplaceNode(simpleNameSyntax, SyntaxFactory.IdentifierName(properAssertMethodName).WithTriviaFrom(simpleNameSyntax));

            ArgumentSyntax valueArgument = CreateValueArgument(conditionNode, valueExpression);
            if (hasNamedArguments)
            {
                NameColonSyntax valueName = SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("value"));
                if (conditionNode.NameColon is { } originalName)
                {
                    valueName = valueName.WithTriviaFrom(originalName);
                }

                valueArgument = valueArgument.WithNameColon(valueName);
            }

            SeparatedSyntaxList<ArgumentSyntax> updatedArguments = argumentList.Arguments.Replace(conditionNode, valueArgument);

            if (hasNamedArguments)
            {
                for (int i = 0; i < updatedArguments.Count; i++)
                {
                    if (i != conditionIndex && updatedArguments[i].NameColon is null)
                    {
                        updatedArguments = updatedArguments.Replace(
                            updatedArguments[i],
                            updatedArguments[i].WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("message"))));
                    }
                }
            }

            ArgumentSyntax expectedTypeArgument = SyntaxFactory.Argument(
                SyntaxFactory.TypeOfExpression(checkedType.WithoutTrivia()))
                .WithAdditionalAnnotations(Formatter.Annotation);

            if (hasNamedArguments)
            {
                expectedTypeArgument = expectedTypeArgument.WithNameColon(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("expectedType")));
            }

            updatedArguments = updatedArguments.Insert(conditionIndex + 1, expectedTypeArgument);
            editor.ReplaceNode(argumentList, argumentList.WithArguments(updatedArguments));
        }

        static ArgumentSyntax CreateValueArgument(ArgumentSyntax condition, ExpressionSyntax value)
            => SyntaxFactory.Argument(
                value
                    .WithLeadingTrivia(condition.GetLeadingTrivia())
                    .WithoutTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

        static TypeSyntax? GetCheckedType(SyntaxNode condition)
        {
            while (condition is ParenthesizedExpressionSyntax parenthesized)
            {
                condition = parenthesized.Expression;
            }

            return condition switch
            {
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type } => type,
                IsPatternExpressionSyntax { Pattern: var pattern } when UnwrapPattern(pattern) is UnaryPatternSyntax { Pattern: var nestedPattern } => GetPatternType(nestedPattern),
                _ => null,
            };
        }

        static TypeSyntax? GetPatternType(PatternSyntax pattern)
            => UnwrapPattern(pattern) switch
            {
                TypePatternSyntax typePattern => typePattern.Type,

                // A type in a pattern can be parsed as a constant pattern and bound as a type.
                ConstantPatternSyntax { Expression: TypeSyntax type } => type,
                ConstantPatternSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => SyntaxFactory.ParseTypeName(memberAccess.ToString()),
                _ => null,
            };

        static PatternSyntax UnwrapPattern(PatternSyntax pattern)
        {
            while (pattern is ParenthesizedPatternSyntax parenthesized)
            {
                pattern = parenthesized.Pattern;
            }

            return pattern;
        }
    }
}

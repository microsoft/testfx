// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsFixer
{
    private static async Task<Document> FixAssertMethodForRemoveArgumentModeAsync(
        Document document,
        IReadOnlyList<Location> additionalLocations,
        SyntaxNode root,
        SimpleNameSyntax simpleNameSyntax,
        string properAssertMethodName,
        bool needsNullableBoolCast,
        CancellationToken cancellationToken)
    {
        // This doesn't properly handle cases like Assert.AreEqual(message: "My message", expected: true, actual: x)
        // The proper handling of this may be Assert.IsTrue(message: "My message", condition: x)
        // Or: Assert.IsTrue(x, "My message")
        // For now this is not handled.
        if (root.FindNode(additionalLocations[0].SourceSpan) is not ArgumentSyntax expectedArgumentToRemove)
        {
            return document;
        }

        if (expectedArgumentToRemove.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);

        int argumentIndexToRemove = argumentList.Arguments.IndexOf(expectedArgumentToRemove);
        IArgumentOperation? collectionArgument = properAssertMethodName == "IsNotNull" &&
            argumentList.Parent is { } invocationSyntax &&
            editor.SemanticModel.GetOperation(invocationSyntax, cancellationToken) is IInvocationOperation invocation
                ? invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1)
                : null;
        ArgumentListSyntax newArgumentList;
        if (additionalLocations.Count > 1 && needsNullableBoolCast &&
            root.FindNode(additionalLocations[1].SourceSpan) is ArgumentSyntax actualArgument)
        {
            Compilation compilation = editor.SemanticModel.Compilation;
            var castExpression = (CastExpressionSyntax)editor.Generator.CastExpression(
                compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(compilation.GetSpecialType(SpecialType.System_Boolean)),
                actualArgument.Expression);
            newArgumentList = argumentList.WithArguments(
                argumentList.Arguments.Replace(actualArgument, SyntaxFactory.Argument(castExpression)).RemoveAt(argumentIndexToRemove));
        }
        else if (collectionArgument is { } nonNullCollectionArgument &&
            nonNullCollectionArgument.Syntax.FirstAncestorOrSelf<ArgumentSyntax>() is { } actualArgumentToCast &&
            actualArgumentToCast.Expression.DescendantNodesAndSelf().Any(node => node is CollectionExpressionSyntax) &&
            nonNullCollectionArgument.Parameter is { Type: { } actualType })
        {
            var castExpression = (CastExpressionSyntax)editor.Generator.CastExpression(
                actualType.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
                actualArgumentToCast.Expression);
            newArgumentList = argumentList.WithArguments(
                argumentList.Arguments.Replace(actualArgumentToCast, actualArgumentToCast.WithExpression(castExpression)).RemoveAt(argumentIndexToRemove));
        }
        else
        {
            newArgumentList = argumentList.WithArguments(argumentList.Arguments.RemoveAt(argumentIndexToRemove));
        }

        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForRemoveArgumentAndReplaceArgumentModeAsync(
        Document document,
        IReadOnlyList<Location> additionalLocations,
        SyntaxNode root,
        SimpleNameSyntax simpleNameSyntax,
        string properAssertMethodName,
        CancellationToken cancellationToken)
    {
        // Handle collection count transformations:
        // Assert.AreEqual(0, list.Count) -> Assert.IsEmpty(list)
        // Assert.AreEqual(list.Count, 0) -> Assert.IsEmpty(list)
        if (root.FindNode(additionalLocations[0].SourceSpan) is not ArgumentSyntax expectedArgumentToRemove)
        {
            return document;
        }

        if (root.FindNode(additionalLocations[1].SourceSpan) is not ArgumentSyntax argumentToBeReplaced ||
            root.FindNode(additionalLocations[2].SourceSpan) is not ExpressionSyntax replacement)
        {
            return document;
        }

        if (expectedArgumentToRemove.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);

        int argumentIndexToRemove = argumentList.Arguments.IndexOf(expectedArgumentToRemove);
        ArgumentListSyntax newArgumentList = argumentList.ReplaceNode(argumentToBeReplaced, argumentToBeReplaced.WithExpression(replacement));
        newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.RemoveAt(argumentIndexToRemove));
        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForRemoveArgumentReplaceArgumentAndAddArgumentModeAsync(
        Document document,
        IReadOnlyList<Location> additionalLocations,
        SyntaxNode root,
        SimpleNameSyntax simpleNameSyntax,
        string properAssertMethodName,
        CancellationToken cancellationToken)
    {
        // Handle LINQ Count predicate transformations:
        // Assert.AreEqual(1, collection.Count(x => x == 1)) -> Assert.ContainsSingle(x => x == 1, collection)
        if (root.FindNode(additionalLocations[0].SourceSpan) is not ArgumentSyntax expectedArgumentToRemove)
        {
            return document;
        }

        if (root.FindNode(additionalLocations[1].SourceSpan, getInnermostNodeForTie: false) is not ArgumentSyntax argumentToBeReplaced ||
            root.FindNode(additionalLocations[2].SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax replacement ||
            root.FindNode(additionalLocations[3].SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax additionalArgument)
        {
            return document;
        }

        if (expectedArgumentToRemove.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, simpleNameSyntax, properAssertMethodName);

        // Calculate indices before any modifications
        int argumentIndexToRemove = argumentList.Arguments.IndexOf(expectedArgumentToRemove);
        int argumentIndexToReplace = argumentList.Arguments.IndexOf(argumentToBeReplaced);
        // Validate that both arguments were found
        if (argumentIndexToRemove == -1 || argumentIndexToReplace == -1)
        {
            return document;
        }

        // For ContainsSingle, we expect argumentIndexToRemove=0 (the constant 1) and argumentIndexToReplace=1 (the Count expression)
        // The general logic below handles any ordering, matching the pattern in FixAssertMethodForRemoveArgumentAndReplaceArgumentModeAsync

        // Replace the second argument with the predicate
        ArgumentSyntax newArgument = argumentToBeReplaced.WithExpression(replacement);
        ArgumentListSyntax newArgumentList = argumentList.ReplaceNode(argumentToBeReplaced, newArgument);

        // Remove the first argument - the index is still valid because ReplaceNode preserves structure
        newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.RemoveAt(argumentIndexToRemove));

        // Calculate where to insert the collection argument
        // After removing the first argument, if the replaced argument was after it, its index decreases by 1
        // We want to insert after the predicate (which is now at the adjusted index)
        int adjustedInsertionIndex = argumentIndexToReplace > argumentIndexToRemove ? argumentIndexToReplace - 1 : argumentIndexToReplace;
        newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.Insert(adjustedInsertionIndex + 1, SyntaxFactory.Argument(additionalArgument).WithAdditionalAnnotations(Formatter.Annotation)));

        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static void FixInvocationMethodName(DocumentEditor editor, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName)
        // NOTE: Switching Assert.IsTrue(x == y) to Assert.AreEqual(x, y) MAY produce an overload resolution error.
        // For example, Assert.AreEqual("string", true) will fail the inference for generic argument.
        // This is not very common and is currently not handled properly.
        // If needed, we can adjust the codefix to account for that case and
        // produce a GenericNameSyntax (e.g, AreEqual<object>) instead of IdentifierNameSyntax (e.g, AreEqual).
        => editor.ReplaceNode(simpleNameSyntax, SyntaxFactory.IdentifierName(properAssertMethodName));
}

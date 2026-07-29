// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Shared logic for the code fixes that replace an imperative "skip this test" guard at the top of a test method
/// with a declarative condition attribute (MSTEST0079, MSTEST0080).
/// </summary>
internal static class SkipGuardCodeFixHelper
{
    /// <summary>
    /// Removes the guard from the method and adds the given attribute to it.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="methodDeclaration">The test method holding the guard.</param>
    /// <param name="ifStatement">The guard to remove.</param>
    /// <param name="attributeName">The attribute name to add, without the 'Attribute' suffix.</param>
    /// <param name="arguments">The attribute arguments, as source text. Can be empty for a parameterless attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    public static async Task<Document> ReplaceGuardWithAttributeAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement,
        string attributeName,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        MethodDeclarationSyntax? modifiedMethod = RemoveIfStatementFromMethod(methodDeclaration, ifStatement);
        if (modifiedMethod is null)
        {
            return document;
        }

        editor.ReplaceNode(methodDeclaration, modifiedMethod.AddAttributeLists(CreateAttributeList(attributeName, arguments)));
        return editor.GetChangedDocument();
    }

    private static AttributeListSyntax CreateAttributeList(string attributeName, string[] arguments)
    {
        AttributeSyntax attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));

        if (arguments.Length > 0)
        {
            attribute = attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(
                        arguments.Select(argument => SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(argument))))));
        }

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
    }

    private static MethodDeclarationSyntax? RemoveIfStatementFromMethod(
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement)
    {
        MethodDeclarationSyntax trackedMethod = methodDeclaration.TrackNodes(ifStatement);
        IfStatementSyntax? trackedIfStatement = trackedMethod.GetCurrentNode(ifStatement);

        if (trackedIfStatement is null)
        {
            return null;
        }

        // Remove the blank line that separated the guard from the rest of the body (if any).
        if (trackedIfStatement.Parent is BlockSyntax block)
        {
            int index = block.Statements.IndexOf(trackedIfStatement);
            if (index >= 0 && index < block.Statements.Count - 1)
            {
                StatementSyntax nextStatement = block.Statements[index + 1];
                SyntaxTriviaList cleanedTrivia = new(nextStatement.GetLeadingTrivia().Where(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)));
                StatementSyntax newNextStatement = nextStatement.WithLeadingTrivia(cleanedTrivia);
                trackedMethod = trackedMethod.ReplaceNode(nextStatement, newNextStatement);
                trackedIfStatement = trackedMethod.GetCurrentNode(ifStatement);
            }
        }

        return trackedIfStatement is not null
            ? trackedMethod.RemoveNode(trackedIfStatement, SyntaxRemoveOptions.KeepNoTrivia)
            : null;
    }
}

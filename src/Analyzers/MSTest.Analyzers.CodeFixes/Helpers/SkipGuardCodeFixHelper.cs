// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Shared logic for the code fixes that replace an imperative "skip this test" guard at the top of a test method
/// with a declarative condition attribute (MSTEST0079, MSTEST0080, MSTEST0083).
/// </summary>
internal static class SkipGuardCodeFixHelper
{
    private const string MSTestNamespace = "global::Microsoft.VisualStudio.TestTools.UnitTesting";

    /// <summary>
    /// Removes the guard from the method and adds the given attribute to it.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="methodDeclaration">The test method holding the guard.</param>
    /// <param name="ifStatement">The guard to remove.</param>
    /// <param name="attributeTypeName">The metadata name of the attribute to add, relative to the MSTest namespace.</param>
    /// <param name="arguments">The attribute arguments, as source text relative to the MSTest namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="qualifyArguments">
    /// Whether each argument is relative to the MSTest namespace. Set to <see langword="false"/> for literals.
    /// </param>
    /// <returns>The updated document.</returns>
    public static async Task<Document> ReplaceGuardWithAttributeAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement,
        string attributeTypeName,
        string[] arguments,
        CancellationToken cancellationToken,
        bool qualifyArguments = true)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        MethodDeclarationSyntax? modifiedMethod = RemoveIfStatementFromMethod(methodDeclaration, ifStatement);
        if (modifiedMethod is null)
        {
            return document;
        }

        editor.ReplaceNode(methodDeclaration, modifiedMethod.AddAttributeLists(CreateAttributeList(attributeTypeName, arguments, qualifyArguments)));
        return editor.GetChangedDocument();
    }

    private static AttributeListSyntax CreateAttributeList(string attributeTypeName, string[] arguments, bool qualifyArguments)
    {
        // Generate root-qualified names and let the simplifier shorten them. The test method may be decorated with a
        // fully qualified '[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]' without importing the namespace,
        // or the file may declare a conflicting type or a 'Microsoft' alias, and anything less than a 'global::'
        // qualified name could bind to the wrong symbol or not compile at all.
        AttributeSyntax attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName($"{MSTestNamespace}.{attributeTypeName}").WithAdditionalAnnotations(Simplifier.Annotation));

        if (arguments.Length > 0)
        {
            attribute = attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(
                        arguments.Select(argument => SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression(qualifyArguments ? $"{MSTestNamespace}.{argument}" : argument)
                                .WithAdditionalAnnotations(Simplifier.Annotation))))));
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
                StatementSyntax newNextStatement = nextStatement.WithLeadingTrivia(
                    RemoveLeadingBlankLines(nextStatement.GetLeadingTrivia()));
                trackedMethod = trackedMethod.ReplaceNode(nextStatement, newNextStatement);
                trackedIfStatement = trackedMethod.GetCurrentNode(ifStatement);
            }
        }

        return trackedIfStatement is not null
            ? trackedMethod.RemoveNode(trackedIfStatement, SyntaxRemoveOptions.KeepNoTrivia)
            : null;
    }

    private static SyntaxTriviaList RemoveLeadingBlankLines(SyntaxTriviaList leadingTrivia)
    {
        // Only strip line endings from the whitespace run that precedes the first comment or directive. Filtering the
        // whole list would also drop the newline that terminates a comment, which would join the following statement
        // onto the comment line and silently comment the code out.
        int firstMeaningfulTrivia = 0;
        while (firstMeaningfulTrivia < leadingTrivia.Count &&
            (leadingTrivia[firstMeaningfulTrivia].IsKind(SyntaxKind.WhitespaceTrivia) ||
                leadingTrivia[firstMeaningfulTrivia].IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            firstMeaningfulTrivia++;
        }

        return new SyntaxTriviaList(
            leadingTrivia.Take(firstMeaningfulTrivia).Where(t => !t.IsKind(SyntaxKind.EndOfLineTrivia))
                .Concat(leadingTrivia.Skip(firstMeaningfulTrivia)));
    }
}

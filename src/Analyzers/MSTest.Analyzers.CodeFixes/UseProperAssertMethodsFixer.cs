// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Globalization;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseProperAssertMethodsFixer))]
[Shared]
public sealed class UseProperAssertMethodsFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseProperAssertMethodsRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        string? mode = diagnostic.Properties[UseProperAssertMethodsAnalyzer.CodeFixModeKey];
        string? properAssertMethodName = diagnostic.Properties[UseProperAssertMethodsAnalyzer.ProperAssertMethodNameKey];
        if (mode is null || properAssertMethodName is null)
        {
            Debug.Fail($"Both '{nameof(mode)}' and '{properAssertMethodName}' are expected to be non-null.");
            return;
        }

        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (diagnosticNode is not InvocationExpressionSyntax invocation)
        {
            Debug.Fail($"Is this an interesting scenario where IInvocationOperation for Assert call isn't associated with InvocationExpressionSyntax? SyntaxNode type: '{diagnosticNode.GetType()}', Text: '{diagnosticNode.GetText()}'");
            return;
        }

        SyntaxNode methodNameIdentifier = invocation.Expression;
        if (methodNameIdentifier is MemberAccessExpressionSyntax memberAccess)
        {
            methodNameIdentifier = memberAccess.Name;
        }

        if (methodNameIdentifier is not IdentifierNameSyntax identifierNameSyntax)
        {
            Debug.Fail($"Is this an interesting scenario where we are unable to retrieve IdentifierNameSyntax corresponding to the assert method? SyntaxNode type: '{methodNameIdentifier}', Text: '{methodNameIdentifier.GetText()}'.");
            return;
        }

        Func<CancellationToken, Task<Document>>? createChangedDocument = null;
        switch (mode)
        {
            case UseProperAssertMethodsAnalyzer.CodeFixModeSimple:
                createChangedDocument = ct => FixAssertMethodForSimpleModeAsync(context.Document, diagnostic.AdditionalLocations[0], diagnostic.AdditionalLocations[1], root, identifierNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeAddArgument:
                createChangedDocument = ct => FixAssertMethodForAddArgumentModeAsync(context.Document, diagnostic.AdditionalLocations[0], diagnostic.AdditionalLocations[1], diagnostic.AdditionalLocations[2], root, identifierNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeRemoveArgument:
                createChangedDocument = ct => FixAssertMethodForRemoveArgumentModeAsync(context.Document, diagnostic.AdditionalLocations, root, identifierNameSyntax, properAssertMethodName, diagnostic.Properties.ContainsKey(UseProperAssertMethodsAnalyzer.NeedsNullableBooleanCastKey), ct);
                break;
            default:
                break;
        }

        if (createChangedDocument is not null)
        {
            context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.UseProperAssertMethodsFix, properAssertMethodName),
                createChangedDocument,
                equivalenceKey: nameof(UseProperAssertMethodsFixer)),
            diagnostic);
        }
    }

    private static async Task<Document> FixAssertMethodForSimpleModeAsync(Document document, Location location1, Location location2, SyntaxNode root, IdentifierNameSyntax identifierNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        // This doesn't properly handle cases like Assert.IsTrue(message: "My message", condition: x == null)
        // The proper handling of this may be Assert.IsNull(message: "My message", value: x)
        // Or: Assert.IsNull(x, "My message")
        // For now this is not handled.
        if (root.FindNode(location1.SourceSpan) is not ArgumentSyntax node1)
        {
            return document;
        }

        if (root.FindNode(location2.SourceSpan) is not ExpressionSyntax node2)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, identifierNameSyntax, properAssertMethodName);
        editor.ReplaceNode(node1, SyntaxFactory.Argument(node2).WithAdditionalAnnotations(Formatter.Annotation));

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForAddArgumentModeAsync(Document document, Location location1, Location location2, Location location3, SyntaxNode root, IdentifierNameSyntax identifierNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        // This doesn't properly handle cases like Assert.IsTrue(message: "My message", condition: x == y)
        // The proper handling of this may be Assert.AreEqual(message: "My message", expected: x, actual: y)
        // Or: Assert.AreEqual(x, y, "My message")
        // For now this is not handled.
        if (root.FindNode(location1.SourceSpan) is not ArgumentSyntax node1)
        {
            return document;
        }

        if (node1.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        if (root.FindNode(location2.SourceSpan) is not ExpressionSyntax node2)
        {
            return document;
        }

        if (root.FindNode(location3.SourceSpan) is not ExpressionSyntax node3)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, identifierNameSyntax, properAssertMethodName);

        ArgumentListSyntax newArgumentList = argumentList;
        newArgumentList = newArgumentList.ReplaceNode(node1, SyntaxFactory.Argument(node2).WithAdditionalAnnotations(Formatter.Annotation));
        int insertionIndex = argumentList.Arguments.IndexOf(node1) + 1;
        newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.Insert(insertionIndex, SyntaxFactory.Argument(node3).WithAdditionalAnnotations(Formatter.Annotation)));

        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> FixAssertMethodForRemoveArgumentModeAsync(
        Document document,
        IReadOnlyList<Location> additionalLocations,
        SyntaxNode root,
        IdentifierNameSyntax identifierNameSyntax,
        string properAssertMethodName,
        bool needsNullableBoolCast,
        CancellationToken cancellationToken)
    {
        // This doesn't properly handle cases like Assert.AreEqual(message: "My message", expected: true, actual: x)
        // The proper handling of this may be Assert.IsTrue(message: "My message", condition: x)
        // Or: Assert.IsTrue(x, "My message")
        // For now this is not handled.
        if (root.FindNode(additionalLocations[0].SourceSpan) is not ArgumentSyntax node1)
        {
            return document;
        }

        if (node1.Parent is not ArgumentListSyntax argumentList)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        FixInvocationMethodName(editor, identifierNameSyntax, properAssertMethodName);

        int argumentIndexToRemove = argumentList.Arguments.IndexOf(node1);
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
        else
        {
            newArgumentList = argumentList.WithArguments(argumentList.Arguments.RemoveAt(argumentIndexToRemove));
        }

        editor.ReplaceNode(argumentList, newArgumentList);

        return editor.GetChangedDocument();
    }

    private static void FixInvocationMethodName(DocumentEditor editor, IdentifierNameSyntax identifierNameSyntax, string properAssertMethodName)
    {
        IdentifierNameSyntax updatedIdentifier = identifierNameSyntax.WithIdentifier(SyntaxFactory.Identifier(identifierNameSyntax.Identifier.LeadingTrivia, properAssertMethodName, identifierNameSyntax.Identifier.TrailingTrivia));
        editor.ReplaceNode(identifierNameSyntax, updatedIdentifier);
    }
}

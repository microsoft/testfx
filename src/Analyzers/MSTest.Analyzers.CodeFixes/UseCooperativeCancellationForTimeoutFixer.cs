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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UseCooperativeCancellationForTimeoutAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCooperativeCancellationForTimeoutFixer))]
[Shared]
public sealed class UseCooperativeCancellationForTimeoutFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseCooperativeCancellationForTimeoutRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the attribute syntax node identified by the diagnostic
        SyntaxNode attributeNode = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
        if (attributeNode is not AttributeSyntax attributeSyntax)
        {
            return;
        }

        // Register code fix to add CooperativeCancellation = true
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseCooperativeCancellationForTimeoutFix,
                createChangedDocument: c => AddCooperativeCancellationAsync(context.Document, attributeSyntax, c),
                equivalenceKey: $"{nameof(UseCooperativeCancellationForTimeoutFixer)}.AddCooperativeCancellation"),
            diagnostic);

        // Register code fix to replace [TestMethod] with [TaskRunTestMethod]
        // Find the test method to check if it uses [TestMethod] attribute
        if (attributeSyntax.Parent?.Parent is MethodDeclarationSyntax methodDeclaration)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.UseTaskRunTestMethodFix,
                    createChangedDocument: c => ReplaceWithTaskRunTestMethodAsync(context.Document, methodDeclaration, c),
                    equivalenceKey: $"{nameof(UseCooperativeCancellationForTimeoutFixer)}.UseTaskRunTestMethod"),
                diagnostic);
        }
    }

    private static async Task<Document> AddCooperativeCancellationAsync(Document document, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeSyntax newAttributeSyntax;

        if (attributeSyntax.ArgumentList == null)
        {
            // No argument list exists, create one with CooperativeCancellation = true
            AttributeArgumentSyntax cooperativeCancellationArg = SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals("CooperativeCancellation"),
                null,
                SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));

            newAttributeSyntax = attributeSyntax.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(cooperativeCancellationArg)));
        }
        else
        {
            // Argument list exists, check if CooperativeCancellation is already specified
            bool hasCooperativeCancellation = false;
            List<AttributeArgumentSyntax> newArguments = [];

            foreach (AttributeArgumentSyntax arg in attributeSyntax.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "CooperativeCancellation")
                {
                    // Replace existing CooperativeCancellation = false with true
                    hasCooperativeCancellation = true;
                    AttributeArgumentSyntax newArg = arg.WithExpression(
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
                    newArguments.Add(newArg);
                }
                else
                {
                    newArguments.Add(arg);
                }
            }

            if (!hasCooperativeCancellation)
            {
                // Add CooperativeCancellation = true to existing arguments
                AttributeArgumentSyntax cooperativeCancellationArg = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.NameEquals("CooperativeCancellation"),
                    null,
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));

                newArguments.Add(cooperativeCancellationArg);
            }

            newAttributeSyntax = attributeSyntax.WithArgumentList(
                attributeSyntax.ArgumentList.WithArguments(
                    SyntaxFactory.SeparatedList(newArguments)));
        }

        // Replace the old attribute with the new one
        editor.ReplaceNode(attributeSyntax, newAttributeSyntax);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> ReplaceWithTaskRunTestMethodAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        // Find the TestMethod and Timeout attributes
        AttributeSyntax? testMethodAttribute = null;
        AttributeSyntax? timeoutAttribute = null;
        int timeoutValue = 0;

        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string attributeName = attribute.Name.ToString();
                
                if (attributeName is "TestMethod" or "TestMethodAttribute")
                {
                    testMethodAttribute = attribute;
                }
                else if (attributeName is "Timeout" or "TimeoutAttribute")
                {
                    timeoutAttribute = attribute;
                    
                    // Extract timeout value from the first argument
                    if (attribute.ArgumentList?.Arguments.Count > 0)
                    {
                        var firstArg = attribute.ArgumentList.Arguments[0];
                        if (firstArg.Expression is LiteralExpressionSyntax literalExpr &&
                            literalExpr.Token.Value is int value)
                        {
                            timeoutValue = value;
                        }
                    }
                }
            }
        }

        if (testMethodAttribute is null || timeoutAttribute is null)
        {
            // Can't apply the fix without both attributes
            return document;
        }

        // Create the new TaskRunTestMethod attribute with timeout parameter
        AttributeArgumentSyntax timeoutArg = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(timeoutValue)));

        AttributeSyntax newAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("TaskRunTestMethod"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(timeoutArg)));

        // Replace the TestMethod attribute with TaskRunTestMethod
        editor.ReplaceNode(testMethodAttribute, newAttribute);
        
        // Remove the Timeout attribute
        editor.RemoveNode(timeoutAttribute);

        return editor.GetChangedDocument();
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = nameof(StringAssertToAssertFixer))]
[Shared]
public sealed class StringAssertToAssertFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.StringAssertToAssertRuleId);

    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics.First();
        if (!diagnostic.Properties.TryGetValue(StringAssertToAssertAnalyzer.ProperAssertMethodNameKey, out string? properAssertMethodName)
            || properAssertMethodName == null)
        {
            return;
        }

        var simpleNameSyntax = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .First();

        // Register a code fix that will invoke the fix operation.
        string title = string.Format(CodeFixResources.StringAssertToAssertTitle, properAssertMethodName);
        CodeAction action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => FixStringAssertAsync(context.Document, root, simpleNameSyntax, properAssertMethodName, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> FixStringAssertAsync(
        Document document,
        SyntaxNode root,
        SimpleNameSyntax simpleNameSyntax,
        string properAssertMethodName,
        CancellationToken cancellationToken)
    {
        // Find the invocation expression that contains the SimpleNameSyntax
        if (simpleNameSyntax.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault() is not InvocationExpressionSyntax invocationExpr)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Replace StringAssert with Assert in the member access expression
        if (invocationExpr.Expression is MemberAccessExpressionSyntax memberAccessExpr)
        {
            // Change StringAssert.MethodName to Assert.MethodName
            var newMemberAccess = memberAccessExpr.WithExpression(SyntaxFactory.IdentifierName("Assert"));
            editor.ReplaceNode(memberAccessExpr, newMemberAccess);
        }

        // Swap the first two arguments
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpr.ArgumentList.Arguments;
        if (arguments.Count >= 2)
        {
            ArgumentSyntax firstArg = arguments[0];
            ArgumentSyntax secondArg = arguments[1];

            // Create new argument list with swapped first two arguments
            var newArguments = new List<ArgumentSyntax>(arguments.Count);
            newArguments.Add(secondArg); // Second argument becomes first
            newArguments.Add(firstArg);  // First argument becomes second
            
            // Add remaining arguments if any
            for (int i = 2; i < arguments.Count; i++)
            {
                newArguments.Add(arguments[i]);
            }

            ArgumentListSyntax newArgumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArguments));
            InvocationExpressionSyntax newInvocationExpr = invocationExpr.WithArgumentList(newArgumentList);
            editor.ReplaceNode(invocationExpr, newInvocationExpr);
        }

        return editor.GetChangedDocument();
    }
}
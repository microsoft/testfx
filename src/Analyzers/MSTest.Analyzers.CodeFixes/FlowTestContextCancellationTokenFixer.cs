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
/// Code fixer for <see cref="FlowTestContextCancellationTokenAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FlowTestContextCancellationTokenFixer))]
[Shared]
public sealed class FlowTestContextCancellationTokenFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.FlowTestContextCancellationTokenRuleId);

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

        // Find the invocation expression identified by the diagnostic
        SyntaxNode node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
        if (node is not InvocationExpressionSyntax invocationExpression)
        {
            return;
        }

        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add TestContext.CancellationTokenSource.Token parameter",
                createChangedDocument: c => AddCancellationTokenParameterAsync(context.Document, invocationExpression, c),
                equivalenceKey: "AddTestContextCancellationToken"),
            diagnostic);
    }

    private static async Task<Document> AddCancellationTokenParameterAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        
        // Create the TestContext.CancellationTokenSource.Token expression
        var testContextExpression = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("TestContext"),
                SyntaxFactory.IdentifierName("CancellationTokenSource")),
            SyntaxFactory.IdentifierName("Token"));

        // Get the current arguments
        ArgumentListSyntax currentArguments = invocationExpression.ArgumentList;
        
        // Check if there's already a CancellationToken argument that we should replace
        var newArguments = new List<ArgumentSyntax>();
        bool replacedCancellationToken = false;

        foreach (ArgumentSyntax arg in currentArguments.Arguments)
        {
            // Simple heuristic: if the argument is CancellationToken.None or default, replace it
            if (IsCancellationTokenArgument(arg))
            {
                newArguments.Add(SyntaxFactory.Argument(testContextExpression));
                replacedCancellationToken = true;
            }
            else
            {
                newArguments.Add(arg);
            }
        }

        // If we didn't replace an existing one, add it as a new argument
        if (!replacedCancellationToken)
        {
            newArguments.Add(SyntaxFactory.Argument(testContextExpression));
        }

        // Create the new argument list
        ArgumentListSyntax newArgumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArguments));

        // Create the new invocation expression
        InvocationExpressionSyntax newInvocation = invocationExpression.WithArgumentList(newArgumentList);

        // Replace the old invocation with the new one
        editor.ReplaceNode(invocationExpression, newInvocation);

        return editor.GetChangedDocument();
    }

    private static bool IsCancellationTokenArgument(ArgumentSyntax argument)
    {
        // Check if this is CancellationToken.None
        if (argument.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "None" &&
            memberAccess.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.ValueText == "CancellationToken")
        {
            return true;
        }

        // Check if this is default(CancellationToken) or similar
        if (argument.Expression is DefaultExpressionSyntax ||
            (argument.Expression is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.DefaultKeyword)))
        {
            return true;
        }

        return false;
    }
}
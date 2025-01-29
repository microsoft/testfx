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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidAssertAreSameWithValueTypesFixer))]
[Shared]
public sealed class AvoidAssertAreSameWithValueTypesFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidAssertAreSameWithValueTypesRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        string? replacement = diagnostic.Properties[AvoidAssertAreSameWithValueTypesAnalyzer.ReplacemenyKey]
            ?? throw ApplicationStateGuard.Unreachable();

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

        if (methodNameIdentifier is not SimpleNameSyntax simpleNameSyntax)
        {
            Debug.Fail($"Is this an interesting scenario where we are unable to retrieve SimpleNameSyntax corresponding to the assert method? SyntaxNode type: '{methodNameIdentifier}', Text: '{methodNameIdentifier.GetText()}'.");
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.AvoidAssertAreSameWithValueTypesFix, replacement),
                ct => FixMethodNameAsync(context.Document, simpleNameSyntax, replacement, ct),
                equivalenceKey: nameof(AvoidAssertAreSameWithValueTypesFixer)),
            diagnostic);
    }

    private static async Task<Document> FixMethodNameAsync(Document document, SimpleNameSyntax simpleNameSyntax, string properAssertMethodName, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(simpleNameSyntax, simpleNameSyntax.WithIdentifier(SyntaxFactory.Identifier(properAssertMethodName)));
        return editor.GetChangedDocument();
    }
}

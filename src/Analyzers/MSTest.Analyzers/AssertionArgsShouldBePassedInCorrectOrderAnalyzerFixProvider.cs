// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MyAnalyzerCodeFixProvider)), Shared]
internal class AssertionArgsShouldBePassedInCorrectOrderAnalyzerFixProvider : CodeFixProvider
{
    private const string title = "Replace with Debug.WriteLine";

    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(MyAnalyzerAnalyzer.DiagnosticId); }
    }

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var invocationExpr = root.FindNode(diagnosticSpan) as InvocationExpressionSyntax;
        var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => ReplaceWithDebugWriteLineAsync(context.Document, memberAccessExpr, c),
                equivalenceKey: title),
            diagnostic);
    }

    private async Task<Document> ReplaceWithDebugWriteLineAsync(Document document, MemberAccessExpressionSyntax memberAccessExpr, CancellationToken cancellationToken)
    {
        var oldExpression = memberAccessExpr;
        var newExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Debug"), SyntaxFactory.IdentifierName("WriteLine"));

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.ReplaceNode(oldExpression, newExpression);

        return document.WithSyntaxRoot(newRoot);
    }
}

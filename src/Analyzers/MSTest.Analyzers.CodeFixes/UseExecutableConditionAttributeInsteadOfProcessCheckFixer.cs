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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseExecutableConditionAttributeInsteadOfProcessCheckFixer))]
[Shared]
public sealed class UseExecutableConditionAttributeInsteadOfProcessCheckFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseExecutableConditionAttributeInsteadOfProcessCheckRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        string? executable = diagnostic.Properties[UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer.ExecutableKey];
        if (executable is null)
        {
            return;
        }

        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        MethodDeclarationSyntax? methodDeclaration = diagnosticNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        IfStatementSyntax? ifStatement = diagnosticNode.FirstAncestorOrSelf<IfStatementSyntax>();

        if (methodDeclaration is null || ifStatement is null)
        {
            return;
        }

        string executableLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(executable)).ToString();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseExecutableConditionAttributeInsteadOfProcessCheckFix,
                createChangedDocument: ct => SkipGuardCodeFixHelper.ReplaceGuardWithAttributeAsync(
                    context.Document,
                    methodDeclaration,
                    ifStatement,
                    "ExecutableConditionAttribute",
                    [executableLiteral],
                    ct,
                    qualifyArguments: false),
                equivalenceKey: nameof(UseExecutableConditionAttributeInsteadOfProcessCheckFixer)),
            diagnostic);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCIConditionAttributeInsteadOfEnvironmentCheckFixer))]
[Shared]
public sealed class UseCIConditionAttributeInsteadOfEnvironmentCheckFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseCIConditionAttributeInsteadOfEnvironmentCheckRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        string? conditionMode = diagnostic.Properties[UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer.ConditionModeKey];
        if (conditionMode is not (UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer.IncludeMode or UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer.ExcludeMode))
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

        // 'CIConditionAttribute' has a single constructor taking the mode, so it's always spelled out.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseCIConditionAttributeInsteadOfEnvironmentCheckFix,
                createChangedDocument: ct => SkipGuardCodeFixHelper.ReplaceGuardWithAttributeAsync(
                    context.Document, methodDeclaration, ifStatement, "CIConditionAttribute", [$"ConditionMode.{conditionMode}"], ct),
                equivalenceKey: nameof(UseCIConditionAttributeInsteadOfEnvironmentCheckFixer)),
            diagnostic);
    }
}

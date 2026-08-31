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
/// Code fixer for <see cref="UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseArchitectureConditionAttributeInsteadOfRuntimeCheckFixer))]
[Shared]
public sealed class UseArchitectureConditionAttributeInsteadOfRuntimeCheckFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseArchitectureConditionAttributeInsteadOfRuntimeCheckRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        string? isNegatedStr = diagnostic.Properties[UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer.IsNegatedKey];
        string? architecture = diagnostic.Properties[UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer.ArchitectureKey];

        if (isNegatedStr is null || architecture is null || !bool.TryParse(isNegatedStr, out bool isNegated))
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

        // '!=' guards keep the test on that architecture only (include mode, which is the default and needs a
        // single argument). '==' guards skip the test on that architecture, so the mode must be spelled out.
        string[] arguments = isNegated
            ? [$"TestArchitectures.{architecture}"]
            : ["ConditionMode.Exclude", $"TestArchitectures.{architecture}"];

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseArchitectureConditionAttributeInsteadOfRuntimeCheckFix,
                createChangedDocument: ct => SkipGuardCodeFixHelper.ReplaceGuardWithAttributeAsync(
                    context.Document, methodDeclaration, ifStatement, "ArchitectureConditionAttribute", arguments, ct),
                equivalenceKey: nameof(UseArchitectureConditionAttributeInsteadOfRuntimeCheckFixer)),
            diagnostic);
    }
}

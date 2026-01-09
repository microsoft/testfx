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
/// Code fix for <see cref="AvoidOutRefTestMethodParametersAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidOutRefTestMethodParametersFixer))]
[Shared]
public sealed class AvoidOutRefTestMethodParametersFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIds.AvoidOutRefTestMethodParametersRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // The diagnostic is reported on the method, so we can directly get the method declaration
        if (root.FindNode(diagnosticSpan) is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AvoidOutRefTestMethodParametersFix,
                createChangedDocument: c => RemoveOutRefModifiersAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(AvoidOutRefTestMethodParametersFixer)),
            diagnostic);
    }

    private static async Task<Document> RemoveOutRefModifiersAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        foreach (ParameterSyntax parameter in methodDeclaration.ParameterList.Parameters)
        {
            int indexToRemove = parameter.Modifiers.IndexOf(SyntaxKind.OutKeyword);
            if (indexToRemove < 0)
            {
                indexToRemove = parameter.Modifiers.IndexOf(SyntaxKind.RefKeyword);
            }

            if (indexToRemove >= 0)
            {
                editor.ReplaceNode(parameter, (node, _) =>
                {
                    var parameter = (ParameterSyntax)node;
                    return parameter.WithModifiers(parameter.Modifiers.RemoveAt(indexToRemove)).WithLeadingTrivia(parameter.GetLeadingTrivia());
                });
            }
        }

        return editor.GetChangedDocument();
    }
}

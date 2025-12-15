// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

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

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        // Find the method declaration identified by the diagnostic.
        MethodDeclarationSyntax? methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AvoidOutRefTestMethodParametersFix,
                createChangedSolution: c => RemoveOutRefModifiersAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(AvoidOutRefTestMethodParametersFixer)),
            diagnostic);
    }

    private static async Task<Solution> RemoveOutRefModifiersAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Remove out/ref modifiers from parameters
        ParameterListSyntax newParameterList = methodDeclaration.ParameterList;
        SeparatedSyntaxList<ParameterSyntax> newParameters = newParameterList.Parameters;

        bool hasChanges = false;
        for (int i = 0; i < newParameters.Count; i++)
        {
            ParameterSyntax parameter = newParameters[i];
            SyntaxTokenList filteredModifiers = SyntaxFactory.TokenList(
                parameter.Modifiers.Where(m => !m.IsKind(SyntaxKind.OutKeyword) && !m.IsKind(SyntaxKind.RefKeyword)));

            if (filteredModifiers.Count != parameter.Modifiers.Count)
            {
                ParameterSyntax newParameter = parameter.WithModifiers(filteredModifiers);
                newParameters = newParameters.Replace(parameter, newParameter);
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.WithParameterList(
                newParameterList.WithParameters(newParameters));

            editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
        }

        Document newDocument = editor.GetChangedDocument();
        return newDocument.Project.Solution;
    }
}

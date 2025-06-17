// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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

        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseCooperativeCancellationForTimeoutFix,
                createChangedDocument: c => AddCooperativeCancellationAsync(context.Document, attributeSyntax, c),
                equivalenceKey: nameof(UseCooperativeCancellationForTimeoutFixer)),
            diagnostic);
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
            List<AttributeArgumentSyntax> newArguments = new();

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
}
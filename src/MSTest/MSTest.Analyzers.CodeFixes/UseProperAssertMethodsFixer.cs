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
/// Code fixer for <see cref="UseProperAssertMethodsAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseProperAssertMethodsFixer))]
[Shared]
public sealed partial class UseProperAssertMethodsFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = [DiagnosticIds.UseProperAssertMethodsRuleId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        string? mode = diagnostic.Properties[UseProperAssertMethodsAnalyzer.CodeFixModeKey];
        string? properAssertMethodName = diagnostic.Properties[UseProperAssertMethodsAnalyzer.ProperAssertMethodNameKey];
        if (mode is null || properAssertMethodName is null)
        {
            Debug.Fail($"Both '{nameof(mode)}' and '{properAssertMethodName}' are expected to be non-null.");
            return;
        }

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

        Func<CancellationToken, Task<Document>>? createChangedDocument = null;
        switch (mode)
        {
            case UseProperAssertMethodsAnalyzer.CodeFixModeSimple:
                createChangedDocument = ct => FixAssertMethodForSimpleModeAsync(context.Document, diagnostic.AdditionalLocations, root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeSimpleWithTypeArgument:
                createChangedDocument = ct => FixAssertMethodForSimpleWithTypeArgumentModeAsync(context.Document, diagnostic.AdditionalLocations[0], diagnostic.AdditionalLocations[1], diagnostic.AdditionalLocations.Count > 2 ? diagnostic.AdditionalLocations[2] : null, root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeAddArgument:
                createChangedDocument = ct => FixAssertMethodForAddArgumentModeAsync(context.Document, diagnostic.AdditionalLocations[0], diagnostic.AdditionalLocations[1], diagnostic.AdditionalLocations[2], root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeAddTwoArguments:
                createChangedDocument = ct => FixAssertMethodForAddTwoArgumentsModeAsync(context.Document, diagnostic.AdditionalLocations[0], diagnostic.AdditionalLocations[1], diagnostic.AdditionalLocations[2], diagnostic.AdditionalLocations[3], root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeRemoveArgument:
                createChangedDocument = ct => FixAssertMethodForRemoveArgumentModeAsync(context.Document, diagnostic.AdditionalLocations, root, simpleNameSyntax, properAssertMethodName, diagnostic.Properties.ContainsKey(UseProperAssertMethodsAnalyzer.NeedsNullableBooleanCastKey), ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeRemoveArgumentAndReplaceArgument:
                createChangedDocument = ct => FixAssertMethodForRemoveArgumentAndReplaceArgumentModeAsync(context.Document, diagnostic.AdditionalLocations, root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            case UseProperAssertMethodsAnalyzer.CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument:
                createChangedDocument = ct => FixAssertMethodForRemoveArgumentReplaceArgumentAndAddArgumentModeAsync(context.Document, diagnostic.AdditionalLocations, root, simpleNameSyntax, properAssertMethodName, ct);
                break;
            default:
                break;
        }

        if (createChangedDocument is not null)
        {
            context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.UseProperAssertMethodsFix, properAssertMethodName),
                createChangedDocument,
                equivalenceKey: nameof(UseProperAssertMethodsFixer)),
            diagnostic);
        }
    }
}

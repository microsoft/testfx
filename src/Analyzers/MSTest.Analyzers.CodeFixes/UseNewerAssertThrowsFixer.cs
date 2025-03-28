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

/// <summary>
/// Code fixer for <see cref="UseNewerAssertThrowsAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNewerAssertThrowsFixer))]
[Shared]
public sealed class UseNewerAssertThrowsFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseNewerAssertThrowsRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

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

        if (methodNameIdentifier is not GenericNameSyntax genericNameSyntax)
        {
            Debug.Fail($"Is this an interesting scenario where we are unable to retrieve GenericNameSyntax corresponding to the assert method? SyntaxNode type: '{methodNameIdentifier}', Text: '{methodNameIdentifier.GetText()}'.");
            return;
        }

        string updatedMethodName = genericNameSyntax.Identifier.Text switch
        {
            "ThrowsException" => "ThrowsExactly",
            "ThrowsExceptionAsync" => "ThrowsExactlyAsync",
            // The analyzer should report a diagnostic only for ThrowsException and ThrowsExceptionAsync
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.UseNewerAssertThrows, updatedMethodName),
                ct => Task.FromResult(context.Document.WithSyntaxRoot(UpdateMethodName(new SyntaxEditor(root, context.Document.Project.Solution.Workspace), invocation, genericNameSyntax, updatedMethodName))),
                equivalenceKey: nameof(UseProperAssertMethodsFixer)),
            diagnostic);
    }

    private static SyntaxNode UpdateMethodName(SyntaxEditor editor, InvocationExpressionSyntax invocation, GenericNameSyntax genericNameSyntax, string updatedMethodName)
    {
        editor.ReplaceNode(genericNameSyntax, genericNameSyntax.WithIdentifier(SyntaxFactory.Identifier(updatedMethodName).WithTriviaFrom(genericNameSyntax.Identifier)));

        // The object[] parameter to format the message is named parameters in the old ThrowsException[Async] methods, but is named messageArgs in the new ThrowsExactly[Async] methods.
        if (invocation.ArgumentList.Arguments.FirstOrDefault(arg => arg.NameColon is { Name.Identifier.Text: "parameters" }) is { } arg)
        {
            editor.ReplaceNode(arg.NameColon!.Name, arg.NameColon!.Name.WithIdentifier(SyntaxFactory.Identifier("messageArgs").WithTriviaFrom(arg.NameColon.Name.Identifier)));
        }

        return editor.GetChangedRoot();
    }
}

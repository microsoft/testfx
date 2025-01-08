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
using Microsoft.CodeAnalysis.Simplification;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNewerAssertThrowsFixer))]
[Shared]
public sealed class UseNewerAssertThrowsFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseNewerAssertThrowsRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

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
                ct => Task.FromResult(context.Document.WithSyntaxRoot(UpdateMethodName(new SyntaxEditor(root, context.Document.Project.Solution.Workspace), invocation, genericNameSyntax, updatedMethodName, diagnostic.AdditionalLocations))),
                equivalenceKey: nameof(UseProperAssertMethodsFixer)),
            diagnostic);
    }

    private static SyntaxNode UpdateMethodName(SyntaxEditor editor, InvocationExpressionSyntax invocation, GenericNameSyntax genericNameSyntax, string updatedMethodName, IReadOnlyList<Location> additionalLocations)
    {
        editor.ReplaceNode(genericNameSyntax, genericNameSyntax.WithIdentifier(SyntaxFactory.Identifier(updatedMethodName).WithTriviaFrom(genericNameSyntax.Identifier)));

        // The object[] parameter to format the message is named parameters in the old ThrowsException[Async] methods, but is named messageArgs in the new ThrowsExactly[Async] methods.
        if (invocation.ArgumentList.Arguments.FirstOrDefault(arg => arg.NameColon is { Name.Identifier.Text: "parameters" }) is { } arg)
        {
            editor.ReplaceNode(arg.NameColon!.Name, arg.NameColon!.Name.WithIdentifier(SyntaxFactory.Identifier("messageArgs").WithTriviaFrom(arg.NameColon.Name.Identifier)));
        }

        if (additionalLocations.Count != 1)
        {
            return editor.GetChangedRoot();
        }

        // The existing ThrowsException call is using the Func<object> overload. The new ThrowsExactly method does not have this overload, so we need to adjust.
        // This is a best effort handling.
        SyntaxNode actionArgument = editor.OriginalRoot.FindNode(additionalLocations[0].SourceSpan, getInnermostNodeForTie: true);

        if (actionArgument is ParenthesizedLambdaExpressionSyntax lambdaSyntax)
        {
            if (lambdaSyntax.ExpressionBody is not null)
            {
                editor.ReplaceNode(
                    lambdaSyntax.ExpressionBody,
                    AssignToDiscard(lambdaSyntax.ExpressionBody));
            }
            else if (lambdaSyntax.Block is not null)
            {
                // This is more complex. We need to iterate through all descendants of type ReturnStatementSyntax, and split it into two statements.
                // The first statement will be an assignment expression to a discard, and the second statement will be 'return;'.
                // We may even need to add extra braces in case the return statement (for example) is originally inside an if statement without braces.
                // For example:
                // if (condition)
                //     return Whatever;
                // should be converted to:
                // if (condition)
                // {
                //     _ = Whatever;
                //     return;
                // }
                // Keep in mind: When descending into descendant nodes, we shouldn't descend into potential other lambda expressions or local functions.
                IEnumerable<ReturnStatementSyntax> returnStatements = lambdaSyntax.Block.DescendantNodes(descendIntoChildren: node => node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)).OfType<ReturnStatementSyntax>();
                foreach (ReturnStatementSyntax returnStatement in returnStatements)
                {
                    if (returnStatement.Expression is not { } returnExpression)
                    {
                        // This should be an error in user code.
                        continue;
                    }

                    ExpressionStatementSyntax returnReplacement = SyntaxFactory.ExpressionStatement(AssignToDiscard(returnStatement.Expression));

                    if (returnStatement.Parent is BlockSyntax blockSyntax)
                    {
                        editor.InsertAfter(returnStatement, SyntaxFactory.ReturnStatement());
                        editor.ReplaceNode(returnStatement, returnReplacement);
                    }
                    else
                    {
                        editor.ReplaceNode(
                            returnStatement,
                            SyntaxFactory.Block(
                                returnReplacement,
                                SyntaxFactory.ReturnStatement()));
                    }
                }
            }
        }
        else if (actionArgument is ExpressionSyntax expressionSyntax)
        {
            editor.ReplaceNode(
                expressionSyntax,
                SyntaxFactory.ParenthesizedLambdaExpression(
                    SyntaxFactory.ParameterList(),
                    block: null,
                    expressionBody: AssignToDiscard(SyntaxFactory.InvocationExpression(SyntaxFactory.ParenthesizedExpression(expressionSyntax).WithAdditionalAnnotations(Simplifier.Annotation)))));
        }

        return editor.GetChangedRoot();
    }

    private static AssignmentExpressionSyntax AssignToDiscard(ExpressionSyntax expression)
        => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("_"), expression);
}

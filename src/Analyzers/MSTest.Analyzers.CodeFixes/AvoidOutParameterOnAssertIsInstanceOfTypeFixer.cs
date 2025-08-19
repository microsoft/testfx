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

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for CS1615: Argument should not be passed with the 'out' keyword when using Assert.IsInstanceOfType.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidOutParameterOnAssertIsInstanceOfTypeFixer))]
[Shared]
public sealed class AvoidOutParameterOnAssertIsInstanceOfTypeFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create("CS1615");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode diagnosticNode = root.FindNode(context.Span, getInnermostNodeForTie: false);

        ArgumentSyntax? outArgument = (diagnosticNode as ArgumentSyntax) ?? diagnosticNode.Parent as ArgumentSyntax;
        if ((outArgument?.Parent as ArgumentListSyntax)?.Parent is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (!IsAssertIsInstanceOfTypeCall(invocation))
        {
            return;
        }

        (string? variableName, TypeSyntax? type) = ExtractVariableNameAndType(outArgument);
        if (variableName is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AvoidOutParameterOnAssertIsInstanceOfTypeFix,
                createChangedDocument: ct => FixIsInstanceOfTypeCallAsync(context.Document, invocation, outArgument, variableName, type, ct),
                equivalenceKey: nameof(AvoidOutParameterOnAssertIsInstanceOfTypeFixer)),
            context.Diagnostics);
    }

    private static bool IsAssertIsInstanceOfTypeCall(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax
        {
            Name: GenericNameSyntax { Identifier.ValueText: "IsInstanceOfType" },
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" } or
                        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Assert" }
        };

    private static (string? VariableName, TypeSyntax? TypeSyntax) ExtractVariableNameAndType(ArgumentSyntax outArgument)
        => outArgument.Expression switch
        {
            DeclarationExpressionSyntax { Type: { } type, Designation: SingleVariableDesignationSyntax singleVar } => (singleVar.Identifier.ValueText, type),
            IdentifierNameSyntax identifierName => (identifierName.Identifier.ValueText, null),
            _ => (null, null),
        };

    private static async Task<Document> FixIsInstanceOfTypeCallAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ArgumentSyntax outArgument,
        string variableName,
        TypeSyntax? type,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        InvocationExpressionSyntax newInvocation = invocation
            .WithArgumentList(invocation.ArgumentList.WithArguments(invocation.ArgumentList.Arguments.Remove(outArgument)))
            .WithoutLeadingTrivia();

        if (type is not null)
        {
            if (invocation.Parent is not ExpressionStatementSyntax expressionStatement)
            {
                return document;
            }

            VariableDeclarationSyntax variableDeclaration = SyntaxFactory.VariableDeclaration(
                type)
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(variableName))
                        .WithInitializer(SyntaxFactory.EqualsValueClause(newInvocation))));

            LocalDeclarationStatementSyntax declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            editor.ReplaceNode(expressionStatement, declarationStatement.WithTriviaFrom(expressionStatement));

            return editor.GetChangedDocument();
        }

        editor.ReplaceNode(invocation, SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(variableName),
            newInvocation).WithLeadingTrivia(invocation.GetLeadingTrivia()));

        return editor.GetChangedDocument();
    }
}

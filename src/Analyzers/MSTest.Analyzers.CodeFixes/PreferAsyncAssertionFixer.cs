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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="PreferAsyncAssertionAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferAsyncAssertionFixer))]
[Shared]
public sealed class PreferAsyncAssertionFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.PreferAsyncAssertionRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode diagnosticNode = root.FindNode(context.Span);

        if (diagnosticNode.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault() is not { } invocationExpression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseAsyncAssertionFix,
                createChangedDocument: ct => UseAsyncAssertionAsync(context.Document, invocationExpression, ct),
                equivalenceKey: nameof(PreferAsyncAssertionFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> UseAsyncAssertionAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        InvocationExpressionSyntax newInvocationExpression = ReplaceAssertMethodName(invocationExpression);
        if (TryGetActionArgumentIndex(invocationExpression, semanticModel, cancellationToken, out int actionArgumentIndex) &&
            TryReplaceAction(newInvocationExpression.ArgumentList.Arguments[actionArgumentIndex], out ArgumentSyntax? newArgument))
        {
            newInvocationExpression = newInvocationExpression.WithArgumentList(
                newInvocationExpression.ArgumentList.WithArguments(newInvocationExpression.ArgumentList.Arguments.Replace(newInvocationExpression.ArgumentList.Arguments[actionArgumentIndex], newArgument)));
        }

        ExpressionSyntax awaitExpression = CreateAwaitExpression(invocationExpression, newInvocationExpression);

        if (invocationExpression.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } methodDeclaration)
        {
            MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.ReplaceNode(invocationExpression, awaitExpression);
            editor.ReplaceNode(methodDeclaration, AddAsyncModifierAndTaskReturnType(newMethodDeclaration, methodDeclaration, semanticModel, cancellationToken));
        }
        else
        {
            editor.ReplaceNode(invocationExpression, awaitExpression);
        }

        return editor.GetChangedDocument();
    }

    private static InvocationExpressionSyntax ReplaceAssertMethodName(InvocationExpressionSyntax invocationExpression)
        => invocationExpression.Expression switch
        {
            MemberAccessExpressionSyntax memberAccessExpression => invocationExpression.WithExpression(memberAccessExpression.WithName(AppendAsyncSuffix(memberAccessExpression.Name))),
            SimpleNameSyntax simpleName => invocationExpression.WithExpression(AppendAsyncSuffix(simpleName)),
            _ => invocationExpression,
        };

    private static SimpleNameSyntax AppendAsyncSuffix(SimpleNameSyntax name)
        => name switch
        {
            GenericNameSyntax genericName => genericName.WithIdentifier(SyntaxFactory.Identifier(
                genericName.Identifier.LeadingTrivia,
                genericName.Identifier.ValueText + "Async",
                genericName.Identifier.TrailingTrivia)),
            IdentifierNameSyntax identifierName => identifierName.WithIdentifier(SyntaxFactory.Identifier(
                identifierName.Identifier.LeadingTrivia,
                identifierName.Identifier.ValueText + "Async",
                identifierName.Identifier.TrailingTrivia)),
            _ => name,
        };

    private static bool TryGetActionArgumentIndex(
        InvocationExpressionSyntax invocationExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out int actionArgumentIndex)
    {
        if (semanticModel.GetOperation(invocationExpression, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            actionArgumentIndex = -1;
            return false;
        }

        foreach (IArgumentOperation argumentOperation in invocationOperation.Arguments)
        {
            if (argumentOperation.Parameter?.Name != "action")
            {
                continue;
            }

            ArgumentSyntax? argumentSyntax = argumentOperation.Syntax.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
            if (argumentSyntax is null)
            {
                continue;
            }

            actionArgumentIndex = invocationExpression.ArgumentList.Arguments.IndexOf(argumentSyntax);
            return actionArgumentIndex >= 0;
        }

        actionArgumentIndex = -1;
        return false;
    }

    private static ExpressionSyntax CreateAwaitExpression(InvocationExpressionSyntax originalInvocationExpression, InvocationExpressionSyntax newInvocationExpression)
    {
        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(newInvocationExpression.WithoutLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        return NeedsParenthesizedAwait(originalInvocationExpression)
            ? SyntaxFactory.ParenthesizedExpression(awaitExpression)
                .WithLeadingTrivia(originalInvocationExpression.GetLeadingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation)
            : awaitExpression
                .WithLeadingTrivia(originalInvocationExpression.GetLeadingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static bool NeedsParenthesizedAwait(InvocationExpressionSyntax invocationExpression)
        => invocationExpression.Parent switch
        {
            MemberAccessExpressionSyntax memberAccessExpression when memberAccessExpression.Expression == invocationExpression => true,
            ElementAccessExpressionSyntax elementAccessExpression when elementAccessExpression.Expression == invocationExpression => true,
            InvocationExpressionSyntax parentInvocationExpression when parentInvocationExpression.Expression == invocationExpression => true,
            ConditionalAccessExpressionSyntax conditionalAccessExpression when conditionalAccessExpression.Expression == invocationExpression => true,
            _ => false,
        };

    private static bool TryReplaceAction(ArgumentSyntax argument, [NotNullWhen(true)] out ArgumentSyntax? newArgument)
    {
        if (!TryReplaceActionExpression(argument.Expression, out ExpressionSyntax? newExpression))
        {
            newArgument = null;
            return false;
        }

        newArgument = argument.WithExpression(newExpression);
        return true;
    }

    private static bool TryReplaceActionExpression(ExpressionSyntax expression, [NotNullWhen(true)] out ExpressionSyntax? newExpression)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesizedExpression &&
            TryReplaceActionExpression(parenthesizedExpression.Expression, out ExpressionSyntax? parenthesizedNewExpression))
        {
            newExpression = parenthesizedNewExpression.WithTriviaFrom(expression);
            return true;
        }

        if (expression is CastExpressionSyntax castExpression &&
            TryReplaceActionExpression(castExpression.Expression, out ExpressionSyntax? castNewExpression))
        {
            newExpression = castNewExpression.WithTriviaFrom(expression);
            return true;
        }

        if (expression is AnonymousMethodExpressionSyntax anonymousMethodExpression &&
            TryGetBlockedTaskExpressionFromBlock(anonymousMethodExpression.Block, out ExpressionSyntax? anonymousMethodAsyncExpression))
        {
            newExpression = SyntaxFactory.ParenthesizedLambdaExpression()
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithBody(anonymousMethodAsyncExpression.WithTriviaFrom(anonymousMethodExpression.Block))
                .WithTriviaFrom(expression);
            return true;
        }

        if (expression is not LambdaExpressionSyntax lambdaExpression ||
            !TryGetBlockedTaskExpressionFromLambda(lambdaExpression, out ExpressionSyntax? asyncExpression))
        {
            newExpression = null;
            return false;
        }

        LambdaExpressionSyntax newLambdaExpression = lambdaExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.WithBody(asyncExpression.WithTriviaFrom(lambdaExpression.Body)),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.WithBody(asyncExpression.WithTriviaFrom(lambdaExpression.Body)),
            _ => lambdaExpression,
        };

        newExpression = newLambdaExpression;
        return true;
    }

    private static MethodDeclarationSyntax AddAsyncModifierAndTaskReturnType(
        MethodDeclarationSyntax methodDeclaration,
        MethodDeclarationSyntax originalMethodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;
        bool isAsync = newMethodDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AsyncKeyword));

        if (!isAsync)
        {
            newMethodDeclaration = newMethodDeclaration.WithModifiers(newMethodDeclaration.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword)));
        }

        bool wasVoid = originalMethodDeclaration.ReturnType.IsVoid();

        if (wasVoid)
        {
            newMethodDeclaration = newMethodDeclaration.WithReturnType(GetTaskReturnType(originalMethodDeclaration, semanticModel, cancellationToken).WithTriviaFrom(newMethodDeclaration.ReturnType));
            if (newMethodDeclaration.ExpressionBody is { } expressionBody)
            {
                newMethodDeclaration = ConvertExpressionBodyToBlock(newMethodDeclaration, expressionBody);
            }
        }
        else if (!isAsync && IsTaskOrValueTaskReturnType(originalMethodDeclaration, semanticModel, cancellationToken) && newMethodDeclaration.Body is { } body)
        {
            newMethodDeclaration = newMethodDeclaration.WithBody((BlockSyntax)new AwaitableReturnStatementRewriter().Visit(body)!);
        }

        return newMethodDeclaration.WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static MethodDeclarationSyntax ConvertExpressionBodyToBlock(MethodDeclarationSyntax methodDeclaration, ArrowExpressionClauseSyntax expressionBody)
        => methodDeclaration
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(expressionBody.Expression)
                    .WithLeadingTrivia(expressionBody.GetLeadingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation)));

    private static TypeSyntax GetTaskReturnType(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        INamedTypeSymbol? taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
        return taskSymbol is not null &&
            SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSpeculativeTypeInfo(methodDeclaration.ReturnType.SpanStart, SyntaxFactory.IdentifierName("Task"), SpeculativeBindingOption.BindAsTypeOrNamespace).Type,
                taskSymbol)
            ? SyntaxFactory.IdentifierName("Task")
            : SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task");
    }

    private static bool IsTaskOrValueTaskReturnType(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        INamedTypeSymbol? taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
        INamedTypeSymbol? valueTaskSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
        ITypeSymbol? returnTypeSymbol = semanticModel.GetTypeInfo(methodDeclaration.ReturnType, cancellationToken).Type;
        return (taskSymbol is not null && SymbolEqualityComparer.Default.Equals(returnTypeSymbol, taskSymbol)) ||
            (valueTaskSymbol is not null && SymbolEqualityComparer.Default.Equals(returnTypeSymbol, valueTaskSymbol));
    }

    private static bool TryGetBlockedTaskExpressionFromLambda(LambdaExpressionSyntax lambdaExpression, [NotNullWhen(true)] out ExpressionSyntax? asyncExpression)
    {
        if (lambdaExpression.Body is ExpressionSyntax expressionBody)
        {
            return TryGetBlockedTaskExpression(expressionBody, out asyncExpression);
        }

        if (lambdaExpression.Body is BlockSyntax blockSyntax)
        {
            return TryGetBlockedTaskExpressionFromBlock(blockSyntax, out asyncExpression);
        }

        asyncExpression = null;
        return false;
    }

    private static bool TryGetBlockedTaskExpressionFromBlock(BlockSyntax blockSyntax, [NotNullWhen(true)] out ExpressionSyntax? asyncExpression)
    {
        if (blockSyntax.Statements.Count == 1 &&
            blockSyntax.Statements[0] is ExpressionStatementSyntax expressionStatement)
        {
            return TryGetBlockedTaskExpression(expressionStatement.Expression, out asyncExpression);
        }

        if (blockSyntax.Statements.Count == 1 &&
            blockSyntax.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression })
        {
            return TryGetBlockedTaskExpression(returnExpression, out asyncExpression);
        }

        asyncExpression = null;
        return false;
    }

    private static bool TryGetBlockedTaskExpression(ExpressionSyntax expression, [NotNullWhen(true)] out ExpressionSyntax? asyncExpression)
    {
        ExpressionSyntax currentExpression = WalkDownParentheses(expression);
        if (currentExpression is InvocationExpressionSyntax getResultInvocation &&
            getResultInvocation.ArgumentList.Arguments.Count == 0 &&
            getResultInvocation.Expression is MemberAccessExpressionSyntax getResultMemberAccess &&
            getResultMemberAccess.Name.Identifier.ValueText == PreferAsyncAssertionAnalyzer.GetResultMethodName &&
            WalkDownParentheses(getResultMemberAccess.Expression) is InvocationExpressionSyntax getAwaiterInvocation &&
            getAwaiterInvocation.ArgumentList.Arguments.Count == 0 &&
            getAwaiterInvocation.Expression is MemberAccessExpressionSyntax getAwaiterMemberAccess &&
            getAwaiterMemberAccess.Name.Identifier.ValueText == PreferAsyncAssertionAnalyzer.GetAwaiterMethodName)
        {
            asyncExpression = WalkDownParentheses(getAwaiterMemberAccess.Expression);
            return true;
        }

        asyncExpression = null;
        return false;
    }

    private static ExpressionSyntax WalkDownParentheses(ExpressionSyntax expression)
    {
        ExpressionSyntax currentExpression = expression;
        while (currentExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            currentExpression = parenthesizedExpression.Expression;
        }

        return currentExpression;
    }

    private sealed class AwaitableReturnStatementRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            => node;

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            List<StatementSyntax>? rewrittenStatements = null;

            for (int i = 0; i < node.Statements.Count; i++)
            {
                StatementSyntax statement = node.Statements[i];
                if (statement is ReturnStatementSyntax { Expression: { } returnExpression } returnStatement)
                {
                    rewrittenStatements ??= AddUnchangedStatements(node.Statements, i);
                    rewrittenStatements.AddRange(CreateAwaitAndReturnStatements(returnStatement, returnExpression));
                    continue;
                }

                var rewrittenStatement = (StatementSyntax)Visit(statement)!;
                if (rewrittenStatements is not null)
                {
                    rewrittenStatements.Add(rewrittenStatement);
                }
                else if (!ReferenceEquals(statement, rewrittenStatement))
                {
                    rewrittenStatements = AddUnchangedStatements(node.Statements, i);
                    rewrittenStatements.Add(rewrittenStatement);
                }
            }

            return rewrittenStatements is null
                ? node
                : node.WithStatements(SyntaxFactory.List(rewrittenStatements));
        }

        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
            => node.Expression is { } expression
                ? SyntaxFactory.Block(CreateAwaitAndReturnStatements(node, expression)).WithAdditionalAnnotations(Formatter.Annotation)
                : node;

        private static List<StatementSyntax> AddUnchangedStatements(SyntaxList<StatementSyntax> statements, int endIndex)
        {
            var rewrittenStatements = new List<StatementSyntax>(statements.Count + 1);
            for (int j = 0; j < endIndex; j++)
            {
                rewrittenStatements.Add(statements[j]);
            }

            return rewrittenStatements;
        }

        private static StatementSyntax[] CreateAwaitAndReturnStatements(ReturnStatementSyntax returnStatement, ExpressionSyntax expression)
        {
            ExpressionStatementSyntax awaitStatement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AwaitExpression(expression.WithoutLeadingTrivia()))
                .WithLeadingTrivia(returnStatement.GetLeadingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            ReturnStatementSyntax newReturnStatement = returnStatement
                .WithExpression(null)
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker)
                .WithAdditionalAnnotations(Formatter.Annotation);

            return [awaitStatement, newReturnStatement];
        }
    }
}

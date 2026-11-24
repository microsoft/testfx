// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers;

public sealed partial class PreferAsyncAssertionFixer
{
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
}

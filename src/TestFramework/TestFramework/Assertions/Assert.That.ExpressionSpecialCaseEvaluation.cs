// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
    /// <summary>
    /// Evaluates an <c>&amp;&amp;</c> or <c>||</c> binary expression with short-circuit semantics
    /// and caches both the operand values (when evaluated) and the resulting bool.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
#endif
    private static bool TryEvaluateShortCircuitBinary(BinaryExpression binaryExpr, Dictionary<Expression, object?> cache)
    {
        EvaluateAllSubExpressions(binaryExpr.Left, cache);
        if (!cache.TryGetValue(binaryExpr.Left, out object? leftValue) || leftValue is not bool leftBool)
        {
            // Left couldn't be evaluated to a bool — let the default leaf evaluation path
            // attempt to compile the whole node so we surface a coherent failure.
            return false;
        }

        bool isAndAlso = binaryExpr.NodeType == ExpressionType.AndAlso;
        bool shouldEvaluateRight = isAndAlso ? leftBool : !leftBool;

        if (!shouldEvaluateRight)
        {
            cache[binaryExpr] = leftBool;
            return true;
        }

        EvaluateAllSubExpressions(binaryExpr.Right, cache);
        if (!cache.TryGetValue(binaryExpr.Right, out object? rightValue) || rightValue is not bool rightBool)
        {
            return false;
        }

        cache[binaryExpr] = isAndAlso ? leftBool && rightBool : leftBool || rightBool;
        return true;
    }

    /// <summary>
    /// Evaluates a <c>??</c> binary expression with short-circuit semantics, walking Right
    /// only when Left is null. The actual result is computed by rebuilding the expression so
    /// any user-supplied <see cref="BinaryExpression.Conversion"/> is preserved.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
#endif
    private static bool TryEvaluateCoalesce(BinaryExpression binaryExpr, Dictionary<Expression, object?> cache)
    {
        EvaluateAllSubExpressions(binaryExpr.Left, cache);
        if (!cache.TryGetValue(binaryExpr.Left, out object? leftValue) || ReferenceEquals(leftValue, FailedToEvaluateSentinel))
        {
            return false;
        }

        if (leftValue is not null)
        {
            // Rebuild with the cached Left so a user-supplied Conversion still runs.
            Expression rebuilt = ReplaceSubExpressionsWithConstants(binaryExpr, cache);
            cache[binaryExpr] = Expression.Lambda(rebuilt).Compile().DynamicInvoke();
            return true;
        }

        EvaluateAllSubExpressions(binaryExpr.Right, cache);
        Expression rebuiltWithRight = ReplaceSubExpressionsWithConstants(binaryExpr, cache);
        cache[binaryExpr] = Expression.Lambda(rebuiltWithRight).Compile().DynamicInvoke();
        return true;
    }

    /// <summary>
    /// Evaluates a ternary expression by walking <see cref="ConditionalExpression.Test"/> first
    /// and only the selected branch, then caching the branch's result as the conditional's value.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
#endif
    private static bool TryEvaluateConditional(ConditionalExpression conditionalExpr, Dictionary<Expression, object?> cache)
    {
        EvaluateAllSubExpressions(conditionalExpr.Test, cache);
        if (!cache.TryGetValue(conditionalExpr.Test, out object? testValue) || testValue is not bool testBool)
        {
            return false;
        }

        Expression chosenBranch = testBool ? conditionalExpr.IfTrue : conditionalExpr.IfFalse;
        EvaluateAllSubExpressions(chosenBranch, cache);
        if (cache.TryGetValue(chosenBranch, out object? branchValue))
        {
            cache[conditionalExpr] = branchValue;
            return true;
        }

        return false;
    }
}

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
    /// Evaluates an expression tree and caches all sub-expression values to avoid re-evaluation.
    /// This ensures expressions with side effects (like method calls) are only executed once.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateAllSubExpressions(Expression, Dictionary<Expression, Object>)")]
#endif
    private static bool EvaluateExpression(Expression expr, Dictionary<Expression, object?> cache)
    {
        // Use a single-pass evaluation that only evaluates each expression once
        EvaluateAllSubExpressions(expr, cache);

        // The root expression should now be cached with a bool value when the walk succeeded.
        if (cache.TryGetValue(expr, out object? result) && result is bool boolResult)
        {
            return boolResult;
        }

        // The walk did not produce a bool for the root (e.g., a sub-expression threw and was
        // cached as the failure sentinel, then the rebuilt parent could not be evaluated either).
        // Re-invoke the original lambda so the user sees the actual exception thrown by their
        // assertion code rather than an unrelated InvalidCastException from the sentinel.
        return Expression.Lambda<Func<bool>>(expr).Compile().Invoke();
    }

    private static bool RequiresSinglePassEvaluation(Expression expr)
        => expr switch
        {
            // Assignments and compound assignments are side-effecting by definition; ensure they
            // always take the single-pass evaluator so the fast path doesn't run them once before
            // EvaluateAllSubExpressions runs them again on the failure-diagnostic path.
            BinaryExpression binaryExpr when IsAssignmentBinaryNodeType(binaryExpr.NodeType) => true,

            BinaryExpression binaryExpr => binaryExpr.Method is not null
                || RequiresSinglePassEvaluation(binaryExpr.Left)
                || RequiresSinglePassEvaluation(binaryExpr.Right),

            UnaryExpression unaryExpr when IsAssignmentUnaryNodeType(unaryExpr.NodeType) => true,

            UnaryExpression unaryExpr => unaryExpr.Method is not null
                || RequiresSinglePassEvaluation(unaryExpr.Operand),

            MemberExpression memberExpr => (memberExpr.Expression is not null && RequiresSinglePassEvaluation(memberExpr.Expression))
                || memberExpr.Member.MemberType == MemberTypes.Property,

            ConditionalExpression conditionalExpr => RequiresSinglePassEvaluation(conditionalExpr.Test)
                || RequiresSinglePassEvaluation(conditionalExpr.IfTrue)
                || RequiresSinglePassEvaluation(conditionalExpr.IfFalse),

            TypeBinaryExpression typeBinaryExpr => RequiresSinglePassEvaluation(typeBinaryExpr.Expression),

            MethodCallExpression or InvocationExpression or NewExpression or ListInitExpression or MemberInitExpression
                or NewArrayExpression or IndexExpression => true,

            _ => false,
        };

    /// <summary>
    /// Evaluates only the *sub-children* of a writable assignment target (the Left of an Assign or
    /// compound assignment, or the Operand of a Pre/Post Increment/Decrement). Walking the writable
    /// wrapper itself would invoke its property/indexer getter once for caching, and the rebuilt
    /// assignment would then invoke it a second time (compound assignments and Increment/Decrement
    /// must read the current value). Restricting the walk to receiver/index arguments ensures the
    /// getter runs exactly once — inside the rebuilt assignment.
    ///
    /// Writable targets are restricted by LINQ expression semantics to <see cref="MemberExpression"/>,
    /// <see cref="IndexExpression"/>, or <see cref="ParameterExpression"/>; the latter has no
    /// sub-children to walk.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateAllSubExpressions(Expression, Dictionary<Expression, Object>)")]
#endif
    private static void EvaluateAssignmentTargetSubChildren(Expression target, Dictionary<Expression, object?> cache)
    {
        // ParameterExpression and static-member MemberExpression are also valid writable targets,
        // but they have no sub-children to walk.
        switch (target)
        {
            case MemberExpression memberExpr when memberExpr.Expression is not null:
                EvaluateAllSubExpressions(memberExpr.Expression, cache);
                break;

            case IndexExpression indexExpr:
                if (indexExpr.Object is not null)
                {
                    EvaluateAllSubExpressions(indexExpr.Object, cache);
                }

                foreach (Expression argument in indexExpr.Arguments)
                {
                    EvaluateAllSubExpressions(argument, cache);
                }

                break;
        }
    }

    private static bool IsAssignmentBinaryNodeType(ExpressionType nodeType)
        => nodeType is ExpressionType.Assign
            or ExpressionType.AddAssign or ExpressionType.AddAssignChecked
            or ExpressionType.SubtractAssign or ExpressionType.SubtractAssignChecked
            or ExpressionType.MultiplyAssign or ExpressionType.MultiplyAssignChecked
            or ExpressionType.DivideAssign or ExpressionType.ModuloAssign
            or ExpressionType.PowerAssign
            or ExpressionType.AndAssign or ExpressionType.OrAssign or ExpressionType.ExclusiveOrAssign
            or ExpressionType.LeftShiftAssign or ExpressionType.RightShiftAssign;

    private static bool IsAssignmentUnaryNodeType(ExpressionType nodeType)
        => nodeType is ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
            or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign;
}

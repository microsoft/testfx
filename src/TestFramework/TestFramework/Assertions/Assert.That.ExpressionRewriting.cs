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
    /// Replaces sub-expressions in an expression tree with constant values from the cache.
    /// This prevents re-execution of side effects when the parent expression is compiled and invoked.
    /// Uses the <c>Update</c> helpers on each node type so node metadata such as
    /// <see cref="BinaryExpression.Method"/> and <see cref="BinaryExpression.Conversion"/> are preserved.
    /// </summary>
    private static Expression ReplaceSubExpressionsWithConstants(Expression expr, Dictionary<Expression, object?> cache)
    {
        switch (expr)
        {
            case BinaryExpression binaryExpr:
                return binaryExpr.Update(
                    ReplaceChildWithConstant(binaryExpr.Left, cache),
                    binaryExpr.Conversion,
                    ReplaceChildWithConstant(binaryExpr.Right, cache));

            case UnaryExpression unaryExpr:
                return unaryExpr.Update(ReplaceChildWithConstant(unaryExpr.Operand, cache));

            case MemberExpression memberExpr when memberExpr.Expression is not null:
                return memberExpr.Update(ReplaceChildWithConstant(memberExpr.Expression, cache));

            case MethodCallExpression callExpr:
                Expression? obj = callExpr.Object is not null
                    ? ReplaceChildWithConstant(callExpr.Object, cache)
                    : null;
                IEnumerable<Expression> callArgs = callExpr.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache));
                return callExpr.Update(obj, callArgs);

            case ConditionalExpression conditionalExpr:
                return conditionalExpr.Update(
                    ReplaceChildWithConstant(conditionalExpr.Test, cache),
                    ReplaceChildWithConstant(conditionalExpr.IfTrue, cache),
                    ReplaceChildWithConstant(conditionalExpr.IfFalse, cache));

            case TypeBinaryExpression typeBinaryExpr:
                return typeBinaryExpr.Update(ReplaceChildWithConstant(typeBinaryExpr.Expression, cache));

            case InvocationExpression invocationExpr:
                return invocationExpr.Update(
                    ReplaceChildWithConstant(invocationExpr.Expression, cache),
                    invocationExpr.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache)));

            case NewExpression newExpr:
                return newExpr.Update(newExpr.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache)));

            case NewArrayExpression newArrayExpr:
                return newArrayExpr.Update(newArrayExpr.Expressions.Select(e => ReplaceChildWithConstant(e, cache)));

            case ListInitExpression listInitExpr:
                NewExpression rebuiltNew = listInitExpr.NewExpression.Update(
                    listInitExpr.NewExpression.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache)));
                IEnumerable<ElementInit> rebuiltInits = listInitExpr.Initializers.Select(init =>
                    init.Update(init.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache))));
                return listInitExpr.Update(rebuiltNew, rebuiltInits);

            case IndexExpression indexExpr when indexExpr.Object is not null:
                Expression indexObj = ReplaceChildWithConstant(indexExpr.Object, cache);
                return indexExpr.Update(indexObj, indexExpr.Arguments.Select(arg => ReplaceChildWithConstant(arg, cache)));

            case IndexExpression indexExpr:
                // Object is null: leave as-is (static indexer scenario — not expressible in C# trees today).
                return indexExpr;

            default:
                // For other expressions or leaf nodes (constants, parameters), return as-is
                return expr;
        }
    }

    /// <summary>
    /// Returns a <see cref="ConstantExpression"/> for <paramref name="child"/> when its value
    /// has been successfully cached and is type-compatible with the child's declared type;
    /// otherwise returns the original child expression unchanged.
    /// </summary>
    private static Expression ReplaceChildWithConstant(Expression child, Dictionary<Expression, object?> cache)
    {
        if (!cache.TryGetValue(child, out object? value))
        {
            return child;
        }

        if (ReferenceEquals(value, FailedToEvaluateSentinel))
        {
            return child;
        }

        if (value is null)
        {
            // null is assignable to any reference / nullable type — guard against value-type slots.
            return !child.Type.IsValueType || Nullable.GetUnderlyingType(child.Type) is not null
                ? Expression.Constant(null, child.Type)
                : child;
        }

        return !child.Type.IsInstanceOfType(value) ? child : Expression.Constant(value, child.Type);
    }
}

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
    /// Recursively evaluates all sub-expressions in the tree and caches their values.
    /// Uses a bottom-up approach: evaluate children first, then replace them with constants
    /// before evaluating the parent. This prevents side effects from being executed multiple times.
    /// Short-circuit operators (<c>&amp;&amp;</c>, <c>||</c>, <c>??</c>) and conditional expressions
    /// honor C# evaluation order so unevaluated branches do not run side effects or throw.
    /// </summary>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
#endif
    private static void EvaluateAllSubExpressions(Expression expr, Dictionary<Expression, object?> cache)
    {
        // If already evaluated, skip to avoid duplicate work
        if (cache.ContainsKey(expr))
        {
            return;
        }

        try
        {
            bool hasChildren = false;

            // First, recursively evaluate all sub-expressions (depth-first traversal).
            // This ensures that when we evaluate a parent expression, all its children
            // are already cached and can be replaced with constant values.
            switch (expr)
            {
                // Short-circuit binary operators: evaluate Left first; only walk Right
                // when the original C# semantics would have executed it. This preserves
                // assertions like `s != null && s.Length > 0` (no NRE) and `flag || Throw()`.
                case BinaryExpression binaryExpr when binaryExpr.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse:
                    if (TryEvaluateShortCircuitBinary(binaryExpr, cache))
                    {
                        return;
                    }

                    break;

                // Null-coalescing: evaluate Left; only walk Right when Left is null.
                case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.Coalesce:
                    if (TryEvaluateCoalesce(binaryExpr, cache))
                    {
                        return;
                    }

                    break;

                // Assignment-style binary expressions (Assign and compound assignments such as
                // AddAssign, MultiplyAssign, ...) require special handling. The Left operand is a
                // writable target — replacing it wholesale with a ConstantExpression during the
                // generic "rebuild and compile" path produces an invalid Assign whose compilation
                // throws. The outer try/catch would then sentinel the parent, and a higher-level
                // rebuild would re-execute the original assignment, running the RHS side effects
                // a second time. Handle these inline:
                //   1. Walk only the writable target's *sub-children* (receiver, index arguments).
                //      We deliberately do NOT evaluate the writable wrapper node itself: doing so
                //      would invoke its property/indexer getter once for caching, and the rebuilt
                //      compound assignment would invoke it a second time as part of its semantics
                //      (compound assignments must read the current value).
                //   2. Walk Right exactly once.
                //   3. Rebuild Left via ReplaceSubExpressionsWithConstants so its sub-children
                //      (receiver, index arguments) become constants but the writable wrapper node
                //      (MemberExpression/IndexExpression/ParameterExpression) is preserved.
                //   4. Substitute Right with its cached constant and invoke the rebuilt assignment
                //      exactly once. The cached binary value is the post-assignment value (for
                //      compound assignments it is the result of the compound operation). This same
                //      value is the new value of Left for every assignment node type covered here,
                //      so cache it on Left as well to keep diagnostic rendering useful without
                //      triggering an extra getter call.
                case BinaryExpression binaryExpr when IsAssignmentBinaryNodeType(binaryExpr.NodeType):
                    EvaluateAssignmentTargetSubChildren(binaryExpr.Left, cache);
                    EvaluateAllSubExpressions(binaryExpr.Right, cache);
                    Expression rebuiltAssignLeft = ReplaceSubExpressionsWithConstants(binaryExpr.Left, cache);
                    Expression rebuiltAssignRight = ReplaceChildWithConstant(binaryExpr.Right, cache);
                    BinaryExpression rebuiltAssign = binaryExpr.Update(rebuiltAssignLeft, binaryExpr.Conversion, rebuiltAssignRight);
                    object? assignResult = Expression.Lambda(rebuiltAssign).Compile().DynamicInvoke();
                    cache[binaryExpr] = assignResult;
                    cache[binaryExpr.Left] = assignResult;
                    return;

                case BinaryExpression binaryExpr:
                    // Evaluate both operands before evaluating the binary operation
                    EvaluateAllSubExpressions(binaryExpr.Left, cache);
                    EvaluateAllSubExpressions(binaryExpr.Right, cache);
                    hasChildren = true;
                    break;

                // Quote wraps an unevaluated expression tree (typically the lambda argument
                // to IQueryable methods like Where/Any). Walking into the operand would
                // compile it into a delegate and prevent rebuilding the Quote, which would
                // break Queryable scenarios. Treat it as a leaf instead.
                case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Quote:
                    break;

                // Unary assignment-style nodes (PreIncrementAssign, PostIncrementAssign, etc.).
                // The Operand is a writable target, so the generic rebuild path that replaces it
                // with a ConstantExpression would produce an invalid node. Walk only the Operand's
                // sub-children (receiver/index arguments) — never the writable wrapper itself, or
                // its getter would run once here and once inside the rebuilt increment. Then
                // rebuild the unary with the Operand's sub-children substituted by cached constants
                // — the writable wrapper node is preserved. For Pre* variants, the unary result
                // equals the post-Operand value, so cache it on Operand for diagnostic rendering.
                // Post* variants would need a re-read to obtain the post-Operand value; we skip
                // that to avoid an additional getter call on property/indexer Operand.
                case UnaryExpression unaryExpr when IsAssignmentUnaryNodeType(unaryExpr.NodeType):
                    EvaluateAssignmentTargetSubChildren(unaryExpr.Operand, cache);
                    Expression rebuiltUnaryOperand = ReplaceSubExpressionsWithConstants(unaryExpr.Operand, cache);
                    UnaryExpression rebuiltUnary = unaryExpr.Update(rebuiltUnaryOperand);
                    object? unaryResult = Expression.Lambda(rebuiltUnary).Compile().DynamicInvoke();
                    cache[unaryExpr] = unaryResult;
                    if (unaryExpr.NodeType is ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign)
                    {
                        cache[unaryExpr.Operand] = unaryResult;
                    }

                    return;

                case UnaryExpression unaryExpr:
                    // Evaluate the operand before evaluating the unary operation
                    EvaluateAllSubExpressions(unaryExpr.Operand, cache);
                    hasChildren = true;
                    break;

                case MemberExpression memberExpr:
                    // For member access (e.g., obj.Property), evaluate the object first
                    if (memberExpr.Expression is not null)
                    {
                        EvaluateAllSubExpressions(memberExpr.Expression, cache);
                        hasChildren = true;
                    }

                    break;

                case MethodCallExpression callExpr:
                    // For method calls, evaluate the target object and all arguments first
                    if (callExpr.Object is not null)
                    {
                        EvaluateAllSubExpressions(callExpr.Object, cache);
                        hasChildren = true;
                    }

                    foreach (Expression argument in callExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                        hasChildren = true;
                    }

                    break;

                case ConditionalExpression conditionalExpr:
                    // Ternary expressions evaluate Test first and only the chosen branch.
                    if (TryEvaluateConditional(conditionalExpr, cache))
                    {
                        return;
                    }

                    break;

                case InvocationExpression invocationExpr:
                    // For delegate invocations, evaluate the delegate and all arguments
                    EvaluateAllSubExpressions(invocationExpr.Expression, cache);
                    foreach (Expression argument in invocationExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                    }

                    hasChildren = true;
                    break;

                case NewExpression newExpr:
                    // For object creation, evaluate all constructor arguments
                    foreach (Expression argument in newExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                        hasChildren = true;
                    }

                    break;

                case ListInitExpression listInitExpr:
                    // For collection initializers, evaluate the inner constructor arguments
                    // and all initializer arguments individually. We intentionally do not
                    // evaluate the wrapping NewExpression as a whole: List<T> initializers
                    // require an unrealized NewExpression at rebuild time, and reusing a
                    // pre-built instance would force the constructor to run a second time.
                    foreach (Expression argument in listInitExpr.NewExpression.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                    }

                    foreach (ElementInit initializer in listInitExpr.Initializers)
                    {
                        foreach (Expression argument in initializer.Arguments)
                        {
                            EvaluateAllSubExpressions(argument, cache);
                        }
                    }

                    hasChildren = true;
                    break;

                case NewArrayExpression newArrayExpr:
                    // For array creation, evaluate all element expressions
                    foreach (Expression expression in newArrayExpr.Expressions)
                    {
                        EvaluateAllSubExpressions(expression, cache);
                        hasChildren = true;
                    }

                    break;

                case IndexExpression indexExpr:
                    // Indexer access (e.g., list[i] expressed as IndexExpression) — evaluate
                    // the indexed object and all argument expressions.
                    if (indexExpr.Object is not null)
                    {
                        EvaluateAllSubExpressions(indexExpr.Object, cache);
                        hasChildren = true;
                    }

                    foreach (Expression argument in indexExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                        hasChildren = true;
                    }

                    break;

                case TypeBinaryExpression typeBinaryExpr:
                    // For type checks (e.g., obj is Type), evaluate the object being tested
                    EvaluateAllSubExpressions(typeBinaryExpr.Expression, cache);
                    hasChildren = true;
                    break;
            }

            // Evaluate the current expression and cache the result.
            // For non-leaf expressions (hasChildren == true), we replace sub-expressions with their cached values first.
            // For leaf expressions (hasChildren == false), we evaluate them directly.
            if (hasChildren)
            {
                // Now build a new expression that replaces sub-expressions with their cached values.
                // This is crucial: by replacing evaluated sub-expressions with constants, we ensure
                // that compiling and invoking this expression won't cause side effects to re-execute.
                Expression replacedExpr = ReplaceSubExpressionsWithConstants(expr, cache);

                // Evaluate the replaced expression - this is now safe because all sub-expressions
                // that could have side effects have been replaced with their constant values.
                object? result = Expression.Lambda(replacedExpr).Compile().DynamicInvoke();
                cache[expr] = result;
            }
            else
            {
                // This is a leaf expression (no children to evaluate).
                // Evaluate it directly and cache the result.
                object? result = Expression.Lambda(expr).Compile().DynamicInvoke();
                cache[expr] = result;
            }
        }
        catch (Exception)
        {
            // If evaluation fails (e.g., null reference, division by zero), cache the failure
            // sentinel so other branches can still be diagnosed. The root caller (EvaluateExpression)
            // detects a non-bool root result and re-invokes the original lambda to surface the
            // user's real exception instead of masking it as an InvalidCastException.
            cache[expr] = FailedToEvaluateSentinel;
        }
    }
}

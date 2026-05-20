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
        catch
        {
            // If evaluation fails (e.g., null reference, division by zero), cache the failure
            // sentinel so other branches can still be diagnosed. The root caller (EvaluateExpression)
            // detects a non-bool root result and re-invokes the original lambda to surface the
            // user's real exception instead of masking it as an InvalidCastException.
            cache[expr] = FailedToEvaluateSentinel;
        }
    }

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

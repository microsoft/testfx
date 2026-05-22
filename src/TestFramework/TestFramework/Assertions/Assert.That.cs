// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
[StackTraceHidden]
public static partial class AssertExtensions
{
    // Constants for standardized display values
    private const string NullDisplay = "null";
    private const string NullAngleBrackets = "<null>";

    // Constants for indexer method names
    private const string GetItemMethodName = "get_Item";
    private const string GetMethodName = "Get";

    // Constants for compiler-generated patterns
    private const string AnonymousTypePrefix = "<>f__AnonymousType";
    private const string ValueWrapperPattern = "value(";
    private const string ArrayLengthWrapperPattern = "ArrayLength(";
    private const string NewKeyword = "new ";
    private const string ActionTypePrefix = "Action`";
    private const string FuncTypePrefix = "Func`";

    // Constants for collection type patterns
    private const string ListInitPattern = "`1()";

    // Constants for parenthesis limits
    private const int MaxConsecutiveParentheses = 2;

    // Sentinel placed in the evaluation cache when a sub-expression cannot be evaluated.
    // Using a reference-identity object (rather than a string) prevents accidentally
    // substituting it for a same-typed operand when rebuilding parent expressions, and
    // lets diagnostic extraction translate it to a localized "<Failed to evaluate>" display.
    private static readonly object FailedToEvaluateSentinel = new();

    /// <summary>
    /// Provides That extension to Assert class.
    /// </summary>
    extension(Assert _)
    {
        /// <summary>
        /// Evaluates a boolean condition and throws an <see cref="AssertFailedException"/> if the condition is <see
        /// langword="false"/>.
        /// </summary>
        /// <param name="condition">An expression representing the condition to evaluate. Cannot be <see langword="null"/>.</param>
        /// <param name="message">An optional message to include in the exception if the assertion fails.</param>
        /// <param name="conditionExpression">The source code of the condition expression. This parameter is automatically populated by the compiler.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="condition"/> is <see langword="null"/>.</exception>
        /// <exception cref="AssertFailedException">Thrown if the evaluated condition is <see langword="false"/>.</exception>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateExpression(Expression, Dictionary<Expression, Object>)")]
#endif
        public static void That(Expression<Func<bool>> condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            TelemetryCollector.TrackAssertionCall("Assert.That");

            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            Dictionary<Expression, object?>? evaluationCache = null;
            bool result;

            if (RequiresSinglePassEvaluation(condition.Body))
            {
                // Potentially side-effecting expressions must be evaluated once while caching values.
                evaluationCache = CreateEvaluationCache();
                result = EvaluateExpression(condition.Body, evaluationCache);
            }
            else
            {
                // For side-effect-free expressions, keep the fast path and only compute details on failures.
                result = condition.Compile().Invoke();
                if (result)
                {
                    return;
                }

                evaluationCache = CreateEvaluationCache();
                EvaluateAllSubExpressions(condition.Body, evaluationCache);
            }

            if (result)
            {
                return;
            }

            var sb = new StringBuilder();
            string expressionText = conditionExpression
                ?? throw new ArgumentNullException(nameof(conditionExpression));
            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatMessageFormat, message));
            }

            string details = ExtractDetails(condition.Body, evaluationCache!);
            if (!string.IsNullOrWhiteSpace(details))
            {
                if (sb.Length == 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(FrameworkMessages.AssertThatDetailsPrefix);
                sb.AppendLine(details);
            }

            Assert.ReportAssertFailed($"Assert.That({expressionText})", sb.ToString().TrimEnd());
        }
    }

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

#pragma warning disable IDE0028 // Keep explicit constructor for broader compiler compatibility when source-building.
    private static Dictionary<Expression, object?> CreateEvaluationCache() => new Dictionary<Expression, object?>();
#pragma warning restore IDE0028

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
    /// Extracts diagnostic details from the failed assertion by identifying variables
    /// and their values in the expression tree. Returns a formatted string showing
    /// variable names and their evaluated values.
    /// </summary>
    private static string ExtractDetails(Expression expr, Dictionary<Expression, object?> evaluationCache)
    {
        // Dictionary to store variable names and their values
        var details = new Dictionary<string, object?>();
        ExtractVariablesFromExpression(expr, details, evaluationCache);

        if (details.Count == 0)
        {
            return string.Empty;
        }

        // Sort details alphabetically by variable name for consistent, readable output
        IOrderedEnumerable<KeyValuePair<string, object?>> sortedDetails = details.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, object?> kvp in sortedDetails)
        {
#if NET
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {kvp.Key} = {FormatValue(kvp.Value)}");
#else
            sb.AppendLine($"  {kvp.Key} = {FormatValue(kvp.Value)}");
#endif
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively walks the expression tree to extract meaningful variable references and their values.
    /// The suppressIntermediateValues parameter controls whether to display the value of intermediate
    /// expressions (like 'new List()' when it's part of 'new List().Count').
    /// </summary>
    private static void ExtractVariablesFromExpression(Expression? expr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache, bool suppressIntermediateValues = false)
    {
        if (expr is null)
        {
            return;
        }

        switch (expr)
        {
            // Special handling for array indexing (myArray[index])
            case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.ArrayIndex:
                HandleArrayIndexExpression(binaryExpr, details, evaluationCache);
                break;

            case BinaryExpression binaryExpr when binaryExpr.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse:
                // Always walk both sides for diagnostics so users see every captured variable.
                // We populate the cache for Right with a safe evaluation pass; the catch inside
                // EvaluateAllSubExpressions stores a sentinel for sub-expressions that would
                // throw (e.g., `s.Length` when s is null) and the detail helpers omit those.
                // Side effects on Right run at most once total — only here, not during the
                // single-pass assertion evaluation that already honored short-circuit.
                ExtractVariablesFromExpression(binaryExpr.Left, details, evaluationCache, suppressIntermediateValues);
                EvaluateAllSubExpressions(binaryExpr.Right, evaluationCache);
                ExtractVariablesFromExpression(binaryExpr.Right, details, evaluationCache, suppressIntermediateValues);
                break;

            case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.Coalesce:
                // Same rationale as AndAlso/OrElse: walk both sides for diagnostic completeness.
                ExtractVariablesFromExpression(binaryExpr.Left, details, evaluationCache, suppressIntermediateValues);
                EvaluateAllSubExpressions(binaryExpr.Right, evaluationCache);
                ExtractVariablesFromExpression(binaryExpr.Right, details, evaluationCache, suppressIntermediateValues);
                break;

            case BinaryExpression binaryExpr:
                // For binary operations (e.g., x > y), extract variables from both sides
                ExtractVariablesFromExpression(binaryExpr.Left, details, evaluationCache, suppressIntermediateValues);
                ExtractVariablesFromExpression(binaryExpr.Right, details, evaluationCache, suppressIntermediateValues);
                break;

            case TypeBinaryExpression typeBinaryExpr:
                // Extract variables from the expression being tested (e.g., 'obj' in 'obj is int')
                ExtractVariablesFromExpression(typeBinaryExpr.Expression, details, evaluationCache, suppressIntermediateValues);
                break;

            // Special handling for ArrayLength expressions (e.g., myArray.Length)
            case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.ArrayLength:
                // Display as "arrayName.Length" for better readability
                string arrayName = GetCleanMemberName(unaryExpr.Operand);
                string lengthDisplayName = $"{arrayName}.Length";
                TryAddExpressionValue(unaryExpr, lengthDisplayName, details, evaluationCache);

                // Only extract the array variable if it's not already a member expression
                // (to avoid duplicate entries like "myArray" and "myArray.Length")
                if (unaryExpr.Operand is not MemberExpression)
                {
                    ExtractVariablesFromExpression(unaryExpr.Operand, details, evaluationCache, suppressIntermediateValues);
                }

                break;

            case UnaryExpression unaryExpr:
                // For other unary operations (e.g., !flag), extract the operand
                ExtractVariablesFromExpression(unaryExpr.Operand, details, evaluationCache, suppressIntermediateValues);
                break;

            case MemberExpression memberExpr:
                // For member access (e.g., obj.Property), add to details
                AddMemberExpressionToDetails(memberExpr, details, evaluationCache);
                break;

            case MethodCallExpression callExpr:
                // Handle method calls with special logic for indexers and boolean methods
                HandleMethodCallExpression(callExpr, details, evaluationCache, suppressIntermediateValues);
                break;

            case ConditionalExpression conditionalExpr:
                // Walk all three parts for diagnostic completeness. The unselected branch is
                // populated via a safe evaluation; sub-expressions that would throw are
                // omitted (the sentinel handling in the detail helpers skips them).
                ExtractVariablesFromExpression(conditionalExpr.Test, details, evaluationCache, suppressIntermediateValues);
                EvaluateAllSubExpressions(conditionalExpr.IfTrue, evaluationCache);
                ExtractVariablesFromExpression(conditionalExpr.IfTrue, details, evaluationCache, suppressIntermediateValues);
                EvaluateAllSubExpressions(conditionalExpr.IfFalse, evaluationCache);
                ExtractVariablesFromExpression(conditionalExpr.IfFalse, details, evaluationCache, suppressIntermediateValues);
                break;

            case InvocationExpression invocationExpr:
                // For delegate invocations, extract from the delegate and all arguments
                ExtractVariablesFromExpression(invocationExpr.Expression, details, evaluationCache, suppressIntermediateValues);
                foreach (Expression argument in invocationExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
                }

                break;

            case NewExpression newExpr:
                // For object creation, extract from constructor arguments
                foreach (Expression argument in newExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
                }

                // Don't display the new object value if it's part of a member access chain
                // (e.g., don't show "new Person()" when displaying "new Person().Name")
                if (!suppressIntermediateValues)
                {
                    string newExprDisplay = GetCleanMemberName(newExpr);
                    TryAddExpressionValue(newExpr, newExprDisplay, details, evaluationCache);
                }

                break;

            case ListInitExpression listInitExpr:
                // For collection initializers, suppress the intermediate 'new List()' but show the elements
                ExtractVariablesFromExpression(listInitExpr.NewExpression, details, evaluationCache, suppressIntermediateValues: true);
                foreach (ElementInit initializer in listInitExpr.Initializers)
                {
                    foreach (Expression argument in initializer.Arguments)
                    {
                        ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
                    }
                }

                break;

            case NewArrayExpression newArrayExpr:
                // For array creation, extract from all element expressions
                foreach (Expression expression in newArrayExpr.Expressions)
                {
                    ExtractVariablesFromExpression(expression, details, evaluationCache, suppressIntermediateValues);
                }

                break;
        }
    }

    /// <summary>
    /// Handles array indexing expressions (e.g., myArray[i]) by displaying them in indexer notation
    /// and extracting the index variable.
    /// </summary>
    private static void HandleArrayIndexExpression(BinaryExpression arrayIndexExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        string arrayName = GetCleanMemberName(arrayIndexExpr.Left);
        string indexValue = GetIndexArgumentDisplay(arrayIndexExpr.Right);
        string indexerDisplay = $"{arrayName}[{indexValue}]";
        TryAddExpressionValue(arrayIndexExpr, indexerDisplay, details, evaluationCache);

        // Extract variables from the index argument
        ExtractVariablesFromExpression(arrayIndexExpr.Right, details, evaluationCache);
    }

    /// <summary>
    /// Adds a member expression (e.g., obj.Property) to the details dictionary with its cached value.
    /// Filters out Func and Action delegates as they don't provide useful diagnostic information.
    /// </summary>
    private static void AddMemberExpressionToDetails(MemberExpression memberExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        // Get a clean, readable name for the member (e.g., "person.Name" instead of compiler-generated text)
        string displayName = GetCleanMemberName(memberExpr);

        if (details.ContainsKey(displayName))
        {
            return;
        }

        bool hasCachedValue = evaluationCache.TryGetValue(memberExpr, out object? cachedValue);

        // Skip sub-expressions that failed safe evaluation (e.g., NRE when walking the unreached
        // branch of `s != null && s.Length > 0`). Surfacing them as "<Failed to evaluate>" would
        // confuse users with a benign short-circuit.
        if (hasCachedValue && ReferenceEquals(cachedValue, FailedToEvaluateSentinel))
        {
            return;
        }

        // Use cached value if available, otherwise mark as failed
        details[displayName] = hasCachedValue
            ? cachedValue
            : FrameworkMessages.AssertThatFailedToEvaluate;

        // Skip Func and Action delegates as they don't provide useful information in assertion failures
        if (IsFuncOrActionType(cachedValue?.GetType()))
        {
            details.Remove(displayName);
            return;
        }

        // Only extract variables from the object being accessed if it's not a member expression
        // or a method call (to avoid showing both "person" and "person.Name" or both
        // "provider.GetBox()" and "provider.GetBox().Value" when the leaf access is sufficient
        // — and to avoid surfacing intermediate side-effecting calls in failure details).
        if (memberExpr.Expression is not null and not MemberExpression and not MethodCallExpression)
        {
            ExtractVariablesFromExpression(memberExpr.Expression, details, evaluationCache, suppressIntermediateValues: true);
        }
    }

    /// <summary>
    /// Handles method call expressions with special logic for:
    /// - Indexers (get_Item and Get methods): displayed as object[index]
    /// - Boolean-returning methods: extract from the target object for better diagnostics
    /// - Other methods: capture the method call itself and extract from arguments.
    /// </summary>
    private static void HandleMethodCallExpression(MethodCallExpression callExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache, bool suppressIntermediateValues = false)
    {
        // Special handling for indexers (e.g., list[0] calls get_Item(0))
        if (callExpr.Method.Name == GetItemMethodName && callExpr.Object is not null && callExpr.Arguments.Count == 1)
        {
            // Display as "listName[indexValue]" for readability
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexValue = GetIndexArgumentDisplay(callExpr.Arguments[0]);
            string indexerDisplay = $"{objectName}[{indexValue}]";
            TryAddExpressionValue(callExpr, indexerDisplay, details, evaluationCache);

            // Extract variables from the index argument but not from the object
            // (to avoid showing both "list" and "list[0]")
            ExtractVariablesFromExpression(callExpr.Arguments[0], details, evaluationCache, suppressIntermediateValues);
        }
        else if (IsArrayGetCall(callExpr))
        {
            // Handle array indexers (e.g., array.Get(i, j) displayed as array[i, j]).
            // In practice this only fires for multidimensional arrays — single-dimensional
            // arrays surface as ArrayIndex in LINQ expressions, not as a Get method call.
            // We gate on the receiver actually being an array so arbitrary user-defined
            // `Get(...)` methods on non-array types are NOT mis-rendered as `obj[...]`
            // (issue #6691); they go through the regular method-call path below.
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexDisplay = string.Join(", ", callExpr.Arguments.Select(GetIndexArgumentDisplay));
            string indexerDisplay = $"{objectName}[{indexDisplay}]";
            TryAddExpressionValue(callExpr, indexerDisplay, details, evaluationCache);

            // Extract variables from the index arguments but not from the object
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
            }
        }
        else
        {
            // Check if the method returns a boolean (e.g., list.Any(), string.Contains())
            if (callExpr.Method.ReturnType == typeof(bool))
            {
                if (callExpr.Object is not null)
                {
                    // For boolean-returning methods, extract details from the object being called.
                    // This provides more useful context (e.g., show "list" and "list.Count" rather than "list.Any()")
                    ExtractVariablesFromExpression(callExpr.Object, details, evaluationCache, suppressIntermediateValues);
                }
            }
            else
            {
                // For non-boolean methods, capture the method call itself using a friendly receiver
                // (issue #6691): static methods get the declaring type, captured-this instance methods
                // render as `this.Method(...)`, extension methods on `this` also render as `this.Method(...)`.
                string methodCallDisplay = GetMethodCallDisplayName(callExpr);
                TryAddExpressionValue(callExpr, methodCallDisplay, details, evaluationCache);

                // Don't extract from the object to avoid duplication
            }

            // Always extract variables from the arguments
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
            }
        }
    }

    /// <summary>
    /// Matches <c>array.Get(i[, j[, k...]])</c> calls on an array receiver. In practice this only
    /// fires for multidimensional arrays (e.g. <c>int[,]</c>) which expose runtime-synthesized
    /// <c>Get</c>/<c>Set</c>/<c>Address</c> methods on the array type itself — not on
    /// <see cref="Array"/>; single-dimensional arrays surface as <see cref="ExpressionType.ArrayIndex"/>
    /// rather than a method call. Gating on the receiver actually being an array prevents arbitrary
    /// user-defined instance methods named <c>Get</c> from being mis-rendered as <c>obj[...]</c>
    /// (issue #6691).
    /// </summary>
    private static bool IsArrayGetCall(MethodCallExpression callExpr)
        => callExpr.Method.Name == GetMethodName
            && callExpr.Object is not null
            && callExpr.Object.Type.IsArray
            && callExpr.Arguments.Count > 0;

    /// <summary>
    /// Builds a friendly display name for a method-call expression so the failure message uses the
    /// same syntax the user wrote. Static methods get prefixed with their declaring type's name;
    /// instance methods on captured <c>this</c> render as <c>this.Method(...)</c>; extension methods
    /// use the first argument as the receiver. Fixes issue #6691.
    /// </summary>
    private static string GetMethodCallDisplayName(MethodCallExpression callExpr)
    {
        string methodName = callExpr.Method.Name;

        // Extension methods are static methods on a static class marked [Extension]; the receiver is the
        // first argument. Render like the user wrote: receiver.Method(rest).
        if (callExpr.Object is null
            && callExpr.Method.IsDefined(typeof(ExtensionAttribute), inherit: false)
            && callExpr.Arguments.Count > 0)
        {
            Expression firstArg = callExpr.Arguments[0];
            Type receiverParamType = callExpr.Method.GetParameters()[0].ParameterType;
            string receiver = IsCapturedThis(firstArg, receiverParamType)
                ? "this"
                : GetCleanMemberName(firstArg);
            string extArgs = string.Join(", ", callExpr.Arguments.Skip(1).Select(static a => CleanExpressionText(a.ToString())));
            return $"{receiver}.{methodName}({extArgs})";
        }

        string argsStr = string.Join(", ", callExpr.Arguments.Select(static a => CleanExpressionText(a.ToString())));

        if (callExpr.Object is null)
        {
            // Regular static method: prefix with a friendly type display (no namespace, nested
            // types separated with `.` instead of the reflection `+`) so nested types keep their
            // nesting context (Outer.Inner.Method rather than Inner.Method).
            string typeName = callExpr.Method.DeclaringType is { } dt
                ? GetFriendlyTypeName(dt)
                : NullAngleBrackets;
            return $"{typeName}.{methodName}({argsStr})";
        }

        if (IsCapturedThis(callExpr.Object, callExpr.Method.DeclaringType))
        {
            return $"this.{methodName}({argsStr})";
        }

        string objectDisplay = GetCleanMemberName(callExpr.Object);
        return $"{objectDisplay}.{methodName}({argsStr})";
    }

    /// <summary>
    /// Returns a user-friendly display name for <paramref name="type"/>: BCL aliases via
    /// <see cref="CleanTypeName(string)"/>, namespace stripped, and nested-type separators
    /// (reflection's <c>+</c>) converted to <c>.</c>.
    /// </summary>
    private static string GetFriendlyTypeName(Type type)
    {
        string raw = type.Name;
        string cleaned = CleanTypeName(raw);
        if (!ReferenceEquals(cleaned, raw))
        {
            return cleaned;
        }

        // Walk up the nesting chain to produce Outer.Inner instead of Outer+Inner.
        return type.IsNested && type.DeclaringType is { } declaring
            ? $"{GetFriendlyTypeName(declaring)}.{type.Name}"
            : type.Name;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="objectExpr"/> is a reference to the enclosing
    /// instance (<c>this</c>) — either accessed via the compiler-synthesized display-class field
    /// (named like <c>&lt;&gt;4__this</c>) or as a <see cref="ConstantExpression"/> representing
    /// the enclosing instance (no-closure case). For the constant form we require the expression's
    /// static type to exactly match its runtime type and be assignable to <paramref name="declaringType"/>,
    /// so inherited methods on <c>this</c> still render as <c>this.Method(...)</c> without
    /// mis-labeling base-typed locals as <c>this</c>.
    /// </summary>
    private static bool IsCapturedThis(Expression objectExpr, Type? declaringType)
        => (objectExpr is MemberExpression me
                && me.Member.Name.StartsWith("<>", StringComparison.Ordinal)
                && me.Member.Name.EndsWith("__this", StringComparison.Ordinal))
            || (declaringType is not null
                && objectExpr is ConstantExpression ce
                && ce.Value is not null
                && ce.Type == ce.Value.GetType()
                && declaringType.IsAssignableFrom(ce.Type));

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

    private static bool IsFuncOrActionType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type == typeof(Action))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        string genericDefinitionName = type.GetGenericTypeDefinition().Name;
        return genericDefinitionName.StartsWith(ActionTypePrefix, StringComparison.Ordinal)
            || genericDefinitionName.StartsWith(FuncTypePrefix, StringComparison.Ordinal);
    }

    private static string GetCleanMemberName(Expression? expr)
        => expr is null
            ? NullAngleBrackets
            : CleanExpressionText(expr.ToString());

    /// <summary>
    /// Gets a display string for an index argument in an indexer expression.
    /// Preserves variable names and source-style expression text for readability (e.g., "i" or
    /// "start + offset" rather than the evaluated constant value), so the failure message keeps
    /// the user's intent visible.
    /// </summary>
    private static string GetIndexArgumentDisplay(Expression indexArg)
    {
        try
        {
            // For literal constant indices, formatting the value gives the cleanest output
            // (e.g., the literal 0 in `array[0]`).
            return indexArg is ConstantExpression constExpr
                ? FormatValue(constExpr.Value)
                : CleanExpressionText(indexArg.ToString());
        }
        catch
        {
            return CleanExpressionText(indexArg.ToString());
        }
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => NullDisplay,
            string s => $"\"{s}\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable<object> e => $"[{string.Join(", ", e.Select(FormatValue))}]",
            IEnumerable e and not string => $"[{string.Join(", ", e.Cast<object>().Select(FormatValue))}]",
            _ => value.ToString() ?? NullAngleBrackets,
        };

    /// <summary>
    /// Cleans up compiler-generated artifacts from expression text to make it more readable.
    /// Removes display classes, compiler wrappers, and formats anonymous types and collections properly.
    /// </summary>
    private static string CleanExpressionText(string raw)
    {
        string cleaned = raw;

        // Remove compiler-generated wrappers FIRST (e.g., "value()", "ArrayLength()")
        // This must happen before display class cleanup to avoid breaking patterns
        cleaned = RemoveCompilerGeneratedWrappers(cleaned);

        // Handle anonymous types - convert compiler-generated type names to C# syntax (e.g., "new { ... }")
        cleaned = RemoveAnonymousTypeWrappers(cleaned);

        // Handle list initialization expressions - convert from Add method calls to collection initializer syntax
        cleaned = CleanListInitializers(cleaned);

        // Handle compiler-generated display classes more comprehensively
        // Removes patterns like "DisplayClass0_1.myVariable" to just "myVariable"
        cleaned = CompilerGeneratedDisplayClassRegex().Replace(cleaned, "$1");

        // Remove unnecessary outer parentheses and excessive consecutive parentheses
        cleaned = CleanParentheses(cleaned);

        return cleaned;
    }

    /// <summary>
    /// Removes anonymous type wrappers (e.g., "new &lt;&gt;f__AnonymousType0(x = 1)")
    /// and replaces them with C# anonymous type syntax (e.g., "new { x = 1 }").
    /// </summary>
    private static string RemoveAnonymousTypeWrappers(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            // Look for anonymous type pattern: new <>f__AnonymousType followed by generic parameters.
            // Use position-aware ordinal comparisons to avoid allocating substring instances on
            // every character of the input.
            if (HasSubstringAt(input, i, NewKeyword) &&
                HasSubstringAt(input, i + NewKeyword.Length, AnonymousTypePrefix))
            {
                // Find the start of the constructor parameters
                int constructorStart = input.IndexOf('(', i + NewKeyword.Length);
                if (constructorStart == -1)
                {
                    result.Append(input[i]);
                    i++;
                    continue;
                }

                // Find the matching closing parenthesis
                int parenCount = 1;
                int constructorEnd = constructorStart + 1;
                while (constructorEnd < input.Length && parenCount > 0)
                {
                    if (input[constructorEnd] == '(')
                    {
                        parenCount++;
                    }
                    else if (input[constructorEnd] == ')')
                    {
                        parenCount--;
                    }

                    constructorEnd++;
                }

                if (parenCount == 0)
                {
                    // Extract the content inside the parentheses and wrap with anonymous type notation
                    string content = input.Substring(constructorStart + 1, constructorEnd - constructorStart - 2);
#if NET
                    result.Append(CultureInfo.InvariantCulture, $"new {{ {content} }}");
#else
                    result.Append($"new {{ {content} }}");
#endif
                    i = constructorEnd;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Cleans up collection initializer expressions by converting verbose Add method calls
    /// (e.g., "new List`1() { Void Add(Int32)(1), Void Add(Int32)(2) }")
    /// to standard C# syntax (e.g., "new List&lt;int&gt; { 1, 2 }").
    /// </summary>
    private static string CleanListInitializers(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Look for list initialization patterns with proper brace matching
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            // Look for "new List`1() {" or similar collection types
            if (TryMatchListInitPattern(input, i, out string collectionType, out int patternEnd))
            {
                // Find the matching closing brace for the initializer
                int braceStart = patternEnd;
                int braceCount = 1;
                int braceEnd = braceStart + 1;

                while (braceEnd < input.Length && braceCount > 0)
                {
                    if (input[braceEnd] == '{')
                    {
                        braceCount++;
                    }
                    else if (input[braceEnd] == '}')
                    {
                        braceCount--;
                    }

                    braceEnd++;
                }

                if (braceCount == 0)
                {
                    // Extract the content between braces
                    string initContent = input.Substring(braceStart + 1, braceEnd - braceStart - 2);

                    // Extract the generic type parameter and arguments from the Add method calls
                    MatchCollection addMatches = ListInitAddArgumentRegex().Matches(initContent);

                    if (addMatches.Count > 0)
                    {
                        // Extract type from the first Add method call
                        Match typeMatch = ListInitAddTypeRegex().Match(initContent);
                        string genericType = "object"; // default fallback

                        if (typeMatch.Success)
                        {
                            string rawType = typeMatch.Groups[1].Value;
                            // Clean up type names like "Int32" to "int", "String" to "string", etc.
                            genericType = CleanTypeName(rawType);
                        }

                        // Extract all arguments from Add method calls
                        var arguments = new List<string>(addMatches.Count);
                        foreach (Match addMatch in addMatches)
                        {
                            string argument = addMatch.Groups[1].Value;
                            arguments.Add(argument);
                        }

                        // Construct the cleaned collection initializer
                        string argumentsList = string.Join(", ", arguments);
#if NET
                        result.Append(CultureInfo.InvariantCulture, $"new {collectionType}<{genericType}> {{ {argumentsList} }}");
#else
                        result.Append($"new {collectionType}<{genericType}> {{ {argumentsList} }}");
#endif
                        i = braceEnd;
                        continue;
                    }
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryMatchListInitPattern(string input, int startIndex, out string collectionType, out int patternEnd)
    {
        collectionType = string.Empty;
        patternEnd = startIndex;

        // Check for "new " at the start (non-allocating ordinal compare).
        if (!HasSubstringAt(input, startIndex, NewKeyword))
        {
            return false;
        }

        int pos = startIndex + NewKeyword.Length;

        // Skip whitespace
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
        {
            pos++;
        }

        // Check for collection type names
        string[] collectionTypes = ["List", "IList", "ICollection", "IEnumerable"];
        string matchedType = string.Empty;

        foreach (string type in collectionTypes)
        {
            if (HasSubstringAt(input, pos, type))
            {
                matchedType = type;
                pos += type.Length;
                break;
            }
        }

        if (string.IsNullOrEmpty(matchedType))
        {
            return false;
        }

        // Check for "`1()" pattern
        if (!HasSubstringAt(input, pos, ListInitPattern))
        {
            return false;
        }

        pos += ListInitPattern.Length;

        // Skip whitespace
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
        {
            pos++;
        }

        // Check for opening brace
        if (pos >= input.Length || input[pos] != '{')
        {
            return false;
        }

        collectionType = matchedType;
        patternEnd = pos;
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> occurs in
    /// <paramref name="input"/> at <paramref name="start"/> using ordinal comparison. Avoids the
    /// substring allocation that <c>input.Substring(start, value.Length) == value</c> would
    /// incur for every probe in the diagnostic text cleanup pipeline.
    /// </summary>
    private static bool HasSubstringAt(string input, int start, string value)
        => start >= 0
            && start <= input.Length - value.Length
            && string.CompareOrdinal(input, start, value, 0, value.Length) == 0;

    private static string CleanTypeName(string typeName)
        => typeName switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "Int16" => "short",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "UInt32" => "uint",
            "UInt64" => "ulong",
            "UInt16" => "ushort",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "Boolean" => "bool",
            "String" => "string",
            "Char" => "char",
            "Object" => "object",

            // Handle System. prefixed type names
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.UInt16" => "ushort",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Boolean" => "bool",
            "System.String" => "string",
            "System.Char" => "char",
            "System.Object" => "object",

            _ => typeName,
        };

    /// <summary>
    /// Removes parentheses that wrap the entire expression and cleans up excessive
    /// consecutive parentheses (e.g., "(((x)))" becomes "x", "((x) &amp;&amp; (y))" becomes "(x) &amp;&amp; (y)").
    /// </summary>
    private static string CleanParentheses(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        input = input.Trim();
        string previous;

        // Keep removing outer parentheses and cleaning excessive ones until no more changes occur
        do
        {
            previous = input;

            // Remove outer parentheses if they wrap the entire expression
            input = RemoveOuterParentheses(input);

            // Clean excessive consecutive parentheses in a single pass
            input = CleanExcessiveParentheses(input);
        }
        while (input != previous); // Repeat until no more changes

        return input;
    }

    /// <summary>
    /// Removes outer parentheses if they wrap the entire expression without serving
    /// a syntactic purpose (e.g., "(x > 5)" becomes "x > 5").
    /// </summary>
    private static string RemoveOuterParentheses(string input)
    {
        if (input.Length < 2 || !input.StartsWith("(", StringComparison.Ordinal) || !input.EndsWith(")", StringComparison.Ordinal))
        {
            return input;
        }

        // Check if the first and last parentheses are truly the outermost pair
        int parenCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                parenCount++;
            }
            else if (input[i] == ')')
            {
                parenCount--;
                // If we reach 0 before the end, the first paren is not the outermost
                if (parenCount == 0 && i < input.Length - 1)
                {
                    return input;
                }
            }
        }

        // If we get here and parenCount is 0, the outer parens can be removed
        return parenCount == 0 ? input.Substring(1, input.Length - 2).Trim() : input;
    }

    /// <summary>
    /// Reduces excessive consecutive identical parentheses to at most 2.
    /// This handles cases where multiple layers of compilation create redundant nesting.
    /// </summary>
    private static string CleanExcessiveParentheses(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder(input.Length);
        int i = 0;

        while (i < input.Length)
        {
            char currentChar = input[i];

            if (currentChar is '(' or ')')
            {
                // Count consecutive identical parentheses
                int count = 1;
                while (i + count < input.Length && input[i + count] == currentChar)
                {
                    count++;
                }

                // Keep at most 2 consecutive parentheses
                int keepCount = Math.Min(count, MaxConsecutiveParentheses);
                result.Append(currentChar, keepCount);
                i += count;
            }
            else
            {
                result.Append(currentChar);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Removes compiler-generated wrapper functions like "value(...)" and "ArrayLength(...)"
    /// that appear in expression tree string representations.
    /// </summary>
    private static string RemoveCompilerGeneratedWrappers(string input)
    {
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            if (TryRemoveWrapper(input, ref i, ValueWrapperPattern, RemoveCompilerGeneratedWrappers, result) ||
                TryRemoveWrapper(input, ref i, ArrayLengthWrapperPattern, content => RemoveCompilerGeneratedWrappers(content) + ".Length", result))
            {
                continue;
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Generic helper method to try removing a specific wrapper pattern from the input string.
    /// Uses a transformation function to convert the unwrapped content as needed.
    /// </summary>
    private static bool TryRemoveWrapper(string input, ref int index, string pattern,
        Func<string, string> transform, StringBuilder result)
    {
        // Check if the pattern exists at the current index
        if (index > input.Length - pattern.Length ||
            !string.Equals(input.Substring(index, pattern.Length), pattern, StringComparison.Ordinal))
        {
            return false;
        }

        int start = index + pattern.Length;
        int parenCount = 1;
        int i = start;

        // Find matching closing parenthesis by counting nesting levels
        while (i < input.Length && parenCount > 0)
        {
            if (input[i] == '(')
            {
                parenCount++;
            }
            else if (input[i] == ')')
            {
                parenCount--;
            }

            i++;
        }

        // If we found a complete wrapper, extract and transform the content
        if (parenCount == 0)
        {
            string content = input.Substring(start, i - start - 1);
            result.Append(transform(content));
            index = i;
            return true;
        }

        // Malformed wrapper, don't consume the pattern
        return false;
    }

    /// <summary>
    /// Attempts to add an expression's value to the details dictionary using the cached value.
    /// Returns true if the value was added, false if the key already exists or the cached value
    /// is the failure sentinel.
    /// </summary>
    private static bool TryAddExpressionValue(Expression expr, string displayName, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        // Use cached value if available
        if (evaluationCache.TryGetValue(expr, out object? cachedValue))
        {
            // Skip sub-expressions that failed safe evaluation (e.g., NRE when walking the
            // unreached branch of a short-circuited operator). Surfacing them as
            // "<Failed to evaluate>" would be misleading for benign short-circuits.
            if (ReferenceEquals(cachedValue, FailedToEvaluateSentinel))
            {
                return false;
            }

            // If the key already exists, check if it has the same value
            if (details.TryGetValue(displayName, out object? existingValue))
            {
                // If values are different, add with a suffix to show multiple evaluations
                if (!Equals(existingValue, cachedValue))
                {
                    int counter = 2;
                    string numberedName;
                    do
                    {
                        numberedName = $"{displayName} ({counter})";
                        counter++;
                    }
                    while (details.ContainsKey(numberedName));

                    details[numberedName] = cachedValue;
                    return true;
                }

                // Same value, don't add duplicate
                return false;
            }

            details[displayName] = cachedValue;
            return true;
        }

        // Don't add if key already exists and we don't have a cached value
        if (details.ContainsKey(displayName))
        {
            return false;
        }

        // Mark as failed if we couldn't evaluate it
        details[displayName] = FrameworkMessages.AssertThatFailedToEvaluate;
        return true;
    }

    /// <summary>
    /// Cached regular expressions used by the diagnostic text cleanup pipeline.
    /// On NET we use the source-generated regex attribute; on netstandard/net462 we cache
    /// a single compiled instance to avoid the per-call allocation and JIT cost that a
    /// freshly-constructed <c>Regex</c> would incur on every failed assertion.
    /// </summary>
#if NET
    [GeneratedRegex(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)")]
    private static partial Regex CompilerGeneratedDisplayClassRegex();

    [GeneratedRegex(@"Void\s+Add\([^)]+\)\(([^)]+)\)")]
    private static partial Regex ListInitAddArgumentRegex();

    [GeneratedRegex(@"Void\s+Add\(([^)]+)\)")]
    private static partial Regex ListInitAddTypeRegex();
#else
    private static readonly Regex CompilerGeneratedDisplayClass = new(
        @"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)",
        RegexOptions.Compiled);

    private static readonly Regex ListInitAddArgument = new(
        @"Void\s+Add\([^)]+\)\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex ListInitAddType = new(
        @"Void\s+Add\(([^)]+)\)",
        RegexOptions.Compiled);

    private static Regex CompilerGeneratedDisplayClassRegex() => CompilerGeneratedDisplayClass;

    private static Regex ListInitAddArgumentRegex() => ListInitAddArgument;

    private static Regex ListInitAddTypeRegex() => ListInitAddType;
#endif
}

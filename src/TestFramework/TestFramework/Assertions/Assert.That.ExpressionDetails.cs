// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
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
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public static partial class AssertExtensions
{
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
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
        {
            // FormatValue runs user-defined formatting code, so any exception type is possible.
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

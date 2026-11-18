// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public static partial class AssertExtensions
{
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
}

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
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The public Assert.That API declares that expression evaluation requires dynamic code.")]
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
}

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
        [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateExpression(Expression, Dictionary<Expression, Object>)")]
        public static void That(Expression<Func<bool>> condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            // Cache to store evaluated expression values to avoid re-evaluation
            var evaluationCache = new Dictionary<Expression, object?>();

            bool result = EvaluateExpression(condition.Body, evaluationCache);

            if (result)
            {
                return;
            }

            var sb = new StringBuilder();
            string expressionText = conditionExpression
                ?? throw new ArgumentNullException(nameof(conditionExpression));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatFailedFormat, expressionText));
            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatMessageFormat, message));
            }

            string details = ExtractDetails(condition.Body, evaluationCache);
            if (!string.IsNullOrWhiteSpace(details))
            {
                sb.AppendLine(FrameworkMessages.AssertThatDetailsPrefix);
                sb.AppendLine(details);
            }

            throw new AssertFailedException(sb.ToString().TrimEnd());
        }
    }

    [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateAllSubExpressions(Expression, Dictionary<Expression, Object>)")]
    private static bool EvaluateExpression(Expression expr, Dictionary<Expression, object?> cache)
    {
        // Use a single-pass evaluation that only evaluates each expression once
        EvaluateAllSubExpressions(expr, cache);

        // The root expression should now be cached
        if (cache.TryGetValue(expr, out object? result))
        {
            return (bool)result!;
        }

        // Fallback - this should not happen if EvaluateAllSubExpressions works correctly
        return false;
    }

    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
    private static void EvaluateAllSubExpressions(Expression expr, Dictionary<Expression, object?> cache)
    {
        // If already evaluated, skip
        if (cache.ContainsKey(expr))
        {
            return;
        }

        try
        {
            // First, recursively evaluate all sub-expressions
            switch (expr)
            {
                case BinaryExpression binaryExpr:
                    EvaluateAllSubExpressions(binaryExpr.Left, cache);
                    EvaluateAllSubExpressions(binaryExpr.Right, cache);
                    break;

                case UnaryExpression unaryExpr:
                    EvaluateAllSubExpressions(unaryExpr.Operand, cache);
                    break;

                case MemberExpression memberExpr:
                    if (memberExpr.Expression is not null)
                    {
                        EvaluateAllSubExpressions(memberExpr.Expression, cache);
                    }

                    break;

                case MethodCallExpression callExpr:
                    if (callExpr.Object is not null)
                    {
                        EvaluateAllSubExpressions(callExpr.Object, cache);
                    }

                    foreach (Expression argument in callExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                    }

                    break;

                case ConditionalExpression conditionalExpr:
                    EvaluateAllSubExpressions(conditionalExpr.Test, cache);
                    EvaluateAllSubExpressions(conditionalExpr.IfTrue, cache);
                    EvaluateAllSubExpressions(conditionalExpr.IfFalse, cache);
                    break;

                case InvocationExpression invocationExpr:
                    EvaluateAllSubExpressions(invocationExpr.Expression, cache);
                    foreach (Expression argument in invocationExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                    }

                    break;

                case NewExpression newExpr:
                    foreach (Expression argument in newExpr.Arguments)
                    {
                        EvaluateAllSubExpressions(argument, cache);
                    }

                    break;

                case ListInitExpression listInitExpr:
                    EvaluateAllSubExpressions(listInitExpr.NewExpression, cache);
                    foreach (ElementInit initializer in listInitExpr.Initializers)
                    {
                        foreach (Expression argument in initializer.Arguments)
                        {
                            EvaluateAllSubExpressions(argument, cache);
                        }
                    }

                    break;

                case NewArrayExpression newArrayExpr:
                    foreach (Expression expression in newArrayExpr.Expressions)
                    {
                        EvaluateAllSubExpressions(expression, cache);
                    }

                    break;

                case TypeBinaryExpression typeBinaryExpr:
                    EvaluateAllSubExpressions(typeBinaryExpr.Expression, cache);
                    break;
            }

            // Now build a new expression that replaces sub-expressions with their cached values
            // This prevents re-execution of side effects
            Expression replacedExpr = ReplaceSubExpressionsWithConstants(expr, cache);

            // Evaluate the replaced expression - this won't cause side effects since
            // all sub-expressions are now constants
            object? result = Expression.Lambda(replacedExpr).Compile().DynamicInvoke();
            cache[expr] = result;
        }
        catch
        {
            cache[expr] = "<Failed to evaluate>";
        }
    }

    private static Expression ReplaceSubExpressionsWithConstants(Expression expr, Dictionary<Expression, object?> cache)
    {
        // If this expression's direct sub-expressions are cached, replace them with constants
        switch (expr)
        {
            case BinaryExpression binaryExpr:
                Expression left = cache.TryGetValue(binaryExpr.Left, out object? leftValue)
                    ? Expression.Constant(leftValue, binaryExpr.Left.Type)
                    : binaryExpr.Left;
                Expression right = cache.TryGetValue(binaryExpr.Right, out object? rightValue)
                    ? Expression.Constant(rightValue, binaryExpr.Right.Type)
                    : binaryExpr.Right;
                return Expression.MakeBinary(binaryExpr.NodeType, left, right);

            case UnaryExpression unaryExpr:
                Expression operand = cache.TryGetValue(unaryExpr.Operand, out object? value)
                    ? Expression.Constant(value, unaryExpr.Operand.Type)
                    : unaryExpr.Operand;
                return Expression.MakeUnary(unaryExpr.NodeType, operand, unaryExpr.Type);

            case MemberExpression memberExpr when memberExpr.Expression is not null && cache.ContainsKey(memberExpr.Expression):
                Expression instance = Expression.Constant(cache[memberExpr.Expression], memberExpr.Expression.Type);
                return Expression.MakeMemberAccess(instance, memberExpr.Member);

            case MethodCallExpression callExpr:
                Expression? obj = callExpr.Object is not null && cache.TryGetValue(callExpr.Object, out object? callExprValue)
                    ? Expression.Constant(callExprValue, callExpr.Object.Type)
                    : callExpr.Object;

                Expression[] args = [.. callExpr.Arguments
                    .Select(arg => cache.TryGetValue(arg, out object? value)
                        ? Expression.Constant(value, arg.Type)
                        : arg)];

                return Expression.Call(obj, callExpr.Method, args);

            case ConditionalExpression conditionalExpr:
                Expression test = cache.TryGetValue(conditionalExpr.Test, out object? testValue)
                    ? Expression.Constant(testValue, conditionalExpr.Test.Type)
                    : conditionalExpr.Test;
                Expression ifTrue = cache.TryGetValue(conditionalExpr.IfTrue, out object? ifTrueValue)
                    ? Expression.Constant(ifTrueValue, conditionalExpr.IfTrue.Type)
                    : conditionalExpr.IfTrue;
                Expression ifFalse = cache.TryGetValue(conditionalExpr.IfFalse, out object? ifFalseValue)
                    ? Expression.Constant(ifFalseValue, conditionalExpr.IfFalse.Type)
                    : conditionalExpr.IfFalse;
                return Expression.Condition(test, ifTrue, ifFalse);

            case TypeBinaryExpression typeBinaryExpr when cache.ContainsKey(typeBinaryExpr.Expression):
                Expression typeExpr = Expression.Constant(cache[typeBinaryExpr.Expression], typeBinaryExpr.Expression.Type);
                return Expression.TypeIs(typeExpr, typeBinaryExpr.TypeOperand);

            default:
                // For other expressions or leaf nodes, return as-is
                return expr;
        }
    }

    private static string ExtractDetails(Expression expr, Dictionary<Expression, object?> evaluationCache)
    {
        var details = new Dictionary<string, object?>();
        ExtractVariablesFromExpression(expr, details, evaluationCache);

        if (details.Count == 0)
        {
            return string.Empty;
        }

        // Sort details alphabetically by variable name for consistent ordering
        IOrderedEnumerable<KeyValuePair<string, object?>> sortedDetails = details.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach ((string name, object? value) in sortedDetails)
        {
#if NET
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {name} = {FormatValue(value)}");
#else
            sb.AppendLine($"  {name} = {FormatValue(value)}");
#endif
        }

        return sb.ToString();
    }

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

            case BinaryExpression binaryExpr:
                ExtractVariablesFromExpression(binaryExpr.Left, details, evaluationCache, suppressIntermediateValues);
                ExtractVariablesFromExpression(binaryExpr.Right, details, evaluationCache, suppressIntermediateValues);
                break;

            case TypeBinaryExpression typeBinaryExpr:
                // Extract variables from the expression being tested (e.g., 'obj' in 'obj is int')
                ExtractVariablesFromExpression(typeBinaryExpr.Expression, details, evaluationCache, suppressIntermediateValues);
                break;

            // Special handling for ArrayLength expressions
            case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.ArrayLength:
                string arrayName = GetCleanMemberName(unaryExpr.Operand);
                string lengthDisplayName = $"{arrayName}.Length";
                TryAddExpressionValue(unaryExpr, lengthDisplayName, details, evaluationCache);

                if (unaryExpr.Operand is not MemberExpression)
                {
                    ExtractVariablesFromExpression(unaryExpr.Operand, details, evaluationCache, suppressIntermediateValues);
                }

                break;

            case UnaryExpression unaryExpr:
                ExtractVariablesFromExpression(unaryExpr.Operand, details, evaluationCache, suppressIntermediateValues);
                break;

            case MemberExpression memberExpr:
                AddMemberExpressionToDetails(memberExpr, details, evaluationCache);
                break;

            case MethodCallExpression callExpr:
                HandleMethodCallExpression(callExpr, details, evaluationCache, suppressIntermediateValues);
                break;

            case ConditionalExpression conditionalExpr:
                ExtractVariablesFromExpression(conditionalExpr.Test, details, evaluationCache, suppressIntermediateValues);
                ExtractVariablesFromExpression(conditionalExpr.IfTrue, details, evaluationCache, suppressIntermediateValues);
                ExtractVariablesFromExpression(conditionalExpr.IfFalse, details, evaluationCache, suppressIntermediateValues);
                break;

            case InvocationExpression invocationExpr:
                ExtractVariablesFromExpression(invocationExpr.Expression, details, evaluationCache, suppressIntermediateValues);
                foreach (Expression argument in invocationExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
                }

                break;

            case NewExpression newExpr:
                foreach (Expression argument in newExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, evaluationCache, suppressIntermediateValues);
                }

                // Don't display the new object value if we're suppressing intermediate values
                // (which happens when it's part of a member access chain)
                if (!suppressIntermediateValues)
                {
                    string newExprDisplay = GetCleanMemberName(newExpr);
                    TryAddExpressionValue(newExpr, newExprDisplay, details, evaluationCache);
                }

                break;

            case ListInitExpression listInitExpr:
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
                foreach (Expression expression in newArrayExpr.Expressions)
                {
                    ExtractVariablesFromExpression(expression, details, evaluationCache, suppressIntermediateValues);
                }

                break;
        }
    }

    private static void HandleArrayIndexExpression(BinaryExpression arrayIndexExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        string arrayName = GetCleanMemberName(arrayIndexExpr.Left);
        string indexValue = GetIndexArgumentDisplay(arrayIndexExpr.Right, evaluationCache);
        string indexerDisplay = $"{arrayName}[{indexValue}]";
        TryAddExpressionValue(arrayIndexExpr, indexerDisplay, details, evaluationCache);

        // Extract variables from the index argument
        ExtractVariablesFromExpression(arrayIndexExpr.Right, details, evaluationCache);
    }

    private static void AddMemberExpressionToDetails(MemberExpression memberExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        string displayName = GetCleanMemberName(memberExpr);

        if (details.ContainsKey(displayName))
        {
            return;
        }

        // Use cached value if available, otherwise try to get it from the cache or mark as failed
        details[displayName] = evaluationCache.TryGetValue(memberExpr, out object? cachedValue)
            ? cachedValue
            : "<Failed to evaluate>";

        // Skip Func and Action delegates as they don't provide useful information in assertion failures
        if (IsFuncOrActionType(cachedValue?.GetType()))
        {
            details.Remove(displayName);
            return;
        }

        // Only extract variables from the object being accessed if it's not a member expression or indexer (which would show the full collection)
        if (memberExpr.Expression is not null and not MemberExpression)
        {
            ExtractVariablesFromExpression(memberExpr.Expression, details, evaluationCache, suppressIntermediateValues: true);
        }
    }

    private static void HandleMethodCallExpression(MethodCallExpression callExpr, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache, bool suppressIntermediateValues = false)
    {
        // Special handling for indexers (get_Item calls)
        if (callExpr.Method.Name == "get_Item" && callExpr.Object is not null && callExpr.Arguments.Count == 1)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexValue = GetIndexArgumentDisplay(callExpr.Arguments[0], evaluationCache);
            string indexerDisplay = $"{objectName}[{indexValue}]";
            TryAddExpressionValue(callExpr, indexerDisplay, details, evaluationCache);

            // Extract variables from the index argument but not from the object.
            ExtractVariablesFromExpression(callExpr.Arguments[0], details, evaluationCache, suppressIntermediateValues);
        }
        else if (callExpr.Method.Name == "Get" && callExpr.Object is not null && callExpr.Arguments.Count > 0)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexDisplay = string.Join(", ", callExpr.Arguments.Select(arg => GetIndexArgumentDisplay(arg, evaluationCache)));
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
            // Check if the method returns a boolean
            if (callExpr.Method.ReturnType == typeof(bool))
            {
                if (callExpr.Object is not null)
                {
                    // For boolean-returning methods, extract details from the object being called
                    // This captures the last non-boolean method call in a chain
                    ExtractVariablesFromExpression(callExpr.Object, details, evaluationCache, suppressIntermediateValues);
                }
            }
            else
            {
                // For non-boolean methods, capture the method call itself
                string methodCallDisplay = GetCleanMemberName(callExpr);
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

    private static bool IsFuncOrActionType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        // Check for Action types
        if (type == typeof(Action) ||
            (type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("Action`", StringComparison.Ordinal)))
        {
            return true;
        }

        // Check for Func types
        return type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("Func`", StringComparison.Ordinal);
    }

    private static string GetCleanMemberName(Expression? expr)
        => expr is null
            ? "<null>"
            : CleanExpressionText(expr.ToString());

    private static string GetIndexArgumentDisplay(Expression indexArg, Dictionary<Expression, object?> evaluationCache)
    {
        try
        {
            if (indexArg is ConstantExpression constExpr)
            {
                return FormatValue(constExpr.Value);
            }

            // For member expressions that are fields or simple variable references,
            // preserve the variable name to help with readability
            if (indexArg is MemberExpression memberExpr && IsVariableReference(memberExpr))
            {
                return CleanExpressionText(indexArg.ToString());
            }

            // For parameter expressions (method parameters), preserve the parameter name
            if (indexArg is ParameterExpression)
            {
                return CleanExpressionText(indexArg.ToString());
            }

            // For complex expressions, preserve the original expression text for better readability
            if (!IsSimpleExpression(indexArg))
            {
                return CleanExpressionText(indexArg.ToString());
            }

            // For other simple expressions, try to use cached value
            if (evaluationCache.TryGetValue(indexArg, out object? cachedValue))
            {
                return FormatValue(cachedValue);
            }

            // Fallback to expression text
            return CleanExpressionText(indexArg.ToString());
        }
        catch
        {
            return CleanExpressionText(indexArg.ToString());
        }
    }

    private static bool IsVariableReference(MemberExpression memberExpr)
        // Check if this is a field or local variable reference (not a property access on an object)
        // Fields typically have Expression as null (static) or ConstantExpression (instance field on captured variable)
        => memberExpr.Expression is null or ConstantExpression;

    private static bool IsSimpleExpression(Expression expr)
        => expr switch
        {
            // Constants are simple
            ConstantExpression => true,
            // Parameter references are simple but should preserve names for indices
            ParameterExpression => false, // Changed: preserve parameter names in indices
            // Member expressions should be evaluated case by case
            MemberExpression => false, // Changed: evaluate member expressions individually
            // Simple unary operations on members like "!flag"
            UnaryExpression unary when unary.Operand is MemberExpression or ParameterExpression => false,
            // Everything else is considered complex (binary operations, method calls, etc.)
            _ => false,
        };

    private static string FormatValue(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable<object> e => $"[{string.Join(", ", e.Select(FormatValue))}]",
            IEnumerable e and not string => $"[{string.Join(", ", e.Cast<object>().Select(FormatValue))}]",
            _ => value.ToString() ?? "<null>",
        };

    private static string CleanExpressionText(string raw)
    {
        // Remove display class names and generated compiler prefixes
        string cleaned = raw;

        // Remove compiler-generated wrappers FIRST, before display class cleanup
        cleaned = RemoveCompilerGeneratedWrappers(cleaned);

        // Handle anonymous types - remove the compiler-generated type wrapper
        cleaned = RemoveAnonymousTypeWrappers(cleaned);

        // Handle list initialization expressions - convert from Add method calls to collection initializer syntax
        cleaned = CleanListInitializers(cleaned);

        // Handle compiler-generated display classes more comprehensively
        // Updated pattern to handle cases with and without parentheses around the display class
        cleaned = CompilerGeneratedDisplayClassRegex().Replace(cleaned, "$1");

        // Remove unnecessary outer parentheses and excessive consecutive parentheses
        cleaned = CleanParentheses(cleaned);

        return cleaned;
    }

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
            // Look for anonymous type pattern: new <>f__AnonymousType followed by generic parameters
            if (i <= input.Length - 4 && input.Substring(i, 4) == "new " &&
                i + 4 < input.Length && input.Substring(i + 4).StartsWith("<>f__AnonymousType", StringComparison.Ordinal))
            {
                // Find the start of the constructor parameters
                int constructorStart = input.IndexOf('(', i + 4);
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
                    string addMethodPattern = @"Void\s+Add\([^)]+\)\(([^)]+)\)";
                    MatchCollection addMatches = Regex.Matches(initContent, addMethodPattern);

                    if (addMatches.Count > 0)
                    {
                        // Extract type from the first Add method call
                        string firstAddPattern = @"Void\s+Add\(([^)]+)\)";
                        Match typeMatch = Regex.Match(initContent, firstAddPattern);
                        string genericType = "object"; // default fallback

                        if (typeMatch.Success)
                        {
                            string rawType = typeMatch.Groups[1].Value;
                            // Clean up type names like "Int32" to "int", "String" to "string", etc.
                            genericType = CleanTypeName(rawType);
                        }

                        // Extract all arguments from Add method calls
                        var arguments = new List<string>();
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

        // Check for "new " at the start
        if (startIndex + 4 >= input.Length || !input.Substring(startIndex, 4).Equals("new ", StringComparison.Ordinal))
        {
            return false;
        }

        int pos = startIndex + 4;

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
            if (pos + type.Length < input.Length &&
                input.Substring(pos, type.Length).Equals(type, StringComparison.Ordinal))
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
        if (pos + 4 >= input.Length || !input.Substring(pos, 4).Equals("`1()", StringComparison.Ordinal))
        {
            return false;
        }

        pos += 4;

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

    private static string RemoveOuterParentheses(string input)
    {
        if (input.Length < 2 || !input.StartsWith('(') || !input.EndsWith(')'))
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
                int keepCount = Math.Min(count, 2);
                result.Append(new string(currentChar, keepCount));
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

    private static string RemoveCompilerGeneratedWrappers(string input)
    {
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            if (TryRemoveWrapper(input, ref i, "value(", RemoveCompilerGeneratedWrappers, result) ||
                TryRemoveWrapper(input, ref i, "ArrayLength(", content => RemoveCompilerGeneratedWrappers(content) + ".Length", result))
            {
                continue;
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryRemoveWrapper(string input, ref int index, string pattern,
        Func<string, string> transform, StringBuilder result)
    {
        if (index > input.Length - pattern.Length ||
            !string.Equals(input.Substring(index, pattern.Length), pattern, StringComparison.Ordinal))
        {
            return false;
        }

        int start = index + pattern.Length;
        int parenCount = 1;
        int i = start;

        // Find matching closing parenthesis
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

        if (parenCount == 0)
        {
            // Extract content and apply transformation
            string content = input.Substring(start, i - start - 1);
            result.Append(transform(content));
            index = i;
            return true;
        }

        // Malformed, don't consume the pattern
        return false;
    }

    private static bool TryAddExpressionValue(Expression expr, string displayName, Dictionary<string, object?> details, Dictionary<Expression, object?> evaluationCache)
    {
        if (details.ContainsKey(displayName))
        {
            return false;
        }

        // Use cached value if available
        if (evaluationCache.TryGetValue(expr, out object? cachedValue))
        {
            details[displayName] = cachedValue;
            return true;
        }

        details[displayName] = "<Failed to evaluate>";
        return true;
    }

#if NET
    [GeneratedRegex(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)")]
    private static partial Regex CompilerGeneratedDisplayClassRegex();
#else
    private static Regex CompilerGeneratedDisplayClassRegex()
        => new(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)", RegexOptions.Compiled);
#endif
}

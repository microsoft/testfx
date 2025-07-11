// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
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
    public static void That(Expression<Func<bool>> condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
    {
        if (condition == null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        if (condition.Compile()())
        {
            return;
        }

        var sb = new StringBuilder();
        string expressionText = conditionExpression
            ?? $"() => {CleanExpressionText(condition.Body.ToString())}";
        sb.AppendLine($"Assert.That({expressionText}) failed.");
        if (!string.IsNullOrWhiteSpace(message))
        {
            sb.AppendLine($"Message: {message}");
        }

        string details = ExtractDetails(condition.Body);
        if (!string.IsNullOrWhiteSpace(details))
        {
            sb.AppendLine("Details:\n" + details);
        }

        throw new AssertFailedException(sb.ToString().TrimEnd());
    }

    private static string ExtractDetails(Expression expr)
    {
        var details = new Dictionary<string, object?>();
        ExtractVariablesFromExpression(expr, details);

        if (details.Count == 0)
        {
            return string.Empty;
        }

        // Sort details alphabetically by variable name for consistent ordering
        IOrderedEnumerable<KeyValuePair<string, object?>> sortedDetails = details.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach ((string name, object? value) in sortedDetails)
        {
            sb.AppendLine($"  {name} = {FormatValue(value)}");
        }

        return sb.ToString();
    }

    private static void ExtractVariablesFromExpression(Expression? expr, Dictionary<string, object?> details)
    {
        if (expr is null)
        {
            return;
        }

        switch (expr)
        {
            // Special handling for array indexing (myArray[index])
            case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.ArrayIndex:
                HandleArrayIndexExpression(binaryExpr, details);
                break;

            case BinaryExpression binaryExpr:
                ExtractVariablesFromExpression(binaryExpr.Left, details);
                ExtractVariablesFromExpression(binaryExpr.Right, details);
                break;

            case TypeBinaryExpression typeBinaryExpr:
                // Extract variables from the expression being tested (e.g., 'obj' in 'obj is int')
                ExtractVariablesFromExpression(typeBinaryExpr.Expression, details);
                break;

            // Special handling for ArrayLength expressions
            case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.ArrayLength:
                HandleArrayLengthExpression(unaryExpr, details);
                break;

            case UnaryExpression unaryExpr:
                ExtractVariablesFromExpression(unaryExpr.Operand, details);
                break;

            case MemberExpression memberExpr:
                AddMemberExpressionToDetails(memberExpr, details);
                break;

            case MethodCallExpression callExpr:
                HandleMethodCallExpression(callExpr, details);
                break;

            case ConditionalExpression conditionalExpr:
                ExtractVariablesFromExpression(conditionalExpr.Test, details);
                ExtractVariablesFromExpression(conditionalExpr.IfTrue, details);
                ExtractVariablesFromExpression(conditionalExpr.IfFalse, details);
                break;

            case ConstantExpression constantExpr when constantExpr.Value is not null:
                // Only include constants that represent captured variables (usually in display classes)
                HandleConstantExpression(constantExpr, details);
                break;

            case InvocationExpression invocationExpr:
                ExtractVariablesFromExpression(invocationExpr.Expression, details);
                foreach (Expression argument in invocationExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details);
                }

                break;

            case NewExpression newExpr:
                foreach (Expression argument in newExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details);
                }

                break;

            case ListInitExpression listInitExpr:
                ExtractVariablesFromExpression(listInitExpr.NewExpression, details);
                foreach (ElementInit initializer in listInitExpr.Initializers)
                {
                    foreach (Expression argument in initializer.Arguments)
                    {
                        ExtractVariablesFromExpression(argument, details);
                    }
                }

                break;

            case NewArrayExpression newArrayExpr:
                foreach (Expression expression in newArrayExpr.Expressions)
                {
                    ExtractVariablesFromExpression(expression, details);
                }

                break;

            case IndexExpression indexExpr:
                HandleIndexExpression(indexExpr, details);
                break;
        }
    }

    private static void HandleArrayIndexExpression(BinaryExpression arrayIndexExpr, Dictionary<string, object?> details)
    {
        string arrayName = GetCleanMemberName(arrayIndexExpr.Left);
        string indexValue = GetIndexArgumentDisplay(arrayIndexExpr.Right);
        string indexerDisplay = $"{arrayName}[{indexValue}]";

        if (!details.ContainsKey(indexerDisplay))
        {
            try
            {
                object? value = Expression.Lambda(arrayIndexExpr).Compile().DynamicInvoke();
                details[indexerDisplay] = value;
            }
            catch
            {
                details[indexerDisplay] = "<Failed to evaluate>";
            }
        }

        // Extract variables from the index argument
        ExtractVariablesFromExpression(arrayIndexExpr.Right, details);

        // Only extract variables from the array if it's a parameter expression,
        // not when it's a member expression (which would show the full array)
        if (arrayIndexExpr.Left is ParameterExpression)
        {
            ExtractVariablesFromExpression(arrayIndexExpr.Left, details);
        }
    }

    private static void HandleArrayLengthExpression(UnaryExpression arrayLengthExpr, Dictionary<string, object?> details)
    {
        string arrayName = GetCleanMemberName(arrayLengthExpr.Operand);
        string lengthDisplayName = $"{arrayName}.Length";

        if (!details.ContainsKey(lengthDisplayName))
        {
            try
            {
                object? value = Expression.Lambda(arrayLengthExpr).Compile().DynamicInvoke();
                details[lengthDisplayName] = value;
            }
            catch
            {
                details[lengthDisplayName] = "<Failed to evaluate>";
            }
        }
    }

    private static void AddMemberExpressionToDetails(MemberExpression memberExpr, Dictionary<string, object?> details)
    {
        string displayName = GetCleanMemberName(memberExpr);

        if (details.ContainsKey(displayName))
        {
            return;
        }

        try
        {
            object? value = Expression.Lambda(memberExpr).Compile().DynamicInvoke();
            details[displayName] = value;
        }
        catch
        {
            details[displayName] = "<Failed to evaluate>";
        }

        // Only extract variables from the object being accessed if it's a parameter or variable reference,
        // not when it's a member expression or indexer (which would show the full collection)
        if (memberExpr.Expression is not null and ParameterExpression)
        {
            ExtractVariablesFromExpression(memberExpr.Expression, details);
        }
    }

    private static void HandleMethodCallExpression(MethodCallExpression callExpr, Dictionary<string, object?> details)
    {
        // Special handling for indexers (get_Item calls)
        if (callExpr.Method.Name == "get_Item" && callExpr.Object is not null && callExpr.Arguments.Count == 1)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexValue = GetIndexArgumentDisplay(callExpr.Arguments[0]);
            string indexerDisplay = $"{objectName}[{indexValue}]";

            if (!details.ContainsKey(indexerDisplay))
            {
                try
                {
                    object? value = Expression.Lambda(callExpr).Compile().DynamicInvoke();
                    details[indexerDisplay] = value;
                }
                catch
                {
                    details[indexerDisplay] = "<Failed to evaluate>";
                }
            }

            // Extract variables from the index argument but not from the object.
            ExtractVariablesFromExpression(callExpr.Arguments[0], details);
        }
        else if (callExpr.Method.Name == "Get" && callExpr.Object is not null && callExpr.Arguments.Count > 0)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexDisplay = string.Join(", ", callExpr.Arguments.Select(GetIndexArgumentDisplay));
            string indexerDisplay = $"{objectName}[{indexDisplay}]";

            if (!details.ContainsKey(indexerDisplay))
            {
                try
                {
                    object? value = Expression.Lambda(callExpr).Compile().DynamicInvoke();
                    details[indexerDisplay] = value;
                }
                catch
                {
                    details[indexerDisplay] = "<Failed to evaluate>";
                }
            }

            // Extract variables from the index arguments but not from the object
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details);
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
                    ExtractVariablesFromExpression(callExpr.Object, details);
                }
            }
            else
            {
                // For non-boolean methods, capture the method call itself
                string methodCallDisplay = GetCleanMemberName(callExpr);

                if (!details.ContainsKey(methodCallDisplay))
                {
                    try
                    {
                        object? value = Expression.Lambda(callExpr).Compile().DynamicInvoke();
                        details[methodCallDisplay] = value;
                    }
                    catch
                    {
                        details[methodCallDisplay] = "<Failed to evaluate>";
                    }
                }

                // Don't extract from the object to avoid duplication
            }

            // Always extract variables from the arguments
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details);
            }
        }
    }

    private static void HandleIndexExpression(IndexExpression indexExpr, Dictionary<string, object?> details)
    {
        string objectName = GetCleanMemberName(indexExpr.Object);
        string indexDisplay = string.Join(", ", indexExpr.Arguments.Select(GetIndexArgumentDisplay));
        string indexerDisplay = $"{objectName}[{indexDisplay}]";

        if (!details.ContainsKey(indexerDisplay))
        {
            try
            {
                object? value = Expression.Lambda(indexExpr).Compile().DynamicInvoke();
                details[indexerDisplay] = value;
            }
            catch
            {
                details[indexerDisplay] = "<Failed to evaluate>";
            }
        }

        // Only extract variables from the object if it's a parameter expression,
        // not when it's a member expression (which would show the full collection)
        if (indexExpr.Object is ParameterExpression)
        {
            ExtractVariablesFromExpression(indexExpr.Object, details);
        }

        foreach (Expression indexArg in indexExpr.Arguments)
        {
            ExtractVariablesFromExpression(indexArg, details);
        }
    }

    private static void HandleConstantExpression(ConstantExpression constantExpr, Dictionary<string, object?> details)
    {
        string constantStr = constantExpr.ToString();

        // Skip display class constants and literal values
        if (constantStr.Contains("DisplayClass") || IsLiteralConstant(constantStr))
        {
            return;
        }

        string cleanName = CleanExpressionText(constantStr);
        if (!details.ContainsKey(cleanName))
        {
            details[cleanName] = constantExpr.Value;
        }
    }

    private static string GetCleanMemberName(Expression? expr)
        => expr is null
            ? "<null>"
            : CleanExpressionText(expr.ToString());

    private static string GetIndexArgumentDisplay(Expression indexArg)
    {
        try
        {
            if (indexArg is ConstantExpression constExpr)
            {
                return FormatValue(constExpr.Value);
            }

            // For complex index expressions, just use the expression string
            return CleanExpressionText(indexArg.ToString());
        }
        catch
        {
            return CleanExpressionText(indexArg.ToString());
        }
    }

    private static bool IsLiteralConstant(string constantString)
    {
        if (string.IsNullOrEmpty(constantString))
        {
            return true;
        }

        // Check for quoted strings
        if (constantString.StartsWith('\"') && constantString.EndsWith('\"'))
        {
            return true;
        }

        // Check for numeric literals (int, float, double, decimal)
        if (constantString.All(c => char.IsDigit(c) || c == '.' || c == '-' || c == 'f' || c == 'd' || c == 'm'))
        {
            return true;
        }

        // Check for boolean literals
        if (constantString is "True" or "False")
        {
            return true;
        }

        // Check for null literal
        if (constantString == "null")
        {
            return true;
        }

        // Check for character literals
        if (constantString.StartsWith('\'') && constantString.EndsWith('\'') && constantString.Length >= 2)
        {
            return true;
        }

        // If it doesn't match any of the above, it's likely a variable reference or complex expression
        return false;
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
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

        // Handle compiler-generated display classes more comprehensively
        // Updated pattern to handle cases with and without parentheses around the display class
        cleaned = CompilerGeneratedDisplayClassRegex().Replace(cleaned, "$1");

        // Convert expression types to their C# equivalents
        cleaned = cleaned.Replace(" AndAlso ", " && ");
        cleaned = cleaned.Replace(" OrElse ", " || ");
        cleaned = cleaned.Replace(" Equal ", " == ");
        cleaned = cleaned.Replace(" NotEqual ", " != ");
        cleaned = cleaned.Replace(" GreaterThan ", " > ");
        cleaned = cleaned.Replace(" LessThan ", " < ");
        cleaned = cleaned.Replace(" GreaterThanOrEqual ", " >= ");
        cleaned = cleaned.Replace(" LessThanOrEqual ", " <= ");

        // Convert conditional expressions: "IIF(condition, trueValue, falseValue)" to "(condition ? trueValue : falseValue)"
        // Don't add extra parentheses since the expression is likely already wrapped
        cleaned = ConditionalExpressionRegex().Replace(cleaned, "$1 ? $2 : $3");

        // Fix lambda expressions: "param => ( body" becomes "param => body"
        cleaned = FixLambdaExpressionRegex().Replace(cleaned, "$1 => $2");

        // Fix property comparisons: "obj.prop > num" becomes "obj.prop > num" (normalize spacing)
        cleaned = FixPropertyComparisonRegex().Replace(cleaned, "$1.$2 > $3");

        // Remove unnecessary outer parentheses and excessive consecutive parentheses
        cleaned = CleanParentheses(cleaned);

        return cleaned;
    }

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
            if (TryRemoveWrapper(input, ref i, "value(", content => content, result) ||
                TryRemoveWrapper(input, ref i, "ArrayLength(", content => content + ".Length", result))
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

#if NET
    [GeneratedRegex(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)")]
    private static partial Regex CompilerGeneratedDisplayClassRegex();

    [GeneratedRegex(@"IIF\(([^,]+),\s*([^,]+),\s*([^)]+)\)")]
    private static partial Regex ConditionalExpressionRegex();

    [GeneratedRegex(@"(\w+)\s*=>\s*\(\s*(\w+)")]
    private static partial Regex FixLambdaExpressionRegex();

    [GeneratedRegex(@"(\w+)\.(\w+)\s*>\s*(\d+)")]
    private static partial Regex FixPropertyComparisonRegex();
#else
    private static Regex CompilerGeneratedDisplayClassRegex()
        => new(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)", RegexOptions.Compiled);

    private static Regex ConditionalExpressionRegex()
        => new(@"IIF\(([^,]+),\s*([^,]+),\s*([^)]+)\)", RegexOptions.Compiled);

    private static Regex FixLambdaExpressionRegex()
        => new(@"(\w+)\s*=>\s*\(\s*(\w+)", RegexOptions.Compiled);

    private static Regex FixPropertyComparisonRegex()
        => new(@"(\w+)\.(\w+)\s*>\s*(\d+)", RegexOptions.Compiled);
#endif
}

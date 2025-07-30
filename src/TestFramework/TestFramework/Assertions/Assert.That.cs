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
            ?? throw new ArgumentNullException(nameof(conditionExpression));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatFailedFormat, expressionText));
        if (!string.IsNullOrWhiteSpace(message))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatMessageFormat, message));
        }

        string details = ExtractDetails(condition.Body);
        if (!string.IsNullOrWhiteSpace(details))
        {
            sb.AppendLine(FrameworkMessages.AssertThatDetailsPrefix);
            sb.AppendLine(details);
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
#if NET
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {name} = {FormatValue(value)}");
#else
            sb.AppendLine($"  {name} = {FormatValue(value)}");
#endif
        }

        return sb.ToString();
    }

    private static void ExtractVariablesFromExpression(Expression? expr, Dictionary<string, object?> details, bool suppressIntermediateValues = false)
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
                ExtractVariablesFromExpression(binaryExpr.Left, details, suppressIntermediateValues);
                ExtractVariablesFromExpression(binaryExpr.Right, details, suppressIntermediateValues);
                break;

            case TypeBinaryExpression typeBinaryExpr:
                // Extract variables from the expression being tested (e.g., 'obj' in 'obj is int')
                ExtractVariablesFromExpression(typeBinaryExpr.Expression, details, suppressIntermediateValues);
                break;

            // Special handling for ArrayLength expressions
            case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.ArrayLength:
                string arrayName = GetCleanMemberName(unaryExpr.Operand);
                string lengthDisplayName = $"{arrayName}.Length";
                TryAddExpressionValue(unaryExpr, lengthDisplayName, details);

                if (unaryExpr.Operand is not MemberExpression)
                {
                    ExtractVariablesFromExpression(unaryExpr.Operand, details, suppressIntermediateValues);
                }

                break;

            case UnaryExpression unaryExpr:
                ExtractVariablesFromExpression(unaryExpr.Operand, details, suppressIntermediateValues);
                break;

            case MemberExpression memberExpr:
                AddMemberExpressionToDetails(memberExpr, details);
                break;

            case MethodCallExpression callExpr:
                HandleMethodCallExpression(callExpr, details, suppressIntermediateValues);
                break;

            case ConditionalExpression conditionalExpr:
                ExtractVariablesFromExpression(conditionalExpr.Test, details, suppressIntermediateValues);
                ExtractVariablesFromExpression(conditionalExpr.IfTrue, details, suppressIntermediateValues);
                ExtractVariablesFromExpression(conditionalExpr.IfFalse, details, suppressIntermediateValues);
                break;

            case InvocationExpression invocationExpr:
                ExtractVariablesFromExpression(invocationExpr.Expression, details, suppressIntermediateValues);
                foreach (Expression argument in invocationExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, suppressIntermediateValues);
                }

                break;

            case NewExpression newExpr:
                foreach (Expression argument in newExpr.Arguments)
                {
                    ExtractVariablesFromExpression(argument, details, suppressIntermediateValues);
                }

                // Don't display the new object value if we're suppressing intermediate values
                // (which happens when it's part of a member access chain)
                if (!suppressIntermediateValues)
                {
                    string newExprDisplay = GetCleanMemberName(newExpr);
                    TryAddExpressionValue(newExpr, newExprDisplay, details);
                }

                break;

            case ListInitExpression listInitExpr:
                ExtractVariablesFromExpression(listInitExpr.NewExpression, details, suppressIntermediateValues: true);
                foreach (ElementInit initializer in listInitExpr.Initializers)
                {
                    foreach (Expression argument in initializer.Arguments)
                    {
                        ExtractVariablesFromExpression(argument, details, suppressIntermediateValues);
                    }
                }

                break;

            case NewArrayExpression newArrayExpr:
                foreach (Expression expression in newArrayExpr.Expressions)
                {
                    ExtractVariablesFromExpression(expression, details, suppressIntermediateValues);
                }

                break;
        }
    }

    private static void HandleArrayIndexExpression(BinaryExpression arrayIndexExpr, Dictionary<string, object?> details)
    {
        string arrayName = GetCleanMemberName(arrayIndexExpr.Left);
        string indexValue = GetIndexArgumentDisplay(arrayIndexExpr.Right);
        string indexerDisplay = $"{arrayName}[{indexValue}]";
        TryAddExpressionValue(arrayIndexExpr, indexerDisplay, details);

        // Extract variables from the index argument
        ExtractVariablesFromExpression(arrayIndexExpr.Right, details);
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

            // Skip Func and Action delegates as they don't provide useful information in assertion failures
            if (IsFuncOrActionType(value?.GetType()))
            {
                return;
            }

            details[displayName] = value;
        }
        catch
        {
            details[displayName] = "<Failed to evaluate>";
        }

        // Only extract variables from the object being accessed if it's not a member expression or indexer (which would show the full collection)
        if (memberExpr.Expression is not null and not MemberExpression)
        {
            ExtractVariablesFromExpression(memberExpr.Expression, details, suppressIntermediateValues: true);
        }
    }

    private static void HandleMethodCallExpression(MethodCallExpression callExpr, Dictionary<string, object?> details, bool suppressIntermediateValues = false)
    {
        // Special handling for indexers (get_Item calls)
        if (callExpr.Method.Name == "get_Item" && callExpr.Object is not null && callExpr.Arguments.Count == 1)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexValue = GetIndexArgumentDisplay(callExpr.Arguments[0]);
            string indexerDisplay = $"{objectName}[{indexValue}]";
            TryAddExpressionValue(callExpr, indexerDisplay, details);

            // Extract variables from the index argument but not from the object.
            ExtractVariablesFromExpression(callExpr.Arguments[0], details, suppressIntermediateValues);
        }
        else if (callExpr.Method.Name == "Get" && callExpr.Object is not null && callExpr.Arguments.Count > 0)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexDisplay = string.Join(", ", callExpr.Arguments.Select(GetIndexArgumentDisplay));
            string indexerDisplay = $"{objectName}[{indexDisplay}]";
            TryAddExpressionValue(callExpr, indexerDisplay, details);

            // Extract variables from the index arguments but not from the object
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details, suppressIntermediateValues);
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
                    ExtractVariablesFromExpression(callExpr.Object, details, suppressIntermediateValues);
                }
            }
            else
            {
                // For non-boolean methods, capture the method call itself
                string methodCallDisplay = GetCleanMemberName(callExpr);
                TryAddExpressionValue(callExpr, methodCallDisplay, details);

                // Don't extract from the object to avoid duplication
            }

            // Always extract variables from the arguments
            foreach (Expression argument in callExpr.Arguments)
            {
                ExtractVariablesFromExpression(argument, details, suppressIntermediateValues);
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

    private static bool TryAddExpressionValue(Expression expr, string displayName, Dictionary<string, object?> details)
    {
        if (details.ContainsKey(displayName))
        {
            return false;
        }

        try
        {
            object? value = Expression.Lambda(expr).Compile().DynamicInvoke();
            details[displayName] = value;
        }
        catch
        {
            details[displayName] = "<Failed to evaluate>";
        }

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

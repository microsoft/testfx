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
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="AssertFailedException">Thrown if the evaluated condition is <see langword="false"/>.</exception>
    public static void That(Expression<Func<bool>> condition, string? message = null)
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
        string expressionText = CleanExpressionText(condition.Body.ToString());
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
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        var stack = new Stack<Expression>();
        stack.Push(expr);

        while (stack.Count > 0)
        {
            Expression current = stack.Pop();

            switch (current)
            {
                case BinaryExpression binaryExpr:
                    if (binaryExpr.NodeType == ExpressionType.AndAlso)
                    {
                        if (!EvaluateBoolean(binaryExpr.Left))
                        {
                            stack.Push(binaryExpr.Left);
                            continue; // short-circuit: right side not evaluated
                        }

                        stack.Push(binaryExpr.Left);
                        stack.Push(binaryExpr.Right);
                        continue;
                    }

                    if (binaryExpr.NodeType == ExpressionType.OrElse)
                    {
                        if (EvaluateBoolean(binaryExpr.Left))
                        {
                            stack.Push(binaryExpr.Left);
                            continue; // short-circuit: right side not evaluated
                        }

                        stack.Push(binaryExpr.Left);
                        stack.Push(binaryExpr.Right);
                        continue;
                    }

                    stack.Push(binaryExpr.Left);
                    stack.Push(binaryExpr.Right);
                    continue;

                case MemberExpression memberExpr when !seen.Contains(memberExpr.ToString()):
                    {
                        seen.Add(memberExpr.ToString());

                        object? value;
                        try
                        {
                            value = Expression.Lambda(memberExpr).Compile().DynamicInvoke();
                        }
                        catch
                        {
                            sb.AppendLine($"  {CleanExpressionText(memberExpr.ToString())} = <Failed to evaluate>");
                            continue;
                        }

                        sb.AppendLine($"  {CleanExpressionText(memberExpr.ToString())} = {FormatValue(value)}");
                        break;
                    }

                case UnaryExpression unaryExpr:
                    {
                        stack.Push(unaryExpr.Operand);
                        break;
                    }

                case MethodCallExpression callExpr:
                    {
                        // For array indexing, add the indexed expression to details
                        if (callExpr.Method.Name == "get_Item" && callExpr.Object != null && callExpr.Arguments.Count == 1)
                        {
                            string indexExpr = CleanExpressionText(callExpr.ToString());
                            if (seen.Add(indexExpr))
                            {
                                try
                                {
                                    object? value = Expression.Lambda(callExpr).Compile().DynamicInvoke();
                                    sb.AppendLine($"  {indexExpr} = {FormatValue(value)}");
                                }
                                catch
                                {
                                    sb.AppendLine($"  {indexExpr} = <Failed to evaluate>");
                                }
                            }
                        }

                        foreach (Expression? arg in callExpr.Arguments)
                        {
                            stack.Push(arg);
                        }

                        if (callExpr.Object != null)
                        {
                            stack.Push(callExpr.Object);
                        }

                        break;
                    }

                case ConditionalExpression conditionalExpr:
                    {
                        stack.Push(conditionalExpr.Test);
                        stack.Push(conditionalExpr.IfTrue);
                        stack.Push(conditionalExpr.IfFalse);
                        break;
                    }

                case ConstantExpression constantExpr when constantExpr.Value != null:
                    {
                        // Skip constants that are part of display classes
                        string constStr = constantExpr.ToString();
                        if (!constStr.Contains("DisplayClass") && !seen.Contains(constStr))
                        {
                            seen.Add(constStr);
                            // Only include if it's a variable reference from a display class, not a literal value
                            // Skip string literals (quoted strings), numeric literals, boolean literals, etc.
                            if (!IsLiteralConstant(constStr))
                            {
                                sb.AppendLine($"  {CleanExpressionText(constStr)} = {FormatValue(constantExpr.Value)}");
                            }
                        }

                        break;
                    }
            }
        }

        return sb.ToString();
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

    private static bool EvaluateBoolean(Expression expr)
    {
        try
        {
            bool val = Expression.Lambda<Func<bool>>(expr).Compile()();
            return val;
        }
        catch
        {
            return false;
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

        // Remove unnecessary outer parentheses - check if the entire expression is wrapped in a single pair
        cleaned = RemoveUnnecessaryOuterParentheses(cleaned);

        // Clean up excessive parentheses - remove any triple or more consecutive opening/closing parens
        cleaned = ExcessiveParenthesesRegex().Replace(cleaned, m =>
        {
            char parenChar = m.Value[0];
            // Keep at most 2 consecutive parentheses
            return new string(parenChar, Math.Min(m.Length, 2));
        });

        return cleaned;
    }

    private static string RemoveUnnecessaryOuterParentheses(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Trim whitespace first
        input = input.Trim();

        // Check if the entire expression is wrapped in parentheses
        if (input.Length >= 2 && input.StartsWith('(') && input.EndsWith(')'))
        {
            // Check if these are the outermost parentheses by ensuring they are balanced
            int parenCount = 0;
            bool isOuterParen = true;

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
                        isOuterParen = false;
                        break;
                    }
                }
            }

            // If the first and last parentheses are indeed the outermost pair, remove them
            if (isOuterParen && parenCount == 0)
            {
                string inner = input.Substring(1, input.Length - 2).Trim();
                // Recursively check for more unnecessary outer parentheses
                return RemoveUnnecessaryOuterParentheses(inner);
            }
        }

        return input;
    }

    private static string RemoveCompilerGeneratedWrappers(string input)
    {
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            // Check for value( wrapper
            if (i <= input.Length - 6 && input.Substring(i, 6) == "value(")
            {
                i += 6; // Skip "value("
                int parenCount = 1;
                int start = i;

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
                    // Extract content between parentheses
                    string content = input.Substring(start, i - start - 1);
                    result.Append(content);
                }
                else
                {
                    // Malformed, just append as is
                    result.Append("value(");
                    i = start;
                }
            }

            // Check for ArrayLength( wrapper
            else if (i <= input.Length - 12 && input.Substring(i, 12) == "ArrayLength(")
            {
                i += 12; // Skip "ArrayLength("
                int parenCount = 1;
                int start = i;

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
                    // Extract content between parentheses and add .Length
                    string content = input.Substring(start, i - start - 1);
                    result.Append(content).Append(".Length");
                }
                else
                {
                    // Malformed, just append as is
                    result.Append("ArrayLength(");
                    i = start;
                }
            }
            else
            {
                result.Append(input[i]);
                i++;
            }
        }

        return result.ToString();
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

    [GeneratedRegex(@"[\(\)]{3,}")]
    private static partial Regex ExcessiveParenthesesRegex();
#else
    private static Regex CompilerGeneratedDisplayClassRegex()
        => new(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)", RegexOptions.Compiled);

    private static Regex ConditionalExpressionRegex()
        => new(@"IIF\(([^,]+),\s*([^,]+),\s*([^)]+)\)", RegexOptions.Compiled);

    private static Regex FixLambdaExpressionRegex()
        => new(@"(\w+)\s*=>\s*\(\s*(\w+)", RegexOptions.Compiled);

    private static Regex FixPropertyComparisonRegex()
        => new(@"(\w+)\.(\w+)\s*>\s*(\d+)", RegexOptions.Compiled);

    private static Regex ExcessiveParenthesesRegex()
        => new(@"[\(\)]{3,}", RegexOptions.Compiled);
#endif
}

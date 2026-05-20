// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
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

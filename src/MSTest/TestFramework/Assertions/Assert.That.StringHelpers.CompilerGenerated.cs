// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
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
        // Check if the pattern exists at the current index (non-allocating ordinal compare
        // to avoid per-probe Substring allocations on the diagnostic cleanup hot path).
        if (!HasSubstringAt(input, index, pattern))
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
#else
    private static readonly Regex CompilerGeneratedDisplayClass = new(
        @"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)",
        RegexOptions.Compiled);

    private static Regex CompilerGeneratedDisplayClassRegex() => CompilerGeneratedDisplayClass;
#endif
}

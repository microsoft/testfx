// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
    private const string AddCallPrefix = "Void Add(";
    private const string StringOperandSuffix = "\")";

    // Hoisted to a static field so TryMatchListInitPattern is allocation-free on the
    // diagnostic text cleanup hot path (CleanListInitializers can call it repeatedly).
    private static readonly string[] ListInitCollectionTypes = ["List", "IList", "ICollection", "IEnumerable"];

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
                // Find the matching closing brace for the initializer. Rendered string constants are unescaped, so
                // quotes nested in an operand (e.g. `"{"}".Length`) can pair with the wrong quote. When the quote-aware
                // scan does not close right after an Add call, fall back to plain brace matching.
                int braceStart = patternEnd;
                bool closed = TryFindInitializerEnd(input, braceStart, honorQuotes: true, out int braceEnd);
                if ((!closed || input[braceEnd - 2] != ')')
                    && TryFindInitializerEnd(input, braceStart, honorQuotes: false, out int plainBraceEnd))
                {
                    closed = true;
                    braceEnd = plainBraceEnd;
                }

                if (closed)
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

                            // Only a char constant renders as a lone brace, including when boxed as object.
                            if (argument is "{" or "}")
                            {
                                argument = $"'{argument}'";
                            }

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

    /// <summary>
    /// Finds the index just past the brace that closes the initializer opened at <paramref name="braceStart"/>.
    /// When <paramref name="honorQuotes"/> is set, rendered char and string constants are skipped instead of
    /// brace-counted. Returns whether the opening brace is balanced.
    /// </summary>
    private static bool TryFindInitializerEnd(string input, int braceStart, bool honorQuotes, out int braceEnd)
    {
        int braceCount = 1;
        braceEnd = braceStart + 1;

        while (braceEnd < input.Length && braceCount > 0)
        {
            char current = input[braceEnd];

            if (honorQuotes)
            {
                // Expression diagnostics render char constants unquoted and string constants unescaped,
                // whatever the element type (e.g. `Void Add(System.Object)(")` or `Void Add(System.Object)("a"b")`).
                // A single-character operand is a value, never a brace or quote delimiter, and a quoted
                // operand ends at the `")` that closes its Add call.
                if (IsAddOperandStart(input, braceEnd))
                {
                    if (IsSingleCharacterOperand(input, braceEnd))
                    {
                        braceEnd++;
                        continue;
                    }

                    if (current == '"' && TryFindStringOperandEnd(input, braceEnd, out int operandEnd))
                    {
                        braceEnd = operandEnd;
                        continue;
                    }
                }

                if (current == '"')
                {
                    braceEnd = SkipRenderedStringConstant(input, braceEnd);
                    continue;
                }
            }

            if (current == '{')
            {
                braceCount++;
            }
            else if (current == '}')
            {
                braceCount--;
            }

            braceEnd++;
        }

        return braceCount == 0;
    }

    /// <summary>
    /// Returns whether <paramref name="index"/> is the first character of an operand rendered as
    /// <c>Void Add(T)(operand)</c>, for any element type <c>T</c>.
    /// </summary>
    private static bool IsAddOperandStart(string input, int index)
    {
        if (index < 2 || input[index - 1] != '(' || input[index - 2] != ')')
        {
            return false;
        }

        int addStart = input.LastIndexOf(AddCallPrefix, index - 2, StringComparison.Ordinal);
        return addStart >= 0 && input.IndexOf(')', addStart + AddCallPrefix.Length) == index - 2;
    }

    private static bool IsSingleCharacterOperand(string input, int index)
        => index + 2 < input.Length && input[index + 1] == ')' && (input[index + 2] is ',' or '}');

    /// <summary>
    /// Finds the end of a quoted Add operand. String constants are rendered as raw text between double
    /// quotes, without escaping, so the operand ends at the first <c>")</c> that is followed by the next
    /// initializer element or the closing brace; this keeps values that contain quotes, braces, or a
    /// trailing backslash intact.
    /// </summary>
    private static bool TryFindStringOperandEnd(string input, int quoteIndex, out int operandEnd)
    {
        for (int closingQuote = input.IndexOf(StringOperandSuffix, quoteIndex + 1, StringComparison.Ordinal);
            closingQuote >= 0;
            closingQuote = input.IndexOf(StringOperandSuffix, closingQuote + 1, StringComparison.Ordinal))
        {
            int next = closingQuote + StringOperandSuffix.Length;
            if (next < input.Length && (input[next] is ',' or '}'))
            {
                operandEnd = next;
                return true;
            }
        }

        operandEnd = quoteIndex;
        return false;
    }

    /// <summary>
    /// Returns the index just past a string constant nested inside an operand, such as a method argument.
    /// An unmatched quote is a raw character (for example a boxed char rendered as <c>Convert(", Object)</c>)
    /// and is skipped on its own.
    /// </summary>
    private static int SkipRenderedStringConstant(string input, int quoteIndex)
    {
        int closingQuote = input.IndexOf('"', quoteIndex + 1);
        return closingQuote < 0 ? quoteIndex + 1 : closingQuote + 1;
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

        // Check for collection type names. Use an indexed for loop (instead of foreach +
        // implicit filter) to satisfy the CodeQL "missed Where" heuristic without paying for a
        // LINQ FirstOrDefault/Where delegate allocation on the diagnostic cleanup hot path.
        string matchedType = string.Empty;

        for (int typeIndex = 0; typeIndex < ListInitCollectionTypes.Length; typeIndex++)
        {
            string type = ListInitCollectionTypes[typeIndex];
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

#if NET
    [GeneratedRegex(@"Void\s+Add\([^)]+\)\(([^)]+)\)")]
    private static partial Regex ListInitAddArgumentRegex();

    [GeneratedRegex(@"Void\s+Add\(([^)]+)\)")]
    private static partial Regex ListInitAddTypeRegex();
#else
    private static readonly Regex ListInitAddArgument = new(
        @"Void\s+Add\([^)]+\)\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex ListInitAddType = new(
        @"Void\s+Add\(([^)]+)\)",
        RegexOptions.Compiled);

    private static Regex ListInitAddArgumentRegex() => ListInitAddArgument;

    private static Regex ListInitAddTypeRegex() => ListInitAddType;
#endif
}

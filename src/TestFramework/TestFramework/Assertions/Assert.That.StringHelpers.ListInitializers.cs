// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
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

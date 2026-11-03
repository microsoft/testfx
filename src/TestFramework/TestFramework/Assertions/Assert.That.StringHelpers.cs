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
    /// Returns <see langword="true"/> if <paramref name="value"/> occurs in
    /// <paramref name="input"/> at <paramref name="start"/> using ordinal comparison. Avoids the
    /// substring allocation that <c>input.Substring(start, value.Length) == value</c> would
    /// incur for every probe in the diagnostic text cleanup pipeline.
    /// </summary>
    private static bool HasSubstringAt(string input, int start, string value)
        => start >= 0
            && start <= input.Length - value.Length
            && string.CompareOrdinal(input, start, value, 0, value.Length) == 0;
}

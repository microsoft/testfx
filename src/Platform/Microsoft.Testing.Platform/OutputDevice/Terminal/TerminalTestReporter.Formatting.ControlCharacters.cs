// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    // SearchValues for efficient detection of control characters
#if NET8_0_OR_GREATER
    private static readonly System.Buffers.SearchValues<char> AllControlChars = CreateControlCharSearchValues(includeWhitespace: true);
    private static readonly System.Buffers.SearchValues<char> NonWhitespaceControlChars = CreateControlCharSearchValues(includeWhitespace: false);

    private static System.Buffers.SearchValues<char> CreateControlCharSearchValues(bool includeWhitespace)
    {
        var controlChars = new List<char>();
        for (char c = '\0'; c <= '\u00FF'; c++) // Check first 256 characters for performance
        {
            if (char.IsControl(c))
            {
                if (includeWhitespace || (c != '\t' && c != '\n' && c != '\r'))
                {
                    controlChars.Add(c);
                }
            }
        }

        return System.Buffers.SearchValues.Create(controlChars.ToArray());
    }
#else
    private static readonly char[] AllControlChars = CreateControlCharArray(includeWhitespace: true);
    private static readonly char[] NonWhitespaceControlChars = CreateControlCharArray(includeWhitespace: false);

    private static char[] CreateControlCharArray(bool includeWhitespace)
    {
        var controlChars = new List<char>();
        for (char c = '\0'; c <= '\u00FF'; c++) // Check first 256 characters for performance
        {
            if (char.IsControl(c))
            {
                if (includeWhitespace || (c != '\t' && c != '\n' && c != '\r'))
                {
                    controlChars.Add(c);
                }
            }
        }

        return [.. controlChars];
    }
#endif

    [return: NotNullIfNotNull(nameof(text))]
    private static string? MakeControlCharactersVisible(string? text, bool normalizeWhitespaceCharacters)
    {
        if (text is null)
        {
            return null;
        }

#if NET8_0_OR_GREATER
        // Use SearchValues to efficiently check if we need to do any work
        System.Buffers.SearchValues<char> searchValues = normalizeWhitespaceCharacters ? AllControlChars : NonWhitespaceControlChars;
        if (text.AsSpan().IndexOfAny(searchValues) == -1)
        {
            return text; // No control characters found, return original string
        }
#else
        // Use IndexOfAny to check if we need to do any work
        char[] searchChars = normalizeWhitespaceCharacters ? AllControlChars : NonWhitespaceControlChars;
        if (text.IndexOfAny(searchChars) == -1)
        {
            return text; // No control characters found, return original string
        }
#endif

        // Pre-allocate StringBuilder with known capacity
        var sb = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            if (char.IsControl(c))
            {
                // Skip normalization for whitespace characters when not requested
                if (!normalizeWhitespaceCharacters && (c == '\t' || c == '\n' || c == '\r'))
                {
                    sb.Append(c);
                }
                else
                {
                    // Convert to Unicode control picture using bit manipulation (0x2400 + char value)
                    // For C0 control characters (0x00-0x1F), this produces proper Unicode control pictures (U+2400-U+241F)
                    // For other control characters, this produces printable characters that won't break console formatting
                    sb.Append((char)(0x2400 + c));
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

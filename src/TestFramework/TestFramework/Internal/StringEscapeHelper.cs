// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class StringEscapeHelper
{
    internal static void AppendEscapedString(
        StringBuilder builder,
        string value,
        int start,
        int end,
        bool escapeUnpairedSurrogates,
        bool useExtendedEscapes)
    {
        for (int i = start; i < end; i++)
        {
            AppendEscapedCharacter(
                builder,
                value[i],
                '"',
                escapeQuote: true,
                escapeBackslash: true,
                escapeUnpairedSurrogates && IsUnpairedSurrogate(value, i),
                useExtendedEscapes);
        }
    }

    internal static void AppendEscapedChar(StringBuilder builder, char value, bool useExtendedEscapes)
        => AppendEscapedCharacter(
            builder,
            value,
            '\'',
            escapeQuote: useExtendedEscapes,
            escapeBackslash: useExtendedEscapes,
            forceUnicodeEscape: useExtendedEscapes && char.IsSurrogate(value),
            useExtendedEscapes);

    internal static int GetEscapedCharacterLength(string value, int index)
    {
        char character = value[index];
        return char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
            ? 2
            : GetEscapedCharacterLength(character, char.IsSurrogate(character));
    }

    internal static bool IsEscapedString(string value, string rendered)
    {
        if (rendered.Length < 2 || rendered[0] != '"' || rendered[rendered.Length - 1] != '"')
        {
            return false;
        }

        int renderedIndex = 1;
        foreach (char character in value)
        {
            if (!TryMatchEscapedCharacter(rendered, ref renderedIndex, character))
            {
                return false;
            }
        }

        return renderedIndex == rendered.Length - 1;
    }

    private static int GetEscapedCharacterLength(char character, bool forceUnicodeEscape)
        => ClassifyEscape(
            character,
            '"',
            escapeQuote: true,
            escapeBackslash: true,
            forceUnicodeEscape,
            useExtendedEscapes: false,
            out _) switch
        {
            EscapeKind.None => 1,
            EscapeKind.Short => 2,
            EscapeKind.Unicode => 6,
            _ => throw new InvalidOperationException(),
        };

    private static void AppendEscapedCharacter(
        StringBuilder builder,
        char character,
        char quote,
        bool escapeQuote,
        bool escapeBackslash,
        bool forceUnicodeEscape,
        bool useExtendedEscapes)
    {
        EscapeKind escapeKind = ClassifyEscape(
            character,
            quote,
            escapeQuote,
            escapeBackslash,
            forceUnicodeEscape,
            useExtendedEscapes,
            out char escapedCharacter);
        if (escapeKind == EscapeKind.None)
        {
            builder.Append(character);
            return;
        }

        builder.Append('\\');
        if (escapeKind == EscapeKind.Short)
        {
            builder.Append(escapedCharacter);
            return;
        }

        builder.Append('u');
        builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
    }

    private static bool IsUnpairedSurrogate(string value, int index)
    {
        char character = value[index];
        return char.IsHighSurrogate(character)
            ? index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])
            : char.IsLowSurrogate(character)
                && (index == 0 || !char.IsHighSurrogate(value[index - 1]));
    }

    private static bool TryMatchEscapedCharacter(string rendered, ref int renderedIndex, char character)
    {
        EscapeKind escapeKind = ClassifyEscape(
            character,
            '"',
            escapeQuote: true,
            escapeBackslash: true,
            forceUnicodeEscape: false,
            useExtendedEscapes: false,
            out char escapedCharacter);
        return escapeKind switch
        {
            EscapeKind.None => TryMatch(rendered, ref renderedIndex, character),
            EscapeKind.Short => TryMatch(rendered, ref renderedIndex, '\\', escapedCharacter),
            EscapeKind.Unicode => TryMatchUnicodeEscape(rendered, ref renderedIndex, character),
            _ => throw new InvalidOperationException(),
        };
    }

    private static EscapeKind ClassifyEscape(
        char character,
        char quote,
        bool escapeQuote,
        bool escapeBackslash,
        bool forceUnicodeEscape,
        bool useExtendedEscapes,
        out char escapedCharacter)
    {
        escapedCharacter = character;
        if ((escapeQuote && character == quote) || (escapeBackslash && character == '\\'))
        {
            return EscapeKind.Short;
        }

        escapedCharacter = character switch
        {
            '\0' => '0',
            '\a' when useExtendedEscapes => 'a',
            '\b' when useExtendedEscapes => 'b',
            '\f' when useExtendedEscapes => 'f',
            '\n' => 'n',
            '\r' => 'r',
            '\t' => 't',
            '\v' when useExtendedEscapes => 'v',
            _ => '\0',
        };

        return escapedCharacter != '\0'
            ? EscapeKind.Short
            : (forceUnicodeEscape
                || char.IsControl(character)
                || (useExtendedEscapes && character is '\u2028' or '\u2029'))
                    ? EscapeKind.Unicode
                    : EscapeKind.None;
    }

    private static bool TryMatch(string rendered, ref int renderedIndex, char expected)
    {
        if (renderedIndex >= rendered.Length - 1 || rendered[renderedIndex] != expected)
        {
            return false;
        }

        renderedIndex++;
        return true;
    }

    private static bool TryMatch(string rendered, ref int renderedIndex, char first, char second)
    {
        if (renderedIndex + 2 > rendered.Length - 1
            || rendered[renderedIndex] != first
            || rendered[renderedIndex + 1] != second)
        {
            return false;
        }

        renderedIndex += 2;
        return true;
    }

    private static bool TryMatchUnicodeEscape(string rendered, ref int renderedIndex, char value)
    {
        if (renderedIndex + 6 > rendered.Length - 1
            || rendered[renderedIndex] != '\\'
            || rendered[renderedIndex + 1] != 'u'
            || rendered[renderedIndex + 2] != GetHexDigit(value >> 12)
            || rendered[renderedIndex + 3] != GetHexDigit(value >> 8)
            || rendered[renderedIndex + 4] != GetHexDigit(value >> 4)
            || rendered[renderedIndex + 5] != GetHexDigit(value))
        {
            return false;
        }

        renderedIndex += 6;
        return true;
    }

    private static char GetHexDigit(int value)
    {
        int nibble = value & 0xF;
        return (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
    }

    private enum EscapeKind
    {
        None,
        Short,
        Unicode,
    }
}

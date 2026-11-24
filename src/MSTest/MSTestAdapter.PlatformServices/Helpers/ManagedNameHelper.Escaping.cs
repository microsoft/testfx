// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

internal static partial class ManagedNameHelper
{
    private static void NormalizeAndAppendString(StringBuilder b, string name)
    {
        b.Append('\'');
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (NeedsEscaping(c, i))
            {
                if (c is '\\' or '\'')
                {
                    b.Append('\\');
                    b.Append(c);
                    continue;
                }
            }

            b.Append(c);
        }

        b.Append('\'');
    }

    private static bool IsNormalized(string s)
    {
        int brackets = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (NeedsEscaping(c, i) && c != '.')
            {
                if (i != 0)
                {
                    if (c == '<')
                    {
                        brackets++;
                        continue;
                    }

                    if (c == '>' && s[i - 1] != '<' && brackets > 0)
                    {
                        brackets--;
                        continue;
                    }
                }

                return false;
            }
        }

        return brackets == 0;
    }

    private static bool NeedsEscaping(char c, int pos)
    {
        if (pos == 0 && char.IsDigit(c))
        {
            return true;
        }

        if (c == '_'
            // 'Digit' does not include letter numbers, which are valid identifiers as per docs https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names'.
            || char.IsLetterOrDigit(c)) // Lu, Ll, Lt, Lm, Lo, or Nd
        {
            return false;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category
            is not UnicodeCategory.LetterNumber // Nl
            and not UnicodeCategory.NonSpacingMark // Mn
            and not UnicodeCategory.SpacingCombiningMark // Mc
            and not UnicodeCategory.ConnectorPunctuation // Pc
            and not UnicodeCategory.Format; // Cf
    }

    internal static string ParseEscapedString(string escapedString)
    {
        var stringBuilder = new StringBuilder();
        int end = 0;
        for (int i = 0; i < escapedString.Length; i++)
        {
            if (escapedString[i] == '\'')
            {
                stringBuilder.Append(escapedString, end, i - end);
                end = i = ParseEscapedStringSegment(escapedString, i + 1, stringBuilder);
            }
        }

        if (stringBuilder.Length == 0)
        {
            return escapedString;
        }

        if (end != 0 && end < escapedString.Length)
        {
            stringBuilder.Append(escapedString, end, escapedString.Length - end);
        }

        return stringBuilder.ToString();
    }

    // Unescapes a C# style escaped string.
    private static int ParseEscapedStringSegment(string escapedStringSegment, int pos, StringBuilder stringBuilder)
    {
        for (int i = pos; i < escapedStringSegment.Length; i++)
        {
            switch (escapedStringSegment[i])
            {
                case '\\':
                    if (escapedStringSegment[i + 1] == 'u')
                    {
                        char c;

                        try
                        {
                            string code = escapedStringSegment.Substring(i + 2, 4);
                            c = (char)Convert.ToInt32(code, 16);
                        }
                        catch
                        {
                            throw new InvalidManagedNameException();
                        }

                        stringBuilder.Append(c);
                        i += 5;
                    }
                    else
                    {
                        stringBuilder.Append(escapedStringSegment[++i]);
                    }

                    break;

                case '\'':
                    return i + 1;

                default:
                    stringBuilder.Append(escapedStringSegment[i]);
                    break;
            }
        }

        throw new InvalidManagedNameException();
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// Minimal JSON support for the string arrays that carry handler UIDs between dotnet test and the modules, usable on
/// every target framework without a JSON serializer.
/// </summary>
internal static class InvocationLifetimeJson
{
    public static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    /// <summary>
    /// Parses a JSON array of strings.
    /// </summary>
    /// <returns>The strings, or <see langword="null"/> when the value is not a JSON array of strings.</returns>
    public static List<string>? TryParseStringArray(string? json)
    {
        if (json is null)
        {
            return null;
        }

        int index = 0;
        SkipWhitespace(json, ref index);
        if (index >= json.Length || json[index] != '[')
        {
            return null;
        }

        index++;
        List<string> values = [];
        SkipWhitespace(json, ref index);
        if (index < json.Length && json[index] == ']')
        {
            index++;
            return IsAtEnd(json, index) ? values : null;
        }

        while (true)
        {
            SkipWhitespace(json, ref index);
            if (TryParseString(json, ref index) is not { } value)
            {
                return null;
            }

            values.Add(value);
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return null;
            }

            if (json[index] == ',')
            {
                index++;
                continue;
            }

            if (json[index] == ']')
            {
                index++;
                return IsAtEnd(json, index) ? values : null;
            }

            return null;
        }
    }

    private static string? TryParseString(string json, ref int index)
    {
        if (index >= json.Length || json[index] != '"')
        {
            return null;
        }

        index++;
        StringBuilder builder = new();
        while (index < json.Length)
        {
            char c = json[index++];
            if (c == '"')
            {
                return builder.ToString();
            }

            if (c != '\\')
            {
                if (c < ' ')
                {
                    return null;
                }

                builder.Append(c);
                continue;
            }

            if (index >= json.Length)
            {
                return null;
            }

            char escaped = json[index++];
            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    builder.Append(escaped);
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'u':
                    if (index + 4 > json.Length
                        || !int.TryParse(json.Substring(index, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    {
                        return null;
                    }

                    builder.Append((char)code);
                    index += 4;
                    break;
                default:
                    return null;
            }
        }

        return null;
    }

    private static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && json[index] is ' ' or '\t' or '\r' or '\n')
        {
            index++;
        }
    }

    private static bool IsAtEnd(string json, int index)
    {
        SkipWhitespace(json, ref index);
        return index == json.Length;
    }
}

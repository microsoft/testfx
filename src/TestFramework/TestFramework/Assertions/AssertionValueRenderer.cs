// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Renders values for display in structured assertion messages following the RFC 012 value rendering rules.
/// </summary>
internal static class AssertionValueRenderer
{
    /// <summary>
    /// Renders a value as a string suitable for display in the evidence block.
    /// </summary>
    internal static string RenderValue(object? value)
        => value switch
        {
            null => "null",
            string s => RenderString(s),
            bool b => b ? "true" : "false",
            char c => RenderChar(c),
            IEnumerable enumerable => RenderCollection(enumerable),
            _ => value.ToString() ?? value.GetType().FullName ?? value.GetType().Name,
        };

    /// <summary>
    /// Renders a string value with double quotes and escape sequences for control characters.
    /// </summary>
    private static string RenderString(string value)
    {
        StringBuilder sb = new(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\0':
                    sb.Append("\\0");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Renders a char value with single quotes and escape sequences.
    /// </summary>
    private static string RenderChar(char value) =>
        value switch
        {
            '\n' => "'\\n'",
            '\r' => "'\\r'",
            '\t' => "'\\t'",
            '\0' => "'\\0'",
            _ when char.IsControl(value) => $"'\\u{(int)value:X4}'",
            _ => $"'{value}'",
        };

    /// <summary>
    /// Renders a collection in JSON-style array notation.
    /// </summary>
    private static string RenderCollection(IEnumerable enumerable)
    {
        StringBuilder sb = new();
        sb.Append('[');
        bool first = true;
        foreach (object? item in enumerable)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            sb.Append(RenderValue(item));
            first = false;
        }

        sb.Append(']');
        return sb.ToString();
    }
}

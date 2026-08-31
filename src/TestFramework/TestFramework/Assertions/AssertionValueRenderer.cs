// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Renders values for display in structured assertion messages following the RFC 012 value rendering rules.
/// </summary>
[StackTraceHidden]
internal static class AssertionValueRenderer
{
    // Cache the built-in renderer as a delegate so passing it to the formatter registry doesn't allocate
    // on every RenderValue call.
    private static readonly Func<object?, string> BuiltInRenderer = RenderBuiltIn;

    /// <summary>
    /// Renders a value as a string suitable for display in the evidence block.
    /// </summary>
    /// <remarks>
    /// User-registered formatters (see <see cref="Assert.AddValueFormatter{T}(Func{T, string?})"/>) are consulted
    /// first for non-<see langword="null"/> values. <see langword="null"/> is always rendered as <c>"null"</c>
    /// and is not exposed to user formatters, to avoid surprising <see cref="NullReferenceException"/>s
    /// inside user code.
    /// </remarks>
    internal static string RenderValue(object? value)
        => value is null
            ? "null"
            : AssertionValueFormatterRegistry.HasFormatters
                ? AssertionValueFormatterRegistry.Render(value, BuiltInRenderer)
                : RenderBuiltIn(value);

    /// <summary>
    /// Determines whether a rendered string is exactly the representation produced by the built-in renderer.
    /// </summary>
    internal static bool IsBuiltInStringRendering(string value, string rendered)
    {
        if (rendered.Length < 2 || rendered[0] != '"' || rendered[rendered.Length - 1] != '"')
        {
            return false;
        }

        int renderedIndex = 1;
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', '"'))
                    {
                        return false;
                    }

                    break;
                case '\\':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', '\\'))
                    {
                        return false;
                    }

                    break;
                case '\n':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', 'n'))
                    {
                        return false;
                    }

                    break;
                case '\r':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', 'r'))
                    {
                        return false;
                    }

                    break;
                case '\t':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', 't'))
                    {
                        return false;
                    }

                    break;
                case '\0':
                    if (!TryMatch(rendered, ref renderedIndex, '\\', '0'))
                    {
                        return false;
                    }

                    break;
                default:
                    if (char.IsControl(c))
                    {
                        if (!TryMatchUnicodeEscape(rendered, ref renderedIndex, c))
                        {
                            return false;
                        }
                    }
                    else if (!TryMatch(rendered, ref renderedIndex, c))
                    {
                        return false;
                    }

                    break;
            }
        }

        return renderedIndex == rendered.Length - 1;
    }

    private static char GetHexDigit(int value)
    {
        int nibble = value & 0xF;
        return (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
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

    private static string RenderBuiltIn(object? value)
        => value switch
        {
            null => "null",
            string s => RenderString(s),
            bool b => b ? "true" : "false",
            char c => RenderChar(c),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan ts => ts.ToString("c", CultureInfo.InvariantCulture),
#if NET6_0_OR_GREATER
            DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("O", CultureInfo.InvariantCulture),
#endif
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            IEnumerable enumerable => RenderCollection(enumerable),
            _ => RenderObject(value),
        };

    private static string RenderObject(object value)
    {
        // Guard against user types whose ToString() throws so the original assertion failure is preserved.
        try
        {
            return value.ToString() ?? value.GetType().FullName ?? value.GetType().Name;
        }
        catch (Exception ex)
        {
            return $"{value.GetType().FullName ?? value.GetType().Name} (ToString threw {ex.GetType().Name})";
        }
    }

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

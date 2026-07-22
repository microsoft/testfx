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
        => string.Equals(rendered, RenderString(value), StringComparison.Ordinal);

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

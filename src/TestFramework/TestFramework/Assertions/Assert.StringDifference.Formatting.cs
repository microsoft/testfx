// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    private static int GetEscapedCharacterLength(string value, int index)
    {
        char c = value[index];
        return char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
            ? 2
            : char.IsSurrogate(c)
                ? 6
                : c switch
                {
                    '"' or '\\' or '\n' or '\r' or '\t' or '\0' => 2,
                    _ when char.IsControl(c) => 6,
                    _ => 1,
                };
    }

    private static void AppendEscapedToken(StringBuilder builder, string value, StringToken token)
    {
        for (int i = token.Start; i < token.End; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
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
                case '\0':
                    builder.Append("\\0");
                    break;
                default:
                    if (char.IsControl(c) || IsUnpairedSurrogate(value, i))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }
    }

    private static bool IsUnpairedSurrogate(string value, int index)
    {
        char c = value[index];
        return char.IsHighSurrogate(c)
            ? index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])
            : char.IsLowSurrogate(c)
                && (index == 0 || !char.IsHighSurrogate(value[index - 1]));
    }

    private static string? CreateCodePointDiagnostic(StringTokenWindow expected, StringTokenWindow actual)
        => NeedsCodePointDiagnostic(expected) || NeedsCodePointDiagnostic(actual)
            ? $"expected {FormatCodePoints(expected)}; actual {FormatCodePoints(actual)}"
            : null;

    private static bool NeedsCodePointDiagnostic(StringTokenWindow window)
        => window.Mismatch is not StringToken token
            || token.ScalarCount != 1
            || token.HasUnpairedSurrogate
            || GetScalar(window.Value, token.Start).Value > 0x7F;

    private static string FormatCodePoints(StringTokenWindow window)
    {
        if (window.Mismatch is not StringToken token)
        {
            return "<end>";
        }

        StringBuilder builder = new();
        int displayed = 0;
        int total = 0;
        for (int i = token.Start; i < token.End;)
        {
            ScalarInfo scalar = GetScalar(window.Value, i);
            if (displayed < MaximumCodePointsToDisplay)
            {
                if (displayed > 0)
                {
                    builder.Append(' ');
                }

                builder.Append("U+");
                builder.Append(scalar.Value.ToString("X4", CultureInfo.InvariantCulture));
                displayed++;
            }

            total++;
            i += scalar.Length;
        }

        if (total > displayed)
        {
            builder.Append(" ... (+");
            builder.Append(total - displayed);
            builder.Append(" code points)");
        }

        return builder.ToString();
    }
}

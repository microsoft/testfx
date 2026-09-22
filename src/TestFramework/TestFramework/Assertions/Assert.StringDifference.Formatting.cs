// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
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

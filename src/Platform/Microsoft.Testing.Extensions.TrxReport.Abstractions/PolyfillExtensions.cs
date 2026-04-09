// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET5_0_OR_GREATER

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class TrxReportAbstractionsPolyfillExtensions
{
    public static global::System.Text.StringBuilder AppendJoin(this global::System.Text.StringBuilder sb, string separator, global::System.Collections.Generic.IEnumerable<string> values)
    {
        bool first = true;
        foreach (string value in values)
        {
            if (!first)
            {
                sb.Append(separator);
            }

            sb.Append(value);
            first = false;
        }

        return sb;
    }
}

#endif

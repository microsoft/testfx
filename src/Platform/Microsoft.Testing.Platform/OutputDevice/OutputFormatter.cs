// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class OutputFormatter
{
    public static string FormatString(string? text)
    {
        if (text == null)
        {
            return "<null>";
        }

        if (text == string.Empty)
        {
            return "<empty>";
        }

#pragma warning disable IDE0046 // Convert to conditional expression
        if (RoslynString.IsNullOrWhiteSpace(text))
        {
            return text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
#pragma warning restore IDE0046 // Convert to conditional expression

        return text;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.Reporting;

internal static class AzDoEscaper
{
    public static string Escape(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case ';':
                    result.Append("%3B");
                    break;
                case '\r':
                    result.Append("%0D");
                    break;
                case '\n':
                    result.Append("%0A");
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        return result.ToString();
    }
}

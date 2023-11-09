// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class StringExtensions
{
#if NETSTANDARD
    public static bool StartsWith(this string s, char c)
        => s.StartsWith(c.ToString(), StringComparison.Ordinal);

    public static bool Contains(this string s, string value, StringComparison comparisonType)
        => s.IndexOf(value, comparisonType) >= 0;
#endif
}

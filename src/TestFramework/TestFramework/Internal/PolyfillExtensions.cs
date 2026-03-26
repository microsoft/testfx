// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Polyfill extension methods for the TestFramework project which targets
// netstandard2.0, net462, and does not have IVT from Microsoft.Testing.Platform.
#if !NET5_0_OR_GREATER

internal static class TestFrameworkPolyfillExtensions
{
    public static bool Contains(this string s, char c) => s.IndexOf(c) >= 0;

    public static bool Contains(this string s, string value, System.StringComparison comparisonType) =>
        s.IndexOf(value, comparisonType) >= 0;

    public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;

    public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;

    public static void Deconstruct<TKey, TValue>(this System.Collections.Generic.KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    public static bool TryAdd<TKey, TValue>(this System.Collections.Generic.Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
            return true;
        }

        return false;
    }
}

#endif

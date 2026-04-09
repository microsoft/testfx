// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Polyfill extension methods specific to the Analyzers project (which targets netstandard2.0
// only and does not reference Microsoft.Testing.Platform).

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class AnalyzerPolyfillExtensions
{
    public static bool IsAssignableTo(this System.Type type, System.Type? targetType)
        => targetType?.IsAssignableFrom(type) ?? false;

    public static void Deconstruct<TKey, TValue>(this System.Collections.Generic.KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    public static TValue GetValueOrDefault<TKey, TValue>(this System.Collections.Generic.IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
        => dictionary.TryGetValue(key, out TValue? value) ? value : defaultValue;

    public static TValue GetValueOrDefault<TKey, TValue>(this System.Collections.Immutable.IImmutableDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
        => dictionary.TryGetValue(key, out TValue? value) ? value : defaultValue;

    public static bool Contains(this string s, char c) => s.IndexOf(c) >= 0;

    public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;

    public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;
}

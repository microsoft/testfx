// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Polyfill extension methods for the Adapter projects which target
// net462 and need APIs not available on .NET Framework.

#if !NET5_0_OR_GREATER

internal static class AdapterPolyfillExtensions
{
    public static bool Contains(this string s, char c) => s.IndexOf(c) >= 0;

    public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;

    public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;

    public static bool IsAssignableTo(this System.Type type, System.Type? targetType)
        => targetType?.IsAssignableFrom(type) ?? false;

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

    public static System.Text.StringBuilder AppendJoin(this System.Text.StringBuilder sb, string separator, System.Collections.Generic.IEnumerable<string> values)
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

    public static System.Text.StringBuilder AppendJoin(this System.Text.StringBuilder sb, char separator, System.Collections.Generic.IEnumerable<string> values) =>
        sb.AppendJoin(separator.ToString(), values);

    public static TValue GetOrAdd<TKey, TValue, TArg>(this System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> dictionary, TKey key, System.Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        where TKey : notnull
        => dictionary.GetOrAdd(key, k => valueFactory(k, factoryArgument));

    public static string[] Split(this string s, char separator, System.StringSplitOptions options) =>
        s.Split(new[] { separator }, options);

    public static string Join(this string separator, char c, System.Collections.Generic.IEnumerable<string> values) =>
        string.Join(separator, values);
}

#endif

#if !NET8_0_OR_GREATER

internal static class AdapterCancellationTokenSourcePolyfill
{
    public static System.Threading.Tasks.Task CancelAsync(this System.Threading.CancellationTokenSource cts)
    {
        cts.Cancel();
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

#endif

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET5_0_OR_GREATER

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillKeyValuePairExtensions
{
    public static void Deconstruct<TKey, TValue>(this System.Collections.Generic.KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}

#endif

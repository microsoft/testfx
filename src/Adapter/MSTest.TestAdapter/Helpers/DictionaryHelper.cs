// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class DictionaryHelper
{
    public static IDictionary<TKey, TValue> ConcatWithOverwrites<TKey, TValue>(
        this IDictionary<TKey, TValue>? source,
        IDictionary<TKey, TValue>? overwrite,
        string sourceFriendlyName = "source",
        string overwriteFriendlyName = "overwrite")
        where TKey : IEquatable<TKey>
    {
        if ((source == null || source?.Count == 0) && (overwrite == null || overwrite?.Count == 0))
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: Both {0} and {1} dictionaries are null or empty, returning empty dictionary.", sourceFriendlyName, overwriteFriendlyName);
            return new Dictionary<TKey, TValue>();
        }

        if (overwrite == null || overwrite?.Count == 0)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: The {0} is null or empty, returning the {1} dictionary.", overwriteFriendlyName, sourceFriendlyName);
            return source!.ToDictionary(p => p.Key, p => p.Value);
        }

        if (source == null || source?.Count == 0)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: The {0} is null or empty, returning the {1} dictionary.", sourceFriendlyName, overwriteFriendlyName);
            return overwrite!.ToDictionary(p => p.Key, p => p.Value);
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: The {0} has {1} keys. And {2} has {3} keys. Merging them.", sourceFriendlyName, source!.Count, overwriteFriendlyName, overwrite!.Count);
        var destination = source.ToDictionary(p => p.Key, p => p.Value);
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: Taking all keys from {0}: {1}.", sourceFriendlyName, string.Join(", ", source.Keys));
        var overwrites = new List<TKey>();
        foreach (TKey k in overwrite.Keys)
        {
#pragma warning disable CA1854 // Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method | False-positive
            if (destination.ContainsKey(k))
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: The {0} already contains key {1}. Overwriting it with value from {2}.", sourceFriendlyName, k, overwriteFriendlyName);
                destination[k] = overwrite[k];
                overwrites.Add(k);
            }
            else
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: The {0} does not contain key {1}. Adding it from {2}.", sourceFriendlyName, k, overwriteFriendlyName);
                destination.Add(k, overwrite[k]);
            }
#pragma warning restore CA1854 // Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("DictionaryHelper.ConcatWithOverwrites: Merging done: Resulting dictionary has keys {0}, overwrites {1}.", string.Join(", ", destination.Keys), string.Join(", ", overwrites));

        return destination;
    }
}

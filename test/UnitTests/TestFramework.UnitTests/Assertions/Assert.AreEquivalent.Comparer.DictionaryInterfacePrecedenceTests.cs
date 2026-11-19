// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerDictionaryInterfacePrecedenceTests : TestContainer
{
    public AreEquivalentComparerDictionaryInterfacePrecedenceTests()
        => EquivalenceComparerCaches.Reset();

    protected override void Dispose(bool disposing)
    {
        try
        {
            EquivalenceComparerCaches.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void AreEquivalent_DictionaryInterfacePrecedence_UsesMutableDictionaryLookup()
    {
        IDictionary<string, object?> expected = new Dictionary<string, object?> { ["key"] = "mutable" };
        DivergentDictionaryViews actual = new();

        Assert.AreEquivalent<object>(expected, actual);
    }

    private sealed class DivergentDictionaryViews : IReadOnlyDictionary<string, string>, IDictionary<string, object?>
    {
        object? IDictionary<string, object?>.this[string key]
        {
            get => "mutable";
            set => throw new NotSupportedException();
        }

        string IReadOnlyDictionary<string, string>.this[string key]
            => "read-only";

        ICollection<string> IDictionary<string, object?>.Keys
            => ["key"];

        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys
            => ["key"];

        ICollection<object?> IDictionary<string, object?>.Values
            => ["mutable"];

        IEnumerable<string> IReadOnlyDictionary<string, string>.Values
            => ["read-only"];

        int ICollection<KeyValuePair<string, object?>>.Count
            => 1;

        int IReadOnlyCollection<KeyValuePair<string, string>>.Count
            => 1;

        bool ICollection<KeyValuePair<string, object?>>.IsReadOnly
            => true;

        void IDictionary<string, object?>.Add(string key, object? value)
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item)
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, object?>>.Clear()
            => throw new NotSupportedException();

        bool IDictionary<string, object?>.ContainsKey(string key)
            => key == "key";

        bool IReadOnlyDictionary<string, string>.ContainsKey(string key)
            => key == "key";

        bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item)
            => item.Key == "key" && Equals(item.Value, "mutable");

        void ICollection<KeyValuePair<string, object?>>.CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
            => array[arrayIndex] = new KeyValuePair<string, object?>("key", "mutable");

        IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
            => ((IEnumerable<KeyValuePair<string, object?>>)new[] { new KeyValuePair<string, object?>("key", "mutable") }).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => new[] { new KeyValuePair<string, object?>("key", "mutable") }.GetEnumerator();

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
            => ((IEnumerable<KeyValuePair<string, string>>)new[] { new KeyValuePair<string, string>("key", "read-only") }).GetEnumerator();

        bool IDictionary<string, object?>.Remove(string key)
            => throw new NotSupportedException();

        bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item)
            => throw new NotSupportedException();

        bool IDictionary<string, object?>.TryGetValue(string key, out object? value)
        {
            value = "mutable";
            return key == "key";
        }

        bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value)
        {
            value = "read-only";
            return key == "key";
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerDictionaryViewTests : TestContainer
{
    public void AreEquivalent_ReadOnlyDictionaryStrictModeReportsAdditionalKey()
    {
        ReadOnlyDictionaryOnly expected = new(new Dictionary<string, int> { ["expected"] = 1 });
        ReadOnlyDictionaryOnly actual = new(new Dictionary<string, int> { ["expected"] = 1, ["extra"] = 2 });
        Action compare = () => Assert.AreEquivalent<object>(expected, actual, strict: true);

        compare.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Should().Contain("key \"extra\" present on actual is not on expected");
    }

    public void AreEquivalent_ReadOnlyDictionarySkipsNullFromNonGenericEnumerator()
    {
        ReadOnlyDictionaryOnly expected = new(new Dictionary<string, int> { ["expected"] = 1 });
        ReadOnlyDictionaryOnly actual = new([]);
        Action compare = () => Assert.AreEquivalent<object>(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Should().Contain("key \"expected\" present on expected is missing from actual");
    }

    public void AreEquivalent_NonGenericDictionaryDisposesEnumerators()
    {
        TrackingHashtable expected = new() { ["key"] = 1 };
        TrackingHashtable actual = new() { ["key"] = 1 };

        Assert.AreEquivalent(expected, actual, strict: true);

        expected.DisposeCount.Should().Be(1);
        actual.DisposeCount.Should().Be(1);
    }

    private sealed class ReadOnlyDictionaryOnly(Dictionary<string, int> entries) : IReadOnlyDictionary<string, int>
    {
        public int this[string key] => entries[key];

        public IEnumerable<string> Keys => entries.Keys;

        public IEnumerable<int> Values => entries.Values;

        public int Count => entries.Count;

        public bool ContainsKey(string key) => entries.ContainsKey(key);

        public bool TryGetValue(string key, out int value) => entries.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => EnumerateWithNull().GetEnumerator();

        private IEnumerable<object?> EnumerateWithNull()
        {
            yield return null;
            foreach (KeyValuePair<string, int> entry in entries)
            {
                yield return entry;
            }
        }
    }

    private sealed class TrackingHashtable : Hashtable
    {
        public int DisposeCount { get; private set; }

        public override IDictionaryEnumerator GetEnumerator()
            => new TrackingDictionaryEnumerator(base.GetEnumerator(), this);

        private sealed class TrackingDictionaryEnumerator(IDictionaryEnumerator enumerator, TrackingHashtable owner) : IDictionaryEnumerator, IDisposable
        {
            public DictionaryEntry Entry => enumerator.Entry;

            public object Key => enumerator.Key;

            public object? Value => enumerator.Value;

            public object Current => enumerator.Current;

            public bool MoveNext() => enumerator.MoveNext();

            public void Reset() => enumerator.Reset();

            public void Dispose() => owner.DisposeCount++;
        }
    }
}

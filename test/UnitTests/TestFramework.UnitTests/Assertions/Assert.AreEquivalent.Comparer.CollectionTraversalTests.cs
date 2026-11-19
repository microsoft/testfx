// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerCollectionTraversalTests : TestContainer
{
    public AreEquivalentComparerCollectionTraversalTests()
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

    public void AreEquivalent_DeepEnumerableNesting_EnforcesMaximumDepth()
    {
        object expected = "expected";
        object actual = "actual";
        for (int i = 0; i < 256; i++)
        {
            expected = new object[] { expected };
            actual = new object[] { actual };
        }

        Action compare = () => Assert.AreEquivalent<object>(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*maximum supported depth of 256*");
    }

    public void AreEquivalent_EnumerableDisposesBothEnumeratorsAfterComparison()
    {
        TrackingEnumerable expected = new(1, 2);
        TrackingEnumerable actual = new(1, 2);

        Assert.AreEquivalent(expected, actual);

        expected.DisposeCount.Should().Be(1);
        actual.DisposeCount.Should().Be(1);
    }

    public void AreEquivalent_EnumerableCountFailureIsNotReplacedByLengthMismatch()
    {
        IEnumerable expected = Array.Empty<int>();
        OneShotCurrentFailureEnumerable actual = new();
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("enumerating the actual collection threw InvalidOperationException: one-shot current failure");
        failure.Message.Should().NotContain("collections differ in length");
    }

    public void AreEquivalent_EnumerableCountFailureAfterFirstExtraItemIsNotReplacedByLengthMismatch()
    {
        IEnumerable expected = Array.Empty<int>();
        CurrentFailureAfterFirstEnumerable actual = new();
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("enumerating the actual collection threw InvalidOperationException: current failure after first item");
        failure.Message.Should().NotContain("collections differ in length");
    }

    public void AreEquivalent_StrictDictionary_ReportsUnexpectedActualKey()
    {
        IDictionary expected = new Hashtable { ["present"] = 1 };
        IDictionary actual = new Hashtable
        {
            ["present"] = 1,
            ["extra"] = 2,
        };
        Action compare = () => Assert.AreEquivalent(expected, actual, strict: true);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at '[\"extra\"]'");
        failure.Message.Should().Contain("key \"extra\" present on actual is not on expected");
    }

    public void AreEquivalent_StrictDictionaryLookupFailure_IdentifiesExpectedSide()
    {
        IDictionary expected = new ContainsFailingHashtable(new InvalidOperationException("expected contains failure"));
        IDictionary actual = new Hashtable { ["extra"] = 2 };
        Action compare = () => Assert.AreEquivalent(expected, actual, strict: true);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("reading the expected dictionary threw InvalidOperationException: expected contains failure");
    }

    public void AreEquivalent_GenericDictionaryInterfaceTakesPrecedenceOverReadOnlyInterface()
        => Assert.AreEquivalent(new SplitSurfaceDictionary(), new SplitSurfaceDictionary());

    public void AreEquivalent_DictionaryUsesFirstGenericDictionaryInterface()
        => Assert.AreEquivalent<object>(new MultipleGenericDictionary(), new MultipleGenericDictionary());

    public void AreEquivalent_StrictDictionary_FatalLookupExceptionIsNotWrapped()
    {
        OutOfMemoryException fatal = new("contains sentinel");
        IDictionary expected = new ContainsFailingHashtable(fatal);
        IDictionary actual = new Hashtable { ["extra"] = 2 };
        Action compare = () => Assert.AreEquivalent(expected, actual, strict: true);

        compare.Should().ThrowExactly<OutOfMemoryException>().Which.Should().BeSameAs(fatal);
    }

    public void AreEquivalent_DeepDictionaryNesting_EnforcesMaximumDepth()
    {
        object expected = "expected";
        object actual = "actual";
        for (int i = 0; i < 256; i++)
        {
            expected = new Hashtable { ["next"] = expected };
            actual = new Hashtable { ["next"] = actual };
        }

        Action compare = () => Assert.AreEquivalent<object>(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*maximum supported depth of 256*");
    }

    private sealed class TrackingEnumerable(params int[] values) : IEnumerable
    {
        private readonly TrackingState _state = new();

        public int DisposeCount => _state.DisposeCount;

        public IEnumerator GetEnumerator()
            => new TrackingEnumerator(values, _state);
    }

    private sealed class TrackingEnumerator(int[] values, TrackingState state) : IEnumerator, IDisposable
    {
        private int _index = -1;

        public object Current => values[_index];

        public bool MoveNext()
            => ++_index < values.Length;

        public void Reset()
            => _index = -1;

        public void Dispose()
            => state.DisposeCount++;
    }

    private sealed class TrackingState
    {
        public int DisposeCount { get; set; }
    }

    private sealed class OneShotCurrentFailureEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator()
            => new OneShotCurrentFailureEnumerator();
    }

    private sealed class OneShotCurrentFailureEnumerator : IEnumerator
    {
        private bool _currentRead;
        private bool _moved;

        public object Current
        {
            get
            {
                if (!_currentRead)
                {
                    _currentRead = true;
                    throw new InvalidOperationException("one-shot current failure");
                }

                return 1;
            }
        }

        public bool MoveNext()
        {
            if (_moved)
            {
                return false;
            }

            _moved = true;
            return true;
        }

        public void Reset()
        {
            _moved = false;
            _currentRead = false;
        }
    }

    private sealed class CurrentFailureAfterFirstEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator()
            => new CurrentFailureAfterFirstEnumerator();
    }

    private sealed class CurrentFailureAfterFirstEnumerator : IEnumerator
    {
        private int _index = -1;

        public object Current
            => _index == 1
                ? throw new InvalidOperationException("current failure after first item")
                : 1;

        public bool MoveNext()
            => ++_index < 2;

        public void Reset()
            => _index = -1;
    }

    private sealed class SplitSurfaceDictionary : IDictionary<string, int>, IReadOnlyDictionary<string, int>
    {
        private static readonly KeyValuePair<string, int>[] Items = [new("key", 42)];

        public int this[string key]
        {
            get => key == "key" ? 42 : throw new KeyNotFoundException();
            set => throw new NotSupportedException();
        }

        public ICollection<string> Keys => ["key"];

        IEnumerable<string> IReadOnlyDictionary<string, int>.Keys => ["key"];

        public ICollection<int> Values => [42];

        IEnumerable<int> IReadOnlyDictionary<string, int>.Values => [42];

        public int Count => 1;

        public bool IsReadOnly => true;

        public void Add(string key, int value)
            => throw new NotSupportedException();

        public void Add(KeyValuePair<string, int> item)
            => throw new NotSupportedException();

        public void Clear()
            => throw new NotSupportedException();

        public bool Contains(KeyValuePair<string, int> item)
            => Items.Contains(item);

        public bool ContainsKey(string key)
            => ((IDictionary<string, int>)this).ContainsKey(key);

        bool IDictionary<string, int>.ContainsKey(string key)
            => key == "key";

        bool IReadOnlyDictionary<string, int>.ContainsKey(string key)
            => false;

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
            => Items.CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
            => ((IEnumerable<KeyValuePair<string, int>>)Items).GetEnumerator();

        public bool Remove(string key)
            => throw new NotSupportedException();

        public bool Remove(KeyValuePair<string, int> item)
            => throw new NotSupportedException();

        public bool TryGetValue(string key, out int value)
            => ((IDictionary<string, int>)this).TryGetValue(key, out value);

        bool IDictionary<string, int>.TryGetValue(string key, out int value)
        {
            value = 42;
            return key == "key";
        }

        bool IReadOnlyDictionary<string, int>.TryGetValue(string key, out int value)
        {
            value = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class MultipleGenericDictionary : IDictionary<string, int>, IDictionary<int, int>
    {
        private static readonly Type FirstDictionaryInterface = typeof(MultipleGenericDictionary)
            .GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        private static readonly bool StringInterfaceIsFirst = typeof(IDictionary<string, int>) == FirstDictionaryInterface;

        private static readonly object FirstEntry = CreateFirstEntry();

        public int Count => 1;

        public bool IsReadOnly => true;

        ICollection<string> IDictionary<string, int>.Keys => ["key"];

        ICollection<int> IDictionary<int, int>.Keys => [42];

        ICollection<int> IDictionary<string, int>.Values => [42];

        ICollection<int> IDictionary<int, int>.Values => [42];

        int IDictionary<string, int>.this[string key]
        {
            get => key == "key" ? 42 : throw new KeyNotFoundException();
            set => throw new NotSupportedException();
        }

        int IDictionary<int, int>.this[int key]
        {
            get => key == 42 ? 42 : throw new KeyNotFoundException();
            set => throw new NotSupportedException();
        }

        void IDictionary<string, int>.Add(string key, int value)
            => throw new NotSupportedException();

        void IDictionary<int, int>.Add(int key, int value)
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, int>>.Add(KeyValuePair<string, int> item)
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<int, int>>.Add(KeyValuePair<int, int> item)
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, int>>.Clear()
            => throw new NotSupportedException();

        void ICollection<KeyValuePair<int, int>>.Clear()
            => throw new NotSupportedException();

        bool ICollection<KeyValuePair<string, int>>.Contains(KeyValuePair<string, int> item)
            => item.Key == "key" && item.Value == 42;

        bool ICollection<KeyValuePair<int, int>>.Contains(KeyValuePair<int, int> item)
            => item.Key == 42 && item.Value == 42;

        bool IDictionary<string, int>.ContainsKey(string key)
            => StringInterfaceIsFirst && key == "key";

        bool IDictionary<int, int>.ContainsKey(int key)
            => !StringInterfaceIsFirst && key == 42;

        void ICollection<KeyValuePair<string, int>>.CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
            => array[arrayIndex] = new("key", 42);

        void ICollection<KeyValuePair<int, int>>.CopyTo(KeyValuePair<int, int>[] array, int arrayIndex)
            => array[arrayIndex] = new(42, 42);

        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator()
            => ((IEnumerable<KeyValuePair<string, int>>)[new("key", 42)]).GetEnumerator();

        IEnumerator<KeyValuePair<int, int>> IEnumerable<KeyValuePair<int, int>>.GetEnumerator()
            => ((IEnumerable<KeyValuePair<int, int>>)[new(42, 42)]).GetEnumerator();

        bool IDictionary<string, int>.Remove(string key)
            => throw new NotSupportedException();

        bool IDictionary<int, int>.Remove(int key)
            => throw new NotSupportedException();

        bool ICollection<KeyValuePair<string, int>>.Remove(KeyValuePair<string, int> item)
            => throw new NotSupportedException();

        bool ICollection<KeyValuePair<int, int>>.Remove(KeyValuePair<int, int> item)
            => throw new NotSupportedException();

        bool IDictionary<string, int>.TryGetValue(string key, out int value)
        {
            value = 42;
            return StringInterfaceIsFirst && key == "key";
        }

        bool IDictionary<int, int>.TryGetValue(int key, out int value)
        {
            value = 42;
            return !StringInterfaceIsFirst && key == 42;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => new[] { FirstEntry }.GetEnumerator();

        private static object CreateFirstEntry()
        {
            Type[] typeArguments = FirstDictionaryInterface.GetGenericArguments();
            object key = typeof(string) == typeArguments[0] ? "key" : 42;
            Type keyValuePairType = typeof(KeyValuePair<,>).MakeGenericType(typeArguments);
            return Activator.CreateInstance(keyValuePairType, key, 42)!;
        }
    }

    private sealed class ContainsFailingHashtable(Exception error) : Hashtable
    {
        public override bool Contains(object key)
            => throw error;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed class TestContextDictionary : IDictionary<string, object?>
{
    private ITestMethod? _testMethod;

    private IDictionary<string, object?> _currentDictionary;
    private bool _isOriginalDictionary;

    public TestContextDictionary(IDictionary<string, object?> originalDictionary, ITestMethod? testMethod)
    {
        // IMPORTANT: TestContextDictionary shouldn't mutate the original dictionary.
        // We keep a flag to track if we are using the original dictionary or a copy.
        // The idea here is to avoid always creating a copy dictionary if users don't end up mutating the dictionary (common scenario).
        _currentDictionary = originalDictionary;
        _isOriginalDictionary = true;
        _testMethod = testMethod;
    }

    public object? this[string key]
    {
        get
        {
            if (key == TestContext.FullyQualifiedTestClassNameLabel)
            {
                return _testMethod?.FullClassName ?? throw new KeyNotFoundException();
            }
            else if (key == TestContext.ManagedTypeLabel)
            {
                return _testMethod?.ManagedTypeName ?? throw new KeyNotFoundException();
            }
            else if (key == TestContext.ManagedMethodLabel)
            {
                return _testMethod?.ManagedMethodName ?? throw new KeyNotFoundException();
            }
            else if (key == TestContext.TestNameLabel)
            {
                return _testMethod?.Name ?? throw new KeyNotFoundException();
            }

            return _currentDictionary[key];
        }

        set
        {
            ThrowIfKnownKey(key);
            CloneDictionaryIfNeeded();

            _currentDictionary[key] = value;
        }
    }

    private sealed class TestContextDictionaryKeyCollection : ICollection<string>
    {
        private readonly TestContextDictionary _testContextDictionary;

        public TestContextDictionaryKeyCollection(TestContextDictionary testContextDictionary)
            => _testContextDictionary = testContextDictionary;

        public int Count => _testContextDictionary.Count;

        public bool IsReadOnly => true;

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(string item) => _testContextDictionary.ContainsKey(item);

        public void CopyTo(string[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (array.Length - arrayIndex < _testContextDictionary.Count)
            {
                throw new ArgumentException();
            }

            // TODO:
        }

        public IEnumerator<string> GetEnumerator() => throw new NotImplementedException();

        public bool Remove(string item) => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public ICollection<string> Keys => new TestContextDictionaryKeyCollection(this);

    public ICollection<object?> Values => throw new NotImplementedException();

    public int Count => _currentDictionary.Count +
        (_testMethod?.FullClassName is null ? 0 : 1) +
        (_testMethod?.ManagedTypeName is null ? 0 : 1) +
        (_testMethod?.ManagedMethodName is null ? 0 : 1) +
        (_testMethod?.Name is null ? 0 : 1);

    public bool IsReadOnly => _currentDictionary.IsReadOnly;

    public void Add(string key, object? value)
    {
        ThrowIfKnownKey(key);
        CloneDictionaryIfNeeded();

        _currentDictionary.Add(key, value);
    }

    public void Add(KeyValuePair<string, object?> item)
        => Add(item.Key, item.Value);

    public void Clear()
    {
        _testMethod = null;
        if (_isOriginalDictionary)
        {
            _currentDictionary = new Dictionary<string, object?>();
            _isOriginalDictionary = false;
        }
        else
        {
            _currentDictionary.Clear();
        }
    }

    public bool Contains(KeyValuePair<string, object?> item)
        => _currentDictionary.TryGetValue(item.Key, out object? value) && EqualityComparer<object?>.Default.Equals(value, item.Value);

    public bool ContainsKey(string key)
    {
        if (key == TestContext.FullyQualifiedTestClassNameLabel)
        {
            return _testMethod?.FullClassName is not null;
        }
        else if (key == TestContext.ManagedTypeLabel)
        {
            return _testMethod?.ManagedTypeName is not null;
        }
        else if (key == TestContext.ManagedMethodLabel)
        {
            return _testMethod?.ManagedMethodName is not null;
        }
        else if (key == TestContext.TestNameLabel)
        {
            return _testMethod?.Name is not null;
        }

        return _currentDictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => throw new NotImplementedException();

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => throw new NotImplementedException();

    public bool Remove(string key)
    {
        ThrowIfKnownKey(key);
        CloneDictionaryIfNeeded();
        return _currentDictionary.Remove(key);
    }

    public bool Remove(KeyValuePair<string, object?> item) => throw new NotImplementedException();

    public bool TryGetValue(string key, out object? value)
    {
        if (key == TestContext.FullyQualifiedTestClassNameLabel)
        {
            value = _testMethod?.FullClassName;
            return value is not null;
        }
        else if (key == TestContext.ManagedTypeLabel)
        {
            value = _testMethod?.ManagedTypeName;
            return value is not null;
        }
        else if (key == TestContext.ManagedMethodLabel)
        {
            value = _testMethod?.ManagedMethodName;
            return value is not null;
        }
        else if (key == TestContext.TestNameLabel)
        {
            value = _testMethod?.Name;
            return value is not null;
        }

        return _currentDictionary.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ThrowIfKnownKey(string key)
    {
        if (key == TestContext.FullyQualifiedTestClassNameLabel ||
                key == TestContext.ManagedTypeLabel ||
                key == TestContext.ManagedMethodLabel ||
                key == TestContext.TestNameLabel)
        {
            throw new InvalidOperationException();
        }
    }

    private void CloneDictionaryIfNeeded()
    {
        if (_isOriginalDictionary)
        {
            _currentDictionary = new Dictionary<string, object?>(_currentDictionary);
            _isOriginalDictionary = false;
        }
    }
}

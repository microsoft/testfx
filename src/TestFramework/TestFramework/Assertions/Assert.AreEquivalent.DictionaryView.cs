// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Abstraction over both <see cref="IDictionary"/> and generic
    /// <see cref="IDictionary{TKey, TValue}"/> / <see cref="IReadOnlyDictionary{TKey, TValue}"/> instances.
    /// Routes lookups back to the source dictionary so the source's own
    /// <see cref="IEqualityComparer{T}"/> for keys is preserved.
    /// </summary>
    private abstract class DictionaryView
    {
        internal abstract IEnumerable<KeyValuePair<object, object?>> Entries { get; }

        internal abstract bool TryGetValue(object key, out object? value);

        internal abstract bool ContainsKey(object key);

        /// <summary>
        /// Variant of <see cref="TryGetValue(object, out object?)"/> for use from contexts that can't
        /// capture an <c>out</c> parameter (e.g., lambdas).
        /// </summary>
        internal DictionaryLookup TryGetValuePair(object key)
        {
            bool found = TryGetValue(key, out object? value);
            return new DictionaryLookup(found, value);
        }
    }

    private readonly struct DictionaryLookup
    {
        internal DictionaryLookup(bool found, object? value)
        {
            Found = found;
            Value = value;
        }

        internal bool Found { get; }

        internal object? Value { get; }
    }

    private sealed class NonGenericDictionaryView : DictionaryView
    {
        private readonly IDictionary _source;

        internal NonGenericDictionaryView(IDictionary source) => _source = source;

        internal override IEnumerable<KeyValuePair<object, object?>> Entries
        {
            get
            {
                IDictionaryEnumerator e = _source.GetEnumerator();
                try
                {
                    while (e.MoveNext())
                    {
                        DictionaryEntry entry = e.Entry;
                        yield return new KeyValuePair<object, object?>(entry.Key, entry.Value);
                    }
                }
                finally
                {
                    (e as IDisposable)?.Dispose();
                }
            }
        }

        internal override bool ContainsKey(object key) => _source.Contains(key);

        internal override bool TryGetValue(object key, out object? value)
        {
            if (!_source.Contains(key))
            {
                value = null;
                return false;
            }

            value = _source[key];
            return true;
        }
    }

    private sealed class GenericDictionaryView : DictionaryView
    {
        private static readonly ConcurrentDictionary<Type, GenericDictionaryAccessors> AccessorCache = new();

        private readonly object _source;
        private readonly GenericDictionaryAccessors _accessors;

        private GenericDictionaryView(object source, GenericDictionaryAccessors accessors)
        {
            _source = source;
            _accessors = accessors;
        }

        internal static GenericDictionaryView Create(object source, Type dictionaryInterface)
            => new(source, AccessorCache.GetOrAdd(dictionaryInterface, GenericDictionaryAccessors.Build));

        internal override IEnumerable<KeyValuePair<object, object?>> Entries
            => _accessors.Enumerate(_source);

        internal override bool ContainsKey(object key)
            => _accessors.KeyType.IsInstanceOfType(key) && _accessors.ContainsKey(_source, key);

        internal override bool TryGetValue(object key, out object? value)
        {
            if (!_accessors.KeyType.IsInstanceOfType(key))
            {
                value = null;
                return false;
            }

            return _accessors.TryGetValue(_source, key, out value);
        }
    }

    /// <summary>
    /// Per-generic-dictionary-interface set of cached reflection accessors. Routes lookups to the
    /// source's native methods (<c>TryGetValue</c>, <c>ContainsKey</c>) so the source's
    /// <see cref="IEqualityComparer{TKey}"/> is honored.
    /// </summary>
    private sealed class GenericDictionaryAccessors
    {
        private readonly MethodInfo _tryGetValueMethod;
        private readonly MethodInfo _containsKeyMethod;
        private readonly PropertyInfo _kvpKey;
        private readonly PropertyInfo _kvpValue;

        private GenericDictionaryAccessors(Type keyType, MethodInfo tryGetValueMethod, MethodInfo containsKeyMethod, PropertyInfo kvpKey, PropertyInfo kvpValue)
        {
            KeyType = keyType;
            _tryGetValueMethod = tryGetValueMethod;
            _containsKeyMethod = containsKeyMethod;
            _kvpKey = kvpKey;
            _kvpValue = kvpValue;
        }

        internal Type KeyType { get; }

        internal static GenericDictionaryAccessors Build(Type dictionaryInterface)
        {
            Type[] args = dictionaryInterface.GetGenericArguments();
            Type keyType = args[0];
            Type valueType = args[1];

            MethodInfo tryGet = dictionaryInterface.GetMethod("TryGetValue")!;
            MethodInfo contains = dictionaryInterface.GetMethod("ContainsKey")!;

            Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
            PropertyInfo keyProp = kvpType.GetProperty("Key")!;
            PropertyInfo valueProp = kvpType.GetProperty("Value")!;

            return new GenericDictionaryAccessors(keyType, tryGet, contains, keyProp, valueProp);
        }

        internal IEnumerable<KeyValuePair<object, object?>> Enumerate(object source)
        {
            foreach (object? item in (IEnumerable)source)
            {
                // KeyValuePair<,> is a value type, so `item` cannot be null when iterated as object;
                // we still dereference defensively in case a custom IEnumerable yields a boxed null.
                if (item is null)
                {
                    continue;
                }

                object key = _kvpKey.GetValue(item)!;
                object? val = _kvpValue.GetValue(item);
                yield return new KeyValuePair<object, object?>(key, val);
            }
        }

        internal bool ContainsKey(object source, object key)
            => (bool)_containsKeyMethod.Invoke(source, [key])!;

        internal bool TryGetValue(object source, object key, out object? value)
        {
            object?[] args = [key, null];
            bool found = (bool)_tryGetValueMethod.Invoke(source, args)!;
            value = args[1];
            return found;
        }
    }
}

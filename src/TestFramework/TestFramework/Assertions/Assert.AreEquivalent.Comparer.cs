// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Walks two object graphs and reports the first structural difference, if any.
    /// </summary>
    private sealed partial class EquivalenceComparer
    {
        // Member info caches keyed by runtime type.
        private const int MaxComparisonDepth = 256;
        private static readonly ConcurrentDictionary<Type, MemberLookup> MemberCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo?> IEquatableEqualsCache = new();
        private static readonly ConcurrentDictionary<Type, bool> IsPrimitiveLikeCache = new();
        private static readonly ConcurrentDictionary<Type, Type?> EnumerableElementTypeCache = new();
        private static readonly ConcurrentDictionary<Type, Type?> DictionaryValueTypeCache = new();

        private readonly bool _strict;

        // Topology maps: once we've paired expected `e` with actual `a`, both maps remember the pairing.
        // Re-encountering the same pair short-circuits as a match (cycle handled). Re-encountering one
        // side paired with a different counterpart on the other side is a topology mismatch.
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
        private readonly Dictionary<object, object> _expectedToActual = new(ReferenceObjectComparer.Instance);
        private readonly Dictionary<object, object> _actualToExpected = new(ReferenceObjectComparer.Instance);
#pragma warning restore IDE0028

        internal EquivalenceComparer(bool strict)
            => _strict = strict;

        internal EquivalenceMismatch? Compare<T>(T? expected, T? actual)
            => Compare(expected, actual, typeof(T), path: string.Empty, depth: 0);

        private EquivalenceMismatch? Compare(object? expected, object? actual, Type declaredType, string path, int depth)
        {
            if (ReferenceEquals(expected, actual))
            {
                return null;
            }

            if (expected is null || actual is null)
            {
                return EquivalenceMismatch.NullMismatch(path, expected, actual);
            }

            if (depth >= MaxComparisonDepth)
            {
                return EquivalenceMismatch.MaxDepthExceeded(path, MaxComparisonDepth);
            }

            Type expectedRuntimeType = expected.GetType();
            Type actualRuntimeType = actual.GetType();

            if (expected is Type || actual is Type)
            {
                return expected is Type && actual is Type
                    ? Equals(expected, actual)
                        ? null
                        : EquivalenceMismatch.ValueMismatch(path, expected, actual)
                    : EquivalenceMismatch.TypeMismatch(path, expectedRuntimeType, actualRuntimeType);
            }

            // Primitive-ish types: trust Equals, but report runtime-type mismatches explicitly when
            // boxed through a wider declared type such as object.
            if (IsPrimitiveLike(expectedRuntimeType) || IsPrimitiveLike(actualRuntimeType))
            {
                return expectedRuntimeType == actualRuntimeType
                    ? Equals(expected, actual)
                        ? null
                        : EquivalenceMismatch.ValueMismatch(path, expected, actual)
                    : EquivalenceMismatch.TypeMismatch(path, expectedRuntimeType, actualRuntimeType);
            }

            // IEquatable<T> shortcut on the declared (compile-time) type, when it is more specific
            // than `object`. We deliberately ignore plain object.Equals overrides and only honor
            // the explicit IEquatable<T> contract.
            if (declaredType != typeof(object))
            {
                switch (InvokeIEquatable(expected, actual, declaredType, out Exception? thrownByUser))
                {
                    case IEquatableOutcome.Equal:
                        return null;
                    case IEquatableOutcome.NotEqual:
                        return EquivalenceMismatch.ValueMismatch(path, expected, actual);
                    case IEquatableOutcome.Threw:
                        return EquivalenceMismatch.IEquatableThrew(path, thrownByUser!);
                }
            }

            // Topology check + cycle guard: we record the pair persistently so that:
            //   * if we re-enter the same pair (via a cycle), we treat it as a match;
            //   * if we re-enter with a different counterpart on either side, that's a topology
            //     mismatch (the two graphs do not have the same shape of references).
            // Skip value types: they cannot form reference cycles, and boxing them into the topology
            // maps would both miss subsequent visits (each box is a fresh reference) and grow the
            // dictionaries unnecessarily.
            // These mappings are intentionally not unwound when a deeper comparison later fails.
            // Comparison is fail-fast, so a non-null mismatch immediately aborts traversal and the
            // comparer instance is never reused to continue from a sibling branch.
            if (!expectedRuntimeType.IsValueType && !actualRuntimeType.IsValueType)
            {
                if (_expectedToActual.TryGetValue(expected, out object? mappedActual))
                {
                    return ReferenceEquals(mappedActual, actual)
                        ? null
                        : EquivalenceMismatch.TopologyMismatch(path);
                }

                if (_actualToExpected.TryGetValue(actual, out object? mappedExpected))
                {
                    return ReferenceEquals(mappedExpected, expected)
                        ? null
                        : EquivalenceMismatch.TopologyMismatch(path);
                }

                _expectedToActual[expected] = actual;
                _actualToExpected[actual] = expected;
            }

            bool expectedIsDictionary = TryCreateDictionaryView(expected, out DictionaryView? expectedDict);
            bool actualIsDictionary = TryCreateDictionaryView(actual, out DictionaryView? actualDict);

            if (expectedIsDictionary && actualIsDictionary)
            {
                Type valueDeclaredType = GetDictionaryValueType(declaredType, expectedRuntimeType, actualRuntimeType);
                return CompareDictionaries(expectedDict!, actualDict!, valueDeclaredType, path, depth);
            }

            // If exactly one side is a dictionary, treat as a structural mismatch rather than
            // collapsing into KeyValuePair-by-KeyValuePair enumeration which would be confusing.
            if (expectedIsDictionary != actualIsDictionary)
            {
                return EquivalenceMismatch.TypeMismatch(path, expectedRuntimeType, actualRuntimeType);
            }

            if (expected is IEnumerable expectedEnum && actual is IEnumerable actualEnum)
            {
                Type elementDeclaredType = GetEnumerableElementType(declaredType, expectedRuntimeType, actualRuntimeType);
                return CompareEnumerables(expectedEnum, actualEnum, elementDeclaredType, path, depth);
            }

            return CompareMembers(expected, actual, expectedRuntimeType, actualRuntimeType, path, depth);
        }

        private enum IEquatableOutcome
        {
            NotApplicable,
            Equal,
            NotEqual,
            Threw,
        }
    }
}

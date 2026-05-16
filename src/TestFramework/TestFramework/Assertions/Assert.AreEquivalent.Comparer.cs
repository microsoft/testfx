// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Walks two object graphs and reports the first structural difference, if any.
    /// </summary>
    private sealed class EquivalenceComparer
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

        private EquivalenceMismatch? CompareDictionaries(DictionaryView expected, DictionaryView actual, Type valueDeclaredType, string path, int depth)
        {
            EquivalenceMismatch? failure = ForEachEntry(expected, isExpected: true, path, kvp =>
            {
                string childPath = AppendDictionaryKey(path, kvp.Key);

                EquivalenceMismatch? lookup = SafeDictionaryOp(
                    actual.TryGetValuePair,
                    kvp.Key,
                    isExpected: false,
                    childPath,
                    out DictionaryLookup lookupResult);
                return lookup is not null
                    ? lookup
                    : !lookupResult.Found
                        ? EquivalenceMismatch.MissingKey(childPath, kvp.Key)
                        : Compare(kvp.Value, lookupResult.Value, valueDeclaredType, childPath, depth + 1);
            });
            if (failure is not null)
            {
                return failure;
            }

            // In strict mode, additional keys on actual are a mismatch. In non-strict mode, they
            // are ignored (matching xUnit's `Assert.Equivalent` behaviour and the public docs).
            if (_strict)
            {
                failure = ForEachEntry(actual, isExpected: false, path, kvp =>
                {
                    string childPath = AppendDictionaryKey(path, kvp.Key);
                    EquivalenceMismatch? lookup = SafeDictionaryOp(
                        expected.ContainsKey,
                        kvp.Key,
                        isExpected: true,
                        childPath,
                        out bool exists);
                    return lookup is not null
                        ? lookup
                        : exists
                            ? null
                            : EquivalenceMismatch.UnexpectedKey(childPath, kvp.Key);
                });
            }

            return failure;
        }

        /// <summary>
        /// Iterates the entries of a <see cref="DictionaryView"/>, catching any user-supplied exceptions
        /// thrown by the underlying enumerator and surfacing them as a structured mismatch.
        /// </summary>
        private static EquivalenceMismatch? ForEachEntry(DictionaryView view, bool isExpected, string path, Func<KeyValuePair<object, object?>, EquivalenceMismatch?> onEntry)
        {
            IEnumerator<KeyValuePair<object, object?>>? enumerator;
            try
            {
                enumerator = view.Entries.GetEnumerator();
            }
            catch (TargetInvocationException tie)
            {
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, ex);
            }

            using (enumerator)
            {
                while (true)
                {
                    KeyValuePair<object, object?> kvp;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            return null;
                        }

                        kvp = enumerator.Current;
                    }
                    catch (TargetInvocationException tie)
                    {
                        return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, tie.InnerException ?? tie);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
                    {
                        return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, ex);
                    }

                    EquivalenceMismatch? nested = onEntry(kvp);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
            }
        }

        /// <summary>
        /// Runs a single dictionary access operation that takes one argument, catching user-supplied
        /// exceptions and surfacing them as a structured mismatch. On success, the operation's return
        /// value is exposed via <paramref name="result"/>.
        /// </summary>
        private static EquivalenceMismatch? SafeDictionaryOp<TArg, TResult>(Func<TArg, TResult> op, TArg arg, bool isExpected, string path, out TResult result)
        {
            try
            {
                result = op(arg);
                return null;
            }
            catch (TargetInvocationException tie)
            {
                result = default!;
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                result = default!;
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, ex);
            }
        }

        private EquivalenceMismatch? CompareEnumerables(IEnumerable expected, IEnumerable actual, Type elementDeclaredType, string path, int depth)
        {
            EquivalenceMismatch? failure = TryGetEnumerator(expected, isExpected: true, path, out IEnumerator expectedEnumerator);
            if (failure is not null)
            {
                return failure;
            }

            failure = TryGetEnumerator(actual, isExpected: false, path, out IEnumerator actualEnumerator);
            if (failure is not null)
            {
                DisposeEnumerator(expectedEnumerator);
                return failure;
            }

            try
            {
                int index = 0;
                while (true)
                {
                    failure = TryMoveNext(expectedEnumerator, isExpected: true, path, out bool expectedHasNext);
                    if (failure is not null)
                    {
                        return failure;
                    }

                    failure = TryMoveNext(actualEnumerator, isExpected: false, path, out bool actualHasNext);
                    if (failure is not null)
                    {
                        return failure;
                    }

                    if (!expectedHasNext || !actualHasNext)
                    {
                        if (expectedHasNext != actualHasNext)
                        {
                            failure = TryGetEnumerableCount(expectedEnumerator, expectedHasNext, isExpected: true, path, index, out int expectedCount);
                            if (failure is not null)
                            {
                                return failure;
                            }

                            failure = TryGetEnumerableCount(actualEnumerator, actualHasNext, isExpected: false, path, index, out int actualCount);
                            return failure ?? EquivalenceMismatch.LengthMismatch(path, expectedCount, actualCount);
                        }

                        return null;
                    }

                    failure = TryGetCurrent(expectedEnumerator, isExpected: true, path, out object? expectedItem);
                    if (failure is not null)
                    {
                        return failure;
                    }

                    failure = TryGetCurrent(actualEnumerator, isExpected: false, path, out object? actualItem);
                    if (failure is not null)
                    {
                        return failure;
                    }

                    EquivalenceMismatch? nested = Compare(expectedItem, actualItem, elementDeclaredType, AppendIndex(path, index), depth + 1);
                    if (nested is not null)
                    {
                        return nested;
                    }

                    index++;
                }
            }
            finally
            {
                DisposeEnumerator(expectedEnumerator);
                DisposeEnumerator(actualEnumerator);
            }
        }

        private EquivalenceMismatch? CompareMembers(object expected, object actual, Type expectedType, Type actualType, string path, int depth)
        {
            MemberLookup expectedMembers = GetMembers(expectedType);

            // When strict mode is on AND runtime types differ, ensure the actual side declares no extra
            // members beyond what's present on expected.
            if (_strict && expectedType != actualType)
            {
                MemberLookup actualMembers = GetMembers(actualType);

                List<string>? extras = null;
                foreach (MemberAccessor am in actualMembers.Sorted)
                {
                    if (!expectedMembers.ByName.ContainsKey(am.Name))
                    {
                        (extras ??= []).Add(am.Name);
                    }
                }

                if (extras is { Count: > 0 })
                {
                    return EquivalenceMismatch.ExtraMembers(path, extras);
                }
            }

            foreach (MemberAccessor member in expectedMembers.Sorted)
            {
                MemberAccessor? matchingActual = FindMember(actualType, member.Name);
                string childPath = AppendMember(path, member.Name);

                if (matchingActual is null)
                {
                    return EquivalenceMismatch.MissingMember(childPath, member.Name);
                }

                object? expectedValue;
                object? actualValue;
                try
                {
                    expectedValue = member.GetValue(expected);
                }
                catch (TargetInvocationException ex)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: true, ex.InnerException ?? ex);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: true, ex);
                }

                try
                {
                    actualValue = matchingActual.GetValue(actual);
                }
                catch (TargetInvocationException ex)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: false, ex.InnerException ?? ex);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: false, ex);
                }

                EquivalenceMismatch? nested = Compare(expectedValue, actualValue, member.MemberType, childPath, depth + 1);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static EquivalenceMismatch? TryGetEnumerator(IEnumerable source, bool isExpected, string path, out IEnumerator enumerator)
        {
            try
            {
                enumerator = source.GetEnumerator();
                return null;
            }
            catch (TargetInvocationException tie)
            {
                enumerator = default!;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                enumerator = default!;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, ex);
            }
        }

        private static EquivalenceMismatch? TryMoveNext(IEnumerator enumerator, bool isExpected, string path, out bool hasNext)
        {
            try
            {
                hasNext = enumerator.MoveNext();
                return null;
            }
            catch (TargetInvocationException tie)
            {
                hasNext = false;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                hasNext = false;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, ex);
            }
        }

        private static EquivalenceMismatch? TryGetCurrent(IEnumerator enumerator, bool isExpected, string path, out object? current)
        {
            try
            {
                current = enumerator.Current;
                return null;
            }
            catch (TargetInvocationException tie)
            {
                current = default;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                current = default;
                return EquivalenceMismatch.EnumerationFailure(path, isExpected, ex);
            }
        }

        private static EquivalenceMismatch? TryGetEnumerableCount(IEnumerator enumerator, bool hasCurrent, bool isExpected, string path, int matchedItemCount, out int count)
        {
            count = matchedItemCount;
            if (!hasCurrent)
            {
                return null;
            }

            EquivalenceMismatch? failure = TryGetCurrent(enumerator, isExpected, path, out _);
            if (failure is not null)
            {
                count = 0;
                return failure;
            }

            count++;
            while (true)
            {
                failure = TryMoveNext(enumerator, isExpected, path, out bool hasNext);
                if (failure is not null)
                {
                    count = 0;
                    return failure;
                }

                if (!hasNext)
                {
                    return null;
                }

                failure = TryGetCurrent(enumerator, isExpected, path, out _);
                if (failure is not null)
                {
                    count = 0;
                    return failure;
                }

                count++;
            }
        }

        private static void DisposeEnumerator(IEnumerator enumerator)
        {
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private enum IEquatableOutcome
        {
            NotApplicable,
            Equal,
            NotEqual,
            Threw,
        }

        private static IEquatableOutcome InvokeIEquatable(object expected, object actual, Type declaredType, out Exception? thrown)
        {
            thrown = null;
            MethodInfo? equalsMethod = IEquatableEqualsCache.GetOrAdd(declaredType, GetIEquatableEqualsMethod);
            if (equalsMethod is null)
            {
                return IEquatableOutcome.NotApplicable;
            }

            // Both sides must be assignable to declaredType for the call to be safe. Since the method
            // came in via the static type, this is true at the public entry point. For nested members
            // (and collection elements), we resolve the method against the *declared* property/field/element
            // type, but the runtime values can be derived types. As long as actual is also assignable, we
            // can still call.
            if (!declaredType.IsInstanceOfType(expected) || !declaredType.IsInstanceOfType(actual))
            {
                return IEquatableOutcome.NotApplicable;
            }

            try
            {
                bool isEqual = (bool)equalsMethod.Invoke(expected, [actual])!;
                return isEqual ? IEquatableOutcome.Equal : IEquatableOutcome.NotEqual;
            }
            catch (TargetInvocationException ex)
            {
                thrown = ex.InnerException ?? ex;
                return IEquatableOutcome.Threw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                thrown = ex;
                return IEquatableOutcome.Threw;
            }
        }

        private static MethodInfo? GetIEquatableEqualsMethod(Type declaredType)
        {
            if (declaredType == typeof(object) || declaredType == typeof(string))
            {
                // string is already handled via the primitive-like path; skip the IEquatable lookup.
                return null;
            }

            Type ieq;
            try
            {
                ieq = typeof(IEquatable<>).MakeGenericType(declaredType);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!ieq.IsAssignableFrom(declaredType))
            {
                return null;
            }

            // Resolve the IEquatable<T>.Equals(T) method on the interface itself. Invoking via reflection
            // performs virtual/interface dispatch, so this finds both implicit and explicit interface
            // implementations on the actual instance.
            return ieq.GetMethod("Equals", [declaredType]);
        }

        private static Type GetEnumerableElementType(Type declaredType, Type expectedRuntimeType, Type actualRuntimeType)
            => TryGetEnumerableElementType(declaredType)
                ?? TryGetEnumerableElementType(expectedRuntimeType)
                ?? TryGetEnumerableElementType(actualRuntimeType)
                ?? typeof(object);

        private static Type? TryGetEnumerableElementType(Type type)
            => EnumerableElementTypeCache.GetOrAdd(type, static t =>
            {
                if (t.IsArray)
                {
                    return t.GetElementType();
                }

                if (t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return t.GetGenericArguments()[0];
                }

                Type? best = null;
                foreach (Type i in t.GetInterfaces())
                {
                    if (i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        Type elem = i.GetGenericArguments()[0];

                        // If multiple IEnumerable<T> are implemented, prefer the most specific (non-object) one.
                        if (best is null || best == typeof(object))
                        {
                            best = elem;
                        }
                    }
                }

                return best;
            });

        private static Type GetDictionaryValueType(Type declaredType, Type expectedRuntimeType, Type actualRuntimeType)
            => TryGetDictionaryValueType(declaredType)
                ?? TryGetDictionaryValueType(expectedRuntimeType)
                ?? TryGetDictionaryValueType(actualRuntimeType)
                ?? typeof(object);

        private static Type? TryGetDictionaryValueType(Type type)
            => DictionaryValueTypeCache.GetOrAdd(type, static t =>
            {
                if (t.IsConstructedGenericType)
                {
                    Type def = t.GetGenericTypeDefinition();
                    if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                    {
                        return t.GetGenericArguments()[1];
                    }
                }

                foreach (Type i in t.GetInterfaces())
                {
                    if (i.IsConstructedGenericType)
                    {
                        Type def = i.GetGenericTypeDefinition();
                        if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                        {
                            return i.GetGenericArguments()[1];
                        }
                    }
                }

                return null;
            });

        private static bool IsPrimitiveLike(Type type)
            => IsPrimitiveLikeCache.GetOrAdd(type, static t =>
            {
                if (t.IsPrimitive || t.IsEnum)
                {
                    return true;
                }

                if (t == typeof(string) || t == typeof(decimal) ||
                    t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
                    t == typeof(TimeSpan) || t == typeof(Guid) ||
                    t == typeof(Uri) || t == typeof(Type) ||
                    t == typeof(Version))
                {
                    return true;
                }

                // DateOnly / TimeOnly / Half / Int128 exist only on newer TFMs; match by full name to avoid #if.
                string? fullName = t.FullName;
                return fullName is "System.DateOnly" or "System.TimeOnly" or "System.Half" or "System.Int128" or "System.UInt128";
            });

        private static bool TryCreateDictionaryView(object value, out DictionaryView? view)
        {
            // Non-generic IDictionary takes precedence over IDictionary<,> / IReadOnlyDictionary<,>:
            // most BCL dictionaries implement both and route both surfaces to the same backing store
            // (so the choice is observationally equivalent), while custom hybrid types are expected
            // to keep their non-generic surface consistent with the generic one. Picking the non-
            // generic path first avoids reflection-based dispatch where possible.
            if (value is IDictionary nonGeneric)
            {
                view = new NonGenericDictionaryView(nonGeneric);
                return true;
            }

            // Look for an IDictionary<TKey, TValue> implementation first; fall back to
            // IReadOnlyDictionary<TKey, TValue> if only the read-only surface is present.
            // When a type implements both (which is the case for most BCL dictionaries), the mutating
            // contract wins deterministically — that's the same precedence System.Collections.Generic.Dictionary
            // expresses, and it lets users override semantics by implementing IDictionary<,> explicitly.
            Type? dictInterface = null;
            Type? readOnlyDictInterface = null;
            foreach (Type i in value.GetType().GetInterfaces())
            {
                if (!i.IsGenericType)
                {
                    continue;
                }

                Type def = i.GetGenericTypeDefinition();
                if (def == typeof(IDictionary<,>))
                {
                    dictInterface = i;
                    break;
                }

                if (def == typeof(IReadOnlyDictionary<,>))
                {
                    readOnlyDictInterface = i;
                }
            }

            Type? selected = dictInterface ?? readOnlyDictInterface;
            if (selected is not null)
            {
                view = GenericDictionaryView.Create(value, selected);
                return true;
            }

            view = null;
            return false;
        }

        private static MemberLookup GetMembers(Type type)
            => MemberCache.GetOrAdd(type, static t =>
            {
                // Collect candidates per name, preferring the most-derived declaration so that
                // `new`-shadowed properties/fields are deterministically resolved to the most-derived
                // member regardless of metadata ordering.
#pragma warning disable IDE0028 // Collection initialization can be simplified — target-typed `new` cannot pass the comparer in the same syntactic form expected.
                Dictionary<string, MemberAccessor> byName = new(StringComparer.Ordinal);
                Dictionary<string, Type> declaringTypes = new(StringComparer.Ordinal);
#pragma warning restore IDE0028

                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    MethodInfo? getter = p.GetGetMethod(nonPublic: false);
                    if (getter is null)
                    {
                        continue;
                    }

                    TryRegisterMostDerived(byName, declaringTypes, p, new MemberAccessor(p.Name, p.PropertyType, p));
                }

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.IsStatic)
                    {
                        continue;
                    }

                    TryRegisterMostDerived(byName, declaringTypes, f, new MemberAccessor(f.Name, f.FieldType, f));
                }

                var sorted = new MemberAccessor[byName.Count];
                int i = 0;
                foreach (MemberAccessor accessor in byName.Values)
                {
                    sorted[i++] = accessor;
                }

                Array.Sort(sorted, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

                return new MemberLookup(sorted, byName);
            });

        private static void TryRegisterMostDerived(Dictionary<string, MemberAccessor> byName, Dictionary<string, Type> declaringTypes, MemberInfo member, MemberAccessor accessor)
        {
            if (byName.ContainsKey(member.Name)
                && !IsMoreDerivedThan(member.DeclaringType, declaringTypes[member.Name]))
            {
                return;
            }

            byName[member.Name] = accessor;
            declaringTypes[member.Name] = member.DeclaringType ?? typeof(object);
        }

        private static bool IsMoreDerivedThan(Type? candidate, Type? incumbent)
            => candidate is not null
                && incumbent is not null
                && candidate != incumbent
                && incumbent.IsAssignableFrom(candidate);

        private static MemberAccessor? FindMember(Type type, string name)
            => GetMembers(type).ByName.TryGetValue(name, out MemberAccessor? found) ? found : null;

        private static string AppendMember(string parent, string name)
            => parent.Length == 0 ? name : $"{parent}.{name}";

        private static string AppendIndex(string parent, int index)
            => $"{parent}[{index.ToString(CultureInfo.InvariantCulture)}]";

        private static string AppendDictionaryKey(string parent, object key)
        {
            string keyPart = $"[{AssertionValueRenderer.RenderValue(key)}]";
            return parent.Length == 0 ? keyPart : parent + keyPart;
        }
    }

    /// <summary>
    /// Reference-equality comparer used for keys in the topology maps of <see cref="EquivalenceComparer"/>.
    /// </summary>
    private sealed class ReferenceObjectComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceObjectComparer Instance = new();

        private ReferenceObjectComparer()
        {
        }

        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Cached set of public instance properties and fields for a type, with both an alphabetically-sorted
    /// array (for deterministic traversal) and a name-keyed dictionary (for O(1) lookup).
    /// </summary>
    private sealed class MemberLookup
    {
        internal MemberLookup(MemberAccessor[] sorted, IReadOnlyDictionary<string, MemberAccessor> byName)
        {
            Sorted = sorted;
            ByName = byName;
        }

        internal MemberAccessor[] Sorted { get; }

        internal IReadOnlyDictionary<string, MemberAccessor> ByName { get; }
    }

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

    /// <summary>
    /// Describes how to read a single member (property or field) from an instance.
    /// </summary>
    private sealed class MemberAccessor
    {
        private readonly PropertyInfo? _property;
        private readonly FieldInfo? _field;

        internal MemberAccessor(string name, Type memberType, PropertyInfo property)
        {
            Name = name;
            MemberType = memberType;
            _property = property;
        }

        internal MemberAccessor(string name, Type memberType, FieldInfo field)
        {
            Name = name;
            MemberType = memberType;
            _field = field;
        }

        internal string Name { get; }

        internal Type MemberType { get; }

        internal object? GetValue(object instance)
            => _property is not null ? _property.GetValue(instance) : _field!.GetValue(instance);
    }

    /// <summary>
    /// A single structural mismatch found by <see cref="EquivalenceComparer"/>, carrying the dotted
    /// member path, a localized reason summary, and any expected/actual snippets to render.
    /// </summary>
    private sealed class EquivalenceMismatch
    {
        private EquivalenceMismatch(string path, string reason, string? expectedText, string? actualText, bool isComparisonFailure)
        {
            Path = path;
            Reason = reason;
            ExpectedText = expectedText;
            ActualText = actualText;
            IsComparisonFailure = isComparisonFailure;
        }

        internal string Path { get; }

        internal string Reason { get; }

        internal string? ExpectedText { get; }

        internal string? ActualText { get; }

        internal bool IsComparisonFailure { get; }

        internal static EquivalenceMismatch ValueMismatch(string path, object? expected, object? actual)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchValue,
                AssertionValueRenderer.RenderValue(expected),
                AssertionValueRenderer.RenderValue(actual),
                isComparisonFailure: false);

        internal static EquivalenceMismatch NullMismatch(string path, object? expected, object? actual)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchNull,
                AssertionValueRenderer.RenderValue(expected),
                AssertionValueRenderer.RenderValue(actual),
                isComparisonFailure: false);

        internal static EquivalenceMismatch TypeMismatch(string path, Type expectedType, Type actualType)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchType, expectedType.FullName ?? expectedType.Name, actualType.FullName ?? actualType.Name),
                expectedType.FullName ?? expectedType.Name,
                actualType.FullName ?? actualType.Name,
                isComparisonFailure: false);

        internal static EquivalenceMismatch TopologyMismatch(string path)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchTopology,
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch LengthMismatch(string path, int expectedCount, int actualCount)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchLength, expectedCount, actualCount),
                expectedCount.ToString(CultureInfo.InvariantCulture),
                actualCount.ToString(CultureInfo.InvariantCulture),
                isComparisonFailure: false);

        internal static EquivalenceMismatch MissingKey(string path, object key)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMissingKey, AssertionValueRenderer.RenderValue(key)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch UnexpectedKey(string path, object key)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchUnexpectedKey, AssertionValueRenderer.RenderValue(key)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch MissingMember(string path, string memberName)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMissingMember, memberName),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch ExtraMembers(string path, IReadOnlyList<string> extras)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchExtraMembers, string.Join(", ", extras)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch IEquatableThrew(string path, Exception thrown)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEquivalentMismatchIEquatableThrew,
                    thrown.GetType().Name,
                    thrown.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch DictionaryAccessFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedDictionaryThrew : FrameworkMessages.AreEquivalentMismatchActualDictionaryThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch EnumerationFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedEnumerationThrew : FrameworkMessages.AreEquivalentMismatchActualEnumerationThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch MemberAccessFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedMemberThrew : FrameworkMessages.AreEquivalentMismatchActualMemberThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch MaxDepthExceeded(string path, int maxDepth)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMaxDepth, maxDepth),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);
    }
}

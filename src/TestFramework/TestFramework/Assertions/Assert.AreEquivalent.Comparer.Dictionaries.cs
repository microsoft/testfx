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
                ThrowIfAssertException(tie.InnerException);
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
                        ThrowIfAssertException(tie.InnerException);
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
                ThrowIfAssertException(tie.InnerException);
                result = default!;
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, tie.InnerException ?? tie);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                result = default!;
                return EquivalenceMismatch.DictionaryAccessFailure(path, isExpected, ex);
            }
        }

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
    }
}

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

        private static EquivalenceMismatch? TryGetEnumerator(IEnumerable source, bool isExpected, string path, out IEnumerator enumerator)
        {
            try
            {
                enumerator = source.GetEnumerator();
                return null;
            }
            catch (TargetInvocationException tie)
            {
                ThrowIfAssertException(tie.InnerException);
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
                ThrowIfAssertException(tie.InnerException);
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
                ThrowIfAssertException(tie.InnerException);
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
    }
}

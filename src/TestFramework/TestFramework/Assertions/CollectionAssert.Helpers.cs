// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions associated
/// with collections within unit tests. If the condition being tested is not
/// met, an exception is thrown.
/// </summary>
public sealed partial class CollectionAssert
{
    #region Helpers

    /// <summary>
    /// Determines whether the first collection is a subset of the second
    /// collection. If either set contains duplicate elements, the number
    /// of occurrences of the element in the subset must be less than or
    /// equal to the number of occurrences in the superset.
    /// </summary>
    /// <param name="subset">
    /// The collection the test expects to be contained in <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection the test expects to contain <paramref name="subset"/>.
    /// </param>
    /// <returns>
    /// True if <paramref name="subset"/> is a subset of
    /// <paramref name="superset"/>, false otherwise.
    /// </returns>
    internal static Tuple<bool, ICollection<object?>> IsSubsetOfHelper(ICollection subset, ICollection superset)
    {
        // $ CONSIDER: The current algorithm counts the number of occurrences of each
        // $ CONSIDER: element in each collection and then compares the count, resulting
        // $ CONSIDER: in an algorithm of ~n*log(n) + m*log(m) + n*log(m). It should be
        // $ CONSIDER: faster to sort both collections and do an element-by-element
        // $ CONSIDER: comparison, which should result in ~n*log(n) + m*log(m) + n.
        var nonSubsetValues = new List<object?>();

        // Count the occurrences of each object in both collections.
        Dictionary<object, int> subsetElements = GetElementCounts(subset.Cast<object>(), EqualityComparer<object>.Default, out int subsetNulls);
        Dictionary<object, int> supersetElements = GetElementCounts(superset.Cast<object>(), EqualityComparer<object>.Default, out int supersetNulls);

        bool isSubset = true;

        // Check null counts first
        if (subsetNulls > supersetNulls)
        {
            isSubset = false;
            // Add the excess null values to non-subset collection
            for (int i = 0; i < (subsetNulls - supersetNulls); i++)
            {
                nonSubsetValues.Add(null);
            }
        }

        // Compare the counts of each object in the subset to the count of that object
        // in the superset.
        foreach (object? element in subsetElements.Keys)
        {
            subsetElements.TryGetValue(element, out int subsetCount);
            supersetElements.TryGetValue(element, out int supersetCount);

            if (subsetCount > supersetCount)
            {
                isSubset = false;
                // Add the excess occurrences to non-subset collection
                int excessCount = subsetCount - supersetCount;
                for (int i = 0; i < excessCount; i++)
                {
                    nonSubsetValues.Add(element);
                }
            }
        }

        return new Tuple<bool, ICollection<object?>>(isSubset, nonSubsetValues);
    }

#pragma warning disable CS8714
    /// <summary>
    /// Constructs a dictionary containing the number of occurrences of each
    /// element in the specified collection.
    /// </summary>
    /// <param name="collection">
    /// The collection to process.
    /// </param>
    /// <param name="comparer">The equality comparer to use when comparing items.</param>
    /// <param name="nullCount">
    /// The number of null elements in the collection.
    /// </param>
    /// <returns>
    /// A dictionary containing the number of occurrences of each element
    /// in the specified collection.
    /// </returns>
    private static Dictionary<T, int> GetElementCounts<T>(IEnumerable<T?> collection, IEqualityComparer<T> comparer, out int nullCount)
    {
        DebugEx.Assert(collection != null, "Collection is Null.");

        var elementCounts = new Dictionary<T, int>(comparer);
        nullCount = 0;

        foreach (T? element in collection)
        {
            if (element == null)
            {
                nullCount++;
                continue;
            }

            elementCounts.TryGetValue(element, out int value);
            value++;
            elementCounts[element] = value;
        }

        return elementCounts;
    }

    /// <summary>
    /// Finds a mismatched element between the two collections. A mismatched
    /// element is one that appears a different number of times in the
    /// expected collection than it does in the actual collection. The
    /// collections are assumed to be different non-null references with the
    /// same number of elements. The caller is responsible for this level of
    /// verification. If there is no mismatched element, the function returns
    /// false and the out parameters should not be used.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare.
    /// </param>
    /// <param name="comparer">The equality comparer to use when comparing items.</param>
    /// <param name="expectedCount">
    /// The expected number of occurrences of
    /// <paramref name="mismatchedElement"/> or 0 if there is no mismatched
    /// element.
    /// </param>
    /// <param name="actualCount">
    /// The actual number of occurrences of
    /// <paramref name="mismatchedElement"/> or 0 if there is no mismatched
    /// element.
    /// </param>
    /// <param name="mismatchedElement">
    /// The mismatched element (may be null) or null if there is no
    /// mismatched element.
    /// </param>
    /// <returns>
    /// true if a mismatched element was found; false otherwise.
    /// </returns>
    private static bool FindMismatchedElement<T>(IEnumerable<T?> expected, IEnumerable<T?> actual, IEqualityComparer<T> comparer, out int expectedCount,
        out int actualCount, out object? mismatchedElement)
    {
        // $ CONSIDER: The current algorithm counts the number of occurrences of each
        // $ CONSIDER: element in each collection and then compares the count, resulting
        // $ CONSIDER: in an algorithm of ~n*log(n) + m*log(m) + n*log(m). It should be
        // $ CONSIDER: faster to sort both collections and do an element-by-element
        // $ CONSIDER: comparison, which should result in ~n*log(n) + m*log(m) + n.

        // Count the occurrences of each object in the both collections
        Dictionary<T, int> expectedElements = GetElementCounts(expected, comparer, out int expectedNulls);
        Dictionary<T, int> actualElements = GetElementCounts(actual, comparer, out int actualNulls);

        if (actualNulls != expectedNulls)
        {
            expectedCount = expectedNulls;
            actualCount = actualNulls;
            mismatchedElement = null;
            return true;
        }

        // Compare the counts of each object. Note that this comparison only needs
        // to be done one way since comparing the total count is a prerequisite to
        // calling this function.
        foreach (T current in expectedElements.Keys)
        {
            expectedElements.TryGetValue(current, out expectedCount);
            actualElements.TryGetValue(current, out actualCount);

            if (expectedCount != actualCount)
            {
                mismatchedElement = current;
                return true;
            }
        }

        // All the elements and counts matched.
        expectedCount = 0;
        actualCount = 0;
        mismatchedElement = null;
        return false;
    }
#pragma warning restore CS8714

    private static bool AreCollectionsEqual(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer,
        ref string reason)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer");
        if (object.ReferenceEquals(expected, actual))
        {
            reason = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.BothCollectionsSameReference, string.Empty);
            return true;
        }

        return CompareIEnumerable(expected, actual, comparer, ref reason);
    }

    private static bool CompareIEnumerable(IEnumerable? expected, IEnumerable? actual, IComparer comparer, ref string reason)
    {
        if ((expected == null) || (actual == null))
        {
            return false;
        }

        var stack = new Stack<Tuple<IEnumerator, IEnumerator, int>>();
        stack.Push(new(expected.GetEnumerator(), actual.GetEnumerator(), 0));

        while (stack.Count > 0)
        {
            Tuple<IEnumerator, IEnumerator, int> cur = stack.Pop();
            IEnumerator expectedEnum = cur.Item1;
            IEnumerator actualEnum = cur.Item2;
            int position = cur.Item3;

            while (expectedEnum.MoveNext())
            {
                if (!actualEnum.MoveNext())
                {
                    reason = FrameworkMessages.NumberOfElementsDiff;
                    return false;
                }

                object? curExpected = expectedEnum.Current;
                object? curActual = actualEnum.Current;
                if (comparer.Compare(curExpected, curActual) == 0)
                {
                    position++;
                }
                else if (curExpected is IEnumerable curExpectedEnum && curActual is IEnumerable curActualEnum)
                {
                    stack.Push(new(expectedEnum, actualEnum, position + 1));
                    stack.Push(new(curExpectedEnum.GetEnumerator(), curActualEnum.GetEnumerator(), 0));
                }
                else
                {
                    reason = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.ElementsAtIndexDontMatch,
                        position,
                        Assert.ReplaceNulls(curExpected),
                        Assert.ReplaceNulls(curActual));
                    return false;
                }
            }

            if (actualEnum.MoveNext() && !expectedEnum.MoveNext())
            {
                reason = FrameworkMessages.NumberOfElementsDiff;
                return false;
            }
        }

        reason = FrameworkMessages.BothCollectionsSameElements;
        return true;
    }

    private static string ConstructFinalMessage(
        string reason,
        string? message)
    {
        string userMessage = Assert.BuildUserMessage(message);
        return userMessage.Length == 0
            ? reason
            : string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CollectionEqualReason, userMessage, reason);
    }

    /// <summary>
    /// compares the objects using object.Equals.
    /// </summary>
    private sealed class ObjectComparer : IComparer
    {
        int IComparer.Compare(object? x, object? y) => Equals(x, y) ? 0 : -1;
    }

    #endregion
}

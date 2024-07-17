// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    private Assert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the Assert functionality.
    /// </summary>
    /// <remarks>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be "public static void IsOfType&lt;T&gt;(this Assert assert, object obj)"
    /// Users could then use a syntax similar to the default assertions which in this case is "Assert.That.IsOfType&lt;Dog&gt;(animal);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
    public static Assert That { get; } = new Assert();

    /// <summary>
    /// Replaces null characters ('\0') with "\\0".
    /// </summary>
    /// <param name="input">
    /// The string to search.
    /// </param>
    /// <returns>
    /// The converted string with null characters replaced by "\\0".
    /// </returns>
    /// <remarks>
    /// This is only public and still present to preserve compatibility with the V1 framework.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(input))]
    public static string? ReplaceNullChars(string? input)
        => StringEx.IsNullOrEmpty(input)
            ? input
            : input.Replace("\0", "\\0");

    /// <summary>
    /// Helper function that creates and throws an AssertionFailedException.
    /// </summary>
    /// <param name="assertionName">
    /// name of the assertion throwing an exception.
    /// </param>
    /// <param name="message">
    /// The assertion failure message.
    /// </param>
    [DoesNotReturn]
    internal static void ThrowAssertFailed(string assertionName, string? message)
        => throw new AssertFailedException(
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, ReplaceNulls(message)));

    /// <summary>
    /// Builds the formatted message using the given user format message and parameters.
    /// </summary>
    /// <param name="format">
    /// A composite format string.
    /// </param>
    /// <param name="parameters">
    /// An object array that contains zero or more objects to format.
    /// </param>
    /// <returns>
    /// The formatted string based on format and parameters.
    /// </returns>
    internal static string BuildUserMessage(string? format, params object?[]? parameters)
        => format is null
            ? ReplaceNulls(format)
            : format.Length == 0
                ? string.Empty
                : parameters == null || parameters.Length == 0
                    ? ReplaceNulls(format)
                    : string.Format(CultureInfo.CurrentCulture, ReplaceNulls(format), parameters);

    /// <summary>
    /// Checks the parameter for valid conditions.
    /// </summary>
    /// <param name="param">
    /// The parameter.
    /// </param>
    /// <param name="assertionName">
    /// The assertion Name.
    /// </param>
    /// <param name="parameterName">
    /// parameter name.
    /// </param>
    /// <param name="message">
    /// message for the invalid parameter exception.
    /// </param>
    /// <param name="parameters">
    /// The parameters.
    /// </param>
    internal static void CheckParameterNotNull([NotNull] object? param, string assertionName, string parameterName,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (param == null)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName, userMessage);
            ThrowAssertFailed(assertionName, finalMessage);
        }
    }

    /// <summary>
    /// Safely converts an object to a string, handling null values and null characters.
    /// Null values are converted to "(null)". Null characters are converted to "\\0".
    /// </summary>
    /// <param name="input">
    /// The object to convert to a string.
    /// </param>
    /// <returns>
    /// The converted string.
    /// </returns>
    [SuppressMessage("ReSharper", "RedundantToStringCall", Justification = "We are ensuring ToString() isn't overloaded in a way to misbehave")]
    [return: NotNull]
    internal static string ReplaceNulls(object? input)
    {
        // Use the localized "(null)" string for null values.
        if (input == null)
        {
            return FrameworkMessages.Common_NullInMessages.ToString();
        }

        // Convert it to a string.
        string? inputString = input.ToString();

        // Make sure the class didn't override ToString and return null.
        return inputString == null ? FrameworkMessages.Common_ObjectString.ToString() : ReplaceNullChars(inputString);
    }

    private static int CompareInternal(string? expected, string? actual, bool ignoreCase, CultureInfo? culture)
#pragma warning disable CA1309 // Use ordinal string comparison
        => string.Compare(expected, actual, ignoreCase, culture);
#pragma warning restore CA1309 // Use ordinal string comparison

    #region EqualsAssertion

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// equality. This object will <b>always</b> throw with Assert.Fail. Please use
    /// Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> False, always. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#pragma warning disable IDE0060 // Remove unused parameter
    public static new bool Equals(object? objA, object? objB)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Fail(FrameworkMessages.DoNotUseAssertEquals);
        return false;
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Determines whether the first collection is a subset of the second
    /// collection. If either set contains duplicate elements, the number
    /// of occurrences of the element in the subset must be less than or
    /// equal to the number of occurrences in the superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
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
    internal static bool IsSubsetOfHelper<T>(IEnumerable<T?>? subset, IEnumerable<T?>? superset)
    {
        // $ CONSIDER: The current algorithm counts the number of occurrences of each
        // $ CONSIDER: element in each collection and then compares the count, resulting
        // $ CONSIDER: in an algorithm of ~n*log(n) + m*log(m) + n*log(m). It should be
        // $ CONSIDER: faster to sort both collections and do an element-by-element
        // $ CONSIDER: comparison, which should result in ~n*log(n) + m*log(m) + n.

        // Count the occurrences of each object in both collections.
        Dictionary<object, int> subsetElements = GetElementCounts(subset?.Cast<object>(), EqualityComparer<object>.Default, out int subsetNulls);
        Dictionary<object, int> supersetElements = GetElementCounts(superset?.Cast<object>(), EqualityComparer<object>.Default, out int supersetNulls);

        if (subsetNulls > supersetNulls)
        {
            return false;
        }

        // Compare the counts of each object in the subset to the count of that object
        // in the superset.
        foreach (object? element in subsetElements.Keys)
        {
            subsetElements.TryGetValue(element, out int subsetCount);
            supersetElements.TryGetValue(element, out int supersetCount);

            if (subsetCount > supersetCount)
            {
                return false;
            }
        }

        // All the elements counts were OK.
        return true;
    }

#pragma warning disable CS8714
    /// <summary>
    /// Constructs a dictionary containing the number of occurrences of each
    /// element in the specified collection.
    /// </summary>
    /// <param name="collection">
    /// The collection to process.
    /// </param>
    /// <param name="nullCount">
    /// The number of null elements in the collection.
    /// </param>
    /// <returns>
    /// A dictionary containing the number of occurrences of each element
    /// in the specified collection.
    /// </returns>
    private static Dictionary<T, int> GetElementCounts<T>(IEnumerable<T?>? collection, IEqualityComparer<T> comparer, out int nullCount)
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
        Dictionary<T, int> expectedElements = GetElementCounts<T>(expected, comparer, out int expectedNulls);
        Dictionary<T, int> actualElements = GetElementCounts<T>(actual, comparer, out int actualNulls);

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

    private static bool AreCollectionsEqual<T>(IEnumerable? expected, IEnumerable? actual, [NotNull] IEqualityComparer<T>? comparer,
        ref string reason)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer", string.Empty);
        if (ReferenceEquals(expected, actual))
        {
            reason = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.BothCollectionsSameReference, string.Empty);
            return true;
        }

        return expected != null && actual != null && CompareIEnumerable(expected, actual, comparer, ref reason);
    }

    private static bool CompareIEnumerable<T>(IEnumerable expected, IEnumerable actual, IEqualityComparer<T> comparer, ref string reason)
    {
        IEnumerator expectedEnum = expected.GetEnumerator();
        IEnumerator actualEnum = actual.GetEnumerator();

        int position = 0;
        while (expectedEnum.MoveNext())
        {
            if (!actualEnum.MoveNext())
            {
                reason = FrameworkMessages.NumberOfElementsDiff;
                return false;
            }

            object? curExpected = expectedEnum.Current;
            object? curActual = actualEnum.Current;

            if (curExpected is IEnumerable curExpectedEnum && curActual is IEnumerable curActualEnum)
            {
                if (!CompareIEnumerable(curExpectedEnum, curActualEnum, comparer, ref reason))
                {
                    reason = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ElementsAtIndexDontMatch,
                    position);
                    return false;
                }
            }
            else if (curExpected == null && curActual == null)
            {
                position++;
                continue;
            }
            else if (curExpected == null || curActual == null || !comparer.Equals((T)curExpected, (T)curActual))
            {
                reason = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ElementsAtIndexDontMatch,
                    position);
                return false;
            }

            position++;
        }

        if (actualEnum.MoveNext())
        {
            reason = FrameworkMessages.NumberOfElementsDiff;
            return false;
        }

        reason = FrameworkMessages.BothCollectionsSameElements;
        return true;
    }

    /// <summary>
    /// compares the objects using object.Equals.
    /// </summary>
    private class ObjectComparer : IComparer
    {
        int IComparer.Compare(object? x, object? y) => Equals(x, y) ? 0 : -1;
    }

    private class ComparerWrapper<T> : IEqualityComparer<T>
    {
        private readonly IComparer _comparer;

        public ComparerWrapper([NotNull] IComparer? comparer)
        {
            Assert.CheckParameterNotNull(comparer, "Assert", "comparer", string.Empty);
            _comparer = comparer;
        }

        bool IEqualityComparer<T>.Equals(T? x, T? y) =>
            // Use the comparer to check if the two items are equal
            _comparer?.Compare(x, y) == 0;

        public int GetHashCode(T obj) =>
           throw new InvalidOperationException("GetHashCode is not suppose to be called in the wrapper.");
    }
    #endregion

}

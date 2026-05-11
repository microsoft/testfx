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
    #region Equivalence

    /// <summary>
    /// Tests whether two collections contain the same elements and throws an
    /// exception if either collection contains an element not in the other
    /// collection.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual)
        => AreEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty);

    /// <summary>
    /// Tests whether two collections contain the same elements and throws an
    /// exception if either collection contains an element not in the other
    /// collection.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element was found
    /// in one of the collections but not the other. The message is shown
    /// in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual, string? message)
        => AreEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message);

    /// <summary>
    /// Tests whether two collections contain the same elements and throws an
    /// exception if either collection contains an element not in the other
    /// collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer)
        => AreEquivalent(expected, actual, comparer, string.Empty);

    /// <summary>
    /// Tests whether two collections contain the same elements and throws an
    /// exception if either collection contains an element not in the other
    /// collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element was found
    /// in one of the collections but not the other. The message is shown
    /// in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        string? message)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer");

        // Check whether one is null while the other is not.
        if (expected == null != (actual == null))
        {
            Assert.ReportAssertFailed("CollectionAssert.AreEquivalent", Assert.BuildUserMessage(message));
        }

        // If the references are the same or both collections are null, they are equivalent.
        if (object.ReferenceEquals(expected, actual) || expected == null)
        {
            return;
        }

        DebugEx.Assert(actual is not null, "actual is not null here");

        int expectedCollectionCount = expected.Count();
        int actualCollectionCount = actual.Count();

        // Check whether the element counts are different.
        if (expectedCollectionCount != actualCollectionCount)
        {
            string userMessage = Assert.BuildUserMessage(message);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.ElementNumbersDontMatch,
                userMessage,
                expectedCollectionCount,
                actualCollectionCount);
            Assert.ReportAssertFailed("CollectionAssert.AreEquivalent", finalMessage);
        }

        // If both collections are empty, they are equivalent.
        if (expectedCollectionCount == 0)
        {
            return;
        }

        // Search for a mismatched element.
        if (FindMismatchedElement(expected, actual, comparer, out int expectedCount, out int actualCount, out object? mismatchedElement))
        {
            string userMessage = Assert.BuildUserMessage(message);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.ActualHasMismatchedElements,
                userMessage,
                expectedCount.ToString(CultureInfo.CurrentCulture.NumberFormat),
                Assert.ReplaceNulls(mismatchedElement),
                actualCount.ToString(CultureInfo.CurrentCulture.NumberFormat));
            Assert.ReportAssertFailed("CollectionAssert.AreEquivalent", finalMessage);
        }

        // All the elements and counts matched.
    }

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent(
        [NotNullIfNotNull(nameof(actual))] ICollection? notExpected, [NotNullIfNotNull(nameof(notExpected))] ICollection? actual)
        => AreNotEquivalent(notExpected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// contains the same elements as <paramref name="notExpected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent(
        [NotNullIfNotNull(nameof(actual))] ICollection? notExpected, [NotNullIfNotNull(nameof(notExpected))] ICollection? actual,
        string? message)
        => AreNotEquivalent(notExpected?.Cast<object>(), actual?.Cast<object>(), comparer: EqualityComparer<object>.Default, message);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? notExpected, [NotNullIfNotNull(nameof(notExpected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer)
        => AreNotEquivalent(notExpected, actual, comparer, string.Empty);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// contains the same elements as <paramref name="notExpected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? notExpected, [NotNullIfNotNull(nameof(notExpected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        string? message)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer");

        // Check whether one is null while the other is not.
        if (notExpected == null != (actual == null))
        {
            return;
        }

        // If the references are the same or both collections are null, they
        // are equivalent. object.ReferenceEquals will handle case where both are null.
        if (object.ReferenceEquals(notExpected, actual))
        {
            string userMessage = Assert.BuildUserMessage(message);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothCollectionsSameReference,
                userMessage);
            Assert.ReportAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }

        DebugEx.Assert(actual is not null, "actual is not null here");
        DebugEx.Assert(notExpected is not null, "expected is not null here");

        // Check whether the element counts are different.
        int notExpectedCount = notExpected.Count();
        int actualCount = actual.Count();
        if (notExpectedCount != actualCount)
        {
            return;
        }

        // If both collections are empty, they are equivalent.
        if (notExpectedCount == 0)
        {
            string userMessage = Assert.BuildUserMessage(message);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothCollectionsEmpty,
                userMessage);
            Assert.ReportAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }

        // Search for a mismatched element.
        if (!FindMismatchedElement(notExpected, actual, comparer, out _, out _, out _))
        {
            string userMessage = Assert.BuildUserMessage(message);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothSameElements,
                userMessage);
            Assert.ReportAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }
    }

    #endregion
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    #region AreEquivalent

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
    public static void AreEquivalent(IEnumerable? expected, IEnumerable? actual)
        => AreEquivalent(expected, actual, string.Empty, null);

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
    public static void AreEquivalent(IEnumerable? expected, IEnumerable? actual, string? message)
        => AreEquivalent(expected, actual, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent(IEnumerable? expected, IEnumerable? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!AreCollectionsEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, out string failureReason))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualFailMsg, userMessage, expected, actual);
            ThrowAssertFailed("Assert.AreEquivalent", finalMessage);
        }
    }

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
    public static void AreEquivalent<T>(IEnumerable<T?>? expected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer)
        => AreEquivalent(expected, actual, comparer, string.Empty, null);

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
    public static void AreEquivalent<T>(IEnumerable<T?>? expected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer, string? message)
        => AreEquivalent(expected, actual, comparer, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent<T>(IEnumerable<T?>? expected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(comparer, "Assert.AreEquivalent", "comparer", string.Empty);
        if (!AreCollectionsEquivalent(expected, actual, comparer, out string failureReason))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualFailMsg, userMessage, expected, actual);
            ThrowAssertFailed("Assert.AreEquivalent", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent(IEnumerable? notExpected, IEnumerable? actual)
        => AreNotEquivalent(notExpected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when the two collections are
    /// equivalent. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent(IEnumerable? notExpected, IEnumerable? actual, string? message)
        => AreNotEquivalent(notExpected, actual, message, null);

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when the two collections are
    /// equivalent. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent(IEnumerable? notExpected, IEnumerable? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (AreCollectionsEquivalent(notExpected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, out _))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreNotEqualFailMsg, userMessage, notExpected, actual);
            ThrowAssertFailed("Assert.AreNotEquivalent", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent<T>(IEnumerable<T?>? notExpected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer)
        => AreNotEquivalent(notExpected, actual, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when the two collections are
    /// equivalent. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent<T>(IEnumerable<T?>? notExpected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer, string? message)
        => AreNotEquivalent(notExpected, actual, comparer, message, null);

    /// <summary>
    /// Tests whether two collections do not contain the same elements and throws an
    /// exception if the two collections contain the same elements without regard to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first collection to compare. This contains the elements the test
    /// expects not to be equivalent to the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when the two collections are
    /// equivalent. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="notExpected"/> and <paramref name="actual"/> are equivalent.
    /// </exception>
    public static void AreNotEquivalent<T>(IEnumerable<T?>? notExpected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(comparer, "Assert.AreNotEquivalent", "comparer", string.Empty);
        if (AreCollectionsEquivalent(notExpected, actual, comparer, out _))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreNotEqualFailMsg, userMessage, notExpected, actual);
            ThrowAssertFailed("Assert.AreNotEquivalent", finalMessage);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to check if two collections are equivalent (contain the same elements regardless of order).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collections.</typeparam>
    /// <param name="expected">The expected collection.</param>
    /// <param name="actual">The actual collection.</param>
    /// <param name="comparer">The comparer to use for element comparison.</param>
    /// <param name="failureReason">The reason for failure if collections are not equivalent.</param>
    /// <returns>True if collections are equivalent, false otherwise.</returns>
    internal static bool AreCollectionsEquivalent<T>(IEnumerable<T?>? expected, IEnumerable<T?>? actual, IEqualityComparer<T> comparer, out string failureReason)
    {
        failureReason = string.Empty;

        if (ReferenceEquals(expected, actual))
        {
            return true;
        }

        if (expected is null && actual is null)
        {
            return true;
        }

        if (expected is null || actual is null)
        {
            failureReason = "One collection is null while the other is not.";
            return false;
        }

        var expectedCounts = new Dictionary<T, int>(comparer);
        var actualCounts = new Dictionary<T, int>(comparer);

        // Count elements in expected collection
        foreach (var item in expected)
        {
            if (expectedCounts.TryGetValue(item!, out int count))
            {
                expectedCounts[item!] = count + 1;
            }
            else
            {
                expectedCounts[item!] = 1;
            }
        }

        // Count elements in actual collection
        foreach (var item in actual)
        {
            if (actualCounts.TryGetValue(item!, out int count))
            {
                actualCounts[item!] = count + 1;
            }
            else
            {
                actualCounts[item!] = 1;
            }
        }

        // Check if counts match for all elements
        if (expectedCounts.Count != actualCounts.Count)
        {
            failureReason = "Collections have different number of unique elements.";
            return false;
        }

        foreach (var kvp in expectedCounts)
        {
            if (!actualCounts.TryGetValue(kvp.Key, out int actualCount) || actualCount != kvp.Value)
            {
                failureReason = "Collections have different element counts.";
                return false;
            }
        }

        return true;
    }

    #endregion
}
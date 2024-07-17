// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    #region Membership

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
    /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected to be in the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> does not contain
    /// element <paramref name="element"/>.
    /// </exception>
    public static void Contains<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element)
        => Contains(collection, element, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
    /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is not in <paramref name="collection"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> does not contain
    /// element <paramref name="element"/>.
    /// </exception>
    public static void Contains<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element, string? message)
        => Contains(collection, element, message, null);

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
    /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is not in <paramref name="collection"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> does not contain
    /// element <paramref name="element"/>.
    /// </exception>
    public static void Contains<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.Contains", "collection", string.Empty);

        foreach (object? current in collection)
        {
            if (Equals(current, element))
            {
                return;
            }
        }

        Assert.ThrowAssertFailed("CollectionAssert.Contains", Assert.BuildUserMessage(message, parameters));
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
    /// /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected not to be in the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains
    /// element <paramref name="element"/>.
    /// </exception>
    public static void DoesNotContain<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element)
        => DoesNotContain(collection, element, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
    /// /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected not to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is in <paramref name="collection"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains
    /// element <paramref name="element"/>.
    /// </exception>
    public static void DoesNotContain<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element, string? message)
        => DoesNotContain(collection, element, message, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
    /// /// <typeparam name="T1">
    /// The type of values to compare.
    /// </typeparam>
    /// <typeparam name="T2">
    /// The type of element to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected not to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is in <paramref name="collection"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains
    /// element <paramref name="element"/>.
    /// </exception>
    public static void DoesNotContain<T1, T2>([NotNull] IEnumerable<T1?>? collection, T2? element, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.DoesNotContain", "collection", string.Empty);

        foreach (object? current in collection)
        {
            if (Equals(current, element))
            {
                Assert.ThrowAssertFailed("CollectionAssert.DoesNotContain", Assert.BuildUserMessage(message, parameters));
            }
        }
    }

    public static void AllItemsAreNotNull<T>([NotNull] IEnumerable<T?>? collection)
        => AllItemsAreNotNull(collection, string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains a null element. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains a null element.
    /// </exception>
    public static void AllItemsAreNotNull<T>([NotNull] IEnumerable<T?>? collection, string? message)
        => AllItemsAreNotNull(collection, message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains a null element. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains a null element.
    /// </exception>
    public static void AllItemsAreNotNull<T>([NotNull] IEnumerable<T?>? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreNotNull", "collection", string.Empty);
        foreach (object? current in collection)
        {
            if (current == null)
            {
                Assert.ThrowAssertFailed("CollectionAssert.AllItemsAreNotNull", Assert.BuildUserMessage(message, parameters));
            }
        }
    }

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique<T>([NotNull] IEnumerable<T?>? collection)
        => AllItemsAreUnique(collection, string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains at least one duplicate element. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique<T>([NotNull] IEnumerable<T?>? collection, string? message)
        => AllItemsAreUnique(collection, message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains at least one duplicate element. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique<T>([NotNull] IEnumerable<T?>? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreUnique", "collection", string.Empty);

        message = Assert.ReplaceNulls(message);

        bool foundNull = false;
#pragma warning disable CS8714
        Dictionary<T, bool> table = [];
#pragma warning restore CS8714
        foreach (T? current in collection)
        {
            if (current == null)
            {
                if (!foundNull)
                {
                    foundNull = true;
                }
                else
                {
                    // Found a second occurrence of null.
                    string userMessage = Assert.BuildUserMessage(message, parameters);
                    string finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.AllItemsAreUniqueFailMsg,
                        userMessage,
                        FrameworkMessages.Common_NullInMessages);

                    Assert.ThrowAssertFailed("CollectionAssert.AllItemsAreUnique", finalMessage);
                }
            }
            else
            {
#pragma warning disable CA1864 // Prefer the 'IDictionary.TryAdd(TKey, TValue)' method
                if (table.ContainsKey(current))
                {
                    string userMessage = Assert.BuildUserMessage(message, parameters);
                    string finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.AllItemsAreUniqueFailMsg,
                        userMessage,
                        Assert.ReplaceNulls(current));

                    Assert.ThrowAssertFailed("CollectionAssert.AllItemsAreUnique", finalMessage);
                }
                else
                {
                    table.Add(current, true);
                }
#pragma warning restore CA1864 // Prefer the 'IDictionary.TryAdd(TKey, TValue)' method
            }
        }
    }

    #endregion

    #region Subset

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or <paramref name="subset"/> contains at least one element not contained in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset)
        => IsSubsetOf(subset, superset, string.Empty, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in
    /// <paramref name="subset"/> is not found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or <paramref name="subset"/> contains at least one element not contained in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset, string? message)
        => IsSubsetOf(subset, superset, message, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in
    /// <paramref name="subset"/> is not found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or <paramref name="subset"/> contains at least one element not contained in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(subset, "CollectionAssert.IsSubsetOf", "subset", string.Empty);
        Assert.CheckParameterNotNull(superset, "CollectionAssert.IsSubsetOf", "superset", string.Empty);
        if (!IsSubsetOfHelper(subset, superset))
        {
            Assert.ThrowAssertFailed("CollectionAssert.IsSubsetOf", Assert.BuildUserMessage(message, parameters));
        }
    }

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset)
        => IsNotSubsetOf(subset, superset, string.Empty, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in
    /// <paramref name="subset"/> is also found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset, string? message)
        => IsNotSubsetOf(subset, superset, message, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in
    /// <paramref name="subset"/> is also found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T?>? subset, [NotNull] IEnumerable<T?>? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(subset, "CollectionAssert.IsNotSubsetOf", "subset", string.Empty);
        Assert.CheckParameterNotNull(superset, "CollectionAssert.IsNotSubsetOf", "superset", string.Empty);
        if (IsSubsetOfHelper(subset, superset))
        {
            Assert.ThrowAssertFailed("CollectionAssert.IsNotSubsetOf", Assert.BuildUserMessage(message, parameters));
        }
    }

    #endregion

    #region Equivalence

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
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if any element was found in one of the collections but not the other.
    /// </exception>
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual)
        => AreEquivalent(expected, actual, EqualityComparer<T>.Default, string.Empty, null);

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
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual,
        string? message)
        => AreEquivalent(expected, actual, EqualityComparer<T>.Default, message, null);

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
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreEquivalent(expected, actual, EqualityComparer<T>.Default, message, parameters);

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
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        string? message)
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
    public static void AreEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer", string.Empty);

        // Check whether one is null while the other is not.
        if (expected == null != (actual == null))
        {
            Assert.ThrowAssertFailed("CollectionAssert.AreEquivalent", Assert.BuildUserMessage(message, parameters));
        }

        // If the references are the same or both collections are null, they are equivalent.
        if (ReferenceEquals(expected, actual) || expected == null)
        {
            return;
        }

        DebugEx.Assert(actual is not null, "actual is not null here");

        int expectedCollectionCount = expected.Count();
        int actualCollectionCount = actual.Count();

        // Check whether the element counts are different.
        if (expectedCollectionCount != actualCollectionCount)
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.ElementNumbersDontMatch,
                userMessage,
                expectedCollectionCount,
                actualCollectionCount);
            Assert.ThrowAssertFailed("CollectionAssert.AreEquivalent", finalMessage);
        }

        // If both collections are empty, they are equivalent.
        if (!expected.Any())
        {
            return;
        }

        // Search for a mismatched element.
        if (FindMismatchedElement(expected, actual, comparer, out int expectedCount, out int actualCount, out object? mismatchedElement))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.ActualHasMismatchedElements,
                userMessage,
                expectedCount.ToString(CultureInfo.CurrentCulture.NumberFormat),
                Assert.ReplaceNulls(mismatchedElement),
                actualCount.ToString(CultureInfo.CurrentCulture.NumberFormat));
            Assert.ThrowAssertFailed("CollectionAssert.AreEquivalent", finalMessage);
        }

        // All the elements and counts matched.
    }

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual)
        => AreNotEquivalent(expected, actual, EqualityComparer<T>.Default, string.Empty, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// contains the same elements as <paramref name="expected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual,
        string? message)
        => AreNotEquivalent(expected, actual, EqualityComparer<T>.Default, message, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first collection to compare. This contains the elements the test
    /// expects to be different than the actual collection.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by
    /// the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// contains the same elements as <paramref name="expected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreNotEquivalent(expected, actual, comparer: EqualityComparer<T>.Default, message, parameters);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
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
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer)
        => AreNotEquivalent(expected, actual, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
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
    /// contains the same elements as <paramref name="expected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        string? message)
        => AreNotEquivalent(expected, actual, comparer, message, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
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
    /// contains the same elements as <paramref name="expected"/>. The message
    /// is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="expected"/> and <paramref name="actual"/> nullabilities don't match,
    /// or if collections contain the same elements, including the same number of duplicate
    /// occurrences of each element.
    /// </exception>
    public static void AreNotEquivalent<T>(
        [NotNullIfNotNull(nameof(actual))] IEnumerable<T?>? expected, [NotNullIfNotNull(nameof(expected))] IEnumerable<T?>? actual, [NotNull] IEqualityComparer<T>? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer", string.Empty);

        // Check whether one is null while the other is not.
        if (expected == null != (actual == null))
        {
            return;
        }

        // If the references are the same or both collections are null, they
        // are equivalent. object.ReferenceEquals will handle case where both are null.
        if (ReferenceEquals(expected, actual))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothCollectionsSameReference,
                userMessage);
            Assert.ThrowAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }

        DebugEx.Assert(actual is not null, "actual is not null here");
        DebugEx.Assert(expected is not null, "expected is not null here");

        // Check whether the element counts are different.
        if (expected.Count() != actual.Count())
        {
            return;
        }

        // If both collections are empty, they are equivalent.
        if (!expected.Any())
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothCollectionsEmpty,
                userMessage);
            Assert.ThrowAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }

        // Search for a mismatched element.
        if (!FindMismatchedElement(expected, actual, comparer, out _, out _, out _))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.BothSameElements,
                userMessage);
            Assert.ThrowAssertFailed("CollectionAssert.AreNotEquivalent", finalMessage);
        }
    }

    #endregion

    #region Type

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the
    /// specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of each element of <paramref name="collection"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null or, <paramref name="expectedType"/> is null,
    /// or some elements of <paramref name="collection"/> do not inherit/implement
    /// <paramref name="expectedType"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType<T>([NotNull] IEnumerable<T?>? collection, [NotNull] Type? expectedType)
        => AllItemsAreInstancesOfType(collection, expectedType, string.Empty, null);

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the
    /// specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of each element of <paramref name="collection"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in
    /// <paramref name="collection"/> is not an instance of
    /// <paramref name="expectedType"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null or, <paramref name="expectedType"/> is null,
    /// or some elements of <paramref name="collection"/> do not inherit/implement
    /// <paramref name="expectedType"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType<T>([NotNull] IEnumerable<T?>? collection, [NotNull] Type? expectedType,
        string? message)
        => AllItemsAreInstancesOfType(collection, expectedType, message, null);

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the
    /// specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of each element of <paramref name="collection"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in
    /// <paramref name="collection"/> is not an instance of
    /// <paramref name="expectedType"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null or, <paramref name="expectedType"/> is null,
    /// or some elements of <paramref name="collection"/> do not inherit/implement
    /// <paramref name="expectedType"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType<T>(
        [NotNull] IEnumerable<T?>? collection, [NotNull] Type? expectedType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreInstancesOfType", "collection", string.Empty);
        Assert.CheckParameterNotNull(expectedType, "CollectionAssert.AllItemsAreInstancesOfType", "expectedType", string.Empty);
        int i = 0;
        foreach (object? element in collection)
        {
            if (expectedType.GetTypeInfo() is { } expectedTypeInfo
                && element?.GetType().GetTypeInfo() is { } elementTypeInfo
                && !expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
            {
                string userMessage = Assert.BuildUserMessage(message, parameters);
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ElementTypesAtIndexDontMatch,
                    userMessage,
                    i,
                    expectedType.ToString(),
                    element.GetType().ToString());
                Assert.ThrowAssertFailed("CollectionAssert.AllItemsAreInstancesOfType", finalMessage);
            }

            i++;
        }
    }

    #endregion
}

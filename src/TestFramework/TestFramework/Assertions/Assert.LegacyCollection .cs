// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    #region Legacy collection Assert
    #region Membership

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
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
    public static void ContainsLegacy([NotNull] ICollection? collection, object? element)
        => Contains(collection?.Cast<object>(), element, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
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
    public static void ContainsLegacy([NotNull] ICollection? collection, object? element, string? message)
        => Contains(collection?.Cast<object>(), element, message, null);

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
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
    public static void ContainsLegacy([NotNull] ICollection? collection, object? element, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => Contains(collection?.Cast<object>(), element, message, parameters);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
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
    public static void DoesNotContainLegacy([NotNull] ICollection? collection, object? element)
        => DoesNotContain(collection?.Cast<object>(), element, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
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
    public static void DoesNotContainLegacy([NotNull] ICollection? collection, object? element, string? message)
        => DoesNotContain(collection?.Cast<object>(), element, message, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
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
    public static void DoesNotContainLegacy([NotNull] ICollection? collection, object? element, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => DoesNotContain(collection?.Cast<object>(), element, message, parameters);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains a null element.
    /// </exception>
    public static void AllItemsAreNotNullLegacy([NotNull] ICollection? collection)
        => AllItemsAreNotNull(collection?.Cast<object>(), string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
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
    public static void AllItemsAreNotNullLegacy([NotNull] ICollection? collection, string? message)
        => AllItemsAreNotNull(collection?.Cast<object>(), message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
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
    public static void AllItemsAreNotNullLegacy([NotNull] ICollection? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => AllItemsAreNotNull(collection?.Cast<object>(), message, parameters);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUniqueLegacy([NotNull] ICollection? collection)
        => AllItemsAreUnique(collection?.Cast<object>(), string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
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
    public static void AllItemsAreUniqueLegacy([NotNull] ICollection? collection, string? message)
        => AllItemsAreUnique(collection?.Cast<object>(), message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
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
    public static void AllItemsAreUniqueLegacy([NotNull] ICollection? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => AllItemsAreUnique(collection?.Cast<object>(), message, parameters);

    #endregion

    #region Subset

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
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
    public static void IsSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset)
        => IsSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), string.Empty, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
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
    public static void IsSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset, string? message)
        => IsSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), message, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
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
    public static void IsSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => IsSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), message, parameters);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
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
    public static void IsNotSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset)
        => IsNotSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), string.Empty, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
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
    public static void IsNotSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset, string? message)
        => IsNotSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), message, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
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
    public static void IsNotSubsetOfLegacy([NotNull] ICollection? subset, [NotNull] ICollection? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => IsNotSubsetOf(subset?.Cast<object>(), superset?.Cast<object>(), message, parameters);
    #endregion

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
    public static void AreEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual)
        => AreEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty, null);

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
    public static void AreEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual,
        string? message)
        => AreEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, null);

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
    public static void AreEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, parameters);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
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
    public static void AreNotEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual)
        => AreNotEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
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
    public static void AreNotEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual,
        string? message)
        => AreNotEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, null);

    /// <summary>
    /// Tests whether two collections contain the different elements and throws an
    /// exception if the two collections contain identical elements without regard
    /// to order.
    /// </summary>
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
    public static void AreNotEquivalentLegacy(
        [NotNullIfNotNull(nameof(actual))] ICollection? expected, [NotNullIfNotNull(nameof(expected))] ICollection? actual,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreNotEquivalent(expected?.Cast<object>(), actual?.Cast<object>(), comparer: EqualityComparer<object>.Default, message, parameters);
    #endregion

    #region Type

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
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
    public static void AllItemsAreInstancesOfTypeLegacy([NotNull] ICollection? collection, [NotNull] Type? expectedType)
        => AllItemsAreInstancesOfType(collection?.Cast<object>(), expectedType, string.Empty, null);

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
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
    public static void AllItemsAreInstancesOfTypeLegacy([NotNull] ICollection? collection, [NotNull] Type? expectedType,
        string? message)
        => AllItemsAreInstancesOfType(collection?.Cast<object>(), expectedType, message, null);

    /// <summary>
    /// Tests whether all elements in the specified collection are instances
    /// of the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements.
    /// </summary>
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
    public static void AllItemsAreInstancesOfTypeLegacy(
        [NotNull] ICollection? collection, [NotNull] Type? expectedType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AllItemsAreInstancesOfType(collection?.Cast<object>(), expectedType, message, parameters);
    #endregion

    #region AreEqual

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual, string? message)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, null);

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, parameters);

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), string.Empty, null);

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer,
        string? message)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), message, null);

    /// <summary>
    /// Tests whether the specified collections are equal and throws an exception
    /// if the two collections are not equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="expected">
    /// The first collection to compare. This is the collection the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqualLegacy(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreEqual(expected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), message, parameters);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual, string? message)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, null);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Whether two elements are the same
    /// is checked using <see cref="object.Equals(object, object)" /> method.
    /// Different references to the same value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), EqualityComparer<object>.Default, message, parameters);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual, [NotNull] IComparer? comparer)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), string.Empty, null);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual, [NotNull] IComparer? comparer,
        string? message)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), message, null);

    /// <summary>
    /// Tests whether the specified collections are unequal and throws an exception
    /// if the two collections are equal. Equality is defined as having the same
    /// elements in the same order and quantity. Different references to the same
    /// value are considered equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first collection to compare. This is the collection the tests expects
    /// not to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second collection to compare. This is the collection produced by the
    /// code under test.
    /// </param>
    /// <param name="comparer">
    /// The compare implementation to use when comparing elements of the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqualLegacy(ICollection? notExpected, ICollection? actual, [NotNull] IComparer? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreNotEqual(notExpected?.Cast<object>(), actual?.Cast<object>(), new ComparerWrapper<object>(comparer), message, parameters);
    #endregion

    #endregion
}

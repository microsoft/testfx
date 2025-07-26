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
    #region AllItemsAreNotNull

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for null elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> or any element in <paramref name="collection"/> is null.
    /// </exception>
    public static void AllItemsAreNotNull(IEnumerable? collection)
        => AllItemsAreNotNull(collection, string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains a null element. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> or any element in <paramref name="collection"/> is null.
    /// </exception>
    public static void AllItemsAreNotNull(IEnumerable? collection, string? message)
        => AllItemsAreNotNull(collection, message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains a null element. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> or any element in <paramref name="collection"/> is null.
    /// </exception>
    public static void AllItemsAreNotNull(IEnumerable? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreNotNull", "collection", string.Empty);

        int nullElementIndex = 0;
        foreach (object? element in collection)
        {
            if (element is null)
            {
                string userMessage = BuildUserMessage(message, parameters);
                string finalMessage = string.Format(CultureInfo.CurrentCulture, "Null element found at index {1}. {0}", userMessage, nullElementIndex);
                ThrowAssertFailed("Assert.AllItemsAreNotNull", finalMessage);
            }

            nullElementIndex++;
        }
    }

    #endregion

    #region AllItemsAreUnique

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for duplicate elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique(IEnumerable? collection)
        => AllItemsAreUnique(collection, string.Empty, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains at least one duplicate element. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique(IEnumerable? collection, string? message)
        => AllItemsAreUnique(collection, message, null);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to look for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains at least one duplicate element. The message is shown in test
    /// results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique(IEnumerable? collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreUnique", "collection", string.Empty);

        if (FindDuplicateElement(collection, out object? duplicateElement))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AllItemsAreUniqueFailMsg, userMessage, ReplaceNulls(duplicateElement));
            ThrowAssertFailed("Assert.AllItemsAreUnique", finalMessage);
        }
    }

    #endregion

    #region AllItemsAreInstancesOfType

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
    /// Thrown if an element in <paramref name="collection"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of an element in <paramref name="collection"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType(IEnumerable? collection, Type? expectedType)
        => AllItemsAreInstancesOfType(collection, expectedType, string.Empty, null);

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
    /// Thrown if an element in <paramref name="collection"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of an element in <paramref name="collection"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType(IEnumerable? collection, Type? expectedType, string? message)
        => AllItemsAreInstancesOfType(collection, expectedType, message, null);

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
    /// Thrown if an element in <paramref name="collection"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of an element in <paramref name="collection"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType(IEnumerable? collection, Type? expectedType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreInstancesOfType", "collection", string.Empty);
        CheckParameterNotNull(expectedType, "Assert.AllItemsAreInstancesOfType", "expectedType", string.Empty);

        int elementIndex = 0;
        foreach (object? element in collection)
        {
            if (element is null)
            {
                string userMessage = BuildUserMessage(message, parameters);
                string finalMessage = string.Format(CultureInfo.CurrentCulture, "Null element found at index {1}. Expected type: <{2}>. {0}", userMessage, elementIndex, expectedType);
                ThrowAssertFailed("Assert.AllItemsAreInstancesOfType", finalMessage);
            }

            if (!expectedType.IsInstanceOfType(element))
            {
                string userMessage = BuildUserMessage(message, parameters);
                string finalMessage = string.Format(CultureInfo.CurrentCulture, "Element at index {1} is not of expected type. Expected: <{2}>. Actual: <{3}>. {0}", userMessage, elementIndex, expectedType, element.GetType());
                ThrowAssertFailed("Assert.AllItemsAreInstancesOfType", finalMessage);
            }

            elementIndex++;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to find duplicate elements in a collection.
    /// </summary>
    /// <param name="collection">The collection to check for duplicates.</param>
    /// <param name="duplicateElement">The first duplicate element found.</param>
    /// <returns>True if a duplicate is found, false otherwise.</returns>
    internal static bool FindDuplicateElement(IEnumerable? collection, out object? duplicateElement)
    {
        duplicateElement = null;

        if (collection is null)
        {
            return false;
        }

        var seen = new HashSet<object>();
        foreach (object? element in collection)
        {
            if (element is not null && !seen.Add(element))
            {
                duplicateElement = element;
                return true;
            }
        }

        return false;
    }

    #endregion
}
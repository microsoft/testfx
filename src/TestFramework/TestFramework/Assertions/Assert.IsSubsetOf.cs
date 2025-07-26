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
    #region IsSubsetOf

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>
    /// </param>
    /// <exception cref="AssertFailedException">
    /// An element in <paramref name="subset"/> is not found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf(IEnumerable? subset, IEnumerable? superset)
        => IsSubsetOf(subset, superset, string.Empty, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in
    /// <paramref name="subset"/> is not found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// An element in <paramref name="subset"/> is not found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf(IEnumerable? subset, IEnumerable? superset, string? message)
        => IsSubsetOf(subset, superset, message, null);

    /// <summary>
    /// Tests whether one collection is a subset of another collection and
    /// throws an exception if any element in the subset is not also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>
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
    /// An element in <paramref name="subset"/> is not found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf(IEnumerable? subset, IEnumerable? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!IsSubsetOfHelper(subset, superset, out object? mismatchedElement))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, "Element <{2}> in subset was not found in superset. {0}", userMessage, subset, ReplaceNulls(mismatchedElement));
            ThrowAssertFailed("Assert.IsSubsetOf", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Every element in <paramref name="subset"/> is also found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf(IEnumerable? subset, IEnumerable? superset)
        => IsNotSubsetOf(subset, superset, string.Empty, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in
    /// <paramref name="subset"/> is also found in <paramref name="superset"/>.
    /// The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Every element in <paramref name="subset"/> is also found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf(IEnumerable? subset, IEnumerable? superset, string? message)
        => IsNotSubsetOf(subset, superset, message, null);

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and
    /// throws an exception if all elements in the subset are also in the
    /// superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>
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
    /// Every element in <paramref name="subset"/> is also found in
    /// <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf(IEnumerable? subset, IEnumerable? superset, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (IsSubsetOfHelper(subset, superset, out _))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, "Subset is a subset of superset. {0}", userMessage);
            ThrowAssertFailed("Assert.IsNotSubsetOf", finalMessage);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to determine if one collection is a subset of another.
    /// </summary>
    /// <param name="subset">The collection to test as a subset.</param>
    /// <param name="superset">The collection to test as a superset.</param>
    /// <param name="mismatchedElement">The first element in subset that is not in superset.</param>
    /// <returns>True if subset is a subset of superset, false otherwise.</returns>
    internal static bool IsSubsetOfHelper(IEnumerable? subset, IEnumerable? superset, out object? mismatchedElement)
    {
        mismatchedElement = null;

        if (subset is null)
        {
            throw new ArgumentNullException(nameof(subset));
        }

        if (superset is null)
        {
            throw new ArgumentNullException(nameof(superset));
        }

        var supersetList = superset.Cast<object>().ToList();

        foreach (object element in subset)
        {
            if (!supersetList.Contains(element))
            {
                mismatchedElement = element;
                return false;
            }
        }

        return true;
    }

    #endregion
}
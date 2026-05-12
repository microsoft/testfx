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
    public static void IsSubsetOf([NotNull] ICollection? subset, [NotNull] ICollection? superset)
        => IsSubsetOf(subset, superset, string.Empty);

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
    public static void IsSubsetOf([NotNull] ICollection? subset, [NotNull] ICollection? superset, string? message)
    {
        Assert.CheckParameterNotNull(subset, "CollectionAssert.IsSubsetOf", "subset");
        Assert.CheckParameterNotNull(superset, "CollectionAssert.IsSubsetOf", "superset");
        Tuple<bool, ICollection<object?>> isSubsetValue = IsSubsetOfHelper(subset, superset);
        if (!isSubsetValue.Item1)
        {
            string returnedSubsetValueMessage = string.Join(", ", isSubsetValue.Item2.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)));

            returnedSubsetValueMessage = string.Format(CultureInfo.InvariantCulture, FrameworkMessages.ReturnedSubsetValueMessage, returnedSubsetValueMessage);
            string userMessage = Assert.BuildUserMessage(message);
            if (string.IsNullOrEmpty(userMessage))
            {
                Assert.ReportAssertFailed("CollectionAssert.IsSubsetOf", returnedSubsetValueMessage);
            }
            else
            {
                Assert.ReportAssertFailed("CollectionAssert.IsSubsetOf", $"{returnedSubsetValueMessage} {userMessage}");
            }
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
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="subset"/> is null, or <paramref name="superset"/> is null,
    /// or all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf([NotNull] ICollection? subset, [NotNull] ICollection? superset)
        => IsNotSubsetOf(subset, superset, string.Empty);

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
    public static void IsNotSubsetOf([NotNull] ICollection? subset, [NotNull] ICollection? superset, string? message)
    {
        Assert.CheckParameterNotNull(subset, "CollectionAssert.IsNotSubsetOf", "subset");
        Assert.CheckParameterNotNull(superset, "CollectionAssert.IsNotSubsetOf", "superset");
        Tuple<bool, ICollection<object?>> isSubsetValue = IsSubsetOfHelper(subset, superset);
        if (isSubsetValue.Item1)
        {
            Assert.ReportAssertFailed("CollectionAssert.IsNotSubsetOf", Assert.BuildUserMessage(message));
        }
    }

    #endregion
}

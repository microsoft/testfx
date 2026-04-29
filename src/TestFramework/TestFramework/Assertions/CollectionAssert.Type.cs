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
    public static void AllItemsAreInstancesOfType([NotNull] ICollection? collection, [NotNull] Type? expectedType)
        => AllItemsAreInstancesOfType(collection, expectedType, string.Empty);

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
    public static void AllItemsAreInstancesOfType(
        [NotNull] ICollection? collection, [NotNull] Type? expectedType, string? message)
    {
        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreInstancesOfType", "collection");
        Assert.CheckParameterNotNull(expectedType, "CollectionAssert.AllItemsAreInstancesOfType", "expectedType");
        int i = 0;
        foreach (object? element in collection)
        {
            if (element?.GetType() is { } elementType
                && !expectedType.IsAssignableFrom(elementType))
            {
                string userMessage = Assert.BuildUserMessage(message);
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ElementTypesAtIndexDontMatch,
                    userMessage,
                    i,
                    expectedType.ToString(),
                    element.GetType().ToString());
                Assert.ReportAssertFailed("CollectionAssert.AllItemsAreInstancesOfType", finalMessage);
            }

            i++;
        }
    }

    #endregion
}

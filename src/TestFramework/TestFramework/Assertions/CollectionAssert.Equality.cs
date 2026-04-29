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
    public static void AreEqual(ICollection? expected, ICollection? actual)
        => AreEqual(expected, actual, string.Empty);

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
    public static void AreEqual(ICollection? expected, ICollection? actual, string? message)
    {
        string reason = string.Empty;
        if (!AreCollectionsEqual(expected, actual, new ObjectComparer(), ref reason))
        {
            string finalMessage = ConstructFinalMessage(reason, message);
            Assert.ReportAssertFailed("CollectionAssert.AreEqual", finalMessage);
        }
    }

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
    public static void AreNotEqual(ICollection? notExpected, ICollection? actual)
        => AreNotEqual(notExpected, actual, string.Empty);

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
    public static void AreNotEqual(ICollection? notExpected, ICollection? actual, string? message)
    {
        string reason = string.Empty;
        if (AreCollectionsEqual(notExpected, actual, new ObjectComparer(), ref reason))
        {
            string finalMessage = ConstructFinalMessage(reason, message);
            Assert.ReportAssertFailed("CollectionAssert.AreNotEqual", finalMessage);
        }
    }

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
    public static void AreEqual(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer)
        => AreEqual(expected, actual, comparer, string.Empty);

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
    public static void AreEqual(ICollection? expected, ICollection? actual, [NotNull] IComparer? comparer, string? message)
    {
        string reason = string.Empty;
        if (!AreCollectionsEqual(expected, actual, comparer, ref reason))
        {
            string finalMessage = ConstructFinalMessage(reason, message);
            Assert.ReportAssertFailed("CollectionAssert.AreEqual", finalMessage);
        }
    }

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
    public static void AreNotEqual(ICollection? notExpected, ICollection? actual, [NotNull] IComparer? comparer)
        => AreNotEqual(notExpected, actual, comparer, string.Empty);

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
    public static void AreNotEqual(ICollection? notExpected, ICollection? actual, [NotNull] IComparer? comparer, string? message)
    {
        string reason = string.Empty;
        if (AreCollectionsEqual(notExpected, actual, comparer, ref reason))
        {
            string finalMessage = ConstructFinalMessage(reason, message);
            Assert.ReportAssertFailed("CollectionAssert.AreNotEqual", finalMessage);
        }
    }

    #endregion
}

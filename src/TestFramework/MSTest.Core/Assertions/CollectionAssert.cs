// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// A collection of helper classes to test various conditions associated
    /// with collections within unit tests. If the condition being tested is not
    /// met, an exception is thrown.
    /// </summary>
    public sealed class CollectionAssert
    {
        private static CollectionAssert that;

        #region Singleton constructor

        private CollectionAssert()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the CollectionAssert functionality.
        /// </summary>
        /// <remarks>
        /// Users can use this to plug-in custom assertions through C# extension methods.
        /// For instance, the signature of a custom assertion provider could be "public static void AreEqualUnordered(this CollectionAssert customAssert, ICollection expected, ICollection actual)"
        /// Users could then use a syntax similar to the default assertions which in this case is "CollectionAssert.That.AreEqualUnordered(list1, list2);"
        /// More documentation is at "https://github.com/Microsoft/testfx-docs".
        /// </remarks>
        public static CollectionAssert That
        {
            get
            {
                if (that == null)
                {
                    that = new CollectionAssert();
                }

                return that;
            }
        }

        #endregion

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
        /// Thrown if <paramref name="element"/> is not found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void Contains(ICollection collection, object element)
        {
            Contains(collection, element, string.Empty, null);
        }

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
        /// Thrown if <paramref name="element"/> is not found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void Contains(ICollection collection, object element, string message)
        {
            Contains(collection, element, message, null);
        }

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
        /// Thrown if <paramref name="element"/> is not found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void Contains(ICollection collection, object element, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(collection, "CollectionAssert.Contains", "collection", string.Empty);

            foreach (object current in collection)
            {
                if (object.Equals(current, element))
                {
                    return;
                }
            }

            Assert.HandleFail("CollectionAssert.Contains", message, parameters);
        }

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
        /// Thrown if <paramref name="element"/> is found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void DoesNotContain(ICollection collection, object element)
        {
            DoesNotContain(collection, element, string.Empty, null);
        }

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
        /// Thrown if <paramref name="element"/> is found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void DoesNotContain(ICollection collection, object element, string message)
        {
            DoesNotContain(collection, element, message, null);
        }

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
        /// Thrown if <paramref name="element"/> is found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void DoesNotContain(ICollection collection, object element, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(collection, "CollectionAssert.DoesNotContain", "collection", string.Empty);

            foreach (object current in collection)
            {
                if (object.Equals(current, element))
                {
                    Assert.HandleFail("CollectionAssert.DoesNotContain", message, parameters);
                }
            }
        }

        /// <summary>
        /// Tests whether all items in the specified collection are non-null and throws
        /// an exception if any element is null.
        /// </summary>
        /// <param name="collection">
        /// The collection in which to search for null elements.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if a null element is found in <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreNotNull(ICollection collection)
        {
            AllItemsAreNotNull(collection, string.Empty, null);
        }

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
        /// Thrown if a null element is found in <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreNotNull(ICollection collection, string message)
        {
            AllItemsAreNotNull(collection, message, null);
        }

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
        /// Thrown if a null element is found in <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreNotNull(ICollection collection, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreNotNull", "collection", string.Empty);
            foreach (object current in collection)
            {
                if (current == null)
                {
                    Assert.HandleFail("CollectionAssert.AllItemsAreNotNull", message, parameters);
                }
            }
        }

        /// <summary>
        /// Tests whether all items in the specified collection are unique or not and
        /// throws if any two elements in the collection are equal.
        /// </summary>
        /// <param name="collection">
        /// The collection in which to search for duplicate elements.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if a two or more equal elements are found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreUnique(ICollection collection)
        {
            AllItemsAreUnique(collection, string.Empty, null);
        }

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
        /// Thrown if a two or more equal elements are found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreUnique(ICollection collection, string message)
        {
            AllItemsAreUnique(collection, message, null);
        }

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
        /// Thrown if a two or more equal elements are found in
        /// <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreUnique(ICollection collection, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreUnique", "collection", string.Empty);

            message = Assert.ReplaceNulls(message);

            bool foundNull = false;
            Dictionary<object, bool> table = new Dictionary<object, bool>();
            foreach (object current in collection)
            {
                if (current == null)
                {
                    if (foundNull == false)
                    {
                        foundNull = true;
                    }
                    else
                    {
                        // Found a second occurrence of null.
                        var finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.AllItemsAreUniqueFailMsg,
                            message ?? string.Empty,
                            FrameworkMessages.Common_NullInMessages);

                        Assert.HandleFail("CollectionAssert.AllItemsAreUnique", finalMessage, parameters);
                    }
                }
                else
                {
                    if (table.ContainsKey(current))
                    {
                        string finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.AllItemsAreUniqueFailMsg,
                            message ?? string.Empty,
                            Assert.ReplaceNulls(current));

                        Assert.HandleFail("CollectionAssert.AllItemsAreUnique", finalMessage, parameters);
                    }
                    else
                    {
                        table.Add(current, true);
                    }
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
        /// <param name="subset">
        /// The collection expected to be a subset of <paramref name="superset"/>.
        /// </param>
        /// <param name="superset">
        /// The collection expected to be a superset of <paramref name="subset"/>
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if an element in <paramref name="subset"/> is not found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsSubsetOf(ICollection subset, ICollection superset)
        {
            IsSubsetOf(subset, superset, string.Empty, null);
        }

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
        /// Thrown if an element in <paramref name="subset"/> is not found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsSubsetOf(ICollection subset, ICollection superset, string message)
        {
            IsSubsetOf(subset, superset, message, null);
        }

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
        /// Thrown if an element in <paramref name="subset"/> is not found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsSubsetOf(ICollection subset, ICollection superset, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(subset, "CollectionAssert.IsSubsetOf", "subset", string.Empty);
            Assert.CheckParameterNotNull(superset, "CollectionAssert.IsSubsetOf", "superset", string.Empty);
            if (!IsSubsetOfHelper(subset, superset))
            {
                Assert.HandleFail("CollectionAssert.IsSubsetOf", message, parameters);
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
        /// Thrown if every element in <paramref name="subset"/> is also found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsNotSubsetOf(ICollection subset, ICollection superset)
        {
            IsNotSubsetOf(subset, superset, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when every element in
        /// <paramref name="subset"/> is also found in <paramref name="superset"/>.
        /// The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if every element in <paramref name="subset"/> is also found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsNotSubsetOf(ICollection subset, ICollection superset, string message)
        {
            IsNotSubsetOf(subset, superset, message, null);
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
        /// <param name="message">
        /// The message to include in the exception when every element in
        /// <paramref name="subset"/> is also found in <paramref name="superset"/>.
        /// The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if every element in <paramref name="subset"/> is also found in
        /// <paramref name="superset"/>.
        /// </exception>
        public static void IsNotSubsetOf(ICollection subset, ICollection superset, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(subset, "CollectionAssert.IsNotSubsetOf", "subset", string.Empty);
            Assert.CheckParameterNotNull(superset, "CollectionAssert.IsNotSubsetOf", "superset", string.Empty);
            if (IsSubsetOfHelper(subset, superset))
            {
                Assert.HandleFail("CollectionAssert.IsNotSubsetOf", message, parameters);
            }
        }

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
        /// Thrown if an element was found in one of the collections but not
        /// the other.
        /// </exception>
        public static void AreEquivalent(ICollection expected, ICollection actual)
        {
            AreEquivalent(expected, actual, string.Empty, null);
        }

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
        /// Thrown if an element was found in one of the collections but not
        /// the other.
        /// </exception>
        public static void AreEquivalent(ICollection expected, ICollection actual, string message)
        {
            AreEquivalent(expected, actual, message, null);
        }

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
        /// Thrown if an element was found in one of the collections but not
        /// the other.
        /// </exception>
        public static void AreEquivalent(ICollection expected, ICollection actual, string message, params object[] parameters)
        {
            // Check whether one is null while the other is not.
            if ((expected == null) != (actual == null))
            {
                Assert.HandleFail("CollectionAssert.AreEquivalent", message, parameters);
            }

            // If the references are the same or both collections are null, they
            // are equivalent.
            if (ReferenceEquals(expected, actual) || expected == null)
            {
                return;
            }

            // Check whether the element counts are different.
            if (expected.Count != actual.Count)
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ElementNumbersDontMatch,
                    message == null ? string.Empty : Assert.ReplaceNulls(message),
                    expected.Count,
                    actual.Count);
                Assert.HandleFail("CollectionAssert.AreEquivalent", finalMessage, parameters);
            }

            // If both collections are empty, they are equivalent.
            if (expected.Count == 0)
            {
                return;
            }

            // Search for a mismatched element.
            if (FindMismatchedElement(expected, actual, out var expectedCount, out var actualCount, out var mismatchedElement))
            {
                var finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.ActualHasMismatchedElements,
                    message == null ? string.Empty : Assert.ReplaceNulls(message),
                    expectedCount.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    Assert.ReplaceNulls(mismatchedElement),
                    actualCount.ToString(CultureInfo.CurrentCulture.NumberFormat));
                Assert.HandleFail("CollectionAssert.AreEquivalent", finalMessage, parameters);
            }

            // All the elements and counts matched.
        }

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
        /// Thrown if the two collections contained the same elements, including
        /// the same number of duplicate occurrences of each element.
        /// </exception>
        public static void AreNotEquivalent(ICollection expected, ICollection actual)
        {
            AreNotEquivalent(expected, actual, string.Empty, null);
        }

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
        /// Thrown if the two collections contained the same elements, including
        /// the same number of duplicate occurrences of each element.
        /// </exception>
        public static void AreNotEquivalent(ICollection expected, ICollection actual, string message)
        {
            AreNotEquivalent(expected, actual, message, null);
        }

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
        /// Thrown if the two collections contained the same elements, including
        /// the same number of duplicate occurrences of each element.
        /// </exception>
        public static void AreNotEquivalent(ICollection expected, ICollection actual, string message, params object[] parameters)
        {
            // Check whether one is null while the other is not.
            if ((expected == null) != (actual == null))
            {
                return;
            }

            // If the references are the same or both collections are null, they
            // are equivalent. object.ReferenceEquals will handle case where both are null.
            if (ReferenceEquals(expected, actual))
            {
                var finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.BothCollectionsSameReference,
                    message == null ? string.Empty : Assert.ReplaceNulls(message));
                Assert.HandleFail("CollectionAssert.AreNotEquivalent", finalMessage, parameters);
            }

            // Check whether the element counts are different.
            if (expected.Count != actual.Count)
            {
                return;
            }

            // If both collections are empty, they are equivalent.
            if (expected.Count == 0)
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.BothCollectionsEmpty,
                    message == null ? string.Empty : Assert.ReplaceNulls(message));
                Assert.HandleFail("CollectionAssert.AreNotEquivalent", finalMessage, parameters);
            }

            // Search for a mismatched element.
            if (!FindMismatchedElement(expected, actual, out var expectedCount, out var actualCount, out var mismatchedElement))
            {
                var finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.BothSameElements,
                    message == null ? string.Empty : Assert.ReplaceNulls(message));
                Assert.HandleFail("CollectionAssert.AreNotEquivalent", finalMessage, parameters);
            }
        }

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
        /// Thrown if an element in <paramref name="collection"/> is null or
        /// <paramref name="expectedType"/> is not in the inheritance hierarchy
        /// of an element in <paramref name="collection"/>.
        /// </exception>
        public static void AllItemsAreInstancesOfType(ICollection collection, Type expectedType)
        {
            AllItemsAreInstancesOfType(collection, expectedType, string.Empty, null);
        }

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
        public static void AllItemsAreInstancesOfType(ICollection collection, Type expectedType, string message)
        {
            AllItemsAreInstancesOfType(collection, expectedType, message, null);
        }

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
        public static void AllItemsAreInstancesOfType(ICollection collection, Type expectedType, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreInstancesOfType", "collection", string.Empty);
            Assert.CheckParameterNotNull(expectedType, "CollectionAssert.AllItemsAreInstancesOfType", "expectedType", string.Empty);
            int i = 0;
            foreach (object element in collection)
            {
                var elementTypeInfo = element?.GetType().GetTypeInfo();
                var expectedTypeInfo = expectedType?.GetTypeInfo();
                if (expectedTypeInfo != null && elementTypeInfo != null && !expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
                {
                    var finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.ElementTypesAtIndexDontMatch,
                        message == null ? string.Empty : Assert.ReplaceNulls(message),
                        i,
                        expectedType.ToString(),
                        element.GetType().ToString());

                    Assert.HandleFail("CollectionAssert.AllItemsAreInstancesOfType", finalMessage, parameters);
                }

                i++;
            }
        }

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
        public static void AreEqual(ICollection expected, ICollection actual)
        {
            AreEqual(expected, actual, string.Empty, null);
        }

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
        public static void AreEqual(ICollection expected, ICollection actual, string message)
        {
            AreEqual(expected, actual, message, null);
        }

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
        public static void AreEqual(ICollection expected, ICollection actual, string message, params object[] parameters)
        {
            string reason = string.Empty;
            if (!AreCollectionsEqual(expected, actual, new ObjectComparer(), ref reason))
            {
                Assert.HandleFail("CollectionAssert.AreEqual", string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CollectionEqualReason, message, reason), parameters);
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
        public static void AreNotEqual(ICollection notExpected, ICollection actual)
        {
            AreNotEqual(notExpected, actual, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="actual"/>
        /// is equal to <paramref name="notExpected"/>. The message is shown in
        /// test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(ICollection notExpected, ICollection actual, string message)
        {
            AreNotEqual(notExpected, actual, message, null);
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
        public static void AreNotEqual(ICollection notExpected, ICollection actual, string message, params object[] parameters)
        {
            string reason = string.Empty;
            if (AreCollectionsEqual(notExpected, actual, new ObjectComparer(), ref reason))
            {
                Assert.HandleFail("CollectionAssert.AreNotEqual", string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CollectionEqualReason, message, reason), parameters);
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
        public static void AreEqual(ICollection expected, ICollection actual, IComparer comparer)
        {
            AreEqual(expected, actual, comparer, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="actual"/>
        /// is not equal to <paramref name="expected"/>. The message is shown in
        /// test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to
        /// <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(ICollection expected, ICollection actual, IComparer comparer, string message)
        {
            AreEqual(expected, actual, comparer, message, null);
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
        public static void AreEqual(ICollection expected, ICollection actual, IComparer comparer, string message, params object[] parameters)
        {
            string reason = string.Empty;
            if (!AreCollectionsEqual(expected, actual, comparer, ref reason))
            {
                Assert.HandleFail("CollectionAssert.AreEqual", string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CollectionEqualReason, message, reason), parameters);
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
        public static void AreNotEqual(ICollection notExpected, ICollection actual, IComparer comparer)
        {
            AreNotEqual(notExpected, actual, comparer, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="actual"/>
        /// is equal to <paramref name="notExpected"/>. The message is shown in
        /// test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(ICollection notExpected, ICollection actual, IComparer comparer, string message)
        {
            AreNotEqual(notExpected, actual, comparer, message, null);
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
        public static void AreNotEqual(ICollection notExpected, ICollection actual, IComparer comparer, string message, params object[] parameters)
        {
            string reason = string.Empty;
            if (AreCollectionsEqual(notExpected, actual, comparer, ref reason))
            {
                Assert.HandleFail("CollectionAssert.AreNotEqual", string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CollectionEqualReason, message, reason), parameters);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determines whether the first collection is a subset of the second
        /// collection. If either set contains duplicate elements, the number
        /// of occurrences of the element in the subset must be less than or
        /// equal to the number of occurrences in the superset.
        /// </summary>
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
        internal static bool IsSubsetOfHelper(ICollection subset, ICollection superset)
        {
            // $ CONSIDER: The current algorithm counts the number of occurrences of each
            // $ CONSIDER: element in each collection and then compares the count, resulting
            // $ CONSIDER: in an algorithm of ~n*log(n) + m*log(m) + n*log(m). It should be
            // $ CONSIDER: faster to sort both collections and do an element-by-element
            // $ CONSIDER: comparison, which should result in ~n*log(n) + m*log(m) + n.

            // Count the occurrences of each object in both collections.
            Dictionary<object, int> subsetElements = GetElementCounts(subset, out var subsetNulls);
            Dictionary<object, int> supersetElements = GetElementCounts(superset, out var supersetNulls);

            if (subsetNulls > supersetNulls)
            {
                return false;
            }

            // Compare the counts of each object in the subset to the count of that object
            // in the superset.
            foreach (object element in subsetElements.Keys)
            {
                subsetElements.TryGetValue(element, out var subsetCount);
                supersetElements.TryGetValue(element, out var supersetCount);

                if (subsetCount > supersetCount)
                {
                    return false;
                }
            }

            // All the elements counts were OK.
            return true;
        }

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
        private static Dictionary<object, int> GetElementCounts(ICollection collection, out int nullCount)
        {
            Debug.Assert(collection != null, "Collection is Null.");

            Dictionary<object, int> elementCounts = new Dictionary<object, int>();
            nullCount = 0;

            foreach (object element in collection)
            {
                if (element == null)
                {
                    nullCount++;
                    continue;
                }

                elementCounts.TryGetValue(element, out var value);
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
        private static bool FindMismatchedElement(ICollection expected, ICollection actual, out int expectedCount, out int actualCount, out object mismatchedElement)
        {
            // $ CONSIDER: The current algorithm counts the number of occurrences of each
            // $ CONSIDER: element in each collection and then compares the count, resulting
            // $ CONSIDER: in an algorithm of ~n*log(n) + m*log(m) + n*log(m). It should be
            // $ CONSIDER: faster to sort both collections and do an element-by-element
            // $ CONSIDER: comparison, which should result in ~n*log(n) + m*log(m) + n.

            // Count the occurrences of each object in the both collections
            Dictionary<object, int> expectedElements = GetElementCounts(expected, out var expectedNulls);
            Dictionary<object, int> actualElements = GetElementCounts(actual, out var actualNulls);

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
            foreach (object current in expectedElements.Keys)
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

        private static bool AreCollectionsEqual(ICollection expected, ICollection actual, IComparer comparer, ref string reason)
        {
            Assert.CheckParameterNotNull(comparer, "Assert.AreCollectionsEqual", "comparer", string.Empty);
            if (!ReferenceEquals(expected, actual))
            {
                if ((expected == null) || (actual == null))
                {
                    return false;
                }

                if (expected.Count != actual.Count)
                {
                    reason = FrameworkMessages.NumberOfElementsDiff;
                    return false;
                }

                IEnumerator expectedEnum = expected.GetEnumerator();
                IEnumerator actualEnum = actual.GetEnumerator();
                int i = 0;
                while (expectedEnum.MoveNext() && actualEnum.MoveNext())
                {
                    bool areEqual = comparer.Compare(expectedEnum.Current, actualEnum.Current) == 0;
                    if (!areEqual)
                    {
                        reason = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.ElementsAtIndexDontMatch,
                            i);
                        return false;
                    }

                    i++;
                }

                // we come here only if we match
                reason = FrameworkMessages.BothCollectionsSameElements;
                return true;
            }

            reason = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.BothCollectionsSameReference, string.Empty);
            return true;
        }

        /// <summary>
        /// compares the objects using object.Equals
        /// </summary>
        private class ObjectComparer : IComparer
        {
            #region IComparer Members

            int IComparer.Compare(object x, object y)
            {
                if (!object.Equals(x, y))
                {
                    return -1;
                }

                return 0;
            }

            #endregion
        }

        #endregion
    }
}

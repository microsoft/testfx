// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public class CollectionAssertTests : TestContainer
{
    public void InstanceShouldReturnAnInstanceOfCollectionAssert() => Verify(CollectionAssert.Instance is not null);

    public void InstanceShouldCacheCollectionAssertInstance() => Verify(CollectionAssert.Instance == CollectionAssert.Instance);

    public void CollectionAssertContainsNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetMatchingElement();
        CollectionAssert.Contains(collection, element);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertContainsMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetMatchingElement();
        CollectionAssert.Contains(collection, element, "message");
        _ = collection.Count; // no warning
    }

    public void CollectionAssertContainsMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetMatchingElement();
        CollectionAssert.Contains(collection, element, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertDoesNotContainNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetNotMatchingElement();
        CollectionAssert.DoesNotContain(collection, element);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertDoesNotContainMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetNotMatchingElement();
        CollectionAssert.DoesNotContain(collection, element, "message");
        _ = collection.Count; // no warning
    }

    public void CollectionAssertDoesNotContainMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        object? element = GetNotMatchingElement();
        CollectionAssert.DoesNotContain(collection, element, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreNotNullNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreNotNull(collection);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreNotNullMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreNotNull(collection, "message");
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreNotNullMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreNotNull(collection, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreUniqueNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreUnique(collection);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreUniqueMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreUnique(collection, "message");
        _ = collection.Count; // no warning
    }

    public void CollectionAssertAllItemsAreUniqueMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        CollectionAssert.AllItemsAreUnique(collection, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
    }

    public void CollectionAssertIsSubsetOfNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetMatchingSuperSet();
        CollectionAssert.IsSubsetOf(collection, superset);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsSubsetOfMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetMatchingSuperSet();
        CollectionAssert.IsSubsetOf(collection, superset, "message");
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsSubsetOfMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetMatchingSuperSet();
        CollectionAssert.IsSubsetOf(collection, superset, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsNotSubsetOfNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetNotMatchingSuperSet();
        CollectionAssert.IsNotSubsetOf(collection, superset);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsNotSubsetOfMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetNotMatchingSuperSet();
        CollectionAssert.IsNotSubsetOf(collection, superset, "message");
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsNotSubsetOfMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetNotMatchingSuperSet();
        CollectionAssert.IsNotSubsetOf(collection, superset, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertAllItemsAreInstancesOfTypeNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        Type? type = GetStringType();
        CollectionAssert.AllItemsAreInstancesOfType(collection, type);
        _ = collection.Count; // no warning
        type.ToString(); // no warning
    }

    public void CollectionAssertAllItemsAreInstancesOfTypeMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        Type? type = GetStringType();
        CollectionAssert.AllItemsAreInstancesOfType(collection, type, "message");
        _ = collection.Count; // no warning
        type.ToString(); // no warning
    }

    public void CollectionAssertAllItemsAreInstancesOfTypeMessageParametersNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        Type? type = GetStringType();
        CollectionAssert.AllItemsAreInstancesOfType(collection, type, "message format {0} {1}", 1, 2);
        _ = collection.Count; // no warning
        type.ToString(); // no warning
    }

    public void CollectionAssertAreEqualComparerNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetCollection();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreEqual(collection1, collection2, comparer);
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreEqual_WithIgnoreCaseComparer_DoesNotThrow()
    {
        List<string> expected = ["one", "two"];
        List<string> actual = ["ONE", "tWo"];
        CollectionAssert.AreEqual(expected, actual, StringComparer.OrdinalIgnoreCase);
    }

    public void CollectionAssertAreEqual_WithNestedDiffSizedArrays_Fails()
    {
        int[][] expected = [[1, 2], [3, 4], [5, 6], [7, 8], [9]];
        int[][] actual = [[1, 2], [999, 999, 999, 999, 999], [5, 6], [], [9]];
        VerifyThrows(() => CollectionAssert.AreEqual(expected, actual));
    }

    public void CollectionAssertAreEqual_WithCaseSensetiveComparer_Fails()
    {
        List<string> expected = ["one", "two"];
        List<string> actual = ["ONE", "tWo"];
        VerifyThrows(() => CollectionAssert.AreEqual(expected, actual, StringComparer.Ordinal));
    }

    public void CollectionAssertAreEqualComparerMessageNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetCollection();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreEqual(collection1, collection2, comparer, "message");
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreEqualComparerMessageParametersNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetCollection();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreEqual(collection1, collection2, comparer, "message format {0} {1}", 1, 2);
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreEqual_EqualNestedLists_Passes()
    {
        ICollection? collection1 = GetNestedLists();
        ICollection? collection2 = GetNestedLists();

        CollectionAssert.AreEqual(collection1, collection2);
    }

    public void CollectionAssertAreEqual_EqualDeeplyNestedLists_Passes()
    {
        ICollection collection1 = GenerateDeeplyNestedCollection(5);
        ICollection collection2 = GenerateDeeplyNestedCollection(5);

        CollectionAssert.AreEqual(collection1, collection2);
    }

    public void CollectionAssertAreEqual_NotEqualNestedLists_Fails()
    {
        ICollection? collection1 = GetNestedLists();
        ICollection? collection2 = GetNotMatchingNestedLists();

        VerifyThrows(() => CollectionAssert.AreEqual(collection1, collection2));
    }

    public void CollectionAssertAreEqual_EqualNonICollectionInnerCollection_Passes()
    {
        ICollection? collection1 = GetNonICollectionInnerCollection();
        ICollection? collection2 = GetNonICollectionInnerCollection();

        CollectionAssert.AreEqual(collection1, collection2);
    }

    public void CollectionAssertAreEqual_NotEqualNonICollectionInnerCollection_Fails()
    {
        ICollection? collection1 = GetNonICollectionInnerCollection();
        ICollection? collection2 = GetNotMatchingGetNonICollectionInnerCollection();

        VerifyThrows(() => CollectionAssert.AreEqual(collection1, collection2));
    }

    public void CollectionAssertAreNotEqual_NotEqualNestedLists_Passes()
    {
        ICollection? collection1 = GetNestedLists();
        ICollection? collection2 = GetNotMatchingNestedLists();

        CollectionAssert.AreNotEqual(collection1, collection2);
    }

    public void CollectionAssertAreNotEqual_WithIgnoreCaseComparer_Fails()
    {
        List<string> expected = ["one", "two"];
        List<string> actual = ["ONE", "tWo"];
        VerifyThrows(() => CollectionAssert.AreNotEqual(expected, actual, StringComparer.OrdinalIgnoreCase));
    }

    public void CollectionAssertAreNotEqual_WithCaseSensitiveComparer_Passes()
    {
        List<string> expected = ["one", "two"];
        List<string> actual = ["ONE", "tWo"];
        CollectionAssert.AreNotEqual(expected, actual, StringComparer.Ordinal);
    }

    public void CollectionAssertAreNotEqual_EqualNestedLists_Fails()
    {
        ICollection? collection1 = GetNestedLists();
        ICollection? collection2 = GetNestedLists();

        VerifyThrows(() => CollectionAssert.AreNotEqual(collection1, collection2));
    }

    public void CollectionAssertAreNotEqual_EqualNonICollectionInnerCollection_Fails()
    {
        ICollection? collection1 = GetNonICollectionInnerCollection();
        ICollection? collection2 = GetNonICollectionInnerCollection();

        VerifyThrows(() => CollectionAssert.AreNotEqual(collection1, collection2));
    }

    public void CollectionAssertAreNotEqual_NotEqualNonICollectionInnerCollection_Passes()
    {
        ICollection? collection1 = GetNonICollectionInnerCollection();
        ICollection? collection2 = GetNotMatchingGetNonICollectionInnerCollection();

        CollectionAssert.AreNotEqual(collection1, collection2);
    }

    public void CollectionAssertAreNotEqual_NotEqualDeeplyNestedLists_Passes()
    {
        ICollection collection1 = GenerateDeeplyNestedCollection(5);
        ICollection collection2 = GenerateDeeplyNestedCollection(4);

        CollectionAssert.AreNotEqual(collection1, collection2);
    }

    public void CollectionAssertAreNotEqualComparerNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreNotEqual(collection1, collection2, comparer);
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreNotEqualComparerMessageNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreNotEqual(collection1, collection2, comparer, "message");
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreNotEqualComparerMessageParametersNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreNotEqual(collection1, collection2, comparer, "message format {0} {1}", 1, 2);
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreEquivalent_SameItemsWithDifferentOrder_DoesNotThrow()
    {
        ICollection? collection1 = GetMatchingSuperSet();
        ICollection? collection2 = GetReversedMatchingSuperSet();
        CollectionAssert.AreEquivalent(collection1, collection2);
    }

    public void CollectionAssertAreEquivalent_WithMatchingNullableSets_DoesNotThrow()
    {
        ICollection retSetWithNulls = new[] { "item", null };

        ICollection getMatchingSetWithNulls = new[] { "item", null };

        CollectionAssert.AreEquivalent(retSetWithNulls, getMatchingSetWithNulls);
    }

    public void CollectionAssertAreEquivalent_FailWhenNotEquivalent_WithMessage()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreEquivalent(collection1, collection2, "message"));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreEquivalent_FailWhenNotEquivalent_WithMessageAndParams()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreEquivalent(collection1, collection2, "message format {0} {1}", 1, 2));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreEquivalent_WithInsensitiveCaseComparer_DoesNotThrow()
    {
        ICollection? collection1 = GetMatchingSuperSet();
        ICollection? collection2 = GetLettersCaseMismatchingSuperSet();
        CollectionAssert.AreEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveEqualityComparer());
    }

    public void CollectionAssertAreEquivalent_FailsWithInsensitiveCaseComparer_WithMessage()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetLettersCaseMismatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveEqualityComparer(), "message"));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreEquivalent_FailsWithInsensitiveCaseComparer_WithMessageAndParams()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetLettersCaseMismatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveEqualityComparer(), "message format {0} {1}", 1, 2));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreNotEquivalent_SameItemsWithDifferentOrder_DoesNotThrow()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        CollectionAssert.AreNotEquivalent(collection1, collection2);
    }

    public void CollectionAssertAreNotEquivalent_FailWhenNotEquivalent_WithMessage()
    {
        ICollection? collection1 = GetReversedMatchingSuperSet();
        ICollection? collection2 = GetMatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreNotEquivalent(collection1, collection2, "message"));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreNotEquivalent_FailWhenNotEquivalent_WithMessageAndParams()
    {
        ICollection? collection1 = GetReversedMatchingSuperSet();
        ICollection? collection2 = GetMatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreNotEquivalent(collection1, collection2, "message format {0} {1}", 1, 2));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreNotEquivalent_WithInsensitiveCaseComparer_DoesNotThrow()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperSet();
        CollectionAssert.AreNotEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), EqualityComparer<object>.Default);
    }

    public void CollectionAssertAreNotEquivalent_FailsWithInsensitiveCaseComparer_WithMessage()
    {
        ICollection? collection1 = GetMatchingSuperSet();
        ICollection? collection2 = GetLettersCaseMismatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreNotEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveNotEqualityComparer(), "message"));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreNotEquivalent_FailsWithInsensitiveCaseComparer_WithMessageAndParams()
    {
        ICollection? collection1 = GetMatchingSuperSet();
        ICollection? collection2 = GetLettersCaseMismatchingSuperSet();
        Exception ex = VerifyThrows(() => CollectionAssert.AreNotEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveNotEqualityComparer(), "message format {0} {1}", 1, 2));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreNotEquivalent_FailsWithTwoNullsAndComparer_WithMessageAndParams()
    {
        Exception ex = VerifyThrows(() => CollectionAssert.AreNotEquivalent(null, null, new CaseInsensitiveNotEqualityComparer(), "message format {0} {1}", 1, 2));
        Verify(ex.Message.Contains("message"));
    }

    public void CollectionAssertAreEqualWithoutUserMessage_FailsWithGoodMessage()
    {
        Exception ex = VerifyThrows(() => CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { 1, 5, 3 }));
        Assert.AreEqual(
            """
            CollectionAssert.AreEqual failed. Element at index 1 do not match.
            Expected: 2
            Actual: 5
            """, ex.Message);
    }

    public void CollectionAssertAreEqualWithUserMessage_FailsWithGoodMessage()
    {
        Exception ex = VerifyThrows(() => CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { 1, 5, 3 }, "User-provided message"));
        Assert.AreEqual(
            """
            CollectionAssert.AreEqual failed. User-provided message. Element at index 1 do not match.
            Expected: 2
            Actual: 5
            """, ex.Message);
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance

    private static List<object> GenerateDeeplyNestedCollection(int depth)
    {
        if (depth == 0)
        {
            return [new ReadOnlyCollection<int>([.. Enumerable.Range(1, 10)])];
        }

        var nestedCollection = new List<object>();
        for (int i = 0; i < 10; i++)
        {
            nestedCollection.Add(GenerateDeeplyNestedCollection(depth - 1));
        }

        return nestedCollection;
    }

    private ICollection? GetCollection() => new[] { "item" };

    private object? GetMatchingElement() => "item";

    private object? GetNotMatchingElement() => "not found";

    private ICollection? GetMatchingSuperSet() => new[] { "item", "item2" };

    private ICollection? GetLettersCaseMismatchingSuperSet() => new[] { "Item", "iTem2" };

    private ICollection? GetReversedMatchingSuperSet() => new[] { "item2", "item" };

    private ICollection? GetNotMatchingSuperSet() => new[] { "item3" };

    private ICollection? GetNestedLists() => new List<List<int>>
        {
            new() { 1, 2, 3 },
            new() { 4, 5, 6 },
        };

    private ICollection? GetNotMatchingNestedLists() => new List<List<int>>
        {
            new() { 4, 5, 6 },
            new() { 1, 2, 3 },
        };

    private ICollection? GetNonICollectionInnerCollection() => new List<ReadOnlyCollection<int>>
        {
            new([1, 2]),
            new([3, 4]),
        };

    private ICollection? GetNotMatchingGetNonICollectionInnerCollection() => new List<ReadOnlyCollection<int>>
        {
            new([6, 5]),
            new([3, 4]),
        };

    private Type? GetStringType() => typeof(string);

    private IComparer? GetComparer() => new ObjectComparer();
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

    private class ObjectComparer : IComparer
    {
        int IComparer.Compare(object? x, object? y) => Equals(x, y) ? 0 : -1;
    }

    private class CaseInsensitiveEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(string obj) => obj.ToUpperInvariant().GetHashCode();
    }

    private class CaseInsensitiveNotEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => !string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(string obj) => obj.ToUpperInvariant().GetHashCode();
    }

    #region Obsolete methods tests
#if DEBUG
    public void ObsoleteEqualsMethodThrowsAssertFailedException()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Exception ex = VerifyThrows(() => CollectionAssert.Equals("test", "test"));
#pragma warning restore CS0618 // Type or member is obsolete
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("CollectionAssert.Equals should not be used for Assertions"));
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Exception ex = VerifyThrows(() => CollectionAssert.ReferenceEquals(obj, obj));
#pragma warning restore CS0618 // Type or member is obsolete
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("CollectionAssert.ReferenceEquals should not be used for Assertions"));
    }
#endif
    #endregion
}

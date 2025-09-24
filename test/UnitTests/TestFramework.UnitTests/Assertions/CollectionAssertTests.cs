// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public class CollectionAssertTests : TestContainer
{
    public void InstanceShouldReturnAnInstanceOfCollectionAssert() => (CollectionAssert.That is not null).Should().BeTrue();

    public void InstanceShouldCacheCollectionAssertInstance() => (CollectionAssert.That == CollectionAssert.That).Should().BeTrue();

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

    public void CollectionAssertIsSubsetOfNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetMatchingSuperset();
        CollectionAssert.IsSubsetOf(collection, superset);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsSubsetOfMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetMatchingSuperset();
        CollectionAssert.IsSubsetOf(collection, superset, "message");
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsSubsetOf_ReturnedSubsetValueMessage_ThrowExceptionMessage()
    {
        // Arrange
        ICollection? collection = GetSubsetCollection();
        ICollection? superset = GetSupersetCollection();

        // Act
        Action action = () => CollectionAssert.IsSubsetOf(collection, superset);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("CollectionAssert.IsSubsetOf failed. Element(s) <iem, a, b> is/are not present in the collection.");
    }

    public void CollectionAssertIsSubsetOf_WithMessage_ReturnedSubsetValueMessage_ThrowExceptionMessage()
    {
        // Arrange
        ICollection? collection = GetSubsetCollection();
        ICollection? superset = GetSupersetCollection();

        // Act
        Action action = () => CollectionAssert.IsSubsetOf(collection, superset, "message");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("CollectionAssert.IsSubsetOf failed. Element(s) <iem, a, b> is/are not present in the collection. message");
    }

    public void CollectionAssertIsNotSubsetOfNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetNotMatchingSuperset();
        CollectionAssert.IsNotSubsetOf(collection, superset);
        _ = collection.Count; // no warning
        _ = superset.Count; // no warning
    }

    public void CollectionAssertIsNotSubsetOfMessageNullabilityPostConditions()
    {
        ICollection? collection = GetCollection();
        ICollection? superset = GetNotMatchingSuperset();
        CollectionAssert.IsNotSubsetOf(collection, superset, "message");
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
        Action action = () => CollectionAssert.AreEqual(expected, actual);
        action.Should().Throw<Exception>();
    }

    public void CollectionAssertAreEqual_WithCaseSensetiveComparer_Fails()
    {
        List<string> expected = ["one", "two"];
        List<string> actual = ["ONE", "tWo"];
        Action action = () => CollectionAssert.AreEqual(expected, actual, StringComparer.Ordinal);
        action.Should().Throw<Exception>();
    }

    public void CollectionAssertAreEqualComparerMessageNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetCollection();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreEqual(collection1, collection2, comparer, "message");
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

        Action action = () => CollectionAssert.AreEqual(collection1, collection2);
        action.Should().Throw<Exception>();
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

        Action action = () => CollectionAssert.AreEqual(collection1, collection2);
        action.Should().Throw<Exception>();
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
        Action action = () => CollectionAssert.AreNotEqual(expected, actual, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<Exception>();
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

        Action action = () => CollectionAssert.AreNotEqual(collection1, collection2);
        action.Should().Throw<Exception>();
    }

    public void CollectionAssertAreNotEqual_EqualNonICollectionInnerCollection_Fails()
    {
        ICollection? collection1 = GetNonICollectionInnerCollection();
        ICollection? collection2 = GetNonICollectionInnerCollection();

        Action action = () => CollectionAssert.AreNotEqual(collection1, collection2);
        action.Should().Throw<Exception>();
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
        ICollection? collection2 = GetMatchingSuperset();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreNotEqual(collection1, collection2, comparer);
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreNotEqualComparerMessageNullabilityPostConditions()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperset();
        IComparer? comparer = GetComparer();
        CollectionAssert.AreNotEqual(collection1, collection2, comparer, "message");
        comparer.ToString(); // no warning
    }

    public void CollectionAssertAreEquivalent_SameItemsWithDifferentOrder_DoesNotThrow()
    {
        ICollection? collection1 = GetMatchingSuperset();
        ICollection? collection2 = GetReversedMatchingSuperset();
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
        ICollection? collection2 = GetMatchingSuperset();
        Action action = () => CollectionAssert.AreEquivalent(collection1, collection2, "message");
        action.Should().Throw<Exception>().And
            .Message.Should().Contain("message");
    }

    public void CollectionAssertAreEquivalent_WithInsensitiveCaseComparer_DoesNotThrow()
    {
        ICollection? collection1 = GetMatchingSuperset();
        ICollection? collection2 = GetLettersCaseMismatchingSuperset();
        CollectionAssert.AreEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveEqualityComparer());
    }

    public void CollectionAssertAreEquivalent_FailsWithInsensitiveCaseComparer_WithMessage()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetLettersCaseMismatchingSuperset();
        Action action = () => CollectionAssert.AreEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveEqualityComparer(), "message");
        action.Should().Throw<Exception>().And
            .Message.Should().Contain("message");
    }

    public void CollectionAssertAreNotEquivalent_SameItemsWithDifferentOrder_DoesNotThrow()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperset();
        CollectionAssert.AreNotEquivalent(collection1, collection2);
    }

    public void CollectionAssertAreNotEquivalent_FailWhenNotEquivalent_WithMessage()
    {
        ICollection? collection1 = GetReversedMatchingSuperset();
        ICollection? collection2 = GetMatchingSuperset();
        Action action = () => CollectionAssert.AreNotEquivalent(collection1, collection2, "message");
        action.Should().Throw<Exception>().And
            .Message.Should().Contain("message");
    }

    public void CollectionAssertAreNotEquivalent_WithInsensitiveCaseComparer_DoesNotThrow()
    {
        ICollection? collection1 = GetCollection();
        ICollection? collection2 = GetMatchingSuperset();
        CollectionAssert.AreNotEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), EqualityComparer<object>.Default);
    }

    public void CollectionAssertAreNotEquivalent_FailsWithInsensitiveCaseComparer_WithMessage()
    {
        ICollection? collection1 = GetMatchingSuperset();
        ICollection? collection2 = GetLettersCaseMismatchingSuperset();
        Action action = () => CollectionAssert.AreNotEquivalent(collection1?.Cast<string>(), collection2?.Cast<string>(), new CaseInsensitiveNotEqualityComparer(), "message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("message");
    }

    public void CollectionAssertAreNotEquivalent_FailsWithTwoNullsAndComparer_WithMessageAndParams()
    {
        Action action = () => CollectionAssert.AreNotEquivalent(null, null, new CaseInsensitiveNotEqualityComparer(), "message format");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("message");
    }

    public void CollectionAssertAreEqualWithoutUserMessage_FailsWithGoodMessage()
    {
        Action action = () => CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { 1, 5, 3 });
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("""
            CollectionAssert.AreEqual failed. Element at index 1 do not match.
            Expected: 2
            Actual: 5
            """);
    }

    public void CollectionAssertAreEqualWithUserMessage_FailsWithGoodMessage()
    {
        Action action = () => CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { 1, 5, 3 }, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be(
            """
            CollectionAssert.AreEqual failed. User-provided message. Element at index 1 do not match.
            Expected: 2
            Actual: 5
            """);
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

    private ICollection? GetSubsetCollection() => new[] { "iem", "a", "b" };

    private object? GetMatchingElement() => "item";

    private object? GetNotMatchingElement() => "not found";

    private ICollection? GetMatchingSuperset() => new[] { "item", "item2" };

    private ICollection? GetSupersetCollection() => new[] { "item", "item2", "c", "d" };

    private ICollection? GetLettersCaseMismatchingSuperset() => new[] { "Item", "iTem2" };

    private ICollection? GetReversedMatchingSuperset() => new[] { "item2", "item" };

    private ICollection? GetNotMatchingSuperset() => new[] { "item3" };

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
        Action action = () => CollectionAssert.Equals("test", "test");
#pragma warning restore CS0618 // Type or member is obsolete
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("CollectionAssert.Equals should not be used for Assertions");
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Action action = () => CollectionAssert.ReferenceEquals(obj, obj);
#pragma warning restore CS0618 // Type or member is obsolete
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("CollectionAssert.ReferenceEquals should not be used for Assertions");
    }
#endif
    #endregion
}

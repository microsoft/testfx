// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public class CollectionAssertTests : TestContainer
{
    public void ThatShouldReturnAnInstanceOfCollectionAssert() => Verify(CollectionAssert.That is not null);

    public void ThatShouldCacheCollectionAssertInstance() => Verify(CollectionAssert.That == CollectionAssert.That);

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

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private ICollection? GetCollection() => new[] { "item" };

    private object? GetMatchingElement() => "item";

    private object? GetNotMatchingElement() => "not found";

    private ICollection? GetMatchingSuperSet() => new[] { "item", "item2" };

    private ICollection? GetNotMatchingSuperSet() => new[] { "item3" };

    private Type? GetStringType() => typeof(string);

    private IComparer? GetComparer() => new ObjectComparer();
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

    private class ObjectComparer : IComparer
    {
        int IComparer.Compare(object? x, object? y) => Equals(x, y) ? 0 : -1;
    }
}

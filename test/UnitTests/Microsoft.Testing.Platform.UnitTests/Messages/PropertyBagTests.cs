// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PropertyBagTests
{
    [TestMethod]
    public void Ctors_CorrectlyInit()
    {
        PropertyBag property = new([new DummyProperty(), PassedTestNodeStateProperty.CachedInstance]);
        Assert.IsNotNull(property._testNodeStateProperty);
        Assert.IsNotNull(property._property);

        property = new(new IProperty[] { new DummyProperty(), PassedTestNodeStateProperty.CachedInstance }.AsEnumerable());
        Assert.IsNotNull(property._testNodeStateProperty);
        Assert.IsNotNull(property._property);
    }

    [TestMethod]
    public void Ctors_With_WrongInit_ShouldFail()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = new PropertyBag([new DummyProperty(), PassedTestNodeStateProperty.CachedInstance, PassedTestNodeStateProperty.CachedInstance]));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = new PropertyBag(new IProperty[] { new DummyProperty(), PassedTestNodeStateProperty.CachedInstance, PassedTestNodeStateProperty.CachedInstance }.AsEnumerable()));
    }

    [TestMethod]
    public void Counts_ShouldBe_Correct()
    {
        PropertyBag property = new([new DummyProperty(), new DummyProperty(), PassedTestNodeStateProperty.CachedInstance]);
        Assert.AreEqual(3, property.Count);
    }

    [TestMethod]
    public void AddGet_Of_TestNodeStateProperty_Succed()
    {
        PropertyBag property = new();

        Assert.IsFalse(property.Any<TestNodeStateProperty>());
        Assert.IsEmpty(property.OfType<TestNodeStateProperty>());

        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.Single<TestNodeStateProperty>());
        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.SingleOrDefault<TestNodeStateProperty>());
        Assert.IsTrue(property.Any<TestNodeStateProperty>());
        Assert.HasCount(1, property.OfType<TestNodeStateProperty>());
    }

    [TestMethod]
    public void Add_Of_TestNodeStateProperty_More_Than_One_Time_Fail()
    {
        PropertyBag property = new();
        property.Add(PassedTestNodeStateProperty.CachedInstance);
        Assert.ThrowsExactly<InvalidOperationException>(() => property.Add(PassedTestNodeStateProperty.CachedInstance));
    }

    [TestMethod]
    public void Add_Same_Instance_More_Times_Fail()
    {
        PropertyBag property = new();
        DummyProperty dummyProperty = new();
        property.Add(dummyProperty);
        Assert.ThrowsExactly<InvalidOperationException>(() => property.Add(dummyProperty));
    }

    [TestMethod]
    public void Any_Should_Return_CorrectBoolean()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());
        property.Add(new DummyProperty());
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.IsTrue(property.Any<TestNodeStateProperty>());
        Assert.IsTrue(property.Any<DummyProperty>());
    }

    [TestMethod]
    public void SingleOrDefault_Should_Return_CorrectObject()
    {
        PropertyBag property = new();
        DummyProperty prop = new();
        property.Add(prop);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.SingleOrDefault<TestNodeStateProperty>());
        Assert.AreEqual(prop, property.SingleOrDefault<DummyProperty>());
        Assert.IsNull(property.SingleOrDefault<DummyProperty2>());

        property.Add(new DummyProperty());
        Assert.ThrowsExactly<InvalidOperationException>(property.SingleOrDefault<DummyProperty>);
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_TestNodeStateProperty_WhenPresent()
    {
        PropertyBag property = new();
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        // Exercises the _testNodeStateProperty fast path.
        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.FirstOrDefault<TestNodeStateProperty>());
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_Null_WhenTestNodeStateProperty_NotPresent()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());

        // Exercises the IsAssignableFrom early-return without walking the linked list.
        Assert.IsNull(property.FirstOrDefault<TestNodeStateProperty>());
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_Null_WhenSubtype_NotPresent()
    {
        PropertyBag property = new();
        property.Add(new FailedTestNodeStateProperty());

        // Exercises the subtype guard: asking for a different TestNodeStateProperty subtype must return null.
        Assert.IsNull(property.FirstOrDefault<PassedTestNodeStateProperty>());
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_CorrectObject_WhenSingleMatchExists()
    {
        PropertyBag property = new();
        DummyProperty prop = new();
        property.Add(prop);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(prop, property.FirstOrDefault<DummyProperty>());
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_FirstObject_WhenMultipleMatchesExist()
    {
        PropertyBag property = new();
        DummyProperty prop1 = new();
        DummyProperty prop2 = new();
        property.Add(prop1);
        property.Add(prop2);

        // Unlike SingleOrDefault, FirstOrDefault must NOT throw when multiple matches exist.
        DummyProperty? result = property.FirstOrDefault<DummyProperty>();
        Assert.IsNotNull(result);
        Assert.IsTrue(result == prop1 || result == prop2);
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_Null_WhenNoMatchExists()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());

        // Exercises the linked-list miss path.
        Assert.IsNull(property.FirstOrDefault<DummyProperty2>());
    }

    [TestMethod]
    public void FirstOrDefault_Should_Return_Null_WhenBagIsEmpty()
    {
        PropertyBag property = new();

        Assert.IsNull(property.FirstOrDefault<DummyProperty>());
        Assert.IsNull(property.FirstOrDefault<TestNodeStateProperty>());
    }

    [TestMethod]
    public void Single_Should_Return_CorrectObject()
    {
        PropertyBag property = new();
        DummyProperty prop = new();
        property.Add(prop);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.Single<TestNodeStateProperty>());
        Assert.AreEqual(prop, property.Single<DummyProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(property.Single<DummyProperty2>);

        property.Add(new DummyProperty());
        Assert.ThrowsExactly<InvalidOperationException>(property.Single<DummyProperty>);
    }

    [TestMethod]
    public void OfType_Should_Return_CorrectObject()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());
        property.Add(new DummyProperty());
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.OfType<TestNodeStateProperty>().Single());
        Assert.HasCount(2, property.OfType<DummyProperty>());

        // No DummyProperty2 in the bag — exercises the "no match found" path in the while-loop
        Assert.IsEmpty(property.OfType<DummyProperty2>());
    }

    [TestMethod]
    public void OfType_WithSingleMatch_ReturnsSingleItemArray()
    {
        PropertyBag property = new();
        DummyProperty singleProperty = new();
        property.Add(singleProperty);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        DummyProperty[] result = property.OfType<DummyProperty>();

        Assert.HasCount(1, result);
        Assert.AreSame(singleProperty, result[0]);
    }

    [TestMethod]
    public void OfType_WithOnlyTestNodeStateProperty_ReturnsEmpty()
    {
        PropertyBag property = new();
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        // _property is null; _testNodeStateProperty is set — exercises the new early-return path
        Assert.IsEmpty(property.OfType<DummyProperty>());
    }

    [TestMethod]
    public void AsEnumerable_Should_Return_CorrectItems()
    {
        PropertyBag property = new();
        DummyProperty objA = new();
        DummyProperty objB = new();
        property.Add(objA);
        property.Add(objB);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.HasCount(3, property.AsEnumerable());

        var list = property.AsEnumerable().ToList();
        foreach (IProperty prop in list.ToArray())
        {
            list.Remove(prop);
        }

        Assert.IsEmpty(list);

        list = [.. property.AsEnumerable()];
        foreach (IProperty prop in property)
        {
            list.Remove(prop);
        }

        Assert.IsEmpty(list);
    }

    [TestMethod]
    public void EmptyProperties_Should_NotFail()
    {
        PropertyBag property = new();
        Assert.AreEqual(0, property.Count);
        Assert.IsFalse(property.Any<TestNodeStateProperty>());
        Assert.IsNull(property.SingleOrDefault<TestNodeStateProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(property.Single<TestNodeStateProperty>);
        Assert.IsEmpty(property.OfType<TestNodeStateProperty>());
        Assert.IsEmpty(property.AsEnumerable());

        foreach (IProperty item in property)
        {
            Assert.Fail("no item expected");
        }
    }

    private sealed class DummyProperty : IProperty;

    private sealed class DummyProperty2 : IProperty;
}

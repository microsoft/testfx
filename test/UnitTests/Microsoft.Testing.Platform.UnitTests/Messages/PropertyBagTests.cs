// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class PropertyBagTests : TestBase
{
    public PropertyBagTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void Ctors_CorrectlyInit()
    {
        PropertyBag property = new([new DummyProperty(), PassedTestNodeStateProperty.CachedInstance]);
        Assert.IsNotNull(property._testNodeStateProperty);
        Assert.IsNotNull(property._property);

        property = new(new IProperty[] { new DummyProperty(), PassedTestNodeStateProperty.CachedInstance }.AsEnumerable());
        Assert.IsNotNull(property._testNodeStateProperty);
        Assert.IsNotNull(property._property);
    }

    public void Ctors_With_WrongInit_ShouldFail()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new PropertyBag([new DummyProperty(), PassedTestNodeStateProperty.CachedInstance, PassedTestNodeStateProperty.CachedInstance]));
        Assert.Throws<InvalidOperationException>(() => _ = new PropertyBag(new IProperty[] { new DummyProperty(), PassedTestNodeStateProperty.CachedInstance, PassedTestNodeStateProperty.CachedInstance }.AsEnumerable()));
    }

    public void Counts_ShouldBe_Correct()
    {
        PropertyBag property = new([new DummyProperty(), new DummyProperty(), PassedTestNodeStateProperty.CachedInstance]);
        Assert.AreEqual(3, property.Count);
    }

    public void AddGet_Of_TestNodeStateProperty_Succed()
    {
        PropertyBag property = new();

        Assert.IsFalse(property.Any<TestNodeStateProperty>());
        Assert.AreEqual(0, property.OfType<TestNodeStateProperty>().Length);

        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.Single<TestNodeStateProperty>());
        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.SingleOrDefault<TestNodeStateProperty>());
        Assert.IsTrue(property.Any<TestNodeStateProperty>());
        Assert.AreEqual(1, property.OfType<TestNodeStateProperty>().Length);
    }

    public void Add_Of_TestNodeStateProperty_More_Than_One_Time_Fail()
    {
        PropertyBag property = new();
        property.Add(PassedTestNodeStateProperty.CachedInstance);
        Assert.Throws<InvalidOperationException>(() => property.Add(PassedTestNodeStateProperty.CachedInstance));
    }

    public void Add_Same_Instance_More_Times_Fail()
    {
        PropertyBag property = new();
        DummyProperty dummyProperty = new();
        property.Add(dummyProperty);
        Assert.Throws<InvalidOperationException>(() => property.Add(dummyProperty));
    }

    public void Any_Should_Return_CorrectBoolean()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());
        property.Add(new DummyProperty());
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.IsTrue(property.Any<TestNodeStateProperty>());
        Assert.IsTrue(property.Any<DummyProperty>());
    }

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
        Assert.Throws<InvalidOperationException>(() => property.SingleOrDefault<DummyProperty>());
    }

    public void Single_Should_Return_CorrectObject()
    {
        PropertyBag property = new();
        DummyProperty prop = new();
        property.Add(prop);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.Single<TestNodeStateProperty>());
        Assert.AreEqual(prop, property.Single<DummyProperty>());
        Assert.Throws<InvalidOperationException>(() => property.Single<DummyProperty2>());

        property.Add(new DummyProperty());
        Assert.Throws<InvalidOperationException>(() => property.Single<DummyProperty>());
    }

    public void OfType_Should_Return_CorrectObject()
    {
        PropertyBag property = new();
        property.Add(new DummyProperty());
        property.Add(new DummyProperty());
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(PassedTestNodeStateProperty.CachedInstance, property.OfType<TestNodeStateProperty>().Single());
        Assert.AreEqual(2, property.OfType<DummyProperty>().Length);
    }

    public void AsEnumerable_Should_Return_CorrectItems()
    {
        PropertyBag property = new();
        DummyProperty objA = new();
        DummyProperty objB = new();
        property.Add(objA);
        property.Add(objB);
        property.Add(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(3, property.AsEnumerable().Count());

        var list = property.AsEnumerable().ToList();
        foreach (IProperty prop in list.ToArray())
        {
            list.Remove(prop);
        }

        Assert.IsEmpty(list);

        list = property.AsEnumerable().ToList();
        foreach (IProperty prop in property)
        {
            list.Remove(prop);
        }

        Assert.IsEmpty(list);
    }

    public void EmptyProperties_Should_NotFail()
    {
        PropertyBag property = new();
        Assert.AreEqual(0, property.Count);
        Assert.IsFalse(property.Any<TestNodeStateProperty>());
        Assert.IsNull(property.SingleOrDefault<TestNodeStateProperty>());
        Assert.Throws<InvalidOperationException>(() => property.Single<TestNodeStateProperty>());
        Assert.AreEqual(0, property.OfType<TestNodeStateProperty>().Length);
        Assert.AreEqual(0, property.AsEnumerable().Count());

        foreach (IProperty item in property)
        {
            Assert.IsTrue(false, "no item expected");
        }
    }

    private sealed class DummyProperty : IProperty;

    private sealed class DummyProperty2 : IProperty;
}

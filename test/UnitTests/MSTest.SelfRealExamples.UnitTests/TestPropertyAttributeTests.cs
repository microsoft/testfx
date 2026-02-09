// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
[TestProperty("DummyTestClassBaseKey1", "DummyTestClassBaseValue1")]
[TestProperty("DummyTestClassBaseKey2", "DummyTestClassBaseValue2")]
public class TestPropertyAttributeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestProperty("TestMethodKeyFromBase", "TestMethodValueFromBase")]
    public virtual void VirtualTestMethodInBaseAndDerived()
    {
        Assert.IsTrue(TestContext.Properties.TryGetValue("TestMethodKeyFromBase", out object? value1));
        Assert.AreEqual("TestMethodValueFromBase", value1);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey1", out object? value2));
        Assert.AreEqual("DummyTestClassBaseValue1", value2);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey2", out object? value3));
        Assert.AreEqual("DummyTestClassBaseValue2", value3);
    }

#pragma warning disable MSTEST0007 // [TestProperty] can only be set on methods marked with [TestMethod]
    [TestProperty("NonTestMethodKeyFromBase", "NonTestMethodValueFromBase")]
#pragma warning restore MSTEST0007 // [TestProperty] can only be set on methods marked with [TestMethod]
    public virtual void VirtualTestMethodInDerivedButNotTestMethodInBase()
    {
    }
}

[TestClass]
[TestProperty("DummyTestClassDerivedKey1", "DummyTestClassValue1")]
[TestProperty("DummyTestClassDerivedKey2", "DummyTestClassValue2")]
public class DerivedTestPropertyAttributeTests : TestPropertyAttributeTests
{
    [TestProperty("DerivedMethod1Key", "DerivedMethod1Value")]
    [TestMethod]
    public override void VirtualTestMethodInBaseAndDerived()
    {
        Assert.IsTrue(TestContext.Properties.TryGetValue("DerivedMethod1Key", out object? value1));
        Assert.AreEqual("DerivedMethod1Value", value1);

        Assert.IsTrue(TestContext.Properties.TryGetValue("TestMethodKeyFromBase", out object? value2));
        Assert.AreEqual("TestMethodValueFromBase", value2);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassDerivedKey1", out object? value3));
        Assert.AreEqual("DummyTestClassValue1", value3);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassDerivedKey2", out object? value4));
        Assert.AreEqual("DummyTestClassValue2", value4);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey1", out object? value5));
        Assert.AreEqual("DummyTestClassBaseValue1", value5);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey2", out object? value6));
        Assert.AreEqual("DummyTestClassBaseValue2", value6);
    }

    [TestProperty("DerivedMethod2Key", "DerivedMethod2Value")]
    [TestMethod]
    public override void VirtualTestMethodInDerivedButNotTestMethodInBase()
    {
        Assert.IsTrue(TestContext.Properties.TryGetValue("DerivedMethod2Key", out object? value1));
        Assert.AreEqual("DerivedMethod2Value", value1);

        Assert.IsTrue(TestContext.Properties.TryGetValue("NonTestMethodKeyFromBase", out object? value2));
        Assert.AreEqual("NonTestMethodValueFromBase", value2);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassDerivedKey1", out object? value3));
        Assert.AreEqual("DummyTestClassValue1", value3);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassDerivedKey2", out object? value4));
        Assert.AreEqual("DummyTestClassValue2", value4);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey1", out object? value5));
        Assert.AreEqual("DummyTestClassBaseValue1", value5);

        Assert.IsTrue(TestContext.Properties.TryGetValue("DummyTestClassBaseKey2", out object? value6));
        Assert.AreEqual("DummyTestClassBaseValue2", value6);
    }
}

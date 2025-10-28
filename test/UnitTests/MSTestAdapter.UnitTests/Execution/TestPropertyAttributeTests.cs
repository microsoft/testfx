// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestPropertyAttributeTests : TestContainer
{
    private readonly TypeCache _typeCache;

    public TestPropertyAttributeTests()
    {
        _typeCache = new TypeCache(new ReflectHelper());
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        testablePlatformServiceProvider.MockFileOperations.Setup(x => x.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>())).Returns(GetType().Assembly);
        PlatformServiceProvider.Instance = testablePlatformServiceProvider;

        ReflectHelper.Instance.ClearCache();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    #region GetTestMethodInfo tests

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassCorrectly()
    {
        string className = typeof(DummyTestClassBase).FullName;
        var testMethod = new TestMethod(nameof(DummyTestClassBase.VirtualTestMethodInBaseAndDerived), className, typeof(DummyTestClassBase).Assembly.GetName().Name, isAsync: false);

        var testContext = new TestContextImplementation(testMethod, new StringWriter(), new Dictionary<string, object>());

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext,
            false);

        Assert.IsTrue(testContext.TryGetPropertyValue("TestMethodKeyFromBase", out object value1));
        Assert.AreEqual("TestMethodValueFromBase", value1);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object value2));
        Assert.AreEqual("DummyTestClassBaseValue1", value2);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object value3));
        Assert.AreEqual("DummyTestClassBaseValue2", value3);

        TestPlatform.ObjectModel.Trait[] traits = ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassBase).GetMethod(nameof(DummyTestClassBase.VirtualTestMethodInBaseAndDerived))).ToArray();
        Assert.AreEqual(3, traits.Length);
        Assert.AreEqual("TestMethodKeyFromBase", traits[0].Name);
        Assert.AreEqual("TestMethodValueFromBase", traits[0].Value);
        Assert.AreEqual("DummyTestClassBaseKey1", traits[1].Name);
        Assert.AreEqual("DummyTestClassBaseValue1", traits[1].Value);
        Assert.AreEqual("DummyTestClassBaseKey2", traits[2].Name);
        Assert.AreEqual("DummyTestClassBaseValue2", traits[2].Value);
    }

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassAndBaseClassesAndOverriddenMethodsCorrectly_OverriddenIsTestMethod()
    {
        string className = typeof(DummyTestClassDerived).FullName;
        var testMethod = new TestMethod(nameof(DummyTestClassDerived.VirtualTestMethodInBaseAndDerived), className, typeof(DummyTestClassBase).Assembly.GetName().Name, isAsync: false);

        var testContext = new TestContextImplementation(testMethod, new StringWriter(), new Dictionary<string, object>());

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext,
            false);

        Assert.IsTrue(testContext.TryGetPropertyValue("DerivedMethod1Key", out object value1));
        Assert.AreEqual("DerivedMethod1Value", value1);

        Assert.IsTrue(testContext.TryGetPropertyValue("TestMethodKeyFromBase", out object value2));
        Assert.AreEqual("TestMethodValueFromBase", value2);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassDerivedKey1", out object value3));
        Assert.AreEqual("DummyTestClassValue1", value3);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassDerivedKey2", out object value4));
        Assert.AreEqual("DummyTestClassValue2", value4);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object value5));
        Assert.AreEqual("DummyTestClassBaseValue1", value5);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object value6));
        Assert.AreEqual("DummyTestClassBaseValue2", value6);

        TestPlatform.ObjectModel.Trait[] traits = ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassDerived).GetMethod(nameof(DummyTestClassDerived.VirtualTestMethodInBaseAndDerived))).ToArray();
        Assert.AreEqual(6, traits.Length);
        Assert.AreEqual("DerivedMethod1Key", traits[0].Name);
        Assert.AreEqual("DerivedMethod1Value", traits[0].Value);
        Assert.AreEqual("TestMethodKeyFromBase", traits[1].Name);
        Assert.AreEqual("TestMethodValueFromBase", traits[1].Value);
        Assert.AreEqual("DummyTestClassDerivedKey1", traits[2].Name);
        Assert.AreEqual("DummyTestClassValue1", traits[2].Value);
        Assert.AreEqual("DummyTestClassDerivedKey2", traits[3].Name);
        Assert.AreEqual("DummyTestClassValue2", traits[3].Value);
        Assert.AreEqual("DummyTestClassBaseKey1", traits[4].Name);
        Assert.AreEqual("DummyTestClassBaseValue1", traits[4].Value);
        Assert.AreEqual("DummyTestClassBaseKey2", traits[5].Name);
        Assert.AreEqual("DummyTestClassBaseValue2", traits[5].Value);
    }

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassAndBaseClassesAndOverriddenMethodsCorrectly_OverriddenIsNotTestMethod()
    {
        string className = typeof(DummyTestClassDerived).FullName;
        var testMethod = new TestMethod(nameof(DummyTestClassDerived.VirtualTestMethodInDerivedButNotTestMethodInBase), className, typeof(DummyTestClassBase).Assembly.GetName().Name, isAsync: false);

        var testContext = new TestContextImplementation(testMethod, new StringWriter(), new Dictionary<string, object>());

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext,
            false);

        Assert.IsTrue(testContext.TryGetPropertyValue("DerivedMethod2Key", out object value1));
        Assert.AreEqual("DerivedMethod2Value", value1);

        Assert.IsTrue(testContext.TryGetPropertyValue("NonTestMethodKeyFromBase", out object value2));
        Assert.AreEqual("NonTestMethodValueFromBase", value2);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassDerivedKey1", out object value3));
        Assert.AreEqual("DummyTestClassValue1", value3);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassDerivedKey2", out object value4));
        Assert.AreEqual("DummyTestClassValue2", value4);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object value5));
        Assert.AreEqual("DummyTestClassBaseValue1", value5);

        Assert.IsTrue(testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object value6));
        Assert.AreEqual("DummyTestClassBaseValue2", value6);

        TestPlatform.ObjectModel.Trait[] traits = ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassDerived).GetMethod(nameof(DummyTestClassDerived.VirtualTestMethodInDerivedButNotTestMethodInBase))).ToArray();
        Assert.AreEqual(6, traits.Length);
        Assert.AreEqual("DerivedMethod2Key", traits[0].Name);
        Assert.AreEqual("DerivedMethod2Value", traits[0].Value);
        Assert.AreEqual("NonTestMethodKeyFromBase", traits[1].Name);
        Assert.AreEqual("NonTestMethodValueFromBase", traits[1].Value);
        Assert.AreEqual("DummyTestClassDerivedKey1", traits[2].Name);
        Assert.AreEqual("DummyTestClassValue1", traits[2].Value);
        Assert.AreEqual("DummyTestClassDerivedKey2", traits[3].Name);
        Assert.AreEqual("DummyTestClassValue2", traits[3].Value);
        Assert.AreEqual("DummyTestClassBaseKey1", traits[4].Name);
        Assert.AreEqual("DummyTestClassBaseValue1", traits[4].Value);
        Assert.AreEqual("DummyTestClassBaseKey2", traits[5].Name);
        Assert.AreEqual("DummyTestClassBaseValue2", traits[5].Value);
    }

    #endregion
    #region dummy implementations

    [TestClass]
    [TestProperty("DummyTestClassBaseKey1", "DummyTestClassBaseValue1")]
    [TestProperty("DummyTestClassBaseKey2", "DummyTestClassBaseValue2")]
    internal class DummyTestClassBase
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestProperty("TestMethodKeyFromBase", "TestMethodValueFromBase")]
        public virtual void VirtualTestMethodInBaseAndDerived()
        {
        }

        [TestProperty("NonTestMethodKeyFromBase", "NonTestMethodValueFromBase")]
        public virtual void VirtualTestMethodInDerivedButNotTestMethodInBase()
        {
        }
    }

    [TestClass]
    [TestProperty("DummyTestClassDerivedKey1", "DummyTestClassValue1")]
    [TestProperty("DummyTestClassDerivedKey2", "DummyTestClassValue2")]
    internal class DummyTestClassDerived : DummyTestClassBase
    {
        [TestProperty("DerivedMethod1Key", "DerivedMethod1Value")]
        [TestMethod]
        public override void VirtualTestMethodInBaseAndDerived() => base.VirtualTestMethodInBaseAndDerived();

        [TestProperty("DerivedMethod2Key", "DerivedMethod2Value")]
        [TestMethod]
        public override void VirtualTestMethodInDerivedButNotTestMethodInBase() => base.VirtualTestMethodInDerivedButNotTestMethodInBase();
    }

    #endregion
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

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
        testablePlatformServiceProvider.MockFileOperations.Setup(x => x.LoadAssembly(It.IsAny<string>())).Returns(GetType().Assembly);
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

    private static TestContextImplementation CreateTestContextImplementationForMethod(TestMethod testMethod)
        => new(testMethod, null, new Dictionary<string, object?>(), null, null);

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassCorrectly()
    {
        string className = typeof(DummyTestClassBase).FullName!;
        var testMethod = new TestMethod(nameof(DummyTestClassBase.VirtualTestMethodInBaseAndDerived), className, typeof(DummyTestClassBase).Assembly.GetName().Name!, displayName: null);

        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext);

        testContext.TryGetPropertyValue("TestMethodKeyFromBase", out object? value1).Should().BeTrue();
        value1.Should().Be("TestMethodValueFromBase");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object? value2).Should().BeTrue();
        value2.Should().Be("DummyTestClassBaseValue1");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object? value3).Should().BeTrue();
        value3.Should().Be("DummyTestClassBaseValue2");

        TestPlatform.ObjectModel.Trait[] traits = [.. ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassBase).GetMethod(nameof(DummyTestClassBase.VirtualTestMethodInBaseAndDerived))!)];
        traits.Length.Should().Be(3);
        traits[0].Name.Should().Be("TestMethodKeyFromBase");
        traits[0].Value.Should().Be("TestMethodValueFromBase");
        traits[1].Name.Should().Be("DummyTestClassBaseKey1");
        traits[1].Value.Should().Be("DummyTestClassBaseValue1");
        traits[2].Name.Should().Be("DummyTestClassBaseKey2");
        traits[2].Value.Should().Be("DummyTestClassBaseValue2");
    }

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassAndBaseClassesAndOverriddenMethodsCorrectly_OverriddenIsTestMethod()
    {
        string className = typeof(DummyTestClassDerived).FullName!;
        var testMethod = new TestMethod(nameof(DummyTestClassDerived.VirtualTestMethodInBaseAndDerived), className, typeof(DummyTestClassBase).Assembly.GetName().Name!, displayName: null);

        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext);

        testContext.TryGetPropertyValue("DerivedMethod1Key", out object? value1).Should().BeTrue();
        value1.Should().Be("DerivedMethod1Value");

        testContext.TryGetPropertyValue("TestMethodKeyFromBase", out object? value2).Should().BeTrue();
        value2.Should().Be("TestMethodValueFromBase");

        testContext.TryGetPropertyValue("DummyTestClassDerivedKey1", out object? value3).Should().BeTrue();
        value3.Should().Be("DummyTestClassValue1");

        testContext.TryGetPropertyValue("DummyTestClassDerivedKey2", out object? value4).Should().BeTrue();
        value4.Should().Be("DummyTestClassValue2");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object? value5).Should().BeTrue();
        value5.Should().Be("DummyTestClassBaseValue1");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object? value6).Should().BeTrue();
        value6.Should().Be("DummyTestClassBaseValue2");

        TestPlatform.ObjectModel.Trait[] traits = [.. ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassDerived).GetMethod(nameof(DummyTestClassDerived.VirtualTestMethodInBaseAndDerived))!)];
        traits.Length.Should().Be(6);
        traits[0].Name.Should().Be("DerivedMethod1Key");
        traits[0].Value.Should().Be("DerivedMethod1Value");
        traits[1].Name.Should().Be("TestMethodKeyFromBase");
        traits[1].Value.Should().Be("TestMethodValueFromBase");
        traits[2].Name.Should().Be("DummyTestClassDerivedKey1");
        traits[2].Value.Should().Be("DummyTestClassValue1");
        traits[3].Name.Should().Be("DummyTestClassDerivedKey2");
        traits[3].Value.Should().Be("DummyTestClassValue2");
        traits[4].Name.Should().Be("DummyTestClassBaseKey1");
        traits[4].Value.Should().Be("DummyTestClassBaseValue1");
        traits[5].Name.Should().Be("DummyTestClassBaseKey2");
        traits[5].Value.Should().Be("DummyTestClassBaseValue2");
    }

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassAndBaseClassesAndOverriddenMethodsCorrectly_OverriddenIsNotTestMethod()
    {
        string className = typeof(DummyTestClassDerived).FullName!;
        var testMethod = new TestMethod(nameof(DummyTestClassDerived.VirtualTestMethodInDerivedButNotTestMethodInBase), className, typeof(DummyTestClassBase).Assembly.GetName().Name!, displayName: null);

        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        _ = _typeCache.GetTestMethodInfo(
            testMethod,
            testContext);

        testContext.TryGetPropertyValue("DerivedMethod2Key", out object? value1).Should().BeTrue();
        value1.Should().Be("DerivedMethod2Value");

        testContext.TryGetPropertyValue("NonTestMethodKeyFromBase", out object? value2).Should().BeTrue();
        value2.Should().Be("NonTestMethodValueFromBase");

        testContext.TryGetPropertyValue("DummyTestClassDerivedKey1", out object? value3).Should().BeTrue();
        value3.Should().Be("DummyTestClassValue1");

        testContext.TryGetPropertyValue("DummyTestClassDerivedKey2", out object? value4).Should().BeTrue();
        value4.Should().Be("DummyTestClassValue2");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey1", out object? value5).Should().BeTrue();
        value5.Should().Be("DummyTestClassBaseValue1");

        testContext.TryGetPropertyValue("DummyTestClassBaseKey2", out object? value6).Should().BeTrue();
        value6.Should().Be("DummyTestClassBaseValue2");

        TestPlatform.ObjectModel.Trait[] traits = [.. ReflectHelper.Instance.GetTestPropertiesAsTraits(typeof(DummyTestClassDerived).GetMethod(nameof(DummyTestClassDerived.VirtualTestMethodInDerivedButNotTestMethodInBase))!)];
        traits.Length.Should().Be(6);
        traits[0].Name.Should().Be("DerivedMethod2Key");
        traits[0].Value.Should().Be("DerivedMethod2Value");
        traits[1].Name.Should().Be("NonTestMethodKeyFromBase");
        traits[1].Value.Should().Be("NonTestMethodValueFromBase");
        traits[2].Name.Should().Be("DummyTestClassDerivedKey1");
        traits[2].Value.Should().Be("DummyTestClassValue1");
        traits[3].Name.Should().Be("DummyTestClassDerivedKey2");
        traits[3].Value.Should().Be("DummyTestClassValue2");
        traits[4].Name.Should().Be("DummyTestClassBaseKey1");
        traits[4].Value.Should().Be("DummyTestClassBaseValue1");
        traits[5].Name.Should().Be("DummyTestClassBaseKey2");
        traits[5].Value.Should().Be("DummyTestClassBaseValue2");
    }

    #endregion
    #region dummy implementations

    [TestClass]
    [TestProperty("DummyTestClassBaseKey1", "DummyTestClassBaseValue1")]
    [TestProperty("DummyTestClassBaseKey2", "DummyTestClassBaseValue2")]
    internal class DummyTestClassBase
    {
        public TestContext TestContext { get; set; } = null!;

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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestPropertyAttributeTests : TestContainer
{
    public TestPropertyAttributeTests()
    {
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

    public void GetTestMethodInfoShouldAddPropertiesFromContainingClassCorrectly()
    {
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Moq;
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TypeValidatorTests
{
    #region private variables

    private TypeValidator _typeValidator;
    private Mock<ReflectHelper> _mockReflectHelper;
    private List<string> _warnings;

    #endregion

    #region TestInit

    [TestInitialize]
    public void TestInit()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeValidator = new TypeValidator(_mockReflectHelper.Object);
        _warnings = new List<string>();
    }

    #endregion

    #region Type is class, TestClassAttribute or attribute derived from TestClassAttribute

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonClassTypes()
    {
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(IDummyInterface), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForClassesNotHavingTestClassAttributeOrDerivedAttributeTypes()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForClassesMarkedByAnAttributeDerivedFromTestClass()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
        _mockReflectHelper.Setup(
            rh => rh.HasAttributeDerivedFrom(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
        Assert.IsTrue(_typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings));
    }

    #endregion

    #region Public/Nested public test classes

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningForNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(_warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(InternalTestClass).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(_warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(OuterClass.NestedInternalClass).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsTrue(_typeValidator.IsValidTestClass(typeof(PublicTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForNestedPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsTrue(_typeValidator.IsValidTestClass(typeof(OuterClass.NestedPublicClass), _warnings));
    }

    #endregion

    #region Discovery of internal test classes enabled

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForPrivateTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        var nestedPrivateClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "NestedPrivateClass");

        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(nestedPrivateClassType, _warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForInaccessibleTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        var inaccessibleClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "InaccessiblePublicClass");

        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(inaccessibleClassType, _warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningsForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        Assert.AreEqual(0, _warnings.Count);
    }

    #endregion

    #region Generic types

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonAbstractGenericTypes()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForNonAbstractGenericTypes()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings);
        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(_warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(GenericClass<>).FullName));
    }

    #endregion

    #region TestContext signature

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), _warnings);
        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(_warnings, string.Format(Resource.UTA_ErrorInValidTestContextSignature, typeof(ClassWithTestContextGetterOnly).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForTestClassesWithValidTestContextSignature()
    {
        SetupTestClass();
        Assert.IsTrue(_typeValidator.IsValidTestClass(typeof(ClassWithTestContext), _warnings));
    }

    #endregion

    #region Abstract Test classes

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForAbstractTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldNotReportWarningsForAbstractTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForGenericAbstractTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(_typeValidator.IsValidTestClass(typeof(AbstractGenericClass<>), _warnings));
        Assert.AreEqual(0, _warnings.Count);
    }

    #endregion

    #region HasCorrectTestContext tests

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnTrueForClassesWithNoTestContextProperty()
    {
        Assert.IsTrue(TypeValidator.HasCorrectTestContextSignature(typeof(PublicTestClass)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithNoSetters()
    {
        Assert.IsFalse(TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextGetterOnly)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithPrivateSetter()
    {
        Assert.IsFalse(TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextPrivateSetter)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithStaticSetter()
    {
        Assert.IsFalse(TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithStaticTestContext)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithAbstractSetter()
    {
        Assert.IsFalse(TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithAbstractTestContext)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldNotThrowForAGenericClassWithRandomProperties()
    {
        Assert.IsTrue(TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithProperty<>)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnTrueForAGenericClassWithTestContext()
    {
        Assert.IsTrue(TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithTestContext<>)));
    }

    #endregion

    #region IsValidTestTypeTests

    [TestMethod]
    public void AllTypesContainAllPrivateClasses()
    {
        // The names of private types are not accessible by nameof or typeof, ensure that we have them in the
        // list of our test types, to avoid bugs caused by typos.
        var allTypes = GetAllTestTypes().Select(t => t.Name).ToArray();
        var privateTypes = typeof(PrivateClassNames).GetProperties().Select(n => n.Name).ToArray();
        privateTypes.Should().HaveCountGreaterOrEqualTo(1);
        var privateType = privateTypes.Should().BeSubsetOf(allTypes);
    }

    [TestMethod]
    public void TypeHasValidAccessibilityShouldReturnTrueForAllPublicTypesIncludingNestedPublicTypes()
    {
        Type[] allTypes = GetAllTestTypes();

        var expectedDiscoveredTypes = new[]
        {
            nameof(PublicClass2),
            nameof(PublicClass3),
            nameof(PublicClass2.PublicNestedClassInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass.PublicClassNestedInPublicClassNestedInPublicClass),
        };

        var discoverInternal = false;
        var actualDiscoveredTypes = allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        actualDiscoveredTypes.Should().BeEquivalentTo(expectedDiscoveredTypes);
    }

    [TestMethod]
    public void TypeHasValidAccessibilityShouldReturnFalseForAllTypesThatAreNotPublicOrOneOfTheirDeclaringTypesIsNotPublic()
    {
        Type[] allTypes = GetAllTestTypes();

        var expectedNonDiscoveredTypes = new[]
        {
            nameof(InternalClass),
            nameof(InternalClass.PublicClassNestedInInternalClass),
            nameof(InternalClass.InternalClassNestedInInternalClass),
            nameof(InternalClass2),
            nameof(PublicClass2.InternalNestedClassInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass.InternalClassNestedInPublicClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass.PublicClassNestedInInternalClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass.InternalClassNestedInInternalClassNestedInPublicClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass.PublicClassNestedInInternalClassNestedInInternalClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass.InternalClassNestedInInternalClassNestedInInternalClass),

            nameof(PrivateClassNames.ProtectedInteralNestedClassInPublicClass),
            nameof(PrivateClassNames.ProtectedNestedClassInPublicClass),
            nameof(PrivateClassNames.PrivateProtectedNestedClassInPublicClass),
            nameof(PrivateClassNames.PrivateClassNestedInPublicClass), // from PublicClass2
            nameof(PrivateClassNames.ProtectedInteralClassNestedInInternalClass),
            nameof(PrivateClassNames.ProtectedClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateProtectedClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass),
            nameof(PrivateClassNames.PublicClassNestedInPrivateClassNestedInPublicClass),
            nameof(PrivateClassNames.PublicClassNestedInPrivateClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateClassNestedInPublicClass), // from PublicClass3
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass),
        };

        var discoverInternal = false;
        var actualDiscoveredTypes = allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        actualDiscoveredTypes.Should().BeEquivalentTo(expectedNonDiscoveredTypes, o => o.WithTracing());
    }

    [TestMethod]
    public void TypeHasValidAccessibilityShouldReturnTrueForAllPublicAndInternalTypesIncludingNestedTypesWhenDiscoverInternalIsEnabled()
    {
        Type[] allTypes = GetAllTestTypes();

        var expectedDiscoveredTypes = new[]
        {
            nameof(PublicClass2),
            nameof(PublicClass3),
            nameof(InternalClass),
            nameof(InternalClass.PublicClassNestedInInternalClass),
            nameof(InternalClass.InternalClassNestedInInternalClass),
            nameof(InternalClass2),
            nameof(PublicClass2.PublicNestedClassInPublicClass),
            nameof(PublicClass2.InternalNestedClassInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass.PublicClassNestedInPublicClassNestedInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass.InternalClassNestedInPublicClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass.PublicClassNestedInInternalClassNestedInPublicClass),
            nameof(PublicClass3.InternalClassNestedInPublicClass.InternalClassNestedInInternalClassNestedInPublicClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass.PublicClassNestedInInternalClassNestedInInternalClass),
            nameof(InternalClass2.InternalClassNestedInInternalClass.InternalClassNestedInInternalClassNestedInInternalClass),
        };

        var discoverInternal = true;
        var actualDiscoveredTypes = allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        actualDiscoveredTypes.Should().BeEquivalentTo(expectedDiscoveredTypes);
    }

    [TestMethod]
    public void TypeHasValidAccessibilityShouldReturnFalseForAllTypesThatAreNotPublicOrInternalOrOneOfTheirDeclaringTypesIsNotPublicOrInternalWhenDiscoverInternalIsEnabled()
    {
        Type[] allTypes = GetAllTestTypes();

        var expectedNonDiscoveredTypes = new[]
        {
            nameof(PrivateClassNames.ProtectedInteralNestedClassInPublicClass),
            nameof(PrivateClassNames.ProtectedNestedClassInPublicClass),
            nameof(PrivateClassNames.PrivateProtectedNestedClassInPublicClass),
            nameof(PrivateClassNames.PrivateClassNestedInPublicClass), // from PublicClass2
            nameof(PrivateClassNames.ProtectedInteralClassNestedInInternalClass),
            nameof(PrivateClassNames.ProtectedClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateProtectedClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass),
            nameof(PrivateClassNames.PublicClassNestedInPrivateClassNestedInPublicClass),
            nameof(PrivateClassNames.PublicClassNestedInPrivateClassNestedInInternalClass),
            nameof(PrivateClassNames.PrivateClassNestedInPublicClass), // from PublicClass3
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass),
        };

        var discoverInternal = true;
        var actualDiscoveredTypes = allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        actualDiscoveredTypes.Should().BeEquivalentTo(expectedNonDiscoveredTypes, o => o.WithTracing());
    }

    private static Type[] GetAllTestTypes()
    {
        var types = new[] { typeof(PublicClass2), typeof(PublicClass3), typeof(InternalClass), typeof(InternalClass2) };
        var nestedTypes = types.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).ToArray();
        var nestedNestedTypes = nestedTypes.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).ToArray();
        var allTypes = new[] { types, nestedTypes, nestedNestedTypes }.SelectMany(t => t).ToArray();
        return allTypes;
    }
    #endregion

    #region private methods

    private void SetupTestClass()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
    }

    #endregion
}

#region Dummy Types

#pragma warning disable SA1201 // Elements must appear in the correct order
public interface IDummyInterface
#pragma warning restore SA1201 // Elements must appear in the correct order
{
}

public abstract class AbstractGenericClass<T>
{
}

public class GenericClass<T>
{
}

public class ClassWithTestContextGetterOnly
{
    public UTFExtension.TestContext TestContext { get; }
}

public class ClassWithTestContextPrivateSetter
{
    public UTFExtension.TestContext TestContext { get; private set; }
}

public class ClassWithStaticTestContext
{
    public static UTFExtension.TestContext TestContext { get; set; }
}

public abstract class ClassWithAbstractTestContext
{
    public abstract UTFExtension.TestContext TestContext { get; set; }
}

public class ClassWithTestContext
{
    public UTFExtension.TestContext TestContext { get; set; }
}

public class GenericClassWithProperty<T>
{
    public static T ReturnAStableSomething { get; }

    public T ReturnSomething { get; set; }

    public bool Something { get; }
}

public class GenericClassWithTestContext<T>
{
    public static T ReturnAStableSomething { get; }

    public T ReturnSomething { get; set; }

    public bool Something { get; }

    public UTFExtension.TestContext TestContext { get; set; }
}

public class PublicTestClass
{
}

public abstract class AbstractTestClass
{
}

public class OuterClass
{
    public class NestedPublicClass
    {
    }

    internal class NestedInternalClass
    {
    }

    private class NestedPrivateClass
    {
        public class InaccessiblePublicClass
        {
        }
    }
}

public class PublicClass2
{
    public class PublicNestedClassInPublicClass
    {
    }

    internal class InternalNestedClassInPublicClass
    {
    }

    protected internal class ProtectedInteralNestedClassInPublicClass
    {
    }

    protected class ProtectedNestedClassInPublicClass
    {
    }

    private protected class PrivateProtectedNestedClassInPublicClass
    {
    }

    private class PrivateClassNestedInPublicClass
    {
    }
}

public class PublicClass3
{
    public class PublicClassNestedInPublicClass
    {
        public class PublicClassNestedInPublicClassNestedInPublicClass
        {
        }

        internal class InternalClassNestedInPublicClassNestedInPublicClass
        {
        }
    }

    internal class InternalClassNestedInPublicClass
    {
        public class PublicClassNestedInInternalClassNestedInPublicClass
        {
        }

        internal class InternalClassNestedInInternalClassNestedInPublicClass
        {
        }
    }

    private class PrivateClassNestedInPublicClass
    {
        public class PublicClassNestedInPrivateClassNestedInPublicClass
        {
        }
    }
}

internal class InternalTestClass
{
}

internal class InternalClass
{
    public class PublicClassNestedInInternalClass
    {
    }

    internal class InternalClassNestedInInternalClass
    {
    }

    protected internal class ProtectedInteralClassNestedInInternalClass
    {
    }

    protected class ProtectedClassNestedInInternalClass
    {
    }

    private protected class PrivateProtectedClassNestedInInternalClass
    {
    }

    private class PrivateClassNestedInInternalClass
    {
    }
}

internal class InternalClass2
{
    internal class InternalClassNestedInInternalClass
    {
        public class PublicClassNestedInInternalClassNestedInInternalClass
        {
        }

        internal class InternalClassNestedInInternalClassNestedInInternalClass
        {
        }
    }

    private class PrivateClassNestedInInternalClass
    {
        public class PublicClassNestedInPrivateClassNestedInInternalClass
        {
        }
    }
}

/// <summary>
/// Names of types that are not accessible via nameof or typeof due to type constraints, and
/// other non public and non internal types for consistency.
/// </summary>
internal class PrivateClassNames
{
    public string ProtectedInteralNestedClassInPublicClass { get; }

    public string ProtectedNestedClassInPublicClass { get; }

    public string PrivateProtectedNestedClassInPublicClass { get; }

    public string PrivateClassNestedInPublicClass { get; }

    public string ProtectedInteralClassNestedInInternalClass { get; }

    public string ProtectedClassNestedInInternalClass { get; }

    public string PrivateProtectedClassNestedInInternalClass { get; }

    public string PrivateClassNestedInInternalClass { get; }

    public string PublicClassNestedInPrivateClassNestedInPublicClass { get; }

    public string PublicClassNestedInPrivateClassNestedInInternalClass { get; }
}

#endregion

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

    private TypeValidator typeValidator;
    private Mock<ReflectHelper> mockReflectHelper;
    private List<string> warnings;

    #endregion

    #region TestInit

    [TestInitialize]
    public void TestInit()
    {
        mockReflectHelper = new Mock<ReflectHelper>();
        typeValidator = new TypeValidator(mockReflectHelper.Object);
        warnings = new List<string>();
    }

    #endregion

    #region Type is class, TestClassAttribute or attribute derived from TestClassAttribute

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonClassTypes()
    {
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(IDummyInterface), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForClassesNotHavingTestClassAttributeOrDerivedAttributeTypes()
    {
        mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(TypeValidatorTests), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForClassesMarkedByAnAttributeDerivedFromTestClass()
    {
        mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
        mockReflectHelper.Setup(
            rh => rh.HasAttributeDerivedFrom(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(TypeValidatorTests), warnings));
    }

    #endregion

    #region Public/Nested public test classes

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(InternalTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningForNonPublicTestClasses()
    {
        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), warnings);
        Assert.AreEqual(1, warnings.Count);
        CollectionAssert.Contains(warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(InternalTestClass).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), warnings);
        Assert.AreEqual(1, warnings.Count);
        CollectionAssert.Contains(warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(OuterClass.NestedInternalClass).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(PublicTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForNestedPublicTestClasses()
    {
        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(OuterClass.NestedPublicClass), warnings));
    }

    #endregion

    #region Discovery of internal test classes enabled

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(InternalTestClass), warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), warnings);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForPrivateTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        var nestedPrivateClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "NestedPrivateClass");

        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(nestedPrivateClassType, warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForInaccessibleTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        var inaccessibleClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "InaccessiblePublicClass");

        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(inaccessibleClassType, warnings));
    }

    [TestMethod]
    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningsForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), warnings);
        Assert.AreEqual(0, warnings.Count);
    }

    #endregion

    #region Generic types

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForNonAbstractGenericTypes()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(GenericClass<>), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForNonAbstractGenericTypes()
    {
        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(GenericClass<>), warnings);
        Assert.AreEqual(1, warnings.Count);
        CollectionAssert.Contains(warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(GenericClass<>).FullName));
    }

    #endregion

    #region TestContext signature

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldReportWarningsForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), warnings);
        Assert.AreEqual(1, warnings.Count);
        CollectionAssert.Contains(warnings, string.Format(Resource.UTA_ErrorInValidTestContextSignature, typeof(ClassWithTestContextGetterOnly).FullName));
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnTrueForTestClassesWithValidTestContextSignature()
    {
        SetupTestClass();
        Assert.IsTrue(typeValidator.IsValidTestClass(typeof(ClassWithTestContext), warnings));
    }

    #endregion

    #region Abstract Test classes

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForAbstractTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(AbstractTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestClassShouldNotReportWarningsForAbstractTestClasses()
    {
        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(AbstractTestClass), warnings);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void IsValidTestClassShouldReturnFalseForGenericAbstractTestClasses()
    {
        SetupTestClass();
        Assert.IsFalse(typeValidator.IsValidTestClass(typeof(AbstractGenericClass<>), warnings));
        Assert.AreEqual(0, warnings.Count);
    }

    #endregion

    #region HasCorrectTestContext tests

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnTrueForClassesWithNoTestContextProperty()
    {
        Assert.IsTrue(typeValidator.HasCorrectTestContextSignature(typeof(PublicTestClass)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithNoSetters()
    {
        Assert.IsFalse(typeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextGetterOnly)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithPrivateSetter()
    {
        Assert.IsFalse(typeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextPrivateSetter)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithStaticSetter()
    {
        Assert.IsFalse(typeValidator.HasCorrectTestContextSignature(typeof(ClassWithStaticTestContext)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithAbstractSetter()
    {
        Assert.IsFalse(typeValidator.HasCorrectTestContextSignature(typeof(ClassWithAbstractTestContext)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldNotThrowForAGenericClassWithRandomProperties()
    {
        Assert.IsTrue(typeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithProperty<>)));
    }

    [TestMethod]
    public void HasCorrectTestContextSignatureShouldReturnTrueForAGenericClassWithTestContext()
    {
        Assert.IsTrue(typeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithTestContext<>)));
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
            .Where(t => typeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
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
            .Where(t => !typeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
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
            .Where(t => typeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
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
            .Where(t => !typeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
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
        mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
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

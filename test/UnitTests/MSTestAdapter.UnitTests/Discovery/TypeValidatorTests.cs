// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class TypeValidatorTests : TestContainer
{
    #region private variables

    private readonly TypeValidator _typeValidator;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly List<string> _warnings;

    #endregion

    #region TestInit

    public TypeValidatorTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeValidator = new TypeValidator(_mockReflectHelper.Object);
        _warnings = [];
    }

    #endregion

    #region Type is class, TestClassAttribute or attribute derived from TestClassAttribute

    public void IsValidTestClassShouldReturnFalseForNonClassTypes() => Verify(!_typeValidator.IsValidTestClass(typeof(IDummyInterface), _warnings));

    public void IsValidTestClassShouldReturnFalseForClassesNotHavingTestClassAttributeOrDerivedAttributeTypes()
    {
        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(It.IsAny<Type>(), false)).Returns(false);
        Verify(!_typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings));
    }

    public void IsValidTestClassShouldReturnTrueForClassesMarkedByAnAttributeDerivedFromTestClass()
    {
        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(false);
        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(true);
        Verify(_typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings));
    }

    #endregion

    #region Public/Nested public test classes

    public void IsValidTestClassShouldReturnFalseForNonPublicTestClasses()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings));
    }

    public void IsValidTestClassShouldReportWarningForNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorNonPublicTestClass, typeof(InternalTestClass).FullName)));
    }

    public void IsValidTestClassShouldReturnFalseForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings));
    }

    public void IsValidTestClassShouldReportWarningsForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorNonPublicTestClass, typeof(OuterClass.NestedInternalClass).FullName)));
    }

    public void IsValidTestClassShouldReturnTrueForPublicTestClasses()
    {
        SetupTestClass();
        Verify(_typeValidator.IsValidTestClass(typeof(PublicTestClass), _warnings));
    }

    public void IsValidTestClassShouldReturnTrueForNestedPublicTestClasses()
    {
        SetupTestClass();
        Verify(_typeValidator.IsValidTestClass(typeof(OuterClass.NestedPublicClass), _warnings));
    }

    #endregion

    #region Discovery of internal test classes enabled

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        Verify(typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings));
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        Verify(_warnings.Count == 0);
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        Verify(typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings));
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForPrivateTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        Type nestedPrivateClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "NestedPrivateClass");

        SetupTestClass();
        Verify(!typeValidator.IsValidTestClass(nestedPrivateClassType, _warnings));
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForInaccessibleTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        Type inaccessibleClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "InaccessiblePublicClass");

        SetupTestClass();
        Verify(!typeValidator.IsValidTestClass(inaccessibleClassType, _warnings));
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningsForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        Verify(_warnings.Count == 0);
    }

    #endregion

    #region Generic types

    public void IsValidTestClassShouldReturnFalseForNonAbstractGenericTypes()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings));
    }

    public void IsValidTestClassShouldReportWarningsForNonAbstractGenericTypes()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings);
        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorTestClassIsGenericNonAbstract, typeof(GenericClass<>).FullName)));
    }

    #endregion

    #region TestContext signature

    public void IsValidTestClassShouldReturnFalseForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), _warnings));
    }

    public void IsValidTestClassShouldReportWarningsForTestClassesWithInvalidTestContextSignature()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), _warnings);
        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorInValidTestContextSignature, typeof(ClassWithTestContextGetterOnly).FullName)));
    }

    public void IsValidTestClassShouldReturnTrueForTestClassesWithValidTestContextSignature()
    {
        SetupTestClass();
        Verify(_typeValidator.IsValidTestClass(typeof(ClassWithTestContext), _warnings));
    }

    #endregion

    #region Abstract Test classes

    public void IsValidTestClassShouldReturnFalseForAbstractTestClasses()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings));
    }

    public void IsValidTestClassShouldNotReportWarningsForAbstractTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings);
        Verify(_warnings.Count == 0);
    }

    public void IsValidTestClassShouldReturnFalseForGenericAbstractTestClasses()
    {
        SetupTestClass();
        Verify(!_typeValidator.IsValidTestClass(typeof(AbstractGenericClass<>), _warnings));
        Verify(_warnings.Count == 0);
    }

    #endregion

    #region HasCorrectTestContext tests

    public void HasCorrectTestContextSignatureShouldReturnTrueForClassesWithNoTestContextProperty() => Verify(TypeValidator.HasCorrectTestContextSignature(typeof(PublicTestClass)));

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithNoSetters() => Verify(!TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextGetterOnly)));

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithPrivateSetter() => Verify(!TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextPrivateSetter)));

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithStaticSetter() => Verify(!TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithStaticTestContext)));

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithAbstractSetter() => Verify(!TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithAbstractTestContext)));

    public void HasCorrectTestContextSignatureShouldNotThrowForAGenericClassWithRandomProperties() => Verify(TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithProperty<>)));

    public void HasCorrectTestContextSignatureShouldReturnTrueForAGenericClassWithTestContext() => Verify(TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithTestContext<>)));

    #endregion

    #region IsValidTestTypeTests

    public void AllTypesContainAllPrivateClasses()
    {
        // The names of private types are not accessible by nameof or typeof, ensure that we have them in the
        // list of our test types, to avoid bugs caused by typos.
        string[] allTypes = GetAllTestTypes().Select(t => t.Name).ToArray();
        string[] privateTypes = typeof(PrivateClassNames).GetProperties().Select(n => n.Name).ToArray();
        Verify(privateTypes.Length >= 1);

        foreach (string type in privateTypes)
        {
            Verify(allTypes.Contains(type));
        }
    }

    public void TypeHasValidAccessibilityShouldReturnTrueForAllPublicTypesIncludingNestedPublicTypes()
    {
        Type[] allTypes = GetAllTestTypes();

        string[] expectedDiscoveredTypes =
        [
            nameof(PublicClass2),
            nameof(PublicClass3),
            nameof(PublicClass2.PublicNestedClassInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass),
            nameof(PublicClass3.PublicClassNestedInPublicClass.PublicClassNestedInPublicClassNestedInPublicClass)
        ];

        bool discoverInternal = false;
        string[] actualDiscoveredTypes = allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedDiscoveredTypes);
        Verify(actualDiscoveredTypes.SequenceEqual(expectedDiscoveredTypes));
    }

    public void TypeHasValidAccessibilityShouldReturnFalseForAllTypesThatAreNotPublicOrOneOfTheirDeclaringTypesIsNotPublic()
    {
        Type[] allTypes = GetAllTestTypes();

        string[] expectedNonDiscoveredTypes =
        [
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
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass)
        ];

        bool discoverInternal = false;
        string[] actualDiscoveredTypes = allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedNonDiscoveredTypes);
        Verify(actualDiscoveredTypes.SequenceEqual(expectedNonDiscoveredTypes));
    }

    public void TypeHasValidAccessibilityShouldReturnTrueForAllPublicAndInternalTypesIncludingNestedTypesWhenDiscoverInternalIsEnabled()
    {
        Type[] allTypes = GetAllTestTypes();

        string[] expectedDiscoveredTypes =
        [
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
            nameof(InternalClass2.InternalClassNestedInInternalClass.InternalClassNestedInInternalClassNestedInInternalClass)
        ];

        bool discoverInternal = true;
        string[] actualDiscoveredTypes = allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedDiscoveredTypes);
        Verify(actualDiscoveredTypes.SequenceEqual(expectedDiscoveredTypes));
    }

    public void TypeHasValidAccessibilityShouldReturnFalseForAllTypesThatAreNotPublicOrInternalOrOneOfTheirDeclaringTypesIsNotPublicOrInternalWhenDiscoverInternalIsEnabled()
    {
        Type[] allTypes = GetAllTestTypes();

        string[] expectedNonDiscoveredTypes =
        [
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
            nameof(PrivateClassNames.PrivateClassNestedInInternalClass)
        ];

        bool discoverInternal = true;
        string[] actualDiscoveredTypes = allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t.GetTypeInfo(), discoverInternal))
            .Select(t => t.Name).ToArray();

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedNonDiscoveredTypes);
        Verify(actualDiscoveredTypes.SequenceEqual(expectedNonDiscoveredTypes));
    }

    private static Type[] GetAllTestTypes()
    {
        Type[] types = [typeof(PublicClass2), typeof(PublicClass3), typeof(InternalClass), typeof(InternalClass2)];
        Type[] nestedTypes = types.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).ToArray();
        Type[] nestedNestedTypes = nestedTypes.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).ToArray();
        Type[] allTypes = new[] { types, nestedTypes, nestedNestedTypes }.SelectMany(t => t).ToArray();
        return allTypes;
    }
    #endregion

    #region private methods

    private void SetupTestClass() => _mockReflectHelper.Setup(rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(true);

    #endregion
}

#region Dummy Types

#pragma warning disable SA1201 // Elements must appear in the correct order
public interface IDummyInterface
#pragma warning restore SA1201 // Elements must appear in the correct order
{
}

public abstract class AbstractGenericClass<T>;

public class GenericClass<T>;

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
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static T ReturnAStableSomething { get; }
#pragma warning restore CA1000 // Do not declare static members on generic types

    public T ReturnSomething { get; set; }

    public bool Something { get; }
}

public class GenericClassWithTestContext<T>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static T ReturnAStableSomething { get; }
#pragma warning restore CA1000 // Do not declare static members on generic types

    public T ReturnSomething { get; set; }

    public bool Something { get; }

    public UTFExtension.TestContext TestContext { get; set; }
}

public class PublicTestClass;

public abstract class AbstractTestClass;

public class OuterClass
{
    public class NestedPublicClass;

    internal class NestedInternalClass;

    private class NestedPrivateClass
    {
        public class InaccessiblePublicClass;
    }
}

public class PublicClass2
{
    public class PublicNestedClassInPublicClass;

    internal class InternalNestedClassInPublicClass;

    protected internal class ProtectedInteralNestedClassInPublicClass;

    protected class ProtectedNestedClassInPublicClass;

    private protected class PrivateProtectedNestedClassInPublicClass;

    private sealed class PrivateClassNestedInPublicClass;
}

public class PublicClass3
{
    public class PublicClassNestedInPublicClass
    {
        public class PublicClassNestedInPublicClassNestedInPublicClass;

        internal class InternalClassNestedInPublicClassNestedInPublicClass;
    }

    internal class InternalClassNestedInPublicClass
    {
        public class PublicClassNestedInInternalClassNestedInPublicClass;

        internal class InternalClassNestedInInternalClassNestedInPublicClass;
    }

    private sealed class PrivateClassNestedInPublicClass
    {
        public sealed class PublicClassNestedInPrivateClassNestedInPublicClass;
    }
}

internal class InternalTestClass;

internal class InternalClass
{
    public class PublicClassNestedInInternalClass;

    internal class InternalClassNestedInInternalClass;

    protected internal class ProtectedInteralClassNestedInInternalClass;

    protected class ProtectedClassNestedInInternalClass;

    private protected class PrivateProtectedClassNestedInInternalClass;

    private sealed class PrivateClassNestedInInternalClass;
}

internal class InternalClass2
{
    internal class InternalClassNestedInInternalClass
    {
        public class PublicClassNestedInInternalClassNestedInInternalClass;

        internal class InternalClassNestedInInternalClassNestedInInternalClass;
    }

    private sealed class PrivateClassNestedInInternalClass
    {
        public sealed class PublicClassNestedInPrivateClassNestedInInternalClass;
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

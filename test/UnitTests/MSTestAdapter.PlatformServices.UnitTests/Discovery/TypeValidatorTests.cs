// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using Moq;

using TestFramework.ForTestingMSTest;

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

    public void IsValidTestClassShouldReturnFalseForNonClassTypes() => _typeValidator.IsValidTestClass(typeof(IDummyInterface), _warnings).Should().BeFalse();

    public void IsValidTestClassShouldReturnFalseForClassesNotHavingTestClassAttributeOrDerivedAttributeTypes()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<Type>(), false)).Returns(false);
        _typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings).Should().BeFalse();
    }

    public void IsValidTestClassShouldReturnTrueForClassesMarkedByAnAttributeDerivedFromTestClass()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(false);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(true);
        _typeValidator.IsValidTestClass(typeof(TypeValidatorTests), _warnings).Should().BeTrue();
    }

    #endregion

    #region Public/Nested public test classes

    public void IsValidTestClassShouldReturnFalseForNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings).Should().BeFalse();
    }

    public void IsValidTestClassShouldReportWarningForNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        _warnings.Count.Should().Be(1);
        _warnings.Should().Contain(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorNonPublicTestClass, typeof(InternalTestClass).FullName));
    }

    public void IsValidTestClassShouldReturnFalseForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings).Should().BeFalse();
    }

    public void IsValidTestClassShouldReportWarningsForNestedNonPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        _warnings.Count.Should().Be(1);
        _warnings.Should().Contain(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorNonPublicTestClass, typeof(OuterClass.NestedInternalClass).FullName));
    }

    public void IsValidTestClassShouldReturnTrueForPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(PublicTestClass), _warnings).Should().BeTrue();
    }

    public void IsValidTestClassShouldReturnTrueForNestedPublicTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(OuterClass.NestedPublicClass), _warnings).Should().BeTrue();
    }

    #endregion

    #region Discovery of internal test classes enabled

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings).Should().BeTrue();
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningForInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(InternalTestClass), _warnings);
        _warnings.Count.Should().Be(0);
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnTrueForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings).Should().BeTrue();
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForPrivateTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        Type nestedPrivateClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "NestedPrivateClass");

        SetupTestClass();
        typeValidator.IsValidTestClass(nestedPrivateClassType, _warnings).Should().BeFalse();
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldReturnFalseForInaccessibleTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        Type inaccessibleClassType = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == "InaccessiblePublicClass");

        SetupTestClass();
        typeValidator.IsValidTestClass(inaccessibleClassType, _warnings).Should().BeFalse();
    }

    public void WhenInternalDiscoveryIsEnabledIsValidTestClassShouldNotReportWarningsForNestedInternalTestClasses()
    {
        var typeValidator = new TypeValidator(_mockReflectHelper.Object, true);

        SetupTestClass();
        typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), _warnings);
        _warnings.Count.Should().Be(0);
    }

    #endregion

    #region Generic types

    public void IsValidTestClassShouldReturnFalseForNonAbstractGenericTypes()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings).Should().BeFalse();
    }

    public void IsValidTestClassShouldReportWarningsForNonAbstractGenericTypes()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(GenericClass<>), _warnings);
        _warnings.Count.Should().Be(1);
        _warnings.Should().Contain(string.Format(CultureInfo.InvariantCulture, Resource.UTA_ErrorTestClassIsGenericNonAbstract, typeof(GenericClass<>).FullName));
    }

    #endregion

    #region TestContext signature

    public void IsValidTestClassShouldReturnTrueForTestClassesWithReadOnlyTestContextSignature()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), _warnings).Should().BeTrue();
        _warnings.Count.Should().Be(0);
    }

    public void IsValidTestClassShouldReturnTrueForTestClassesWithValidTestContextSignature()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(ClassWithTestContext), _warnings).Should().BeTrue();
    }

    #endregion

    #region Abstract Test classes

    public void IsValidTestClassShouldReturnFalseForAbstractTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings).Should().BeFalse();
    }

    public void IsValidTestClassShouldNotReportWarningsForAbstractTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(AbstractTestClass), _warnings);
        _warnings.Count.Should().Be(0);
    }

    public void IsValidTestClassShouldReturnFalseForGenericAbstractTestClasses()
    {
        SetupTestClass();
        _typeValidator.IsValidTestClass(typeof(AbstractGenericClass<>), _warnings).Should().BeFalse();
        _warnings.Count.Should().Be(0);
    }

    #endregion

    #region HasCorrectTestContext tests

    public void HasCorrectTestContextSignatureShouldReturnTrueForClassesWithNoTestContextProperty() => TypeValidator.HasCorrectTestContextSignature(typeof(PublicTestClass)).Should().BeTrue();

    public void HasCorrectTestContextSignatureShouldReturnTrueForTestContextsWithNoSetters() => TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextGetterOnly)).Should().BeTrue();

    public void HasCorrectTestContextSignatureShouldReturnTrueForTestContextsWithPrivateSetter() => TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextPrivateSetter)).Should().BeTrue();

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithStaticSetter() => TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithStaticTestContext)).Should().BeFalse();

    public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithAbstractSetter() => TypeValidator.HasCorrectTestContextSignature(typeof(ClassWithAbstractTestContext)).Should().BeFalse();

    public void HasCorrectTestContextSignatureShouldNotThrowForAGenericClassWithRandomProperties() => TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithProperty<>)).Should().BeTrue();

    public void HasCorrectTestContextSignatureShouldReturnTrueForAGenericClassWithTestContext() => TypeValidator.HasCorrectTestContextSignature(typeof(GenericClassWithTestContext<>)).Should().BeTrue();

    #endregion

    #region IsValidTestTypeTests

    public void AllTypesContainAllPrivateClasses()
    {
        // The names of private types are not accessible by nameof or typeof, ensure that we have them in the
        // list of our test types, to avoid bugs caused by typos.
        string[] allTypes = [.. GetAllTestTypes().Select(t => t.Name)];
        string[] privateTypes = [.. typeof(PrivateClassNames).GetProperties().Select(n => n.Name)];
        privateTypes.Length.Should().BeGreaterThanOrEqualTo(1);

        foreach (string type in privateTypes)
        {
            allTypes.Should().Contain(type);
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
        string[] actualDiscoveredTypes = [.. allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t, discoverInternal))
            .Select(t => t.Name)];

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedDiscoveredTypes);
        actualDiscoveredTypes.Should().Equal(expectedDiscoveredTypes);
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
        string[] actualDiscoveredTypes = [.. allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t, discoverInternal))
            .Select(t => t.Name)];

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedNonDiscoveredTypes);
        actualDiscoveredTypes.Should().Equal(expectedNonDiscoveredTypes);
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
        string[] actualDiscoveredTypes = [.. allTypes
            .Where(t => TypeValidator.TypeHasValidAccessibility(t, discoverInternal))
            .Select(t => t.Name)];

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedDiscoveredTypes);
        actualDiscoveredTypes.Should().Equal(expectedDiscoveredTypes);
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
        string[] actualDiscoveredTypes = [.. allTypes
            .Where(t => !TypeValidator.TypeHasValidAccessibility(t, discoverInternal))
            .Select(t => t.Name)];

        Array.Sort(actualDiscoveredTypes);
        Array.Sort(expectedNonDiscoveredTypes);
        actualDiscoveredTypes.Should().Equal(expectedNonDiscoveredTypes);
    }

    private static Type[] GetAllTestTypes()
    {
        Type[] types = [typeof(PublicClass2), typeof(PublicClass3), typeof(InternalClass), typeof(InternalClass2)];
        Type[] nestedTypes = [.. types.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))];
        Type[] nestedNestedTypes = [.. nestedTypes.SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))];
        Type[] allTypes = [.. new[] { types, nestedTypes, nestedNestedTypes }.SelectMany(t => t)];
        return allTypes;
    }
    #endregion

    #region private methods

    private void SetupTestClass() => _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<TypeInfo>(), false)).Returns(true);

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
    public TestContext TestContext => null!;
}

public class ClassWithTestContextPrivateSetter
{
    public TestContext TestContext { get; private set; } = null!;
}

public class ClassWithStaticTestContext
{
    public static TestContext TestContext { get; set; } = null!;
}

public abstract class ClassWithAbstractTestContext
{
    public abstract TestContext TestContext { get; set; }
}

public class ClassWithTestContext
{
    public TestContext TestContext { get; set; } = null!;
}

public class GenericClassWithProperty<T>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static T ReturnAStableSomething => default!;
#pragma warning restore CA1000 // Do not declare static members on generic types

    public T ReturnSomething { get; set; } = default!;

    public bool Something { get; }
}

public class GenericClassWithTestContext<T>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static T ReturnAStableSomething => default!;
#pragma warning restore CA1000 // Do not declare static members on generic types

    public T ReturnSomething { get; set; } = default!;

    public bool Something { get; }

    public TestContext TestContext { get; set; } = null!;
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
    public string ProtectedInteralNestedClassInPublicClass => null!;

    public string ProtectedNestedClassInPublicClass => null!;

    public string PrivateProtectedNestedClassInPublicClass => null!;

    public string PrivateClassNestedInPublicClass => null!;

    public string ProtectedInteralClassNestedInInternalClass => null!;

    public string ProtectedClassNestedInInternalClass => null!;

    public string PrivateProtectedClassNestedInInternalClass => null!;

    public string PrivateClassNestedInInternalClass => null!;

    public string PublicClassNestedInPrivateClassNestedInPublicClass => null!;

    public string PublicClassNestedInPrivateClassNestedInInternalClass => null!;
}

#endregion

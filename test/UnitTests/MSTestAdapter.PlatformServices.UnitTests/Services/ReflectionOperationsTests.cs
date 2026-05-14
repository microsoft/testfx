// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

[DoNotParallelize]
public class ReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;
    private readonly AttributeMockingHelper _attributeMockingHelper;
    private readonly Mock<MethodInfo> _method;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    // NOTE: This class mutates the shared static PlatformServiceProvider.Instance (set in
    // the constructor, restored in Dispose). Tests that call GetCustomAttributesCached or
    // methods that go through NotCachedReflectionAccessor depend on this static being set
    // to _testablePlatformServiceProvider. [DoNotParallelize] is applied to prevent
    // cross-class race conditions with other test classes that follow the same pattern.
    public ReflectionOperationsTests()
    {
        _reflectionOperations = new ReflectionOperations();
        _method = new Mock<MethodInfo>();
        _method.Setup(x => x.MemberType).Returns(MemberTypes.Method);
        _method.Setup(x => x.ReflectedType).Returns(typeof(ReflectionOperationsTests));

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _attributeMockingHelper = new(_testablePlatformServiceProvider.MockReflectionOperations);

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    #region GetCustomAttributes Tests

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        object[] attributes = GetMemberAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    private object[] GetMemberAttributes(Type type)
        => [.. _reflectionOperations.GetCustomAttributes(type).Where(x => x.GetType().FullName != "System.Runtime.CompilerServices.NullableContextAttribute")];

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        object[] attributes = GetMemberAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    #endregion

    #region GetTestCategories Tests

    /// <summary>
    /// Testing test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtClassLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel"];
        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel1"), new TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel3")], MemberTypes.All);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];
        string[] expected = ["MethodLevel", "ClassLevel", "AsmLevel1", "AsmLevel2", "AsmLevel3"];

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldConcatCustomAttributeOfSameType()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel1")], MemberTypes.All);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel2")], MemberTypes.TypeInfo);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel1")], MemberTypes.Method);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel2")], MemberTypes.Method);

        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];
        string[] expected = ["MethodLevel1", "MethodLevel2", "ClassLevel1", "ClassLevel2", "AsmLevel1", "AsmLevel2"];

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel")], MemberTypes.All);

        string[] expected = ["AsmLevel"];

        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing multiple test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtClassLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel"), new TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel", "ClassLevel1"];
        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing multiple test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel"), new TestCategoryAttribute("AsmLevel1")], MemberTypes.All);

        string[] expected = ["AsmLevel", "AsmLevel1"];
        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];
        actual.Should().Equal(expected);
    }

    /// <summary>
    /// Testing test category attributes adorned at method level - regression.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtMethodLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] expected = ["MethodLevel"];
        string[] actual = [.. _reflectionOperations.GetTestCategories(_method.Object, typeof(ReflectionOperationsTests))];

        actual.Should().Equal(expected);
    }

    #endregion

    #region GetTestPropertiesAsTraits Tests

    /// <summary>
    /// Testing test property attributes adorned at method level.
    /// </summary>
    public void GetTestPropertiesAsTraitsShouldIncludeTestPropertiesAtMethodLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestPropertyAttribute), [new TestPropertyAttribute("MethodKey", "MethodValue")], MemberTypes.Method);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(_method.Object);

        actual.Should().BeEquivalentTo(new[] { new Trait("MethodKey", "MethodValue") }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Testing test property attributes adorned at class level.
    /// </summary>
    public void GetTestPropertiesAsTraitsShouldIncludeTestPropertiesAtClassLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestPropertyAttribute), [new TestPropertyAttribute("ClassKey", "ClassValue")], MemberTypes.TypeInfo);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(_method.Object);

        actual.Should().BeEquivalentTo(new[] { new Trait("ClassKey", "ClassValue") }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Testing test property attributes adorned at both method and class level.
    /// </summary>
    public void GetTestPropertiesAsTraitsShouldIncludeTestPropertiesAtAllLevels()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestPropertyAttribute), [new TestPropertyAttribute("MethodKey", "MethodValue")], MemberTypes.Method);
        _attributeMockingHelper.AddCustomAttribute(typeof(TestPropertyAttribute), [new TestPropertyAttribute("ClassKey", "ClassValue")], MemberTypes.TypeInfo);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(_method.Object);

        actual.Should().BeEquivalentTo(new[] { new Trait("MethodKey", "MethodValue"), new Trait("ClassKey", "ClassValue") }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Testing multiple test property attributes adorned at method level.
    /// </summary>
    public void GetTestPropertiesAsTraitsShouldIncludeMultipleTestPropertiesAtMethodLevel()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestPropertyAttribute), [new TestPropertyAttribute("Key1", "Value1"), new TestPropertyAttribute("Key2", "Value2")], MemberTypes.Method);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(_method.Object);

        actual.Should().BeEquivalentTo(new[] { new Trait("Key1", "Value1"), new Trait("Key2", "Value2") }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Testing that empty array is returned when no test properties are defined.
    /// </summary>
    public void GetTestPropertiesAsTraitsShouldReturnEmptyWhenNoTestProperties()
    {
        _attributeMockingHelper.AddCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("SomeCategory")], MemberTypes.Method);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(_method.Object);

        actual.Should().BeEmpty();
    }

    #endregion

    #region IsAttributeDefined Tests

    public void IsAttributeDefinedShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeTrue();
    }

    public void IsAttributeDefinedShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestClassAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeFalse();
    }

    public void IsAttributeDefinedShouldReturnFromCache()
    {
        var rh = new ReflectionOperations();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        var memberInfo = (MethodInfo)MethodBase.GetCurrentMethod()!;

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();

        // Validate that reflection APIs are not called again.
        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo), Times.Once);
    }

    public void HasAttributeDerivedFromShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeTrue();
    }

    public void HasAttributeDerivedFromShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestClassAttribute>(mockMemberInfo.Object).Should().BeFalse();
    }

    public void HasAttributeDerivedFromShouldReturnFromCache()
    {
        var rh = new ReflectionOperations();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        var memberInfo = (MethodInfo)MethodBase.GetCurrentMethod()!;

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();

        // Validate that reflection APIs are not called again.
        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo), Times.Once);
    }

    public void HasAttributeDerivedFromShouldReturnFalseQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns((object[])null!);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeFalse();
    }

    #endregion

    #region GetFirstAttributeOrDefault Tests

    public void GetFirstAttributeOrDefaultShouldReturnAttributeWhenPresent()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var expectedAttribute = new DummySealedAttribute("value");
        var attributes = new Attribute[] { expectedAttribute };

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(attributes);

        rh.GetFirstAttributeOrDefault<DummySealedAttribute>(mockMemberInfo.Object).Should().BeSameAs(expectedAttribute);
    }

    public void GetFirstAttributeOrDefaultShouldReturnNullWhenNotPresent()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new DummyAAttribute("foo") };

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(attributes);

        rh.GetFirstAttributeOrDefault<DummySealedAttribute>(mockMemberInfo.Object).Should().BeNull();
    }

    #endregion

    #region GetSingleAttributeOrDefault Tests

    public void GetSingleAttributeOrDefaultShouldReturnAttributeWhenPresent()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var expectedAttribute = new DummyAAttribute("value");
        var attributes = new Attribute[] { expectedAttribute };

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(attributes);

        rh.GetSingleAttributeOrDefault<DummyAAttribute>(mockMemberInfo.Object).Should().BeSameAs(expectedAttribute);
    }

    public void GetSingleAttributeOrDefaultShouldReturnNullWhenNotPresent()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new DummySingleAAttribute("foo") };

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(attributes);

        rh.GetSingleAttributeOrDefault<DummyAAttribute>(mockMemberInfo.Object).Should().BeNull();
    }

    public void GetSingleAttributeOrDefaultShouldThrowWhenMultipleAttributesPresent()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new DummyAAttribute("first"), new DummyAAttribute("second") };

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(attributes);

        Action action = () => rh.GetSingleAttributeOrDefault<DummyAAttribute>(mockMemberInfo.Object);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(DummyAAttribute)}*");
    }

    #endregion

    #region Null Guard Tests

    public void IsAttributeDefinedShouldThrowWhenProviderIsNull()
    {
        var rh = new ReflectionOperations();
        Action action = () => rh.IsAttributeDefined<TestMethodAttribute>(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    public void GetFirstAttributeOrDefaultShouldThrowWhenProviderIsNull()
    {
        var rh = new ReflectionOperations();
        Action action = () => rh.GetFirstAttributeOrDefault<DummySealedAttribute>(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    public void GetSingleAttributeOrDefaultShouldThrowWhenProviderIsNull()
    {
        var rh = new ReflectionOperations();
        Action action = () => rh.GetSingleAttributeOrDefault<DummyAAttribute>(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    public void GetAttributesShouldThrowWhenProviderIsNull()
    {
        var rh = new ReflectionOperations();
        Action action = () => _ = rh.GetAttributes<DummyAAttribute>(null!).ToArray();
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    public void GetCustomAttributesCachedShouldThrowWhenProviderIsNull()
    {
        var rh = new ReflectionOperations();
        Action action = () => rh.GetCustomAttributesCached(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    #endregion

    #region GetAttributes Tests

    public void GetAttributesShouldReturnMatchingAttributes()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var expected = new DummyAAttribute("x");

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(new Attribute[] { expected, new DummySingleAAttribute("y") });

        rh.GetAttributes<DummyAAttribute>(mockMemberInfo.Object).Should().ContainSingle().Which.Should().BeSameAs(expected);
    }

    public void GetAttributesShouldReturnEmptyWhenNoMatchingAttribute()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(new Attribute[] { new DummySingleAAttribute("y") });

        rh.GetAttributes<DummyAAttribute>(mockMemberInfo.Object).Should().BeEmpty();
    }

    public void GetAttributesShouldReturnDerivedAttributes()
    {
        var rh = new ReflectionOperations();
        var mockMemberInfo = new Mock<MemberInfo>();
        var derivedAttr = new DummySingleAAttribute("derived");

        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object))
            .Returns(new Attribute[] { derivedAttr });

        // DummySingleAAttribute does NOT derive from DummyAAttribute, so this should be empty.
        rh.GetAttributes<DummyAAttribute>(mockMemberInfo.Object).Should().BeEmpty();

        // But should match its own type.
        rh.GetAttributes<DummySingleAAttribute>(mockMemberInfo.Object).Should().ContainSingle().Which.Should().BeSameAs(derivedAttr);
    }

    #endregion

    #region GetTestPropertiesAsTraits Additional Tests

    public void GetTestPropertiesAsTraitsShouldNotIncludeClassLevelAttributesWhenReflectedTypeIsNull()
    {
        var methodWithNullReflectedType = new Mock<MethodInfo>();
        methodWithNullReflectedType.Setup(x => x.MemberType).Returns(MemberTypes.Method);
        methodWithNullReflectedType.Setup(x => x.ReflectedType).Returns((Type)null!);
        _attributeMockingHelper.AddCustomAttribute(
            typeof(TestPropertyAttribute),
            [new TestPropertyAttribute("ClassKey", "ClassValue")],
            MemberTypes.TypeInfo);

        Trait[] actual = _reflectionOperations.GetTestPropertiesAsTraits(methodWithNullReflectedType.Object);

        actual.Should().BeEmpty();
    }

    #endregion

    #region MockableReflectionOperations Tests

    public void MockableReflectionOperationsGetCustomAttributesCachedShouldThrowForUnsupportedProvider()
    {
        var mockReflectionOps = new Mock<IReflectionOperations>();
        var mockable = MockableReflectionOperations.Create(mockReflectionOps);
        var mockProvider = new Mock<ICustomAttributeProvider>();

        Action action = () => mockable.GetCustomAttributesCached(mockProvider.Object);
        action.Should().Throw<ArgumentException>().WithParameterName("attributeProvider");
    }

    public void MockableReflectionOperationsGetCustomAttributesCachedShouldThrowForNullProvider()
    {
        var mockReflectionOps = new Mock<IReflectionOperations>();
        var mockable = MockableReflectionOperations.Create(mockReflectionOps);

        Action action = () => mockable.GetCustomAttributesCached(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("attributeProvider");
    }

    #endregion

    #region Helpers

    private static string[] GetAttributeValuePairs(object[] attributes)
    {
        var attribValuePairs = new List<string>();
        foreach (object attrib in attributes)
        {
            if (attrib is DummySingleAAttribute dummySingleAAttribute)
            {
                attribValuePairs.Add("DummySingleA : " + dummySingleAAttribute.Value);
            }
            else if (attrib is DummyAAttribute dummyAAttribute)
            {
                attribValuePairs.Add("DummyA : " + dummyAAttribute.Value);
            }
        }

        return [.. attribValuePairs];
    }

    internal class AttributeMockingHelper
    {
        public AttributeMockingHelper(Mock<IReflectionOperations> mockReflectionOperations) => _mockReflectionOperations = mockReflectionOperations;

        /// <summary>
        /// A collection to hold mock custom attributes.
        /// MemberTypes.All for assembly level
        /// MemberTypes.TypeInfo for class level
        /// MemberTypes.Method for method level.
        /// </summary>
        private readonly List<(Type Type, Attribute Attribute, MemberTypes MemberType)> _data = [];
        private readonly Mock<IReflectionOperations> _mockReflectionOperations;

        /// <summary>
        /// Adds (not replaces) attribute entries of <paramref name="memberTypes"/>
        /// to the internal list used by the mock handler. Successive calls for the
        /// same <paramref name="memberTypes"/> are cumulative — previous entries are
        /// kept alongside new ones.
        /// </summary>
        public void AddCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
        {
            foreach (Attribute attribute in values)
            {
                _data.Add((type, attribute, memberTypes));
            }

            _mockReflectionOperations.Setup(r => r.GetCustomAttributes(It.IsAny<MemberInfo>()))
                .Returns<ICustomAttributeProvider>(GetCustomAttributesNotCached);
            _mockReflectionOperations.Setup(r => r.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>()))
                .Returns<ICustomAttributeProvider, Type>((assembly, _) => GetCustomAttributesNotCached(assembly));
        }

        public object[] GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider)
        {
            var foundAttributes = new List<Attribute>();
            foreach ((Type Type, Attribute Attribute, MemberTypes MemberType) attributeData in _data)
            {
                if (attributeProvider is MethodInfo && (attributeData.MemberType == MemberTypes.Method))
                {
                    foundAttributes.Add(attributeData.Attribute);
                }
                else if (attributeProvider is TypeInfo && (attributeData.MemberType == MemberTypes.TypeInfo))
                {
                    foundAttributes.Add(attributeData.Attribute);
                }
                else if (attributeProvider is Assembly && attributeData.MemberType == MemberTypes.All)
                {
                    foundAttributes.Add(attributeData.Attribute);
                }
            }

            return foundAttributes.ToArray();
        }
    }

    #endregion

    #region Dummy Test Classes

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class DummySealedAttribute : Attribute
    {
        public DummySealedAttribute(string foo) => Value = foo;

        public string Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    public class DummyAAttribute : Attribute
    {
        public DummyAAttribute(string foo) => Value = foo;

        public string Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class DummySingleAAttribute : Attribute
    {
        public DummySingleAAttribute(string foo) => Value = foo;

        public string Value { get; set; }
    }

    [DummyA("ba")]
    private class DummyBaseTestClass
    {
        [DummyA("base")]
        [DummySingleA("base")]
        public virtual void DummyVTestMethod1()
        {
        }

        public void DummyBTestMethod2()
        {
        }
    }

    [DummyA("a")]
    private class DummyTestClass : DummyBaseTestClass
    {
        [DummyA("derived")]
        [DummySingleA("derived")]
        public override void DummyVTestMethod1()
        {
        }

        [DummySingleA("derived")]
        public void DummyTestMethod2()
        {
        }
    }

    #endregion
}

#region Dummy Implementations

internal class TestableExtendedTestMethod : TestMethodAttribute;

#endregion


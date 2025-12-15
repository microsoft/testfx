// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class ReflectHelperTests : TestContainer
{
    private readonly ReflectHelper _reflectHelper;
    private readonly AttributeMockingHelper _attributeMockingHelper;
    private readonly Mock<MethodInfo> _method;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public ReflectHelperTests()
    {
        _reflectHelper = new();
        _method = new Mock<MethodInfo>();
        _method.Setup(x => x.MemberType).Returns(MemberTypes.Method);

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

    /// <summary>
    /// Testing test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtClassLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel"];
        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel1"), new TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel3")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];
        string[] expected = ["MethodLevel", "ClassLevel", "AsmLevel1", "AsmLevel2", "AsmLevel3"];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldConcatCustomAttributeOfSameType()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel1")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel2")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel1")], MemberTypes.Method);
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel2")], MemberTypes.Method);

        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];
        string[] expected = ["MethodLevel1", "MethodLevel2", "ClassLevel1", "ClassLevel2", "AsmLevel1", "AsmLevel2"];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel")], MemberTypes.All);

        string[] expected = ["AsmLevel"];

        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing multiple test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtClassLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("ClassLevel"), new TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel", "ClassLevel1"];
        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing multiple test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("AsmLevel"), new TestCategoryAttribute("AsmLevel1")], MemberTypes.All);

        string[] expected = ["AsmLevel", "AsmLevel1"];
        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];
        expected.SequenceEqual(actual).Should().BeTrue();
    }

    /// <summary>
    /// Testing test category attributes adorned at method level - regression.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtMethodLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(TestCategoryBaseAttribute), [new TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] expected = ["MethodLevel"];
        string[] actual = [.. _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests))];

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    public void IsAttributeDefinedShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeTrue();
    }

    public void IsAttributeDefinedShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestClassAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeFalse();
    }

    public void IsAttributeDefinedShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        MethodInfo memberInfo = typeof(ReflectHelperTests).GetMethod("IsAttributeDefinedShouldReturnFromCache")!;

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();

        // Validate that reflection APIs are not called again.
        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo), Times.Once);

        // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>()), Times.Never);
    }

    public void HasAttributeDerivedFromShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeTrue();
    }

    public void HasAttributeDerivedFromShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns(attributes);

        rh.IsAttributeDefined<TestClassAttribute>(mockMemberInfo.Object).Should().BeFalse();
    }

    public void HasAttributeDerivedFromShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        MethodInfo memberInfo = typeof(ReflectHelperTests).GetMethod("HasAttributeDerivedFromShouldReturnFromCache")!;

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo)).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();

        // Validate that reflection APIs are not called again.
        rh.IsAttributeDefined<TestMethodAttribute>(memberInfo).Should().BeTrue();
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo), Times.Once);

        // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>()), Times.Never);
    }

    public void HasAttributeDerivedFromShouldReturnFalseQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object)).
            Returns((object[])null!);

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(TestMethodAttribute))).
            Returns(attributes);

        rh.IsAttributeDefined<TestMethodAttribute>(mockMemberInfo.Object).Should().BeFalse();
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

        public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
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
}

#region Dummy Implementations

public class TestableExtendedTestMethod : TestMethodAttribute;

#endregion

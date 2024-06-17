// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

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
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel"];
        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel1"), new UTF.TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel3")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("ClassLevel")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        string[] expected = ["MethodLevel", "ClassLevel", "AsmLevel1", "AsmLevel2", "AsmLevel3"];

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldConcatCustomAttributeOfSameType()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel1")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel2")], MemberTypes.All);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("ClassLevel2")], MemberTypes.TypeInfo);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("MethodLevel1")], MemberTypes.Method);
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("MethodLevel2")], MemberTypes.Method);

        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        string[] expected = ["MethodLevel1", "MethodLevel2", "ClassLevel1", "ClassLevel2", "AsmLevel1", "AsmLevel2"];

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel")], MemberTypes.All);

        string[] expected = ["AsmLevel"];

        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing multiple test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtClassLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("ClassLevel"), new UTF.TestCategoryAttribute("ClassLevel1")], MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel", "ClassLevel1"];
        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing multiple test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtAssemblyLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("AsmLevel"), new UTF.TestCategoryAttribute("AsmLevel1")], MemberTypes.All);

        string[] expected = ["AsmLevel", "AsmLevel1"];
        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at method level - regression.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtMethodLevel()
    {
        _attributeMockingHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), [new UTF.TestCategoryAttribute("MethodLevel")], MemberTypes.Method);

        string[] expected = ["MethodLevel"];
        string[] actual = _reflectHelper.GetTestCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    public void IsAttributeDefinedShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(rh.IsNonDerivedAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void IsAttributeDefinedShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestClassAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(!rh.IsNonDerivedAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void IsAttributeDefinedShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        MethodInfo memberInfo = typeof(ReflectHelperTests).GetMethod("IsAttributeDefinedShouldReturnFromCache");

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
            Returns(attributes);

        Verify(rh.IsNonDerivedAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));

        // Validate that reflection APIs are not called again.
        Verify(rh.IsNonDerivedAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo, true), Times.Once);

        // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>(), It.IsAny<bool>()), Times.Never);
    }

    public void HasAttributeDerivedFromShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(rh.IsDerivedAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(!rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        MethodInfo memberInfo = typeof(ReflectHelperTests).GetMethod("HasAttributeDerivedFromShouldReturnFromCache");

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
            Returns(attributes);

        Verify(rh.IsDerivedAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));

        // Validate that reflection APIs are not called again.
        Verify(rh.IsDerivedAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo, true), Times.Once);

        // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>(), It.IsAny<bool>()), Times.Never);
    }

    public void HasAttributeDerivedFromShouldReturnFalseQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns((object[])null);

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true)).
            Returns(attributes);

        Verify(!rh.IsNonDerivedAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void GettingAttributesShouldNotReturnInheritedAttributesWhenAskingForNonInheritedAttributes()
    {
        // This test checks that we get non-inherited attributes when asking for the same type.
        // Reflect helper is internally caching the attributes so we don't ask Reflection for them over and over,
        // and in the past there was a bug that stored the first ask for the attributes in the cache, not differentiating
        // if you asked for inherited, or non-inherited attributes. So if that bug is again put in place you would get 2 attributes
        // in both answers.
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(It.IsAny<Type>(), /* inherit */ true)).
            Returns(new object[] { new TestClassAttribute(), new TestClassAttribute() });

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(It.IsAny<Type>(), /* inherit */ false)).
            Returns(new object[] { new TestClassAttribute() });

        var inheritedAttributes = rh.GetDerivedAttributes<TestClassAttribute>(typeof(object), inherit: true).ToArray();
        var nonInheritedAttributes = rh.GetDerivedAttributes<TestClassAttribute>(typeof(object), inherit: false).ToArray();

        Verify(inheritedAttributes.Length == 2);
        Verify(nonInheritedAttributes.Length == 1);
    }

    internal class AttributeMockingHelper
    {
        public AttributeMockingHelper(Mock<IReflectionOperations> mockReflectionOperations)
        {
            _mockReflectionOperations = mockReflectionOperations;
        }

        /// <summary>
        /// A collection to hold mock custom attributes.
        /// MemberTypes.All for assembly level
        /// MemberTypes.TypeInfo for class level
        /// MemberTypes.Method for method level.
        /// </summary>
        private readonly List<(Type Type, Attribute Attribute, MemberTypes MemberType)> _data = new();
        private readonly Mock<IReflectionOperations> _mockReflectionOperations;

        public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
        {
            foreach (Attribute attribute in values)
            {
                _data.Add((type, attribute, memberTypes));
            }

            _mockReflectionOperations.Setup(r => r.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<bool>()))
                .Returns<ICustomAttributeProvider, bool>(GetCustomAttributesNotCached);
            _mockReflectionOperations.Setup(r => r.GetCustomAttributes(It.IsAny<Assembly>(), It.IsAny<Type>()))
                .Returns<ICustomAttributeProvider, Type>((assembly, _) => GetCustomAttributesNotCached(assembly, false));
        }

        public object[] GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit)
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

public class TestableExtendedTestMethod : UTF.TestMethodAttribute;

#endregion

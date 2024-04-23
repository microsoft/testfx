// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class ReflectHelperTests : TestContainer
{
    private readonly TestableReflectHelper _reflectHelper;
    private readonly Mock<MethodInfo> _method;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public ReflectHelperTests()
    {
        _reflectHelper = new TestableReflectHelper();
        _method = new Mock<MethodInfo>();
        _method.Setup(x => x.MemberType).Returns(MemberTypes.Method);

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
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
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel"];
        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel1"), new UTF.TestCategoryAttribute("AsmLevel2") }, MemberTypes.All);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel3") }, MemberTypes.All);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel") }, MemberTypes.Method);

        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        string[] expected = ["MethodLevel", "ClassLevel", "AsmLevel1", "AsmLevel2", "AsmLevel3"];

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at class, assembly and method level are getting collected.
    /// </summary>
    public void GetTestCategoryAttributeShouldConcatCustomAttributeOfSameType()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel1") }, MemberTypes.All);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel2") }, MemberTypes.All);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel1") }, MemberTypes.TypeInfo);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel2") }, MemberTypes.TypeInfo);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel1") }, MemberTypes.Method);
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel2") }, MemberTypes.Method);

        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        string[] expected = ["MethodLevel1", "MethodLevel2", "ClassLevel1", "ClassLevel2", "AsmLevel1", "AsmLevel2"];

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAssemblyLevel()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel") }, MemberTypes.All);

        string[] expected = ["AsmLevel"];

        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing multiple test category attribute adorned at class level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtClassLevel()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel"), new UTF.TestCategoryAttribute("ClassLevel1") }, MemberTypes.TypeInfo);

        string[] expected = ["ClassLevel", "ClassLevel1"];
        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing multiple test category attributes adorned at assembly level.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtAssemblyLevel()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel"), new UTF.TestCategoryAttribute("AsmLevel1") }, MemberTypes.All);

        string[] expected = ["AsmLevel", "AsmLevel1"];
        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();
        Verify(expected.SequenceEqual(actual));
    }

    /// <summary>
    /// Testing test category attributes adorned at method level - regression.
    /// </summary>
    public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtMethodLevel()
    {
        _reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel") }, MemberTypes.Method);

        string[] expected = ["MethodLevel"];
        var actual = _reflectHelper.GetCategories(_method.Object, typeof(ReflectHelperTests)).ToArray();

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

        Verify(rh.IsAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void IsAttributeDefinedShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestClassAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(!rh.IsAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void IsAttributeDefinedShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        var memberInfo = typeof(ReflectHelperTests).GetMethod("IsAttributeDefinedShouldReturnFromCache");

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
            Returns(attributes);

        Verify(rh.IsAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));

        // Validate that reflection APIs are not called again.
        Verify(rh.IsAttributeDefined<UTF.TestMethodAttribute>(memberInfo, true));
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo, true), Times.Once);

        // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
        _testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>(), It.IsAny<bool>()), Times.Never);
    }

    public void IsAttributeDefinedShouldReturnTrueQueryingASpecificAttributesExistenceEvenIfGettingAllAttributesFail()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new UTF.TestMethodAttribute() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns((object[])null);

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true)).
            Returns(attributes);

        Verify(rh.IsAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(rh.HasAttributeDerivedFrom<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns(attributes);

        Verify(!rh.IsAttributeDefined<UTF.TestClassAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnFromCache()
    {
        var rh = new ReflectHelper();

        // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
        var memberInfo = typeof(ReflectHelperTests).GetMethod("HasAttributeDerivedFromShouldReturnFromCache");

        // new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
            Returns(attributes);

        Verify(rh.HasAttributeDerivedFrom<UTF.TestMethodAttribute>(memberInfo, true));

        // Validate that reflection APIs are not called again.
        Verify(rh.HasAttributeDerivedFrom<UTF.TestMethodAttribute>(memberInfo, true));
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

        Verify(!rh.IsAttributeDefined<UTF.TestMethodAttribute>(mockMemberInfo.Object, true));
    }

    public void HasAttributeDerivedFromShouldReturnTrueQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
    {
        var rh = new ReflectHelper();
        var mockMemberInfo = new Mock<MemberInfo>();
        var attributes = new Attribute[] { new TestableExtendedTestMethod() };

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
            Returns((object[])null);

        _testablePlatformServiceProvider.MockReflectionOperations.
            Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(TestableExtendedTestMethod), true)).
            Returns(attributes);

        Verify(rh.IsAttributeDefined<TestableExtendedTestMethod>(mockMemberInfo.Object, true));
    }
}

#region Dummy Implementations

public class TestableExtendedTestMethod : UTF.TestMethodAttribute
{
}

#endregion

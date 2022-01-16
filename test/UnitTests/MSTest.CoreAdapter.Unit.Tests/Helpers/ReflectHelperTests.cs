// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Moq;

    using TestableImplementations;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ReflectHelperTests
    {
        private TestableReflectHelper reflectHelper;
        private Mock<MethodInfo> method;
        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void IntializeTests()
        {
            this.reflectHelper = new TestableReflectHelper();
            this.method = new Mock<MethodInfo>();
            this.method.Setup(x => x.MemberType).Returns(MemberTypes.Method);

            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.testablePlatformServiceProvider.SetupMockReflectionOperations();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        /// <summary>
        /// Testing test category attribute adorned at class level
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtClassLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);

            string[] expected = new[] { "ClassLevel" };
            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing test category attributes adorned at class, assembly and method level are getting collected.
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel1"), new UTF.TestCategoryAttribute("AsmLevel2") }, MemberTypes.All);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel3") }, MemberTypes.All);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel") }, MemberTypes.Method);

            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();
            string[] expected = new[] { "MethodLevel", "ClassLevel", "AsmLevel1", "AsmLevel2", "AsmLevel3" };

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing test category attributes adorned at class, assembly and method level are getting collected.
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldConcatCustomAttributeOfSameType()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel1") }, MemberTypes.All);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel2") }, MemberTypes.All);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel1") }, MemberTypes.TypeInfo);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel2") }, MemberTypes.TypeInfo);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel1") }, MemberTypes.Method);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel2") }, MemberTypes.Method);

            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();
            string[] expected = new[] { "MethodLevel1", "MethodLevel2", "ClassLevel1", "ClassLevel2", "AsmLevel1", "AsmLevel2" };

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing test category attributes adorned at assembly level
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAssemblyLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel") }, MemberTypes.All);

            string[] expected = new[] { "AsmLevel" };

            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing multiple test category attribute adorned at class level
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtClassLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel"), new UTF.TestCategoryAttribute("ClassLevel1") }, MemberTypes.TypeInfo);

            string[] expected = new[] { "ClassLevel", "ClassLevel1" };
            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing multiple test category attributes adorned at assembly level
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeMultipleTestCategoriesAtAssemblyLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel"), new UTF.TestCategoryAttribute("AsmLevel1") }, MemberTypes.All);

            string[] expected = new[] { "AsmLevel", "AsmLevel1" };
            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing test category attributes adorned at method level - regression
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtMethodLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel") }, MemberTypes.Method);

            string[] expected = new[] { "MethodLevel" };
            var actual = this.reflectHelper.GetCategories(this.method.Object, typeof(ReflectHelperTests)).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void IsAttributeDefinedShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new UTF.TestMethodAttribute() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns(attribs);

            Assert.IsTrue(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true));
        }

        [TestMethod]
        public void IsAttributeDefinedShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new UTF.TestClassAttribute() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns(attribs);

            Assert.IsFalse(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true));
        }

        [TestMethod]
        public void IsAttributeDefinedShouldReturnFromCache()
        {
            var rh = new ReflectHelper();

            // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
            var memberInfo = typeof(ReflectHelperTests).GetMethod("IsAttributeDefinedShouldReturnFromCache");

            // new Mock<MemberInfo>();
            var attribs = new Attribute[] { new UTF.TestMethodAttribute() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
                Returns(attribs);

            Assert.IsTrue(rh.IsAttributeDefined(memberInfo, typeof(UTF.TestMethodAttribute), true));

            // Validate that reflection APIs are not called again.
            Assert.IsTrue(rh.IsAttributeDefined(memberInfo, typeof(UTF.TestMethodAttribute), true));
            this.testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo, true), Times.Once);

            // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
            this.testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void IsAttributeDefinedShouldReturnTrueQueryingASpecificAttributesExistenceEvenIfGettingAllAttributesFail()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new UTF.TestMethodAttribute() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns((object[])null);

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true)).
                Returns(attribs);

            Assert.IsTrue(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true));
        }

        [TestMethod]
        public void HasAttributeDerivedFromShouldReturnTrueIfSpecifiedAttributeIsDefinedOnAMember()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new TestableExtendedTestMethod() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns(attribs);

            Assert.IsTrue(rh.HasAttributeDerivedFrom(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true));
        }

        [TestMethod]
        public void HasAttributeDerivedFromShouldReturnFalseIfSpecifiedAttributeIsNotDefinedOnAMember()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new TestableExtendedTestMethod() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns(attribs);

            Assert.IsFalse(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(UTF.TestClassAttribute), true));
        }

        [TestMethod]
        public void HasAttributeDerivedFromShouldReturnFromCache()
        {
            var rh = new ReflectHelper();

            // Not using mocks here because for some reason a dictionary match of the mock is not returning true in the product code.
            var memberInfo = typeof(ReflectHelperTests).GetMethod("HasAttributeDerivedFromShouldReturnFromCache");

            // new Mock<MemberInfo>();
            var attribs = new Attribute[] { new TestableExtendedTestMethod() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(memberInfo, true)).
                Returns(attribs);

            Assert.IsTrue(rh.HasAttributeDerivedFrom(memberInfo, typeof(UTF.TestMethodAttribute), true));

            // Validate that reflection APIs are not called again.
            Assert.IsTrue(rh.HasAttributeDerivedFrom(memberInfo, typeof(UTF.TestMethodAttribute), true));
            this.testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(memberInfo, true), Times.Once);

            // Also validate that reflection APIs for an individual type is not called since the cache gives us what we need already.
            this.testablePlatformServiceProvider.MockReflectionOperations.Verify(ro => ro.GetCustomAttributes(It.IsAny<MemberInfo>(), It.IsAny<Type>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void HasAttributeDerivedFromShouldReturnFalseQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new TestableExtendedTestMethod() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns((object[])null);

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true)).
                Returns(attribs);

            Assert.IsFalse(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(UTF.TestMethodAttribute), true));
        }

        [TestMethod]
        public void HasAttributeDerivedFromShouldReturnTrueQueryingProvidedAttributesExistenceIfGettingAllAttributesFail()
        {
            var rh = new ReflectHelper();
            var mockMemberInfo = new Mock<MemberInfo>();
            var attribs = new Attribute[] { new TestableExtendedTestMethod() };

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, true)).
                Returns((object[])null);

            this.testablePlatformServiceProvider.MockReflectionOperations.
                Setup(ro => ro.GetCustomAttributes(mockMemberInfo.Object, typeof(TestableExtendedTestMethod), true)).
                Returns(attribs);

            Assert.IsTrue(rh.IsAttributeDefined(mockMemberInfo.Object, typeof(TestableExtendedTestMethod), true));
        }
    }

    #region Dummy Implementations

    public class TestableExtendedTestMethod : UTF.TestMethodAttribute
    {
    }

    #endregion
}

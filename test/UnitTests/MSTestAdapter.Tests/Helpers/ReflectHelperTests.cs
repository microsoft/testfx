// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using Moq;

    using TestableImplementations;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ReflectHelperTests
    {
        private TestableReflectHelper reflectHelper;
        private Mock<MethodInfo> method;

        [TestInitialize]
        public void IntializeTests()
        {
            this.reflectHelper = new TestableReflectHelper();
            this.method = new Mock<MethodInfo>();
            this.method.Setup(x => x.MemberType).Returns(MemberTypes.Method);
            this.method.Setup(x => x.DeclaringType).Returns(typeof(ReflectHelperTests));
        }

        /// <summary>
        /// Testing test category attribute adorned at class level
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtClassLevel()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);
                        
            string[] expected = new[] { "ClassLevel" };
            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Testing test category attributes adorned at calss, assembly and method level are getting collected.
        /// </summary>
        [TestMethod]
        public void GetTestCategoryAttributeShouldIncludeTestCategoriesAtAllLevels()
        {
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("AsmLevel") }, MemberTypes.All);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("ClassLevel") }, MemberTypes.TypeInfo);
            this.reflectHelper.SetCustomAttribute(typeof(UTF.TestCategoryBaseAttribute), new[] { new UTF.TestCategoryAttribute("MethodLevel") }, MemberTypes.Method);

            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();
            string[] expected = new[] { "MethodLevel", "ClassLevel", "AsmLevel" };

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

            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();

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
            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();

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
            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();
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
            var actual = this.reflectHelper.GetCategories(this.method.Object).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

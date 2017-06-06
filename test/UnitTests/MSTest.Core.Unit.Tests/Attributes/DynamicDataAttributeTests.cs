// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.Attributes;

    using Moq;

    using MSTestAdapter.TestUtilities;

    using UnitTestFramework.Tests;

    using CollectionAssert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using DataRowAttribute = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute;
    using TestInitializeV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;

    [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute]
    public class DynamicDataAttributeTests
    {
        private DummyTestClass dummyTestClass;

        private DynamicDataAttribute dynamicDataAttribute;

        private MethodInfo testMethodInfo;

        [TestInitializeV1]
        public void TestInit()
        {
            this.dummyTestClass = new DummyTestClass();
            this.testMethodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        }

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void GetDataShoudThrowExceptionIfInvalidPropertyNameIsSpecifiedOrPropertyDoesNotExist()
        {
            Action action = () =>
                {
                    this.dynamicDataAttribute = new DynamicDataAttribute("ABC");
                    this.dynamicDataAttribute.GetData(this.testMethodInfo);
                };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void GetDataShouldReadDataFromProperty()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void GetDataShouldReadDataFromPropertyInDifferntClass()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty", typeof(DummyTestClass));
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void GetDataShouldReadDataFromMethod()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", DynamicDataSourceType.Method);
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void GetDataShouldReadDataFromMethodInDifferentClass()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", typeof(DummyTestClass), DynamicDataSourceType.Method);
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }
    }

    /// <summary>
    /// The dummy test class.
    /// </summary>
    public class DummyTestClass
    {
        /// <summary>
        /// Gets the reusable test data property.
        /// </summary>
        public static IEnumerable<object[]> ReusableTestDataProperty
        {
            get
            {
                return new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };
            }
        }

        /// <summary>
        /// The reusable test data method.
        /// </summary>
        /// <returns>
        /// The <see cref="IEnumerable"/>.
        /// </returns>
        public static IEnumerable<object[]> ReusableTestDataMethod()
        {
            return new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };
        }

        /// <summary>
        /// The test method 1.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DynamicData("ReusableTestDataProperty")]
        public void TestMethod1()
        {
        }

        /// <summary>
        /// The test method 2.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DynamicData("ReusableTestDataMethod")]
        public void TestMethod2()
        {
        }
    }
}

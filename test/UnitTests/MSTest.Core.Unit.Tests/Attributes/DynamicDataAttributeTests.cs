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

    using MSTestAdapter.TestUtilities;

    using TestFrameworkV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClassAttribute]
    public class DynamicDataAttributeTests
    {
        private DummyTestClass dummyTestClass;

        private DynamicDataAttribute dynamicDataAttribute;

        private MethodInfo testMethodInfo;

        [TestFrameworkV1.TestInitialize]
        public void TestInit()
        {
            this.dummyTestClass = new DummyTestClass();
            this.testMethodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShoudThrowExceptionIfInvalidPropertyNameIsSpecifiedOrPropertyDoesNotExist()
        {
            Action action = () =>
                {
                    this.dynamicDataAttribute = new DynamicDataAttribute("ABC");
                    this.dynamicDataAttribute.GetData(this.testMethodInfo);
                };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReadDataFromProperty()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReadDataFromPropertyInDifferntClass()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty", typeof(DummyTestClass));
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReadDataFromMethod()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", DynamicDataSourceType.Method);
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReadDataFromMethodInDifferentClass()
        {
            var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", typeof(DummyTestClass), DynamicDataSourceType.Method);
            var data = this.dynamicDataAttribute.GetData(methodInfo);
            Assert.IsTrue(data is IEnumerable<object[]>);
            Assert.IsTrue(data.ToList().Count == 2);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldThrowExceptionIfPropertyReturnsNull()
        {
            Action action = () =>
            {
                var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod4");
                this.dynamicDataAttribute = new DynamicDataAttribute("NullProperty", typeof(DummyTestClass));
                this.dynamicDataAttribute.GetData(methodInfo);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldThrowExceptionIfPropertyDoesNotReturnCorrectType()
        {
            Action action = () =>
            {
                var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod3");
                this.dynamicDataAttribute = new DynamicDataAttribute("WrongDataTypeProperty", typeof(DummyTestClass));
                this.dynamicDataAttribute.GetData(methodInfo);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnDisplayName()
        {
            var data = new object[] { 1, 2, 3 };

            var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("TestMethod1 (1,2,3)", displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnEmptyStringIfDataIsNull()
        {
            var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, null);
            Assert.IsNull(displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowIfDataHasNullValues()
        {
            var data = new string[] { "value1", "value2", null };
            var data1 = new string[] { null, "value1", "value2" };
            var data2 = new string[] { "value1", null, "value2" };

            var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("TestMethod1 (value1,value2,)", displayName);

            displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data1);
            Assert.AreEqual("TestMethod1 (,value1,value2)", displayName);

            displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data2);
            Assert.AreEqual("TestMethod1 (value1,,value2)", displayName);
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
        /// Gets the reusable test data property.
        /// </summary>
        public static IEnumerable<object[]> NullProperty
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the reusable test data property.
        /// </summary>
        public static IEnumerable<object[]> WrongDataTypeProperty
        {
            get
            {
                return null;
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

        /// <summary>
        /// The test method 3.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DynamicData("WrongDataTypeProperty")]
        public void TestMethod3()
        {
        }

        /// <summary>
        /// The test method 4.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DynamicData("NullProperty")]
        public void TestMethod4()
        {
        }

        /// <summary>
        /// DataRow test method 1.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DataRow("First", "Second", null)]
        [DataRow(null, "First", "Second")]
        [DataRow("First", null, "Second")]
        public void DataRowTestMethod()
        {
        }
    }
}

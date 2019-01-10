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
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty2", typeof(DummyTestClass2));
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
            this.dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod2", typeof(DummyTestClass2), DynamicDataSourceType.Method);
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
        public void GetDataShouldThrowExceptionIfPropertyReturnsEmpty()
        {
            Action action = () =>
            {
                var methodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod5");
                this.dynamicDataAttribute = new DynamicDataAttribute("EmptyProperty", typeof(DummyTestClass));
                this.dynamicDataAttribute.GetData(methodInfo);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentException));
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
        public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayName()
        {
            var data = new object[] { 1, 2, 3 };

            this.dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName";
            var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("DynamicDataTestWithDisplayName TestMethod1 with 3 parameters", displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayNameInDifferentClass()
        {
            var data = new object[] { 1, 2, 3 };

            this.dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName2";
            this.dynamicDataAttribute.DynamicDataDisplayNameDeclaringType = typeof(DummyTestClass2);
            var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("DynamicDataTestWithDisplayName TestMethod1 with 3 parameters", displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodMissingParameters()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithMissingParameters";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidReturnType()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidReturnType";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidFirstParameterType()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidFirstParameterType";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidSecondParameterType()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidSecondParameterType";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodNonStatic()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameNonStatic";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodPrivate()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNamePrivate";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowExceptionWithMissingDynamicDataDisplayNameMethod()
        {
            Action action = () =>
            {
                var data = new object[] { 1, 2, 3 };

                this.dynamicDataAttribute.DynamicDataDisplayName = "MissingCustomDynamicDataDisplayName";
                var displayName = this.dynamicDataAttribute.GetDisplayName(this.testMethodInfo, data);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
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
        /// Gets the null test data property.
        /// </summary>
        public static IEnumerable<object[]> NullProperty
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the empty test data property.
        /// </summary>
        public static IEnumerable<object[]> EmptyProperty
        {
            get
            {
                return new object[][] { };
            }
        }

        /// <summary>
        /// Gets the wrong test data property i.e. Property returning something other than
        /// expected data type of IEnumerable<object[]>
        /// </summary>
        public static string WrongDataTypeProperty
        {
            get
            {
                return "Dummy";
            }
        }

        /// <summary>
        /// The reusable test data method.
        /// </summary>
        /// <returns>
        /// The <see cref="IEnumerable{T}"/>.
        /// </returns>
        public static IEnumerable<object[]> ReusableTestDataMethod()
        {
            return new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };
        }

        /// <summary>
        /// The custom display name method.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetCustomDynamicDataDisplayName(MethodInfo methodInfo, object[] data)
        {
            return string.Format("DynamicDataTestWithDisplayName {0} with {1} parameters", methodInfo.Name, data.Length);
        }

        /// <summary>
        /// Custom display name method with missing parameters.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetDynamicDataDisplayNameWithMissingParameters()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Custom display name method with invalid return type.
        /// </summary>
        public static void GetDynamicDataDisplayNameWithInvalidReturnType()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Custom display name method with invalid first parameter type.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetDynamicDataDisplayNameWithInvalidFirstParameterType(string methodInfo, object[] data)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Custom display name method with invalid second parameter.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetDynamicDataDisplayNameWithInvalidSecondParameterType(MethodInfo methodInfo, string data)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Custom display name method that is not static.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetDynamicDataDisplayNameNonStatic(MethodInfo methodInfo, object[] data)
        {
            throw new InvalidOperationException();
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
        /// The test method 5.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [DynamicData("EmptyProperty")]
        public void TestMethod5()
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

        /// <summary>
        /// Custom display name method that is private.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string GetDynamicDataDisplayNamePrivate(MethodInfo methodInfo, object[] data)
        {
            throw new InvalidOperationException();
        }
    }

    public class DummyTestClass2
    {
        /// <summary>
        /// Gets the reusable test data property.
        /// </summary>
        public static IEnumerable<object[]> ReusableTestDataProperty2
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
        public static IEnumerable<object[]> ReusableTestDataMethod2()
        {
            return new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };
        }

        /// <summary>
        /// The custom display name method.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetCustomDynamicDataDisplayName2(MethodInfo methodInfo, object[] data)
        {
            return string.Format("DynamicDataTestWithDisplayName {0} with {1} parameters", methodInfo.Name, data.Length);
        }
    }
}

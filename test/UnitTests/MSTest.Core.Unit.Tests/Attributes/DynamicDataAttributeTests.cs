// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

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
    private DummyTestClass _dummyTestClass;
    private DynamicDataAttribute _dynamicDataAttribute;
    private MethodInfo _testMethodInfo;

    [TestFrameworkV1.TestInitialize]
    public void TestInit()
    {
        _dummyTestClass = new DummyTestClass();
        _testMethodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShoudThrowExceptionIfInvalidPropertyNameIsSpecifiedOrPropertyDoesNotExist()
    {
        void action()
        {
            _dynamicDataAttribute = new DynamicDataAttribute("ABC");
            _dynamicDataAttribute.GetData(_testMethodInfo);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldReadDataFromProperty()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Assert.IsNotNull(data);
        Assert.IsTrue(data.ToList().Count == 2);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldReadDataFromPropertyInDifferntClass()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty2", typeof(DummyTestClass2));
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Assert.IsNotNull(data);
        Assert.IsTrue(data.ToList().Count == 2);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldReadDataFromMethod()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", DynamicDataSourceType.Method);
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Assert.IsNotNull(data);
        Assert.IsTrue(data.ToList().Count == 2);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldReadDataFromMethodInDifferentClass()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod2", typeof(DummyTestClass2), DynamicDataSourceType.Method);
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Assert.IsNotNull(data);
        Assert.IsTrue(data.ToList().Count == 2);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldThrowExceptionIfPropertyReturnsNull()
    {
        void action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod4");
            _dynamicDataAttribute = new DynamicDataAttribute("NullProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldThrowExceptionIfPropertyReturnsEmpty()
    {
        void action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod5");
            _dynamicDataAttribute = new DynamicDataAttribute("EmptyProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDataShouldThrowExceptionIfPropertyDoesNotReturnCorrectType()
    {
        void action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod3");
            _dynamicDataAttribute = new DynamicDataAttribute("WrongDataTypeProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldReturnDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Assert.AreEqual("TestMethod1 (1,2,3)", displayName);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName";
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Assert.AreEqual("DynamicDataTestWithDisplayName TestMethod1 with 3 parameters", displayName);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayNameInDifferentClass()
    {
        var data = new object[] { 1, 2, 3 };

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName2";
        _dynamicDataAttribute.DynamicDataDisplayNameDeclaringType = typeof(DummyTestClass2);
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Assert.AreEqual("DynamicDataTestWithDisplayName TestMethod1 with 3 parameters", displayName);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodMissingParameters()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithMissingParameters";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidReturnType()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidReturnType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidFirstParameterType()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidFirstParameterType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidSecondParameterType()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidSecondParameterType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodNonStatic()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameNonStatic";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodPrivate()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNamePrivate";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowExceptionWithMissingDynamicDataDisplayNameMethod()
    {
        void action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "MissingCustomDynamicDataDisplayName";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldReturnEmptyStringIfDataIsNull()
    {
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, null);
        Assert.IsNull(displayName);
    }

    [TestFrameworkV1.TestMethod]
    public void GetDisplayNameShouldThrowIfDataHasNullValues()
    {
        var data = new string[] { "value1", "value2", null };
        var data1 = new string[] { null, "value1", "value2" };
        var data2 = new string[] { "value1", null, "value2" };

        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Assert.AreEqual("TestMethod1 (value1,value2,)", displayName);

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data1);
        Assert.AreEqual("TestMethod1 (,value1,value2)", displayName);

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data2);
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
    public static IEnumerable<object[]> ReusableTestDataProperty => new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };

    /// <summary>
    /// Gets the null test data property.
    /// </summary>
    public static IEnumerable<object[]> NullProperty => null;

    /// <summary>
    /// Gets the empty test data property.
    /// </summary>
    public static IEnumerable<object[]> EmptyProperty => new object[][] { };

    /// <summary>
    /// Gets the wrong test data property i.e. Property returning something other than
    /// expected data type of IEnumerable<object[]>
    /// </summary>
    public static string WrongDataTypeProperty => "Dummy";

    /// <summary>
    /// The reusable test data method.
    /// </summary>
    /// <returns>
    /// The <see cref="IEnumerable{T}"/>.
    /// </returns>
    public static IEnumerable<object[]> ReusableTestDataMethod() => new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };

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
    public static string GetCustomDynamicDataDisplayName(MethodInfo methodInfo, object[] data) => string.Format("DynamicDataTestWithDisplayName {0} with {1} parameters", methodInfo.Name, data.Length);

    /// <summary>
    /// Custom display name method with missing parameters.
    /// </summary>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    public static string GetDynamicDataDisplayNameWithMissingParameters() => throw new InvalidOperationException();

    /// <summary>
    /// Custom display name method with invalid return type.
    /// </summary>
    public static void GetDynamicDataDisplayNameWithInvalidReturnType() => throw new InvalidOperationException();

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
    public static string GetDynamicDataDisplayNameWithInvalidFirstParameterType(string methodInfo, object[] data) => throw new InvalidOperationException();

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
    public static string GetDynamicDataDisplayNameWithInvalidSecondParameterType(MethodInfo methodInfo, string data) => throw new InvalidOperationException();

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
    public string GetDynamicDataDisplayNameNonStatic(MethodInfo methodInfo, object[] data) => throw new InvalidOperationException();

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
    private static string GetDynamicDataDisplayNamePrivate(MethodInfo methodInfo, object[] data) => throw new InvalidOperationException();
}

public class DummyTestClass2
{
    /// <summary>
    /// Gets the reusable test data property.
    /// </summary>
    public static IEnumerable<object[]> ReusableTestDataProperty2 => new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };

    /// <summary>
    /// The reusable test data method.
    /// </summary>
    /// <returns>
    /// The <see cref="IEnumerable"/>.
    /// </returns>
    public static IEnumerable<object[]> ReusableTestDataMethod2() => new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };

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
    public static string GetCustomDynamicDataDisplayName2(MethodInfo methodInfo, object[] data) => string.Format("DynamicDataTestWithDisplayName {0} with {1} parameters", methodInfo.Name, data.Length);
}

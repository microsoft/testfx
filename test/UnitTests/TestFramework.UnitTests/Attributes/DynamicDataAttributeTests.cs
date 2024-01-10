// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class DynamicDataAttributeTests : TestContainer
{
    private readonly DummyTestClass _dummyTestClass;
    private readonly MethodInfo _testMethodInfo;
    private DynamicDataAttribute _dynamicDataAttribute;

    public DynamicDataAttributeTests()
    {
        _dummyTestClass = new DummyTestClass();
        _testMethodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
    }

    public void GetDataShouldThrowExceptionIfInvalidPropertyNameIsSpecifiedOrPropertyDoesNotExist()
    {
        void Action()
        {
            _dynamicDataAttribute = new DynamicDataAttribute("ABC");
            _dynamicDataAttribute.GetData(_testMethodInfo);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDataShouldReadDataFromProperty()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromPropertyInDifferentClass()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty2", typeof(DummyTestClass2));
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromMethod()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", DynamicDataSourceType.Method);
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromMethodInDifferentClass()
    {
        var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod2", typeof(DummyTestClass2), DynamicDataSourceType.Method);
        var data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldThrowExceptionIfPropertyReturnsNull()
    {
        void Action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod4");
            _dynamicDataAttribute = new DynamicDataAttribute("NullProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDataShouldThrowExceptionIfPropertyReturnsEmpty()
    {
        void Action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod5");
            _dynamicDataAttribute = new DynamicDataAttribute("EmptyProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentException);
    }

    public void GetDataShouldThrowExceptionIfPropertyDoesNotReturnCorrectType()
    {
        void Action()
        {
            var methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod3");
            _dynamicDataAttribute = new DynamicDataAttribute("WrongDataTypeProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldReturnDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify("TestMethod1 (1,2,3)".SequenceEqual(displayName));
    }

    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName";
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DynamicDataTestWithDisplayName TestMethod1 with 3 parameters");
    }

    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayNameInDifferentClass()
    {
        var data = new object[] { 1, 2, 3 };

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName2";
        _dynamicDataAttribute.DynamicDataDisplayNameDeclaringType = typeof(DummyTestClass2);
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DynamicDataTestWithDisplayName TestMethod1 with 3 parameters");
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodMissingParameters()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithMissingParameters";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidReturnType()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidReturnType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidFirstParameterType()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidFirstParameterType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidSecondParameterType()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidSecondParameterType";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodNonStatic()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameNonStatic";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodPrivate()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNamePrivate";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldThrowExceptionWithMissingDynamicDataDisplayNameMethod()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dynamicDataAttribute.DynamicDataDisplayName = "MissingCustomDynamicDataDisplayName";
            var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayNameShouldReturnEmptyStringIfDataIsNull()
    {
        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, null);
        Verify(displayName is null);
    }

    public void GetDisplayNameHandlesNullValues()
    {
        var data = new string[] { "value1", "value2", null };
        var data1 = new string[] { null, "value1", "value2" };
        var data2 = new string[] { "value1", null, "value2" };

        var displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "TestMethod1 (value1,value2,)");

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data1);
        Verify(displayName == "TestMethod1 (,value1,value2)");

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data2);
        Verify(displayName == "TestMethod1 (value1,,value2)");
    }
}

/// <summary>
/// The dummy test class.
/// </summary>
[TestClass]
public class DummyTestClass
{
    /// <summary>
    /// Gets the reusable test data property.
    /// </summary>
    public static IEnumerable<object[]> ReusableTestDataProperty
    {
        get
        {
            return new[] { new object[] { 1, 2, 3 }, [4, 5, 6] };
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
            return Array.Empty<object[]>();
        }
    }

    /// <summary>
    /// Gets the wrong test data property i.e. Property returning something other than
    /// expected data type of <see cref="IEnumerable{T}"/>.
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
        return new[] { new object[] { 1, 2, 3 }, [4, 5, 6] };
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
        => $"DynamicDataTestWithDisplayName {methodInfo.Name} with {data.Length} parameters";

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
    [TestMethod]
    [DynamicData("ReusableTestDataProperty")]
    public void TestMethod1()
    {
    }

    /// <summary>
    /// The test method 2.
    /// </summary>
    [TestMethod]
    [DynamicData("ReusableTestDataMethod")]
    public void TestMethod2()
    {
    }

    /// <summary>
    /// The test method 3.
    /// </summary>
    [TestMethod]
    [DynamicData("WrongDataTypeProperty")]
    public void TestMethod3()
    {
    }

    /// <summary>
    /// The test method 4.
    /// </summary>
    [TestMethod]
    [DynamicData("NullProperty")]
    public void TestMethod4()
    {
    }

    /// <summary>
    /// The test method 5.
    /// </summary>
    [TestMethod]
    [DynamicData("EmptyProperty")]
    public void TestMethod5()
    {
    }

    /// <summary>
    /// DataRow test method 1.
    /// </summary>
    [DataRow("First", "Second", null)]
    [DataRow(null, "First", "Second")]
    [DataRow("First", null, "Second")]
    [TestMethod]
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
#pragma warning disable IDE0051 // Remove unused private members
    private static string GetDynamicDataDisplayNamePrivate(MethodInfo methodInfo, object[] data)
#pragma warning restore IDE0051 // Remove unused private members
    {
        throw new InvalidOperationException();
    }
}

public class DisplayNameTestClass
{
    [TestMethod(displayName: "MethodDisplayName")]
    public void TestMethodWithDisplayName()
    {
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
            return new[] { new object[] { 1, 2, 3 }, [4, 5, 6] };
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
        return new[] { new object[] { 1, 2, 3 }, [4, 5, 6] };
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
        => $"DynamicDataTestWithDisplayName {methodInfo.Name} with {data.Length} parameters";
}

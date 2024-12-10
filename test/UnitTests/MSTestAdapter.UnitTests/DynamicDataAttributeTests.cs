// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
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

        // Initializes DynamicDataProvider. Normally this happens automatically but we are running outside of test adapter.
        _ = PlatformServiceProvider.Instance;
        DynamicDataAttribute.TestIdGenerationStrategy = TestIdGenerationStrategy.FullyQualified;
    }

    public void GetDataShouldThrowExceptionIfInvalidPropertyNameIsSpecifiedOrPropertyDoesNotExist() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            _dynamicDataAttribute = new DynamicDataAttribute("ABC");
            _dynamicDataAttribute.GetData(_testMethodInfo);
        });

    public void GetDataShouldReadDataFromProperty()
    {
        MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty");
        IEnumerable<object[]> data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromPropertyInDifferentClass()
    {
        MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataProperty2", typeof(DummyTestClass2));
        IEnumerable<object[]> data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromMethod()
    {
        MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod", DynamicDataSourceType.Method);
        IEnumerable<object[]> data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldReadDataFromMethodInDifferentClass()
    {
        MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod2");
        _dynamicDataAttribute = new DynamicDataAttribute("ReusableTestDataMethod2", typeof(DummyTestClass2), DynamicDataSourceType.Method);
        IEnumerable<object[]> data = _dynamicDataAttribute.GetData(methodInfo);
        Verify(data is not null);
        Verify(data.ToList().Count == 2);
    }

    public void GetDataShouldThrowExceptionIfPropertyReturnsNull() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod4");
            _dynamicDataAttribute = new DynamicDataAttribute("NullProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        });

    public void GetDataShouldNotThrowExceptionIfPropertyReturnsEmpty()
    {
        MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod5");
        _dynamicDataAttribute = new DynamicDataAttribute("EmptyProperty", typeof(DummyTestClass));
        IEnumerable<object[]> data = _dynamicDataAttribute.GetData(methodInfo);
        // The callers in AssemblyEnumerator and TestMethodRunner are responsible
        // for throwing an exception if data is empty and ConsiderEmptyDataSourceAsInconclusive is false.
        Verify(!data.Any());
    }

    public void GetDataShouldThrowExceptionIfPropertyDoesNotReturnCorrectType() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            MethodInfo methodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod3");
            _dynamicDataAttribute = new DynamicDataAttribute("WrongDataTypeProperty", typeof(DummyTestClass));
            _dynamicDataAttribute.GetData(methodInfo);
        });

    public void GetDisplayNameShouldReturnDisplayName()
    {
        object[] data = [1, 2, 3];

        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify("TestMethod1 (1,2,3)".SequenceEqual(displayName));
    }

    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayName()
    {
        object[] data = [1, 2, 3];

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName";
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DynamicDataTestWithDisplayName TestMethod1 with 3 parameters");
    }

    public void GetDisplayNameShouldReturnDisplayNameWithDynamicDataDisplayNameInDifferentClass()
    {
        object[] data = [1, 2, 3];

        _dynamicDataAttribute.DynamicDataDisplayName = "GetCustomDynamicDataDisplayName2";
        _dynamicDataAttribute.DynamicDataDisplayNameDeclaringType = typeof(DummyTestClass2);
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DynamicDataTestWithDisplayName TestMethod1 with 3 parameters");
    }

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodMissingParameters() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithMissingParameters";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidReturnType() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidReturnType";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidFirstParameterType() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidFirstParameterType";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodInvalidSecondParameterType() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameWithInvalidSecondParameterType";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodNonStatic() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNameNonStatic";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithDynamicDataDisplayNameMethodPrivate() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "GetDynamicDataDisplayNamePrivate";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldThrowExceptionWithMissingDynamicDataDisplayNameMethod() =>
        VerifyThrows<ArgumentNullException>(() =>
        {
            object[] data = [1, 2, 3];

            _dynamicDataAttribute.DynamicDataDisplayName = "MissingCustomDynamicDataDisplayName";
            _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        });

    public void GetDisplayNameShouldReturnEmptyStringIfDataIsNull()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, null);
        Verify(displayName is null);
    }

    public void GetDisplayNameHandlesNullValues()
    {
        string[] data = ["value1", "value2", null];
        string[] data1 = [null, "value1", "value2"];
        string[] data2 = ["value1", null, "value2"];

        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "TestMethod1 (\"value1\",\"value2\",null)");

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data1);
        Verify(displayName == "TestMethod1 (null,\"value1\",\"value2\")");

        displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, data2);
        Verify(displayName == "TestMethod1 (\"value1\",null,\"value2\")");
    }

    public void GetDisplayNameForArrayOfMultipleItems()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, [new[] { "a", "b", "c" }]);
        Verify(displayName == "TestMethod1 ([\"a\",\"b\",\"c\"])");
    }

    public void GetDisplayNameForMultipleArraysOfOneItem()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, [new[] { "a" }, new[] { "1" }]);
        Verify(displayName == "TestMethod1 ([\"a\"],[\"1\"])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItems()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, [new[] { "a", "b", "c" }, new[] { "1", "2", "3" }]);
        Verify(displayName == "TestMethod1 ([\"a\",\"b\",\"c\"],[\"1\",\"2\",\"3\"])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItemsValueTypes()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, [new[] { 1, 2, 3 }, new[] { 4, 5, 6 }]);
        Verify(displayName == "TestMethod1 ([1,2,3],[4,5,6])");
    }

    public void GetDisplayNameForMultipleArraysOfArraysOfMultipleItems()
    {
        string displayName = _dynamicDataAttribute.GetDisplayName(_testMethodInfo, [new[] { ["a", "b", "c"], ["d", "e", "f"], new[] { "gh", "ij", "kl" } }, new[] { 'm', 'n', 'o' }, new[] { ["1", "2", "3"], ["4", "5", "6"], new[] { "7", "8", "9" } }]);
        Verify(displayName == "TestMethod1 ([[\"a\",\"b\",\"c\"],[\"d\",\"e\",\"f\"],[\"gh\",\"ij\",\"kl\"]],['m','n','o'],[[\"1\",\"2\",\"3\"],[\"4\",\"5\",\"6\"],[\"7\",\"8\",\"9\"]])");
    }

#if NETCOREAPP
    public void DynamicDataSource_WithTuple_Works()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithTuple), typeof(TestClassTupleData));
        dynamicDataAttribute.GetData(testMethodInfo);

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithTuple), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        dynamicDataAttribute.GetData(testMethodInfo);
    }

    public void DynamicDataSource_WithValueTuple_Works()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithValueTuple), typeof(TestClassTupleData));
        dynamicDataAttribute.GetData(testMethodInfo);

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithValueTuple), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        dynamicDataAttribute.GetData(testMethodInfo);
    }

    public void DynamicDataSource_WithValueTupleWithTupleSyntax_Works()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithValueTupleWithTupleSyntax), typeof(TestClassTupleData));
        dynamicDataAttribute.GetData(testMethodInfo);

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithValueTupleWithTupleSyntax), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        dynamicDataAttribute.GetData(testMethodInfo);
    }
#else
    public void DynamicDataSource_WithTuple_Throws()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithTuple), typeof(TestClassTupleData), DynamicDataSourceType.Property);

        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithTuple), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));
    }

    public void DynamicDataSource_WithValueTuple_Throws()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithValueTuple), typeof(TestClassTupleData), DynamicDataSourceType.Property);
        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithValueTuple), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));
    }

    public void DynamicDataSource_WithValueTupleWithTupleSyntax_Throws()
    {
        MethodInfo testMethodInfo = new TestClassTupleData().GetType().GetTypeInfo().GetDeclaredMethod(nameof(TestClassTupleData.DynamicDataTestWithTuple));
        var dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.DataWithValueTupleWithTupleSyntax), typeof(TestClassTupleData), DynamicDataSourceType.Property);
        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));

        dynamicDataAttribute = new DynamicDataAttribute(nameof(TestClassTupleData.GetDataWithValueTupleWithTupleSyntax), typeof(TestClassTupleData), DynamicDataSourceType.Method);
        VerifyThrows<ArgumentNullException>(() => dynamicDataAttribute.GetData(testMethodInfo));
    }
#endif
}

/// <summary>
/// The dummy test class.
/// </summary>
[TestClass]
internal class DummyTestClass
{
    /// <summary>
    /// Gets the reusable test data property.
    /// </summary>
    public static IEnumerable<object[]> ReusableTestDataProperty => [[1, 2, 3], [4, 5, 6]];

    /// <summary>
    /// Gets the null test data property.
    /// </summary>
    public static IEnumerable<object[]> NullProperty => null;

    /// <summary>
    /// Gets the empty test data property.
    /// </summary>
    public static IEnumerable<object[]> EmptyProperty => Array.Empty<object[]>();

    /// <summary>
    /// Gets the wrong test data property i.e. Property returning something other than
    /// expected data type of <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static string WrongDataTypeProperty => "Dummy";

    /// <summary>
    /// The reusable test data method.
    /// </summary>
    /// <returns>
    /// The <see cref="IEnumerable{T}"/>.
    /// </returns>
    public static IEnumerable<object[]> ReusableTestDataMethod() => [[1, 2, 3], [4, 5, 6]];

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
    private static string GetDynamicDataDisplayNamePrivate(MethodInfo methodInfo, object[] data) => throw new InvalidOperationException();
}

public class DummyTestClass2
{
    /// <summary>
    /// Gets the reusable test data property.
    /// </summary>
    public static IEnumerable<object[]> ReusableTestDataProperty2 => [[1, 2, 3], [4, 5, 6]];

    /// <summary>
    /// The reusable test data method.
    /// </summary>
    /// <returns>
    /// The <see cref="IEnumerable"/>.
    /// </returns>
    public static IEnumerable<object[]> ReusableTestDataMethod2() => [[1, 2, 3], [4, 5, 6]];

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

[TestClass]
internal class TestClassTupleData
{
    public static IEnumerable<Tuple<int, string>> GetDataWithTuple()
    {
        yield return new(0, "0");
        yield return new(1, "1");
    }

    public static IEnumerable<Tuple<int, string>> DataWithTuple
    {
        get
        {
            yield return new(0, "0");
            yield return new(1, "1");
        }
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1141:Use tuple syntax", Justification = "We want to explicitly test this syntax")]
    public static IEnumerable<ValueTuple<int, string>> GetDataWithValueTuple()
    {
        yield return new(0, "0");
        yield return new(1, "1");
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1141:Use tuple syntax", Justification = "We want to explicitly test this syntax")]
    public static IEnumerable<ValueTuple<int, string>> DataWithValueTuple
    {
        get
        {
            yield return new(0, "0");
            yield return new(1, "1");
        }
    }

    public static IEnumerable<(int Integer, string AsString)> GetDataWithValueTupleWithTupleSyntax()
    {
        yield return (0, "0");
        yield return (1, "1");
    }

    public static IEnumerable<(int Integer, string AsString)> DataWithValueTupleWithTupleSyntax
    {
        get
        {
            yield return (0, "0");
            yield return (1, "1");
        }
    }

    [DataTestMethod]
    public void DynamicDataTestWithTuple(int value, string integerAsString)
    {
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;
public class DataRowAttributeTests : TestContainer
{
    private readonly DummyDataRowTestClass _dummyTestClass;
    private readonly MethodInfo _testMethodInfo;
    private DataRowAttribute _dataRowAttribute;

    public DataRowAttributeTests()
    {
        _dummyTestClass = new DummyDataRowTestClass();
        _testMethodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
        _dataRowAttribute = new DataRowAttribute();
    }

    public void GetDisplayName_WithDisplayNameMethod_ShouldReturnDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        _dataRowAttribute.DisplayNameMethod = "GetCustomDisplayName";
        var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DataRowTestWithDisplayNameMethod TestMethod1 with 3 parameters");
    }

    public void GetDisplayName_WithDisplayNameMethodInDifferentClass_ShouldReturnDisplayName()
    {
        var data = new object[] { 1, 2, 3 };

        _dataRowAttribute.DisplayNameMethod = "GetCustomDisplayName2";
        _dataRowAttribute.DisplayNameMethodDeclaringType = typeof(DummyDataRowTestClass2);
        var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        Verify(displayName == "DataRowTestWithDisplayNameMethod TestMethod1 with 3 parameters");
    }

    public void GetDisplayName_WithDisplayNameMethodMissingParameters_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodWithMissingParameters";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithDisplayNameMethodInvalidReturnType_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodWithInvalidReturnType";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithDisplayNameMethodInvalidFirstParameterType_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodWithInvalidFirstParameterType";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithDisplayNameMethodInvalidSecondParameterType_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodWithInvalidSecondParameterType";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithDisplayNameMethodNonStatic_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodNonStatic";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithDisplayNameMethodPrivate_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "GetDisplayNameMethodPrivate";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void GetDisplayName_WithMissingDisplayNameMethod_ShouldThrowException()
    {
        void Action()
        {
            var data = new object[] { 1, 2, 3 };

            _dataRowAttribute.DisplayNameMethod = "MissingCustomDisplayNameMethod";
            var displayName = _dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        }

        var ex = VerifyThrows(Action);
        Verify(ex is ArgumentNullException);
    }

    public void DefaultConstructorSetsEmptyArrayPassed()
    {
        var dataRow = new DataRowAttribute();

        Verify(System.Array.Empty<object>().SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        Verify(new object[] { "mercury" }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetNullDataPassed()
    {
        var dataRow = new DataRowAttribute(null);

        Verify(new object[] { null }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetMultipleDataValuesPassed()
    {
        var dataRow = new DataRowAttribute("mercury", "venus", "earth");

        Verify(new object[] { "mercury", "venus", "earth" }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetANullDataValuePassedInParams()
    {
        var dataRow = new DataRowAttribute("neptune", null);

        Verify(new object[] { "neptune", null }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetANullDataValuePassedInAsADataArg()
    {
        var dataRow = new DataRowAttribute(null, "logos");

        Verify(new object[] { null, "logos" }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetMultipleDataArrays()
    {
        // Fixes https://github.com/microsoft/testfx/issues/1180
        var dataRow = new DataRowAttribute(new[] { "a" }, new[] { "b" });

        Verify(dataRow.Data.Length == 2);
        Verify(dataRow.Data[0] is string[] array1 && array1.SequenceEqual(new[] { "a" }));
        Verify(dataRow.Data[1] is string[] array2 && array2.SequenceEqual(new[] { "b" }));
    }

    public void GetDataShouldReturnDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        Verify(new object[] { "mercury" }.SequenceEqual(dataRow.GetData(null).Single()));
    }

    public void GetDisplayNameShouldReturnAppropriateName()
    {
        var dataRowAttribute = new DataRowAttribute(null);

        var dummyTestClass = new DummyTestClass();
        var testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        var data = new string[] { "First", "Second", null };
        var data1 = new string[] { null, "First", "Second" };
        var data2 = new string[] { "First", null, "Second" };

        var displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
        Verify(displayName == "DataRowTestMethod (First,Second,)");

        displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data1);
        Verify(displayName == "DataRowTestMethod (,First,Second)");

        displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data2);
        Verify(displayName == "DataRowTestMethod (First,,Second)");
    }

    public void GetDisplayNameShouldReturnSpecifiedDisplayName()
    {
        var dataRowAttribute = new DataRowAttribute(null)
        {
            DisplayName = "DataRowTestWithDisplayName",
        };

        var dummyTestClass = new DummyTestClass();
        var testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        var data = new string[] { "First", "Second", null };

        var displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
        Verify(displayName == "DataRowTestWithDisplayName");
    }

    public void GetDisplayNameForArrayOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        var displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod (System.String[])");
    }

    public void GetDisplayName_AfterOverriding_GetsTheNewDisplayName()
    {
        // Arrange
        var dataRow = new DummyDataRowAttribute();
        var methodInfoMock = new Mock<MethodInfo>();

        // Act
        var displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "Overridden DisplayName");
    }

    public void GetDisplayNameForArrayOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a", "b", "c" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        var displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod (System.String[])");
    }

    public void GetDisplayNameForMultipleArraysOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a" }, new[] { "1" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        var displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod (System.String[],System.String[])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a", "b", "c" }, new[] { "1", "2", "3" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        var displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod (System.String[],System.String[])");
    }

    private class DummyDataRowAttribute : DataRowAttribute
    {
        public DummyDataRowAttribute()
            : base()
        {
        }

        public override string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            return "Overridden DisplayName";
        }
    }

    [TestClass]
    private class DummyDataRowTestClass
    {
        [TestMethod]
        [DataRow(new[] { 1, 2, 3 })]
        public void TestMethod1()
        {
        }

        /// <summary>
        /// Custom display name method with missing parameters.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetDisplayNameMethodWithMissingParameters()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Custom display name method with invalid return type.
        /// </summary>
        public static void GetDisplayNameMethodWithInvalidReturnType()
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
        public static string GetDisplayNameMethodWithInvalidFirstParameterType(string methodInfo, object[] data)
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
        public static string GetDisplayNameMethodWithInvalidSecondParameterType(MethodInfo methodInfo, string data)
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
        public string GetDisplayNameMethodNonStatic(MethodInfo methodInfo, object[] data)
        {
            throw new InvalidOperationException();
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
        private static string GetDisplayNameMethodPrivate(MethodInfo methodInfo, object[] data)
        {
            throw new InvalidOperationException();
        }

        public static string GetCustomDisplayName(MethodInfo methodInfo, object[] data)
        {
            return string.Format("DataRowTestWithDisplayNameMethod {0} with {1} parameters", methodInfo.Name, data.Length);
        }
    }

    private class DummyDataRowTestClass2
    {
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
        public static string GetCustomDisplayName2(MethodInfo methodInfo, object[] data)
        {
            return string.Format("DataRowTestWithDisplayNameMethod {0} with {1} parameters", methodInfo.Name, data.Length);
        }
    }
}

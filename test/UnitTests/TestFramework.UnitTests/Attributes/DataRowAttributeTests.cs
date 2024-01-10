// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class DataRowAttributeTests : TestContainer
{
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
}

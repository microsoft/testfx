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
        MethodInfo testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        string[] data = ["First", "Second", null];
        string[] data1 = [null, "First", "Second"];
        string[] data2 = ["First", null, "Second"];

        string displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
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
        MethodInfo testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        string[] data = ["First", "Second", null];

        string displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
        Verify(displayName == "DataRowTestWithDisplayName");
    }

    public void GetDisplayNameForArrayOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(["a"]);
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod ([a])");
    }

    public void GetDisplayName_AfterOverriding_GetsTheNewDisplayName()
    {
        // Arrange
        var dataRow = new DummyDataRowAttribute();
        var methodInfoMock = new Mock<MethodInfo>();

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "Overridden DisplayName");
    }

    public void GetDisplayNameForArrayOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(["a", "b", "c"]);
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod ([a,b,c])");
    }

    public void GetDisplayNameForMultipleArraysOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a" }, new[] { "1" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod ([a],[1])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a", "b", "c" }, new[] { "1", "2", "3" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod ([a,b,c],[1,2,3])");
    }

    public void GetDisplayNameForMultipleArraysOfArraysOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { new[] { "a", "b", "c" }, new[] { "d", "e", "f" }, new[] { "g", "h", "i" } }, new[] { new[] { "1", "2", "3" }, new[] { "4", "5", "6" }, new[] { "7", "8", "9" } });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        Verify(displayName == "MyMethod ([[a,b,c],[d,e,f],[g,h,i]],[[1,2,3],[4,5,6],[7,8,9]])");
    }

    private class DummyDataRowAttribute : DataRowAttribute
    {
        public override string GetDisplayName(MethodInfo methodInfo, object[] data) => "Overridden DisplayName";
    }
}

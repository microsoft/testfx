// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class DataRowAttributeTests : TestContainer
{
    public void DefaultConstructorSetsEmptyArrayPassed()
    {
        var dataRow = new DataRowAttribute();

        dataRow.Data.Should().BeEquivalentTo(Array.Empty<object>());
    }

    public void ConstructorShouldSetDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        dataRow.Data.Should().BeEquivalentTo(new object[] { "mercury" });
    }

    public void ConstructorShouldSetNullDataPassed()
    {
        var dataRow = new DataRowAttribute(null);

        dataRow.Data.Should().BeEquivalentTo(new object?[] { null });
    }

    public void ConstructorShouldSetMultipleDataValuesPassed()
    {
        var dataRow = new DataRowAttribute("mercury", "venus", "earth");

        dataRow.Data.Should().BeEquivalentTo(new object[] { "mercury", "venus", "earth" });
    }

    public void ConstructorShouldSetANullDataValuePassedInParams()
    {
        var dataRow = new DataRowAttribute("neptune", null);

        dataRow.Data.Should().BeEquivalentTo(new object?[] { "neptune", null });
    }

    public void ConstructorShouldSetANullDataValuePassedInAsADataArg()
    {
        var dataRow = new DataRowAttribute(null, "logos");

        dataRow.Data.Should().BeEquivalentTo(new object?[] { null, "logos" });
    }

    public void ConstructorShouldSetMultipleDataArrays()
    {
        // Fixes https://github.com/microsoft/testfx/issues/1180
        var dataRow = new DataRowAttribute(new[] { "a" }, new[] { "b" });

        dataRow.Data.Should().HaveCount(2);
        dataRow.Data[0].Should().BeOfType<string[]>().Which.Should().BeEquivalentTo(["a"]);
        dataRow.Data[1].Should().BeOfType<string[]>().Which.Should().BeEquivalentTo(["b"]);
    }

    public void GetDataShouldReturnDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        dataRow.GetData(null!).Single().Should().BeEquivalentTo(new object[] { "mercury" });
    }

    public void GetDisplayNameShouldReturnAppropriateName()
    {
        var dataRowAttribute = new DataRowAttribute(null);

        var dummyTestClass = new DummyTestClass();
        MethodInfo testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod")!;

        string?[] data = ["First", "Second", null];
        string?[] data1 = [null, "First", "Second"];
        string?[] data2 = ["First", null, "Second"];

        string? displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
        displayName.Should().Be("DataRowTestMethod (\"First\",\"Second\",null)");

        displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data1);
        displayName.Should().Be("DataRowTestMethod (null,\"First\",\"Second\")");

        displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data2);
        displayName.Should().Be("DataRowTestMethod (\"First\",null,\"Second\")");
    }

    public void GetDisplayNameShouldReturnSpecifiedDisplayName()
    {
        var dataRowAttribute = new DataRowAttribute(null)
        {
            DisplayName = "DataRowTestWithDisplayName",
        };

        var dummyTestClass = new DummyTestClass();
        MethodInfo testMethodInfo = dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod")!;

        string?[] data = ["First", "Second", null];

        string? displayName = dataRowAttribute.GetDisplayName(testMethodInfo, data);
        displayName.Should().Be("DataRowTestWithDisplayName");
    }

    public void GetDisplayNameForArrayOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(["a"]);
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([\"a\"])");
    }

    public void GetDisplayName_AfterOverriding_GetsTheNewDisplayName()
    {
        // Arrange
        var dataRow = new DummyDataRowAttribute();
        var methodInfoMock = new Mock<MethodInfo>();

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("Overridden DisplayName");
    }

    public void GetDisplayNameForArrayOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(["a", "b", "c"]);
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([\"a\",\"b\",\"c\"])");
    }

    public void GetDisplayNameForMultipleArraysOfOneItem()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a" }, new[] { "1" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([\"a\"],[\"1\"])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { "a", "b", "c" }, new[] { "1", "2", "3" });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([\"a\",\"b\",\"c\"],[\"1\",\"2\",\"3\"])");
    }

    public void GetDisplayNameForMultipleArraysOfMultipleItemsValueTypes()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { 1, 2, 3 }, new[] { 4, 5, 6 });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([1,2,3],[4,5,6])");
    }

    public void GetDisplayNameForMultipleArraysOfArraysOfMultipleItems()
    {
        // Arrange
        var dataRow = new DataRowAttribute(new[] { ["a", "b", "c"], ["d", "e", "f"], new[] { "gh", "ij", "kl" } }, new[] { 'm', 'n', 'o' }, new[] { ["1", "2", "3"], ["4", "5", "6"], new[] { "7", "8", "9" } });
        var methodInfoMock = new Mock<MethodInfo>();
        methodInfoMock.SetupGet(x => x.Name).Returns("MyMethod");

        // Act
        string? displayName = dataRow.GetDisplayName(methodInfoMock.Object, dataRow.Data);

        // Assert
        displayName.Should().Be("MyMethod ([[\"a\",\"b\",\"c\"],[\"d\",\"e\",\"f\"],[\"gh\",\"ij\",\"kl\"]],['m','n','o'],[[\"1\",\"2\",\"3\"],[\"4\",\"5\",\"6\"],[\"7\",\"8\",\"9\"]])");
    }

    public void GetDisplayNameTreatsDataAsOneArrayForSingleObjectArrayParameter()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DataRowObjectArrayTestMethod))!;

        string? displayName = new DataRowAttribute().GetDisplayName(methodInfo, ["a", null, 'b']);

        displayName.Should().Be("DataRowObjectArrayTestMethod ([\"a\",null,'b'])");
    }

    public void GetDisplayNamePreservesRawStringAndCharacterContents()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DataRowTestMethod))!;

        string? displayName = new DataRowAttribute().GetDisplayName(methodInfo, ["quote\"\\\n", '\'']);

        displayName.Should().Be("DataRowTestMethod (\"quote\"\\\n\",''')");
    }

    public void GetDisplayNameUsesCurrentCultureForValues()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DataRowTestMethod))!;

            string? displayName = new DataRowAttribute().GetDisplayName(methodInfo, [1234.5m]);

            displayName.Should().Be("DataRowTestMethod (1234,5)");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void GetDisplayNameCapturesLocalizedFormatBeforeFormattingValues()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DataRowTestMethod))!;

            string? displayName = new DataRowAttribute().GetDisplayName(methodInfo, [new UICultureChangingValue()]);

            displayName.Should().Be("DataRowTestMethod (value)");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    public void GetDisplayNameRefreshesLocalizedFormatWhenUICultureChanges()
    {
        CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DataRowTestMethod))!;
            var attribute = new DataRowAttribute();

            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            attribute.GetDisplayName(methodInfo, ["value"]).Should().Be("DataRowTestMethod (\"value\")");

            CultureInfo.CurrentUICulture = new CultureInfo("ko-KR");
            attribute.GetDisplayName(methodInfo, ["value"]).Should().Be("DataRowTestMethod(\"value\")");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private class DummyDataRowAttribute : DataRowAttribute
    {
        public override string GetDisplayName(MethodInfo methodInfo, object?[]? data) => "Overridden DisplayName";
    }

    private sealed class UICultureChangingValue
    {
        public override string ToString()
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ko-KR");

            return "value";
        }
    }
}

/// <summary>
/// The dummy test class.
/// </summary>
[TestClass]
public class DummyTestClass
{
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

    public void DataRowObjectArrayTestMethod(object?[] data)
    {
    }
}

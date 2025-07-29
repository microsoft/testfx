// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UnitTests.Helpers;

public class TestDataSourceHelpersTests : TestContainer
{
    public void TryHandleITestDataRow_WithTestDataRow_ShouldExtractTestCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = ["Category1", "Category2"],
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name",
        };
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = []; // No method parameters for this test

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName,
            out IList<string>? testCategories);

        // Assert
        result);
        Verify(extractedData.Should().NotBe(null);
        extractedData.Length.Should().Be(1);
        (string?)extractedData[0].Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
        testCategories.Should().NotBe(null);
        testCategories.Count.Should().Be(2);
        testCategories.Should().Contain("Category1");
        testCategories.Should().Contain("Category2");
    }

    public void TryHandleITestDataRow_WithTestDataRowNullCategories_ShouldReturnNullCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value");
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out _,
            out _,
            out _,
            out IList<string>? testCategories);

        // Assert
        result);
        Verify(testCategories.Should().Be(null);
    }

    public void TryHandleITestDataRow_WithNonTestDataRow_ShouldReturnFalseAndNullCategories()
    {
        // Arrange
        object?[] dataArray = ["regular_string"];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName,
            out IList<string>? testCategories);

        // Assert
        !result);
        extractedData.Should().Be(dataArray);
        ignoreMessage.Should().Be(null);
        displayName.Should().Be(null);
        testCategories.Should().Be(null);
    }

    public void TryHandleITestDataRow_BackwardCompatibilityOverload_ShouldWork()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = ["Category1", "Category2"],
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name",
        };
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName);

        // Assert - should work without TestCategories parameter
        result.Should().BeTrue();
        Verify(extractedData.Should().NotBe(null);
        extractedData.Length.Should().Be(1);
        (string?)extractedData[0].Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
    }
}

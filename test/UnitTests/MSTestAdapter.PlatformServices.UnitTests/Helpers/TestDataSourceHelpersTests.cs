// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UnitTests.Helpers;

public class TestDataSourceHelpersTests : TestContainer
{
    public void TryHandleITestDataRow_ShouldExtractArgumentsDisplayNameAndPreserveOtherMetadata()
    {
        var testData = new TestDataRow<(string, int)>((string.Empty, 42))
        {
            ArgumentsDisplayName = "empty input",
            DisplayName = "full name",
            IgnoreMessage = "ignored row",
            TestCategories = ["row category"],
        };

        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            [testData], [], out object?[] data, out string? ignoreMessage, out string? displayName,
            out IList<string>? categories, out string? argumentsDisplayName);

        result.Should().BeTrue();
        data.Should().Equal(string.Empty, 42);
        ignoreMessage.Should().Be("ignored row");
        displayName.Should().Be("full name");
        categories.Should().Equal("row category");
        argumentsDisplayName.Should().Be("empty input");
    }

    public void TryHandleITestDataRow_ArgumentsDisplayNameDefaultsToNull()
    {
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            [new TestDataRow<string>(string.Empty)], [], out object?[] data, out _, out _, out _, out string? argumentsDisplayName);

        result.Should().BeTrue();
        data.Should().Equal(string.Empty);
        argumentsDisplayName.Should().BeNull();
    }

    public void TryHandleITestDataRow_WithoutWrapperLeavesDataUnchangedAndArgumentsDisplayNameNull()
    {
        object?[] originalData = [string.Empty, 42];
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            originalData, [], out object?[] data, out _, out _, out _, out string? argumentsDisplayName);

        result.Should().BeFalse();
        data.Should().BeSameAs(originalData);
        argumentsDisplayName.Should().BeNull();
    }

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
        result.Should().BeTrue();
        extractedData.Should().NotBeNull();
        extractedData.Length.Should().Be(1);
        ((string?)extractedData[0]).Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
        testCategories.Should().NotBeNull();
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
        result.Should().BeTrue();
        testCategories.Should().BeNull();
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
        result.Should().BeFalse();
        extractedData.Should().BeEquivalentTo(dataArray);
        ignoreMessage.Should().BeNull();
        displayName.Should().BeNull();
        testCategories.Should().BeNull();
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
        extractedData.Should().NotBeNull();
        extractedData.Length.Should().Be(1);
        ((string?)extractedData[0]).Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UnitTests.Helpers;

public class TestDataSourceHelpersTests : TestContainer
{
    public void TryHandleITestDataRow_WithTestDataRow_ShouldExtractTestCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = new List<string> { "Category1", "Category2" },
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name"
        };
        var dataArray = new object?[] { testData };
        var parameters = new ParameterInfo[0]; // No method parameters for this test

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray, 
            parameters, 
            out object?[] extractedData, 
            out string? ignoreMessage, 
            out string? displayName, 
            out IList<string>? testCategories);

        // Assert
        Verify(result == true);
        Verify(extractedData != null);
        Verify(extractedData.Length == 1);
        Verify(extractedData[0] == "test_value");
        Verify(ignoreMessage == "ignore_message");
        Verify(displayName == "display_name");
        Verify(testCategories != null);
        Verify(testCategories.Count == 2);
        Verify(testCategories.Contains("Category1"));
        Verify(testCategories.Contains("Category2"));
    }

    public void TryHandleITestDataRow_WithTestDataRowNullCategories_ShouldReturnNullCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value");
        var dataArray = new object?[] { testData };
        var parameters = new ParameterInfo[0];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray, 
            parameters, 
            out object?[] extractedData, 
            out string? ignoreMessage, 
            out string? displayName, 
            out IList<string>? testCategories);

        // Assert
        Verify(result == true);
        Verify(testCategories == null);
    }

    public void TryHandleITestDataRow_WithNonTestDataRow_ShouldReturnFalseAndNullCategories()
    {
        // Arrange
        var dataArray = new object?[] { "regular_string" };
        var parameters = new ParameterInfo[0];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray, 
            parameters, 
            out object?[] extractedData, 
            out string? ignoreMessage, 
            out string? displayName, 
            out IList<string>? testCategories);

        // Assert
        Verify(result == false);
        Verify(extractedData == dataArray);
        Verify(ignoreMessage == null);
        Verify(displayName == null);
        Verify(testCategories == null);
    }

    public void TryHandleITestDataRow_BackwardCompatibilityOverload_ShouldWork()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = new List<string> { "Category1", "Category2" },
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name"
        };
        var dataArray = new object?[] { testData };
        var parameters = new ParameterInfo[0];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray, 
            parameters, 
            out object?[] extractedData, 
            out string? ignoreMessage, 
            out string? displayName);

        // Assert - should work without TestCategories parameter
        Verify(result == true);
        Verify(extractedData != null);
        Verify(extractedData.Length == 1);
        Verify(extractedData[0] == "test_value");
        Verify(ignoreMessage == "ignore_message");
        Verify(displayName == "display_name");
    }
}
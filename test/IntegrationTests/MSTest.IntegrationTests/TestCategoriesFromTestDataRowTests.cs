// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class TestCategoriesFromTestDataRowTests : CLITestBase
{
    private const string TestAssetName = "TestCategoriesFromTestDataRowProject";

    [TestMethod]
    public async Task TestCategoriesFromTestDataRow_ShouldDiscoverTestsWithIntegrationCategory()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "Integration" category which should come from TestDataRow
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~Integration");

        // Assert - Should discover the test that has Integration category from TestDataRow
        Assert.AreEqual(1, testCases.Length, "Should discover exactly 1 test with Integration category");
        
        var integrationTest = testCases[0];
        Assert.IsTrue(integrationTest.DisplayName.Contains("Integration and Slow"), "Should be the test with Integration and Slow categories");
    }

    [TestMethod]
    public async Task TestCategoriesFromTestDataRow_ShouldDiscoverTestsWithUnitCategory()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "Unit" category which should come from TestDataRow
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~Unit");

        // Assert - Should discover the test that has Unit category from TestDataRow
        Assert.AreEqual(1, testCases.Length, "Should discover exactly 1 test with Unit category");
        
        var unitTest = testCases[0];
        Assert.IsTrue(unitTest.DisplayName.Contains("Unit and Fast"), "Should be the test with Unit and Fast categories");
    }

    [TestMethod]
    public async Task TestCategoriesFromTestDataRow_ShouldCombineMethodAndDataCategories()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "MethodLevel" category (method attribute)
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> methodLevelTests = DiscoverTests(assemblyPath, "TestCategory~MethodLevel");
        
        // Act - Filter by "DataLevel" category (from TestDataRow)
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> dataLevelTests = DiscoverTests(assemblyPath, "TestCategory~DataLevel");

        // Assert - The same test should be found by both filters since categories are merged
        Assert.AreEqual(1, methodLevelTests.Length, "Should discover exactly 1 test with MethodLevel category");
        Assert.AreEqual(1, dataLevelTests.Length, "Should discover exactly 1 test with DataLevel category");
        
        var methodTest = methodLevelTests[0];
        var dataTest = dataLevelTests[0];
        
        Assert.AreEqual(methodTest.FullyQualifiedName, dataTest.FullyQualifiedName, "Both filters should find the same test");
        Assert.IsTrue(methodTest.DisplayName.Contains("method and data categories"), "Should be the test with combined categories");
    }

    [TestMethod]
    public async Task TestCategoriesFromTestDataRow_ShouldExecuteCorrectly()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestCategoriesFromTestDataRowTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert - All tests should pass
        VerifyE2E.TestsPassed(
            testResults,
            "TestMethodWithDynamicDataCategories (Test with Integration and Slow categories)",
            "TestMethodWithDynamicDataCategories (Test with Unit and Fast categories)",
            "TestMethodWithDynamicDataCategories (Test with no additional categories)",
            "TestMethodWithDynamicDataCategories (value4,4)",
            "TestMethodWithMethodLevelCategoriesAndDataCategories (Test with method and data categories)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
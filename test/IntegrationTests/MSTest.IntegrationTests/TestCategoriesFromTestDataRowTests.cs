// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

[TestClass]
public class TestCategoriesFromTestDataRowTests : CLITestBase
{
    private const string TestAssetName = "TestCategoriesFromTestDataRowProject";

    [TestMethod]
    public void TestCategoriesFromTestDataRow_ShouldDiscoverTestsWithIntegrationCategory()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "Integration" category which should come from TestDataRow
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~Integration");

        // Assert - Should discover the test that has Integration category from TestDataRow
        Assert.HasCount(1, testCases, "Should discover exactly 1 test with Integration category");

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase integrationTest = testCases[0];
        Assert.Contains("Integration and Slow", integrationTest.DisplayName, "Should be the test with Integration and Slow categories");
    }

    [TestMethod]
    public void TestCategoriesFromTestDataRow_ShouldDiscoverTestsWithUnitCategory()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "Unit" category which should come from TestDataRow
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~Unit");

        // Assert - Should discover the test that has Unit category from TestDataRow
        Assert.HasCount(1, testCases, "Should discover exactly 1 test with Unit category");

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase unitTest = testCases[0];
        Assert.Contains("Unit and Fast", unitTest.DisplayName, "Should be the test with Unit and Fast categories");
    }

    [TestMethod]
    public void TestCategoriesFromTestDataRow_ShouldCombineMethodAndDataCategories()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act - Filter by "MethodLevel" category (method attribute)
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> methodLevelTests = DiscoverTests(assemblyPath, "TestCategory~MethodLevel");

        // Act - Filter by "DataLevel" category (from TestDataRow)
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> dataLevelTests = DiscoverTests(assemblyPath, "TestCategory~DataLevel");

        // Assert - The same test should be found by both filters since categories are merged
        Assert.HasCount(1, methodLevelTests, "Should discover exactly 1 test with MethodLevel category");
        Assert.HasCount(1, dataLevelTests, "Should discover exactly 1 test with DataLevel category");

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase methodTest = methodLevelTests[0];
        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase dataTest = dataLevelTests[0];

        Assert.AreEqual(methodTest.FullyQualifiedName, dataTest.FullyQualifiedName, "Both filters should find the same test");
        Assert.Contains("method and data categories", methodTest.DisplayName, "Should be the test with combined categories");
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
            "Test with Integration and Slow categories",
            "Test with Unit and Fast categories",
            "Test with no additional categories",
            "TestMethodWithRegularData (\"value4\",4)",
            "Test with method and data categories");

        VerifyE2E.FailedTestCount(testResults, 0);
    }
}

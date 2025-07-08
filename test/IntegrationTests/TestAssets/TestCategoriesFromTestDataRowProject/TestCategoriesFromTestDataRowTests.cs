// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestCategoriesFromTestDataRowProject;

[TestClass]
public class TestCategoriesFromTestDataRowTests
{
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithCategories))]
    public void TestMethodWithDynamicDataCategories(string value, int number)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(value));
        Assert.IsTrue(number > 0);
    }

    public static IEnumerable<ITestDataRow> GetTestDataWithCategories()
    {
        // Test data row with categories
        yield return new TestDataRow<(string, int)>(("value1", 1))
        {
            TestCategories = new List<string> { "Integration", "Slow" },
            DisplayName = "Test with Integration and Slow categories"
        };

        // Test data row with different categories
        yield return new TestDataRow<(string, int)>(("value2", 2))
        {
            TestCategories = new List<string> { "Unit", "Fast" },
            DisplayName = "Test with Unit and Fast categories"
        };

        // Test data row with no categories (should inherit from method level)
        yield return new TestDataRow<(string, int)>(("value3", 3))
        {
            DisplayName = "Test with no additional categories"
        };
    }

    public static IEnumerable<object[]> GetRegularTestData()
    {
        // Regular data row (not TestDataRow) - should work as before
        yield return new object[] { "value4", 4 };
    }

    [TestMethod]
    [DynamicData(nameof(GetRegularTestData))]
    public void TestMethodWithRegularData(string value, int number)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(value));
        Assert.IsTrue(number > 0);
    }

    [TestCategory("MethodLevel")]
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithCategoriesForMethodWithCategory))]
    public void TestMethodWithMethodLevelCategoriesAndDataCategories(string value)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(value));
    }

    public static IEnumerable<TestDataRow<string>> GetTestDataWithCategoriesForMethodWithCategory()
    {
        // This should have both "MethodLevel" and "DataLevel" categories
        yield return new TestDataRow<string>("test")
        {
            TestCategories = new List<string> { "DataLevel" },
            DisplayName = "Test with method and data categories"
        };
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// EXAMPLE: Using TestCategories with TestDataRow
// This demonstrates the new TestCategories functionality added to ITestDataRow

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExampleUsage;

[TestClass]
public class TestCategoriesExampleTests
{
    /// <summary>
    /// Example showing how to use TestCategories with TestDataRow for dynamic data.
    /// Each test case can now have its own categories.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithCategories), DynamicDataSourceType.Method)]
    public void ExampleTestWithDynamicCategories(string value, string expectedResult)
    {
        // Test logic here
        Assert.AreEqual(expectedResult, ProcessValue(value));
    }

    /// <summary>
    /// Example showing how TestCategories from TestDataRow merge with method-level categories.
    /// This test method has "MethodLevel" category, and individual test cases add their own.
    /// </summary>
    [TestCategory("MethodLevel")]
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithMergedCategories), DynamicDataSourceType.Method)]
    public void ExampleTestWithMergedCategories(string value)
    {
        // Test logic here
        Assert.IsNotNull(ProcessValue(value));
    }

    /// <summary>
    /// Test data source that demonstrates TestCategories usage.
    /// </summary>
    public static IEnumerable<object[]> GetTestDataWithCategories()
    {
        // Fast unit test case
        yield return new TestDataRow<(string, string)>(("input1", "output1"))
        {
            TestCategories = new List<string> { "Unit", "Fast" },
            DisplayName = "Fast unit test case"
        };

        // Slow integration test case  
        yield return new TestDataRow<(string, string)>(("input2", "output2"))
        {
            TestCategories = new List<string> { "Integration", "Slow", "Database" },
            DisplayName = "Integration test with database"
        };

        // Performance test case
        yield return new TestDataRow<(string, string)>(("input3", "output3"))
        {
            TestCategories = new List<string> { "Performance", "Load" },
            DisplayName = "Load testing scenario"
        };

        // Regular test case without specific categories
        yield return new TestDataRow<(string, string)>(("input4", "output4"))
        {
            DisplayName = "Standard test case"
            // No TestCategories specified - will inherit any from method/class/assembly level
        };

        // Traditional data row still works
        yield return new object[] { "input5", "output5" };
    }

    /// <summary>
    /// Test data that will merge with method-level categories.
    /// </summary>
    public static IEnumerable<object[]> GetTestDataWithMergedCategories()
    {
        // This will have both "MethodLevel" (from method attribute) and "DataLevel" categories
        yield return new TestDataRow<string>("test_value")
        {
            TestCategories = new List<string> { "DataLevel", "Specific" },
            DisplayName = "Test with combined categories"
        };
    }

    private static string ProcessValue(string input) => $"processed_{input}";
}

/*
 * Usage scenarios enabled by this feature:
 * 
 * 1. FILTERING BY CATEGORY:
 *    - Can now run tests filtered by categories applied to individual test cases
 *    - Example: dotnet test --filter "TestCategory=Fast" will run only fast test cases
 *    - Example: dotnet test --filter "TestCategory=Integration" will run only integration test cases
 * 
 * 2. MIXED SCENARIOS:
 *    - Can have different types of test cases (unit, integration, performance) in same test method
 *    - Each test case can have appropriate categories for filtering/organization
 * 
 * 3. CATEGORY INHERITANCE:
 *    - Test cases inherit categories from method/class/assembly level
 *    - TestDataRow categories are merged with existing categories (no overriding)
 * 
 * 4. BACKWARD COMPATIBILITY:
 *    - Existing TestDataRow usage continues to work unchanged
 *    - TestCategories property is optional (nullable)
 *    - Regular object[] data rows continue to work
 */
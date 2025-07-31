// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

namespace MSTest.TestFramework.UnitTests;

public class TestDataRowTests : TestContainer
{
    public void TestDataRowShouldInitializeWithValue()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);

        Verify(testDataRow.Value == value);
        Verify(testDataRow.IgnoreMessage == null);
        Verify(testDataRow.DisplayName == null);
        Verify(testDataRow.TestCategories == null);
    }

    public void TestDataRowShouldAllowSettingTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Category1", "Category2" };

        testDataRow.TestCategories = testCategories;

        Verify(testDataRow.TestCategories == testCategories);
        Verify(testDataRow.TestCategories.Count == 2);
        Verify(testDataRow.TestCategories.Contains("Category1"));
        Verify(testDataRow.TestCategories.Contains("Category2"));
    }

    public void TestDataRowShouldImplementITestDataRowForTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Integration", "Unit" };
        testDataRow.TestCategories = testCategories;

        ITestDataRow dataRow = testDataRow;

        Verify(dataRow.TestCategories != null);
        Verify(dataRow.TestCategories.Count == 2);
        Verify(dataRow.TestCategories.Contains("Integration"));
        Verify(dataRow.TestCategories.Contains("Unit"));
    }

    public void TestDataRowShouldAllowNullTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value)
        {
            TestCategories = null,
        };

        Verify(testDataRow.TestCategories == null);

        ITestDataRow dataRow = testDataRow;
        Verify(dataRow.TestCategories == null);
    }

    public void TestDataRowShouldAllowEmptyTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var emptyCategories = new List<string>();

        testDataRow.TestCategories = emptyCategories;

        Verify(testDataRow.TestCategories == emptyCategories);
        Verify(testDataRow.TestCategories.Count == 0);

        ITestDataRow dataRow = testDataRow;
        Verify(dataRow.TestCategories != null);
        Verify(dataRow.TestCategories.Count == 0);
    }
}

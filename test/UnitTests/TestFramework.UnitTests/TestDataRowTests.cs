// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class TestDataRowTests : TestContainer
{
    public void TestDataRowShouldInitializeWithValue()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);

        testDataRow.Value.Should().Be(value);
        testDataRow.IgnoreMessage.Should().Be(null);
        testDataRow.DisplayName.Should().Be(null);
        testDataRow.TestCategories.Should().Be(null);
    }

    public void TestDataRowShouldAllowSettingTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Category1", "Category2" };

        testDataRow.TestCategories = testCategories;

        testDataRow.TestCategories.Should().Be(testCategories);
        testDataRow.TestCategories.Count.Should().Be(2);
        testDataRow.TestCategories.Contains("Category1"));
        testDataRow.TestCategories.Should().Contain("Category2");
    }

    public void TestDataRowShouldImplementITestDataRowForTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Integration", "Unit" };
        testDataRow.TestCategories = testCategories;

        ITestDataRow dataRow = testDataRow;

        Verify(dataRow.TestCategories.Should().NotBe(null);
        dataRow.TestCategories.Count.Should().Be(2);
        dataRow.TestCategories.Contains("Integration"));
        dataRow.TestCategories.Should().Contain("Unit");
    }

    public void TestDataRowShouldAllowNullTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value)
        {
            TestCategories = null,
        };

        testDataRow.TestCategories.Should().Be(null);

        ITestDataRow dataRow = testDataRow;
        dataRow.TestCategories.Should().Be(null);
    }

    public void TestDataRowShouldAllowEmptyTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var emptyCategories = new List<string>();

        testDataRow.TestCategories = emptyCategories;

        testDataRow.TestCategories.Should().Be(emptyCategories);
        testDataRow.TestCategories.Count.Should().Be(0);

        ITestDataRow dataRow = testDataRow;
        Verify(dataRow.TestCategories.Should().NotBe(null);
        dataRow.TestCategories.Count.Should().Be(0);
    }
}

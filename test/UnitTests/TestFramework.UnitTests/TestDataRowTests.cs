// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class TestDataRowTests : TestContainer
{
    public void TestDataRowShouldInitializeWithValue()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);

        testDataRow.Value.Should().Be(value);
        testDataRow.IgnoreMessage.Should().BeNull();
        testDataRow.DisplayName.Should().BeNull();
        testDataRow.TestCategories.Should().BeNull();
    }

    public void TestDataRowShouldAllowSettingTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Category1", "Category2" };

        testDataRow.TestCategories = testCategories;

        testDataRow.TestCategories.Should().BeSameAs(testCategories);
        testDataRow.TestCategories.Should().HaveCount(2);
        testDataRow.TestCategories.Should().Contain("Category1");
        testDataRow.TestCategories.Should().Contain("Category2");
    }

    public void TestDataRowShouldImplementITestDataRowForTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var testCategories = new List<string> { "Integration", "Unit" };
        testDataRow.TestCategories = testCategories;

        ITestDataRow dataRow = testDataRow;

        dataRow.TestCategories.Should().NotBeNull();
        dataRow.TestCategories.Should().HaveCount(2);
        dataRow.TestCategories.Should().Contain("Integration");
        dataRow.TestCategories.Should().Contain("Unit");
    }

    public void TestDataRowShouldAllowNullTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value)
        {
            TestCategories = null,
        };

        testDataRow.TestCategories.Should().BeNull();

        ITestDataRow dataRow = testDataRow;
        dataRow.TestCategories.Should().BeNull();
    }

    public void TestDataRowShouldAllowEmptyTestCategories()
    {
        string value = "test_value";
        var testDataRow = new TestDataRow<string>(value);
        var emptyCategories = new List<string>();

        testDataRow.TestCategories = emptyCategories;

        testDataRow.TestCategories.Should().BeSameAs(emptyCategories);
        testDataRow.TestCategories.Should().HaveCount(0);

        ITestDataRow dataRow = testDataRow;
        dataRow.TestCategories.Should().NotBeNull();
        dataRow.TestCategories.Should().HaveCount(0);
    }
}

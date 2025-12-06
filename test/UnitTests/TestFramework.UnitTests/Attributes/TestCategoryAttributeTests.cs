// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class TestCategoryAttributeTests : TestContainer
{
    public void ConstructorShouldSetCategoryPassed()
    {
        var category = new TestCategoryAttribute("UnitTest");

        category.TestCategories.Should().BeEquivalentTo(new[] { "UnitTest" });
    }

    public void ConstructorWithEmptyStringShouldResultInEmptyList()
    {
        var category = new TestCategoryAttribute("");

        category.TestCategories.Should().BeEmpty();
    }

    public void ConstructorWithWhitespaceStringShouldResultInEmptyList()
    {
        var category = new TestCategoryAttribute("   ");

        category.TestCategories.Should().BeEmpty();
    }

    public void ConstructorWithNullShouldResultInEmptyList()
    {
        var category = new TestCategoryAttribute(null!);

        category.TestCategories.Should().BeEmpty();
    }

    public void ConstructorWithValidCategoryShouldPreserveValue()
    {
        var category = new TestCategoryAttribute("Integration");

        category.TestCategories.Should().HaveCount(1);
        category.TestCategories[0].Should().Be("Integration");
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// TestCategory attribute; used to specify the category of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
[SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification = "The TestCategories accessor property exposes the testCategory argument.")]
public sealed class TestCategoryAttribute : TestCategoryBaseAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCategoryAttribute"/> class and applies the category to the test.
    /// </summary>
    /// <param name="testCategory">
    /// The test Category.
    /// </param>
    public TestCategoryAttribute(string testCategory)
    {
        List<string> categories =
        [
            testCategory
        ];
        TestCategories = categories;
    }

    /// <summary>
    /// Gets the test categories that has been applied to the test.
    /// </summary>
    public override IList<string> TestCategories { get; }
}

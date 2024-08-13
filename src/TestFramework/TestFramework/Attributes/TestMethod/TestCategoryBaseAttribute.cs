// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Base class for the "Category" attribute.
/// </summary>
/// <remarks>
/// The reason for this attribute is to let the users create their own implementation of test categories.
/// - test framework (discovery, etc) deals with TestCategoryBaseAttribute.
/// - The reason that TestCategories property is a collection rather than a string,
///   is to give more flexibility to the user. For instance the implementation may be based on enums for which the values can be OR'ed
///   in which case it makes sense to have single attribute rather than multiple ones on the same test.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public abstract class TestCategoryBaseAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCategoryBaseAttribute"/> class.
    /// Applies the category to the test. The strings returned by TestCategories
    /// are used with the /category command to filter tests.
    /// </summary>
    protected TestCategoryBaseAttribute()
    {
    }

    /// <summary>
    /// Gets the test category that has been applied to the test.
    /// </summary>
    public abstract IList<string> TestCategories { get; }
}

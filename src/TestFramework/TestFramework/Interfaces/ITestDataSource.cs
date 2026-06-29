// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Test data source for data driven tests.
/// </summary>
public interface ITestDataSource
{
    /// <summary>
    /// Gets the test data from custom test data source.
    /// </summary>
    /// <param name="methodInfo">
    /// The method info of test method.
    /// </param>
    /// <returns>
    /// Test data for calling test method.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each element of the returned sequence represents the arguments for a single invocation of the test method.
    /// In the common case, an element is an <see cref="object"/> array whose items are the positional arguments
    /// passed to the test method.
    /// </para>
    /// <para>
    /// To attach per-row metadata (such as a custom display name, an ignore reason, or test categories), return a
    /// single-element <see cref="object"/> array whose only item is a <see cref="TestDataRow{T}"/> instance, for
    /// example <c>new object[] { new TestDataRow&lt;(int, int, int)&gt;((1, 2, 3)) { DisplayName = "my row" } }</c>.
    /// When MSTest sees a <see cref="TestDataRow{T}"/>, it unwraps its <see cref="TestDataRow{T}.Value"/> to obtain
    /// the actual test method arguments and applies the row's <see cref="TestDataRow{T}.DisplayName"/>,
    /// <see cref="TestDataRow{T}.IgnoreMessage"/>, and <see cref="TestDataRow{T}.TestCategories"/>.
    /// </para>
    /// <para>
    /// The whole data source can be ignored (rather than a single row) by implementing
    /// <see cref="ITestDataSourceIgnoreCapability"/> on the data source type.
    /// </para>
    /// </remarks>
    IEnumerable<object?[]> GetData(MethodInfo methodInfo);

    /// <summary>
    /// Gets the display name corresponding to test data row for displaying in TestResults.
    /// </summary>
    /// <param name="methodInfo">
    /// The method info of test method.
    /// </param>
    /// <param name="data">
    /// The test data which is passed to test method.
    /// </param>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    string? GetDisplayName(MethodInfo methodInfo, object?[]? data);
}

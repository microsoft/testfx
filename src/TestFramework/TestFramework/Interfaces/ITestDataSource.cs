// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

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

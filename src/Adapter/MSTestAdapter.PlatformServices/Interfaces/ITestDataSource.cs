// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Interface that provides values from data source when data driven tests are run.
/// </summary>
public interface ITestDataSource
{
    /// <summary>
    /// Gets the test data from custom test data source and sets dbconnection in testContext object.
    /// </summary>
    /// <param name="testMethodInfo">
    /// The info of test method.
    /// </param>
    /// <param name="testContext">
    /// Test Context object.
    /// </param>
    /// <returns>
    /// Test data for calling test method.
    /// </returns>
    IEnumerable<object>? GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext);
}

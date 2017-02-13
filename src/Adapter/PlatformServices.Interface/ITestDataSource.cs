// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Interface that provides values from data source when data driven tests are run.
    /// </summary>
    public interface ITestDataSource
    {
        /// <summary>
        /// Gets a value indicating whether testMethod has data driven tests.
        /// </summary>
        /// <param name="testMethodInfo">
        /// The test Method Info.
        /// </param>
        /// <returns>
        /// True of it is a data driven test method. False otherwise.
        /// </returns>
        bool HasDataDrivenTests(UTF.ITestMethod testMethodInfo);

        /// <summary>
        /// Run a data driven test. Test case is executed once for each data row.
        /// </summary>
        /// <param name="testContext">
        /// The test Context.
        /// </param>
        /// <param name="testMethodInfo">
        /// The test Method Info.
        /// </param>
        /// <param name="testMethod">
        /// The test Method.
        /// </param>
        /// <param name="executor">
        /// The default test method executor.
        /// </param>
        /// <returns>
        /// The results after running all the data driven tests.
        /// </returns>
        UTF.TestResult[] RunDataDrivenTest(UTF.TestContext testContext, UTF.ITestMethod testMethodInfo, ITestMethod testMethod, UTF.TestMethodAttribute executor);
    }
}

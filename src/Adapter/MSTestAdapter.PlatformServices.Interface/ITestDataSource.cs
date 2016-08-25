// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using ObjectModel;
    using System;
    using UTF = TestTools.UnitTesting;
    /// <summary>
    /// Interface that provides values from data source when data driven tests are run. 
    /// </summary>
    public interface ITestDataSource
    {
        /// <summary>
        /// Gets value indicating whether testMethod has data driven tests.
        /// </summary>
        bool HasDataDrivenTests(UTF.ITestMethod testMethodInfo);

        /// <summary>
        /// Run a data driven test. Test case is executed once for each data row.
        /// </summary>
        UTF.TestResult[] RunDataDrivenTest(UTF.TestContext testContext, UTF.ITestMethod testMethodInfo, ITestMethod testMethod, UTF.TestMethodAttribute executor);

        /// <summary>
        /// Sets context required for running tests.
        /// </summary>
        /// <param name="source">source parameter used for setting context</param>
        bool SetContext(string source);

        /// <summary>
        /// Resets the context as it was before caliing SetContext()
        /// </summary>
        void ResetContext();
    }
}

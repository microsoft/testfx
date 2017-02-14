// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The platform service that provides values from data source when data driven tests are run.
    /// </summary>
    /// <remarks>
    /// NOTE NOTE NOTE: This platform service refers to the inbox UTF extension assembly for UTF.TestContext which can only be loadable inside of the app domain that discovers/runs
    /// the tests since it can only be found at the test output directory. DO NOT call into this platform service outside of the appdomain context if you do not want to hit
    /// a ReflectionTypeLoadException.
    /// </remarks>
    public class TestDataSource : ITestDataSource
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
        public bool HasDataDrivenTests(UTF.ITestMethod testMethodInfo)
        {
            return false;
        }

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
        public UTF.TestResult[] RunDataDrivenTest(UTF.TestContext testContext, UTF.ITestMethod testMethodInfo, ITestMethod testMethod, UTF.TestMethodAttribute executor)
        {
            return null;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}

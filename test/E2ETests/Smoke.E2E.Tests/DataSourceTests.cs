// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataSourceTests : CLITestBase
    {
        private const string TestAssembly = "DataSourceTestProject.dll";

        [TestMethod]
        public void ExecuteCsvTestDataSourceTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });

            this.ValidatePassedTestsContain(
                "CsvTestMethod (Data Row 0)",
                "CsvTestMethod (Data Row 2)");

            this.ValidateFailedTestsContain(
                "CsvTestMethod (Data Row 1)",
                "CsvTestMethod (Data Row 3)");
        }
    }
}

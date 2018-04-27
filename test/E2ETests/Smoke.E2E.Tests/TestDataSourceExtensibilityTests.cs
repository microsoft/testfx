// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    extern alias FrameworkV1;

    using Microsoft.MSTestV2.CLIAutomation;
    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClass]
    public class TestDataSourceExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestFrameworkV1.TestMethod]
        public void ExecuteTestDataSourceExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain(
                "CustomTestDataSourceTestMethod1 (1,2,3)",
                "CustomTestDataSourceTestMethod1 (4,5,6)");
        }
    }
}

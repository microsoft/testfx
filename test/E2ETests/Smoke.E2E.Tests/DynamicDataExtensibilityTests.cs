// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    extern alias FrameworkV1;

    using Microsoft.MSTestV2.CLIAutomation;
    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClass]
    public class DynamicDataExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestFrameworkV1.TestMethod]
        public void ExecuteDynamicDataExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain(
                "DynamicDataTestMethod1 (string,2,True)",
                "DynamicDataTestMethod2 (string,4,True)",
                "DynamicDataTestMethod3 (string,2,True)",
                "DynamicDataTestMethod3 (string,4,True)");

            this.ValidatePassedTestsContain(
                "DynamicDataTestMethod4 (string,2,True)",
                "DynamicDataTestMethod5 (string,4,True)",
                "DynamicDataTestMethod6 (string,2,True)",
                "DynamicDataTestMethod6 (string,4,True)");
        }
    }
}

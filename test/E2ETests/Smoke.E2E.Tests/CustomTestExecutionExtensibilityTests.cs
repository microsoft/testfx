// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CustomTestExecutionExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestMethod]
        public void ExecuteCustomTestExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain(
                "CustomTestMethod1 - Execution number 1",
                "CustomTestMethod1 - Execution number 2",
                "CustomTestMethod1 - Execution number 4",
                "CustomTestMethod1 - Execution number 5",
                "CustomTestClass1 - Execution number 1",
                "CustomTestClass1 - Execution number 2",
                "CustomTestClass1 - Execution number 4",
                "CustomTestClass1 - Execution number 5");
            this.ValidateFailedTestsContain(
                "CustomTestMethod1 - Execution number 3",
                "CustomTestClass1 - Execution number 3");
        }
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    extern alias FrameworkV1;

    using Microsoft.MSTestV2.CLIAutomation;
    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClass]
    public class CustomTestExecutionExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestFrameworkV1.TestMethod]
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
                TestAssembly,
                "CustomTestMethod1 - Execution number 3",
                "CustomTestClass1 - Execution number 3");
        }

        [TestFrameworkV1.TestMethod]
        public void ExecuteCustomTestExtensibilityWithTestDataTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~CustomTestExTests.CustomTestMethod2");
            this.ValidatePassedTests(
                "CustomTestMethod2 (B)",
                "CustomTestMethod2 (B)",
                "CustomTestMethod2 (B)");
            this.ValidateFailedTests(
                TestAssembly,
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (C)",
                "CustomTestMethod2 (C)",
                "CustomTestMethod2 (C)");
        }
    }
}
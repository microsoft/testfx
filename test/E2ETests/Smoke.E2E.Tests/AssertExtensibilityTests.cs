// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AssertExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestMethod]
        public void ExecuteAssertExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain(
                "FxExtensibilityTestProject.AssertExTest.BasicAssertExtensionTest",
                "FxExtensibilityTestProject.AssertExTest.ChainedAssertExtensionTest");

            this.ValidateFailedTestsContain(
                TestAssembly,
                true,
                "FxExtensibilityTestProject.AssertExTest.BasicFailingAssertExtensionTest",
                "FxExtensibilityTestProject.AssertExTest.ChainedFailingAssertExtensionTest");
        }
    }
}

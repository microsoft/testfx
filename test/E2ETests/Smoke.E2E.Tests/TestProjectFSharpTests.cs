// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestProjectFSharpTests : CLITestBase
    {
        private const string TestAssembly = "FSharpTestProject.dll";

        [TestMethod]
        public void ExecuteCustomTestExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain("Test method passing with a . in it");
            this.ValidateFailedTestsCount(0);
            this.ValidatePassedTestsCount(1);
        }
    }
}

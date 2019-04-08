// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using System.IO;
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TimeoutTests : CLITestBase
    {
        private const string TimeoutTestAssembly = "TimeoutTestProject.dll";
        private const string TimeoutTestAssemblyNetCore = "netcoreapp1.1\\TimeoutTestProjectNetCore.dll";
        private const int TestMethodWaitTimeInMs = 6000;
        private const int OverheadTimeInMs = 2500;
        private const string TimeoutFileToValidateNetCore = "netcoreapp1.1\\TimeoutTestOutputNetCore.txt";
        private const string TimeoutFileToValidate = "TimeoutTestOutput.txt";

        [TestMethod]
        public void ValidateTimeoutTests()
        {
            this.Validate(TimeoutTestAssembly, TimeoutFileToValidate);
        }

        [TestMethod]
        public void ValidateTimeoutTestsNetCore()
        {
            this.Validate(TimeoutTestAssemblyNetCore, TimeoutFileToValidateNetCore);
        }

        private void Validate(string testAssembly, string fileToValidate)
        {
            this.InvokeVsTestForExecution(new string[] { testAssembly });

            this.ValidateTestRunTime(TestMethodWaitTimeInMs + OverheadTimeInMs);

            this.ValidateFailedTestsCount(2);

            this.ValidateFailedTestsContain(
                testAssembly,
                false,
                "TimeoutTestProject.TerimnateLongRunningTasksUsingTokenTestClass.TerimnateLongRunningTasksUsingToken",
                "TimeoutTestProject.SelfTerminatingTestClass.SelfTerminatingTestMethod");

            Assert.IsTrue(File.Exists(this.GetAssetFullPath(fileToValidate)), "Unable to locate the TimeoutTestOutput.txt file");
        }
    }
}

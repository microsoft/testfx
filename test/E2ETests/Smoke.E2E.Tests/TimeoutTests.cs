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
        private const int TestMethodWaitTimeInMs = 6000;
        private const int OverheadTimeInMs = 2500;

        [TestMethod]
        public void ValidateTimeoutTests()
        {
            this.InvokeVsTestForExecution(new string[] { TimeoutTestAssembly });

            this.ValidateTestRunTime(TestMethodWaitTimeInMs + OverheadTimeInMs);

            this.ValidateFailedTestsCount(2);

            this.ValidateFailedTestsContain(
                TimeoutTestAssembly,
                false,
                "TimeoutTestProject.TerimnateLongRunningTasksUsingTokenTestClass.TerimnateLongRunningTasksUsingToken",
                "TimeoutTestProject.SelfTerminatingTestClass.SelfTerminatingTestMethod");

            Assert.IsTrue(File.Exists(this.GetAssetFullPath("TimeoutTestOutput.txt")), "Unable to locate the TimeoutTestOutput.txt file");
        }
    }
}

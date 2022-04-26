// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompatTests : CLITestBase
    {
        private const string OldAdapterTestProject = "CompatTests\\CompatTestProject.dll";
        private const string LatestAdapterTestProject = "DesktopTestProjectx86Debug.dll";

        [TestMethod]
        public void DiscoverCompatTests()
        {
            this.InvokeVsTestForDiscovery(new string[] { OldAdapterTestProject, LatestAdapterTestProject });
            var listOfTests = new string[]
            {
                "CompatTestProject.UnitTest1.PassingTest",
                "CompatTestProject.UnitTest1.FailingTest",
                "CompatTestProject.UnitTest1.SkippingTest",
                "SampleUnitTestProject.UnitTest1.PassingTest",
                "SampleUnitTestProject.UnitTest1.FailingTest",
                "SampleUnitTestProject.UnitTest1.SkippingTest"
            };
            this.ValidateDiscoveredTests(listOfTests);
        }

        [TestMethod]
        public void RunAllCompatTests()
        {
            this.InvokeVsTestForExecution(new string[] { OldAdapterTestProject, LatestAdapterTestProject });

            this.ValidatePassedTestsContain(
                "CompatTestProject.UnitTest1.PassingTest",
                 "SampleUnitTestProject.UnitTest1.PassingTest");

            this.ValidateFailedTestsContain(
                OldAdapterTestProject,
                true,
                "CompatTestProject.UnitTest1.FailingTest");

            this.ValidateFailedTestsContain(
                LatestAdapterTestProject,
                true,
                "SampleUnitTestProject.UnitTest1.FailingTest");

            this.ValidateSkippedTestsContain(
                "CompatTestProject.UnitTest1.SkippingTest",
                "SampleUnitTestProject.UnitTest1.SkippingTest");
        }
    }
}

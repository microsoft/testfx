// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.MSTestV2.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DesktopCSharpCLITests : CLITestBase
    {
        private const string x86DebugTestProject = "DesktopTestProjectx86Debug.dll";
        private const string x64DebugTestProject = "DesktopTestProjectx64Debug.dll";
        private const string x86ReleaseTestProject = "DesktopTestProjectx86Release.dll";
        private const string x64ReleaseTestProject = "DesktopTestProjectx64Release.dll";
        private const string runSetting =
            @"<RunSettings>   
                <RunConfiguration>  
                    <TargetPlatform>x64</TargetPlatform>   
                </RunConfiguration>  
            </RunSettings>";

        [TestMethod]
        public void DiscoverTestsx86Debug()
        {
            string[] sources = { x86DebugTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources);
        }

        [TestMethod]
        public void DiscoverTestsx64Debug()
        {
            string[] sources = { x64DebugTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources,runSetting);
        }

        [TestMethod]
        public void DiscoverTestsx86Release()
        {
            string[] sources = { x86ReleaseTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources);
        }

        [TestMethod]
        public void DiscoverTestsx64Release()
        {
            string[] sources = { x64ReleaseTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources,runSetting);
        }

        [TestMethod]
        public void RunAllTestsx86Debug()
        {
            string[] sources = { x86DebugTestProject };
            this.RunAllTestsAndValidateResults(sources);
        }

        [TestMethod]
        public void RunAllTestsx64Debug()
        {
            string[] sources = { x64DebugTestProject };
            this.RunAllTestsAndValidateResults(sources,runSetting);
        }

        [TestMethod]
        public void RunAllTestsx86Release()
        {
            string[] sources = { x86ReleaseTestProject };
            this.RunAllTestsAndValidateResults(sources);
        }

        [TestMethod]
        public void RunAllTestsx64Release()
        {
            string[] sources = { x64ReleaseTestProject };
            this.RunAllTestsAndValidateResults(sources,runSetting);
        }

        private void DoDiscoveryAndValidateDiscoveredTests(string[] sources, string runSettings = "")
        {
            this.InvokeVsTestForDiscovery(sources, runSettings);
            var listOfTests = new string[] { "SampleUnitTestProject.UnitTest1.PassingTest", "SampleUnitTestProject.UnitTest1.FailingTest", "SampleUnitTestProject.UnitTest1.SkippingTest" };
            this.ValidateDiscoveredTests(listOfTests);
        }

        private void RunAllTestsAndValidateResults(string[] sources, string runSettings = "")
        {
            this.InvokeVsTestForExecution(sources, runSettings);
            this.ValidatePassedTests("SampleUnitTestProject.UnitTest1.PassingTest");
            this.ValidateFailedTests(sources[0],"SampleUnitTestProject.UnitTest1.FailingTest");
            this.ValidateSkippedTests("SampleUnitTestProject.UnitTest1.SkippingTest");
        }

    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DesktopCSharpCLITests : CLITestBase
    {
        private const string X86DebugTestProject = "DesktopTestProjectx86Debug.dll";
        private const string X64DebugTestProject = "DesktopTestProjectx64Debug.dll";
        private const string X86ReleaseTestProject = "DesktopTestProjectx86Release.dll";
        private const string X64ReleaseTestProject = "DesktopTestProjectx64Release.dll";
        private const string RunSetting =
            @"<RunSettings>   
                <RunConfiguration>  
                    <TargetPlatform>x64</TargetPlatform>   
                </RunConfiguration>  
            </RunSettings>";

        [TestMethod]
        public void DiscoverTestsx86Debug()
        {
            string[] sources = { X86DebugTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources);
        }

        [TestMethod]
        public void DiscoverTestsx64Debug()
        {
            string[] sources = { X64DebugTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources, RunSetting);
        }

        [TestMethod]
        public void DiscoverTestsx86Release()
        {
            string[] sources = { X86ReleaseTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources);
        }

        [TestMethod]
        public void DiscoverTestsx64Release()
        {
            string[] sources = { X64ReleaseTestProject };
            this.DoDiscoveryAndValidateDiscoveredTests(sources, RunSetting);
        }

        [TestMethod]
        public void RunAllTestsx86Debug()
        {
            string[] sources = { X86DebugTestProject };
            this.RunAllTestsAndValidateResults(sources);
        }

        [TestMethod]
        public void RunAllTestsx64Debug()
        {
            string[] sources = { X64DebugTestProject };
            this.RunAllTestsAndValidateResults(sources, RunSetting);
        }

        [TestMethod]
        [Ignore] // TODO: no stack trace for failed test both for x86 and x64 but that's checked only for x86
        public void RunAllTestsx86Release()
        {
            string[] sources = { X86ReleaseTestProject };
            this.RunAllTestsAndValidateResults(sources);
        }

        [TestMethod]
        public void RunAllTestsx64Release()
        {
            string[] sources = { X64ReleaseTestProject };
            this.RunAllTestsAndValidateResults(sources, RunSetting);
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
            this.ValidateFailedTests(sources[0], "SampleUnitTestProject.UnitTest1.FailingTest");
            this.ValidateSkippedTests("SampleUnitTestProject.UnitTest1.SkippingTest");
        }
    }
}

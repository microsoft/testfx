// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DeploymentTests : CLITestBase
    {
        private const string TestAssemblyDependency = "DesktopDeployment\\Never\\DeploymentTestProject.dll";
        private const string TestAssemblyMSBuild = "DesktopDeployment\\PreserveNewest\\DeploymentTestProject.dll";
        private const string TestAssemblyNetCore10 = "netcoreapp1.0\\DeploymentTestProjectNetCore.dll";
        private const string TestAssemblyNetCore21 = "netcoreapp2.1\\DeploymentTestProjectNetCore.dll";
        private const string RunSetting =
             @"<RunSettings>   
                <MSTestV2>
                   <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
                </MSTestV2>
            </RunSettings>";

        [TestMethod]
        public void ValidateTestSourceDependencyDeployment()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssemblyDependency });
            this.ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.FailIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
            this.ValidateFailedTestsContain(TestAssemblyMSBuild, true, "DeploymentTestProject.DeploymentTestProject.PassIfFilePresent");
        }

        [TestMethod]
        public void ValidateTestSourceLocationDeployment()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssemblyMSBuild }, RunSetting);
            this.ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.PassIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
            this.ValidateFailedTestsContain(TestAssemblyMSBuild, true, "DeploymentTestProject.DeploymentTestProject.FailIfFilePresent");
        }

        /*
         * This test is disabled because testhost in netcoreapp1.0 is broken for now.

        [TestMethod]
        public void ValidateTestSourceLocationDeploymentNetCore1_0()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssemblyNetCore10 }, null);
            this.ValidatePassedTestsContain("DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.FailIfFilePresent", "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfDeclaredFilesPresent");
            this.ValidateFailedTestsContain("DeploymentTestProjectNetCore.dll", true, "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfFilePresent");
        }
        */

        [TestMethod]
        public void ValidateTestSourceLocationDeploymentNetCore2_1()
        {
            // StackTrace isn't included in release builds with netcoreapp2.1.
            const bool validateStackTrace =
#if DEBUG
            true;
#else
            false;
#endif

            this.InvokeVsTestForExecution(new string[] { TestAssemblyNetCore21 }, null);
            this.ValidatePassedTestsContain("DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.FailIfFilePresent", "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfDeclaredFilesPresent");
            this.ValidateFailedTestsContain(TestAssemblyMSBuild, validateStackTrace, "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfFilePresent");
        }
    }
}

﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class DeploymentTests : CLITestBase
{
    private const string TestAssemblyDependency = "DesktopDeployment\\Never\\DeploymentTestProject.dll";
    private const string TestAssemblyMSBuild = "DesktopDeployment\\PreserveNewest\\DeploymentTestProject.dll";
    private const string TestAssemblyNetCore21 = "netcoreapp2.1\\DeploymentTestProjectNetCore.dll";
    private const string TestAssemblyNetCore31 = "netcoreapp3.1\\DeploymentTestProjectNetCore.dll";
    private const string RunSetting =
         @"<RunSettings>   
                <MSTestV2>
                   <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
                </MSTestV2>
            </RunSettings>";

    public void ValidateTestSourceDependencyDeployment()
    {
        InvokeVsTestForExecution(new string[] { TestAssemblyDependency });
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.FailIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(TestAssemblyMSBuild, true, "DeploymentTestProject.DeploymentTestProject.PassIfFilePresent");
    }

    public void ValidateTestSourceLocationDeployment()
    {
        InvokeVsTestForExecution(new string[] { TestAssemblyMSBuild }, RunSetting);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.PassIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(TestAssemblyMSBuild, true, "DeploymentTestProject.DeploymentTestProject.FailIfFilePresent");
    }

    public void ValidateTestSourceLocationDeploymentNetCore3_1()
    {
        InvokeVsTestForExecution(new string[] { TestAssemblyNetCore31 }, null);
        ValidatePassedTestsContain("DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.FailIfFilePresent", "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(TestAssemblyMSBuild, true, "DeploymentTestProjectNetCore.DeploymentTestProjectNetCore.PassIfFilePresent");
    }

    // TODO @haplois | @evangelink: We need to work on our netcoreapp2.1 tests.
    /*
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
    */
}

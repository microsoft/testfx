// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;
public class DeploymentTests : CLITestBase
{
    private const string TestAssetNever = "DeploymentTestProject.Never";
    private const string TestAssetPreserveNewest = "DeploymentTestProject.PreserveNewest";
    private const string RunSetting =
         @"<RunSettings>   
                <MSTestV2>
                   <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
                </MSTestV2>
            </RunSettings>";

    public void ValidateTestSourceDependencyDeployment_net462()
        => ValidateTestSourceDependencyDeployment("net462");

    public void ValidateTestSourceDependencyDeployment_netcoreapp31()
        => ValidateTestSourceDependencyDeployment("netcoreapp3.1");

    private void ValidateTestSourceDependencyDeployment(string targetFramework)
    {
        InvokeVsTestForExecution(new string[] { TestAssetNever }, targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.FailIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.PassIfFilePresent");
    }

    public void ValidateTestSourceLocationDeployment_net462()
        => ValidateTestSourceLocationDeployment("net462");

    // TODO: Fix me
    private void ValidateTestSourceLocationDeployment_netcoreapp31()
        => ValidateTestSourceLocationDeployment("netcoreapp3.1");

    public void ValidateTestSourceLocationDeployment(string targetFramework)
    {
        InvokeVsTestForExecution(new string[] { TestAssetPreserveNewest }, RunSetting, targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.PassIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.FailIfFilePresent");
    }    
}

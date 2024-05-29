// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class DeploymentTests : CLITestBase
{
    private const string TestAssetNever = "DeploymentTestProject.Never";
    private const string TestAssetPreserveNewest = "DeploymentTestProject.PreserveNewest";
    private const string RunSetting =
        """
        <RunSettings>
          <MSTestV2>
            <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
          </MSTestV2>
        </RunSettings>
        """;

    public void ValidateTestSourceDependencyDeployment_net462()
        => ValidateTestSourceDependencyDeployment("net462");

    public void ValidateTestSourceDependencyDeployment_netcoreapp31()
        => ValidateTestSourceDependencyDeployment("netcoreapp3.1");

    private void ValidateTestSourceDependencyDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetNever], targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.FailIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.PassIfFilePresent");
    }

    public void ValidateTestSourceLocationDeployment_net462()
        => ValidateTestSourceLocationDeployment("net462");

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is currently ignored and that's why we marked it as private")]
    private void ValidateTestSourceLocationDeployment_netcoreapp31()
        => ValidateTestSourceLocationDeployment("netcoreapp3.1");

    public void ValidateTestSourceLocationDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], RunSetting, "DeploymentTestProject", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.PassIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.FailIfFilePresent");
    }

    public void ValidateDirectoryDeployment_net462()
        => ValidateDirectoryDeployment("net462");

    public void ValidateDirectoryDeployment_netcoreapp31()
        => ValidateDirectoryDeployment("netcoreapp3.1");

    public void ValidateDirectoryDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], testCaseFilter: "DirectoryDeploymentTests", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.PreserveNewest.DirectoryDeploymentTests.DirectoryWithForwardSlash", "DeploymentTestProject.PreserveNewest.DirectoryDeploymentTests.DirectoryWithBackSlash");
    }

    public void ValidateFileDeployment_net462()
        => ValidateFileDeployment("net462");

    public void ValidateFileDeployment_netcoreapp31()
        => ValidateFileDeployment("netcoreapp3.1");

    public void ValidateFileDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], testCaseFilter: "FileDeploymentTests", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.PreserveNewest.FileDeploymentTests.FileWithForwardSlash", "DeploymentTestProject.PreserveNewest.FileDeploymentTests.FileWithBackSlash");
    }
}

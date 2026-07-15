// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
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

    [TestMethod]
    [DataRow("net462")]
    [DataRow("net8.0")]
    public void ValidateTestSourceDependencyDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetNever], targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.FailIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.PassIfFilePresent");
    }

    [TestMethod]
    [DataRow("net462")]
    [DataRow("net8.0", IgnoreMessage = "This is currently ignored and that's why we marked it as private")]
    public void ValidateTestSourceLocationDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], RunSetting, "DeploymentTestProject", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.DeploymentTestProject.PassIfFilePresent", "DeploymentTestProject.DeploymentTestProject.PassIfDeclaredFilesPresent");
        ValidateFailedTestsContain(true, "DeploymentTestProject.DeploymentTestProject.FailIfFilePresent");
    }

    [TestMethod]
    [DataRow("net462")]
    [DataRow("net8.0")]
    public void ValidateDirectoryDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], testCaseFilter: "DirectoryDeploymentTests", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.PreserveNewest.DirectoryDeploymentTests.DirectoryWithForwardSlash", "DeploymentTestProject.PreserveNewest.DirectoryDeploymentTests.DirectoryWithBackSlash");
    }

    [TestMethod]
    [DataRow("net462")]
    [DataRow("net8.0")]
    public void ValidateFileDeployment(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetPreserveNewest], testCaseFilter: "FileDeploymentTests", targetFramework: targetFramework);
        ValidatePassedTestsContain("DeploymentTestProject.PreserveNewest.FileDeploymentTests.FileWithForwardSlash", "DeploymentTestProject.PreserveNewest.FileDeploymentTests.FileWithBackSlash");
    }
}

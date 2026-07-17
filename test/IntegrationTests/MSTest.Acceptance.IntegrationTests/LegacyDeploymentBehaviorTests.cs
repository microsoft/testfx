// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

// Legacy equivalence map:
// - MSTest.VstestConsoleWrapper.IntegrationTests.DeploymentTests.ValidateTestSourceDependencyDeployment:
//   no equivalent acceptance method; preserved by TestSourceDependencies_WithCopyToOutputNever_OnlyDeclaredItemsAreDeployed.
// - MSTest.VstestConsoleWrapper.IntegrationTests.DeploymentTests.ValidateTestSourceLocationDeployment:
//   no equivalent acceptance method; preserved by DeployTestSourceDependenciesFalse_UsesSourceOutputLocation.
// - MSTest.VstestConsoleWrapper.IntegrationTests.DeploymentTests.ValidateDirectoryDeployment:
//   no equivalent acceptance method; preserved by DirectoryDeployment_AcceptsForwardAndBackSlashes.
// - MSTest.VstestConsoleWrapper.IntegrationTests.DeploymentTests.ValidateFileDeployment:
//   no equivalent acceptance method; preserved by FileDeployment_AcceptsForwardAndBackSlashes.
// MSTest.Acceptance.IntegrationTests.DeploymentItemTests.AssemblyIsLoadedOnceFromDeploymentDirectory is
// intentionally not duplicated: it covers AppDomain assembly identity, not these source/copy/path behaviors.
[TestClass]
public sealed class LegacyDeploymentBehaviorTests : AcceptanceTestBase<LegacyDeploymentBehaviorTests.TestAssetFixture>
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestSourceDependencies_WithCopyToOutputNever_OnlyDeclaredItemsAreDeployed(string tfm)
    {
        TestHostResult result = await ExecuteAsync(tfm, "FullyQualifiedName~SourceDependencyTests");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 1, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "FailIfUndeclaredNeverFilePresent",
            "PassIfDeclaredNeverFilesPresent");
        LegacyAcceptanceAssert.Failed(result, "PassIfUndeclaredNeverFilePresent");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DeployTestSourceDependenciesFalse_UsesSourceOutputLocation(string tfm)
    {
        TestHostResult result = await ExecuteAsync(
            tfm,
            "FullyQualifiedName~SourceLocationTests",
            "--settings DisableSourceDependencyDeployment.runsettings");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 1, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "PassIfPreserveNewestFilePresent",
            "PassIfDeclaredFilesPresent");
        LegacyAcceptanceAssert.Failed(result, "FailIfPreserveNewestFilePresent");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DirectoryDeployment_AcceptsForwardAndBackSlashes(string tfm)
    {
        TestHostResult result = await ExecuteAsync(
            tfm,
            "FullyQualifiedName~DirectoryDeploymentTests",
            "--settings DisableSourceDependencyDeployment.runsettings");

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "DirectoryWithForwardSlash",
            "DirectoryWithBackSlash");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task FileDeployment_AcceptsForwardAndBackSlashes(string tfm)
    {
        TestHostResult result = await ExecuteAsync(
            tfm,
            "FullyQualifiedName~FileDeploymentTests",
            "--settings DisableSourceDependencyDeployment.runsettings");

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "FileWithForwardSlash",
            "FileWithBackSlash");
    }

    private async Task<TestHostResult> ExecuteAsync(string tfm, string filter, string? additionalArguments = null)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string arguments = $"{additionalArguments} --filter {filter} --output Detailed";
        return await testHost.ExecuteAsync(arguments, cancellationToken: TestContext.CancellationToken);
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "LegacyDeploymentBehavior";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file LegacyDeploymentBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <LangVersion>preview</LangVersion>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
  <ItemGroup>
    <None Update="NeverSource.txt" CopyToOutputDirectory="Never" />
    <None Update="NeverDeclared.txt" CopyToOutputDirectory="Never" />
    <None Update="PreserveNewestSource.txt" CopyToOutputDirectory="PreserveNewest" />
    <None Update="PreserveNewestDeclared.txt" CopyToOutputDirectory="PreserveNewest" />
    <None Update="DisableSourceDependencyDeployment.runsettings" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="ForwardDirectory\**\*" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="BackDirectory\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>

#file DisableSourceDependencyDeployment.runsettings
<RunSettings>
  <MSTest>
    <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
  </MSTest>
</RunSettings>

#file NeverSource.txt
never source dependency

#file NeverDeclared.txt
never declared deployment item

#file PreserveNewestSource.txt
preserve newest source dependency

#file PreserveNewestDeclared.txt
preserve newest declared deployment item

#file ForwardDirectory/forward.txt
forward directory deployment

#file BackDirectory/back.txt
back directory deployment

#file DeploymentCases.cs
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegacyDeploymentBehavior;

[DeploymentItem(@"..\..\..\NeverDeclared.txt")]
[TestClass]
public class SourceDependencyTests
{
    [TestMethod]
    public void PassIfUndeclaredNeverFilePresent()
        => Assert.IsTrue(File.Exists("NeverSource.txt"));

    [TestMethod]
    public void FailIfUndeclaredNeverFilePresent()
        => Assert.IsFalse(File.Exists("NeverSource.txt"));

    [TestMethod]
    public void PassIfDeclaredNeverFilesPresent()
        => Assert.IsTrue(File.Exists("NeverDeclared.txt"));
}

[DeploymentItem("PreserveNewestDeclared.txt")]
[TestClass]
public class SourceLocationTests
{
    [TestMethod]
    public void PassIfPreserveNewestFilePresent()
        => Assert.IsTrue(File.Exists("PreserveNewestSource.txt"));

    [TestMethod]
    public void FailIfPreserveNewestFilePresent()
        => Assert.IsFalse(File.Exists("PreserveNewestSource.txt"));

    [TestMethod]
    public void PassIfDeclaredFilesPresent()
        => Assert.IsTrue(File.Exists("PreserveNewestDeclared.txt"));
}

[TestClass]
public class DirectoryDeploymentTests
{
    [TestMethod]
    [DeploymentItem(@"ForwardDirectory/")]
    public void DirectoryWithForwardSlash()
        => Assert.IsTrue(File.Exists("forward.txt"));

    [TestMethod]
    [DeploymentItem(@"BackDirectory\")]
    public void DirectoryWithBackSlash()
        => Assert.IsTrue(File.Exists("back.txt"));
}

[TestClass]
public class FileDeploymentTests
{
    [TestMethod]
    [DeploymentItem(@"ForwardDirectory/forward.txt")]
    public void FileWithForwardSlash()
        => Assert.IsTrue(File.Exists("forward.txt"));

    [TestMethod]
    [DeploymentItem(@"BackDirectory\back.txt")]
    public void FileWithBackSlash()
        => Assert.IsTrue(File.Exists("back.txt"));
}
""";
    }

    public TestContext TestContext { get; set; } = default!;
}

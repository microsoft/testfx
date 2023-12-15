// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSTestRunnerTests : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    public MSTestRunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task EnableMSTestRunner_True_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentTemplateSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestCurrentVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", "true")
            .PatchCodeWithReplace("$OutputType$", "Exe"),
            addPublicFeeds: true);
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync(
            $"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);
        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
        var testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentTemplateSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestCurrentVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", "false")
            .PatchCodeWithReplace("$OutputType$", "Exe"),
            addPublicFeeds: true);

        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        try
        {
            compilationResult = await DotnetCli.RunAsync($"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            var testHostResult = await testHost.ExecuteAsync();
            Assert.AreEqual(string.Empty, testHostResult.StandardOutput);
        }
        catch (Exception ex)
        {
            if (TargetFrameworks.NetFramework.Any(x => x.Arguments == tfm))
            {
                Assert.IsTrue(ex.Message.Contains("Program does not contain a static 'Main' method suitable for an entry point"), ex.Message);

                // .NET Framework does not insert the entry point for empty program.
                return;
            }
        }
    }

    private const string CurrentTemplateSourceCode = """
#file MSTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <OutputType>$OutputType$</OutputType>
    <EnableMSTestRunner>$EnableMSTestRunner$</EnableMSTestRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";
}

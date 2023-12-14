// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSTestRunnerTests : BaseAcceptanceTests
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    public MSTestRunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task EnableMSTestRunner_True_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentTemplateSourceCode
            .PatchCodeWithRegularExpression("tfm", tfm)
            .PatchCodeWithRegularExpression("mstestversion", MSTestCurrentVersion)
            .PatchCodeWithRegularExpression("enablemstestrunner", "<EnableMSTestRunner>true</EnableMSTestRunner>")
            .PatchCodeWithRegularExpression("outputtype", "<OutputType>Exe</OutputType>"),
            addPublicFeeds: true);
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
        compilationResult = await DotnetCli.RunAsync(
            $"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
            _acceptanceFixture.NuGetGlobalPackagesFolder, failIfReturnValueIsNotZero: false);
        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentTemplateSourceCode
            .PatchCodeWithRegularExpression("tfm", tfm)
            .PatchCodeWithRegularExpression("mstestversion", MSTestCurrentVersion)
            .PatchCodeWithRegularExpression("enablemstestrunner", "<EnableMSTestRunner>false</EnableMSTestRunner>")
            .PatchCodeWithRegularExpression("outputtype", "<OutputType>Exe</OutputType>"),
            addPublicFeeds: true);

        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
        try
        {
            compilationResult = await DotnetCli.RunAsync($"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
            var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });
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
    <TargetFramework>tfm</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    outputtype
    enablemstestrunner
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="MSTest.TestAdapter" Version="mstestversion" />
    <PackageReference Include="MSTest.TestFramework" Version="mstestversion" />
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class RunnerTests : BaseAcceptanceTests
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    public RunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    // [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration), TestArgumentsEntryProviderMethodName = nameof(FormatGetBuildMatrixTfmBuildConfigurationEntry))]
    // public async Task EnableMSTestRunner_True_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration)
    // {
    //    using TestAsset generator = await TestAsset.GenerateAssetAsync(
    //        AssetName,
    //        CurrentTemplateSourceCode
    //        .PatchCodeWithRegularExpression("tfm", tfm)
    //        .PatchCodeWithRegularExpression("mstestversion", MSTestCurrentVersion)
    //        .PatchCodeWithRegularExpression("enablemstestrunner", "<EnableMSTestRunner>true</EnableMSTestRunner>"));
    //    string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
    //    var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
    //    compilationResult = await DotnetCli.RunAsync($"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
    //    var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
    //    var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });
    //    Assert.IsTrue(testHostResult.StandardOutput.Contains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1"));
    // }
    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration), TestArgumentsEntryProviderMethodName = nameof(FormatGetBuildMatrixTfmBuildConfigurationEntry))]
    public async Task EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentTemplateSourceCode
            .PatchCodeWithRegularExpression("tfm", tfm)
            .PatchCodeWithRegularExpression("mstestversion", MSTestCurrentVersion)
            .PatchCodeWithRegularExpression("enablemstestrunner", string.Empty));
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder);
        compilationResult = await DotnetCli.RunAsync($"build -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder,
            failIfReturnValueIsNotZero: !Debugger.IsAttached);
        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });
        Assert.AreEqual(string.Empty, testHostResult.StandardOutput);
    }

    private const string CurrentTemplateSourceCode = """
#file MSTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>tfm</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
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

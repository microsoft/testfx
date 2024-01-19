// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSTestRunnerTests : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private static readonly SemaphoreSlim Lock = new(1);
    private const string AssetName = "MSTestProject";

    public MSTestRunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task EnableMSTestRunner_True_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        await Lock.WaitAsync();
        try
        {
            using TestAsset generator = await TestAsset.GenerateAssetAsync(
                AssetName,
                CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
                .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
                .PatchCodeWithReplace("$Extra$", string.Empty),
                addPublicFeeds: true);
            string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
            var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            compilationResult = await DotnetCli.RunAsync(
                $"{verb} -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
                _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
            var testHostResult = await testHost.ExecuteAsync();
            testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        }
        finally
        {
            Lock.Release();
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task EnableMSTestRunner_True_WithCustomEntryPoint_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        await Lock.WaitAsync();
        try
        {
            using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            (CurrentMSTestSourceCode + """
#file Program.cs

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { typeof(Program).Assembly });
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
""")
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
            .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
            .PatchCodeWithReplace("$Extra$", """
<GenerateTestingPlatformEntryPoint>False</GenerateTestingPlatformEntryPoint>
<LangVersion>preview</LangVersion>
"""),
            addPublicFeeds: true);
            string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
            var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            compilationResult = await DotnetCli.RunAsync(
                $"{verb} -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
                _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
            var testHostResult = await testHost.ExecuteAsync();
            testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        }
        finally
        {
            Lock.Release();
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        await Lock.WaitAsync();
        try
        {
            using TestAsset generator = await TestAsset.GenerateAssetAsync(
        AssetName,
        CurrentMSTestSourceCode
        .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
        .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
        .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
        .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>false</EnableMSTestRunner>")
        .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
        .PatchCodeWithReplace("$Extra$", string.Empty),
        addPublicFeeds: true);
            string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
            var compilationResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            try
            {
                compilationResult = await DotnetCli.RunAsync($"{verb} -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
                var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
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
        finally
        {
            Lock.Release();
        }
    }
}

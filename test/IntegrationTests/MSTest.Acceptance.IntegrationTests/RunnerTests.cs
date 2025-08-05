// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

using SystemTask = System.Threading.Tasks.Task;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class RunnerTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestProject";

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async SystemTask EnableMSTestRunner_True_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
                .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
                .PatchCodeWithReplace("$Extra$", string.Empty));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync(
            $"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        Build binLog = Serialization.Read(compilationResult.BinlogPath);
        Assert.AreNotEqual(0, binLog.FindChildrenRecursive<AddItem>()
            .Count(x => x.Title.Contains("ProjectCapability") && x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async SystemTask EnableMSTestRunner_True_WithCustomEntryPoint_Will_Run_Standalone(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            (CurrentMSTestSourceCode + """
#file Program.cs
using Microsoft.Testing.Platform.Builder;
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
"""));
        await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        await DotnetCli.RunAsync(
            $"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async SystemTask EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>false</EnableMSTestRunner>")
                .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
                .PatchCodeWithReplace("$Extra$", string.Empty));
        await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        try
        {
            await DotnetCli.RunAsync($"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
            var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
            TestHostResult testHostResult = await testHost.ExecuteAsync();
            Assert.AreEqual(string.Empty, testHostResult.StandardOutput);
        }
        catch (Exception ex)
        {
            if (TargetFrameworks.NetFramework.Any(x => x == tfm))
            {
                Assert.Contains("Program does not contain a static 'Main' method suitable for an entry point", ex.Message, ex.Message);
                // .NET Framework does not insert the entry point for empty program.
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async SystemTask EnableMSTestRunner_False_Wont_Flow_TestingPlatformServer_Capability(string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", string.Empty)
                .PatchCodeWithReplace("$OutputType$", string.Empty)
                .PatchCodeWithReplace("$Extra$", string.Empty));

        await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID} ", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        Build binLog = Serialization.Read(result.BinlogPath);
        Assert.IsFalse(binLog.FindChildrenRecursive<AddItem>()
            .Any(x => x.Title.Contains("ProjectCapability") && x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));
    }
}

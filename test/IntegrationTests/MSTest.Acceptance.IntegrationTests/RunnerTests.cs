// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

using SL = Microsoft.Build.Logging.StructuredLogger;
using SystemTask = System.Threading.Tasks.Task;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class RunnerTests : AcceptanceTestBase
{
    private const string AssetName = "MSTestProject";
    private readonly AcceptanceFixture _acceptanceFixture;

    public RunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
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
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync(
            $"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        SL.Build binLog = SL.Serialization.Read(binlogFile);
        Assert.IsNotEmpty(binLog.FindChildrenRecursive<AddItem>()
            .Where(x => x.Title.Contains("ProjectCapability"))
            .Where(x => x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));

        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
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
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync(
            $"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
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
        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        try
        {
            compilationResult = await DotnetCli.RunAsync($"{verb} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -bl:{binlogFile} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
            TestHostResult testHostResult = await testHost.ExecuteAsync();
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

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
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

        string binlogFile = Path.Combine(generator.TargetAssetPath, "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync($"{verb} -bl:{binlogFile} -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID} ", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        SL.Build binLog = SL.Serialization.Read(binlogFile);
        Assert.IsEmpty(binLog.FindChildrenRecursive<AddItem>()
            .Where(x => x.Title.Contains("ProjectCapability"))
            .Where(x => x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));
    }
}

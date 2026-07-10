// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Combinatorial.MSTest;

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
    [CombinatorialData]
    public async SystemTask EnableMSTestRunner_True_Will_Run_Standalone([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration, Verb verb)
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
            $"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}",
            cancellationToken: TestContext.CancellationToken);

        Build binLog = Serialization.Read(compilationResult.BinlogPath);
        Assert.AreNotEqual(0, binLog.FindChildrenRecursive<AddItem>()
            .Count(x => x.Title.Contains("ProjectCapability") && x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [CombinatorialData]
    public async SystemTask EnableMSTestRunner_True_WithCustomEntryPoint_Will_Run_Standalone([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration, Verb verb)
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

        await DotnetCli.RunAsync(
            $"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}",
            cancellationToken: TestContext.CancellationToken);
        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [CombinatorialData]
    public async SystemTask Default_Will_Flow_TestingPlatformServer_Capability([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration, Verb verb)
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

        DotnetMuxerResult result = await DotnetCli.RunAsync($"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID} ", cancellationToken: TestContext.CancellationToken);

        // In v5 MSTest always runs on Microsoft.Testing.Platform, so the TestingPlatformServer capability
        // flows by default (there is no EnableMSTestRunner opt-out anymore).
        Build binLog = Serialization.Read(result.BinlogPath);
        Assert.AreNotEqual(0, binLog.FindChildrenRecursive<AddItem>()
            .Count(x => x.Title.Contains("ProjectCapability") && x.Children.Any(c => ((Item)c).Name == "TestingPlatformServer")));
    }

    public TestContext TestContext { get; set; }
}

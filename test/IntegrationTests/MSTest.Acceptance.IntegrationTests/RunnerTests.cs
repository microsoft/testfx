// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

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

        Build binLog = BinlogReader.Read(compilationResult.BinlogPath!);
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
    public async SystemTask EnableMSTestRunner_False_Will_Run_Empty_Program_EntryPoint_From_Tpv2_SDK([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration, Verb verb)
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

        if (TargetFrameworks.NetFramework.Any(x => x == tfm))
        {
            // Running under .NET Framework, which doesn't generate an empty entry point.
            Exception ex = await Assert.ThrowsAsync<Exception>(async () => await DotnetCli.RunAsync($"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}", cancellationToken: TestContext.CancellationToken));
            Assert.Contains("Program does not contain a static 'Main' method suitable for an entry point", ex.Message);
            return;
        }

        // Running on .NET (Core), building should succeed and we should run empty entry point.
        await DotnetCli.RunAsync($"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}", cancellationToken: TestContext.CancellationToken);
        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration, verb: verb);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(string.Empty, testHostResult.StandardOutput);
        testHostResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    [CombinatorialData]
    public async SystemTask EnableMSTestRunner_False_Wont_Flow_TestingPlatformServer_Capability([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration, Verb verb)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", string.Empty)
                .PatchCodeWithReplace("$OutputType$", string.Empty)
                .PatchCodeWithReplace("$Extra$", string.Empty)
                .PatchCodeWithReplace("</Project>", """
<ItemGroup>
  <ProjectCapability Include="TestContainer" />
</ItemGroup>
<Target Name="PrintProjectCapabilities" BeforeTargets="CoreCompile">
  <Message Text="ProjectCapabilitiesEvaluated" Importance="high" />
  <Message Text="ProjectCapability=[%(ProjectCapability.Identity)]" Importance="high" />
</Target>
</Project>
"""));

        DotnetMuxerResult result = await DotnetCli.RunAsync($"{verb} {generator.TargetAssetPath} -c {buildConfiguration} -r {RID} ", cancellationToken: TestContext.CancellationToken);

        result.AssertOutputContains("ProjectCapabilitiesEvaluated");
        result.AssertOutputContains("ProjectCapability=[TestContainer]");
        result.AssertOutputDoesNotContain("ProjectCapability=[TestingPlatformServer]");
    }

    [TestMethod]
    public async SystemTask TestingPlatformCapabilities_Can_Be_Removed_From_Project()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{TargetFrameworks.NetCurrent}</TargetFramework>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
                .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
                .PatchCodeWithReplace("$Extra$", string.Empty)
                .PatchCodeWithReplace("</Project>", """
<ItemGroup>
  <ProjectCapability Remove="TestingPlatformServer;TestContainer" />
</ItemGroup>
<Target Name="PrintProjectCapabilities" BeforeTargets="CoreCompile">
  <Message Text="ProjectCapabilitiesEvaluated" Importance="high" />
  <Message Text="ProjectCapability=[%(ProjectCapability.Identity)]" Importance="high" />
</Target>
</Project>
"""));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c Debug -r {RID}",
            cancellationToken: TestContext.CancellationToken);

        result.AssertOutputContains("ProjectCapabilitiesEvaluated");
        result.AssertOutputDoesNotContain("ProjectCapability=[TestingPlatformServer]");
        result.AssertOutputDoesNotContain("ProjectCapability=[TestContainer]");
    }

    public TestContext TestContext { get; set; }
}

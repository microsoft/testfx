// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1636 // File header copyright text should match
// Copyright (c) Microsoft Corporation. All rights reserved.
#pragma warning restore SA1636 // File header copyright text should match

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests_Test : AcceptanceTestBase
{
    private const string AssetName = "MSBuildTests";
    private readonly AcceptanceFixture _acceptanceFixture;

    public MSBuildTests_Test(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    internal static TestArgumentsEntry<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> FormatBuildMatrixEntry(TestArgumentsContext ctx)
    {
        var entry = ((string, string, BuildConfiguration, bool))ctx.Arguments;
        return new TestArgumentsEntry<(string, string, BuildConfiguration, bool)>(entry, $"{entry.Item1},{(TargetFrameworks.All.ToMSBuildTargetFrameworks() == entry.Item2 ? "multitfm" : entry.Item2)},{entry.Item3},{(entry.Item4 ? "Succeeded" : "Failed")}");
    }

    internal static IEnumerable<BuildConfiguration> GetBuildConfiguration()
    {
        string[] compilationModes = new[] { "Debug", "Release" };
        foreach (string compilationMode in compilationModes)
        {
            yield return compilationMode == "Debug" ? BuildConfiguration.Debug : BuildConfiguration.Release;
        }
    }

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in GetBuildConfiguration())
            {
                foreach (bool testSucceeded in new bool[] { true, false })
                {
                    foreach (string buildCommand in new string[]
                    {
                        "build -t:Test",
                        "test -p:TestingPlatformDotnetTestSupport=True",
                    })
                    {
                        yield return (buildCommand, tfm.Arguments, compilationMode, testSucceeded);
                    }
                }
            }
        }
    }

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrixMultiTfm()
    {
        foreach (BuildConfiguration compilationMode in GetBuildConfiguration())
        {
            foreach (bool testSucceeded in new bool[] { true, false })
            {
                foreach (string buildCommand in new string[]
                    {
                        "build -t:Test",
                        "test -p:TestingPlatformDotnetTestSupport=True",
                    })
                {
                    yield return (buildCommand, TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode, testSucceeded);
                }
            }
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, tfm, false, new[] { tfm }, compilationMode, testSucceeded);

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfm), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, multiTfm, true, TargetFrameworks.All.Select(x => x.Arguments).ToArray(), compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
        => await RetryHelper.RetryAsync(
        async () =>
        {
            using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                AssetName,
                SourceCode
                .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
                .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
                .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
            string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
            string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
            DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformCommandLineArguments=\"--results-directory %22{testResultFolder}%22\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

            foreach (string tfmToAssert in tfmsToAssert)
            {
                CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
            }
        }, 3, TimeSpan.FromSeconds(10));

    [ArgumentsProvider(nameof(GetBuildMatrix), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, tfm, false, new[] { tfm }, compilationMode, testSucceeded);

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfm), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, multiTfm, true, TargetFrameworks.All.Select(x => x.Arguments).ToArray(), compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                    AssetName,
                    SourceCode
                    .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
                    .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
                    .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
                string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
                string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformCommandLineArguments=\"--treenode-filter /*/*/*/TestMethod1 --results-directory %22{testResultFolder}%22\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} /warnAsError \"{testAsset.TargetAssetPath}\"", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: true);

                foreach (string tfmToAssert in tfmsToAssert)
                {
                    CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
                }
            }, 3, TimeSpan.FromSeconds(10));

    public async Task Invoke_DotnetTest_With_Arch_Switch_x86_Should_Work()
        => await RetryHelper.RetryAsync(
            async () =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return;
                }

                string root = RootFinder.Find();
                string x86Muxer = Path.Combine(root, ".dotnet", "x86");
                var dotnetRootX86 = new Dictionary<string, string>
                {
                    { "DOTNET_ROOT_X86", x86Muxer },
                };

                TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                    AssetName,
                    SourceCode
                    .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
                    .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent.Arguments}</TargetFrameworks>")
                    .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
                string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
                string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test --arch x86 -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false -bl:{binlogFile} /warnAsError \"{testAsset.TargetAssetPath}\"", _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                environmentVariables: dotnetRootX86,
                failIfReturnValueIsNotZero: false);

                string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, "MSBuild Tests_net8.0_x86.log", SearchOption.AllDirectories).Single();
                Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
                string logFileContent = File.ReadAllText(outputFileLog);
                Assert.IsTrue(Regex.IsMatch(logFileContent, ".*win-x86.*"), logFileContent);
                Assert.IsTrue(Regex.IsMatch(logFileContent, @".*dotnet\.exe run.*"), logFileContent);
                Assert.IsTrue(Regex.IsMatch(logFileContent, @".*--arch x86.*"), logFileContent);
            }, 3, TimeSpan.FromSeconds(10));

    private static void CommonAssert(DotnetMuxerResult compilationResult, string tfm, bool testSucceeded, string testResultFolder)
    {
        Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $".*Run tests:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        if (testSucceeded)
        {
            Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $".*error : Tests failed:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        }
        else
        {
            Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $"Tests succeeded:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        }

        string outputFileLog = Path.Combine(testResultFolder, $"MSBuild Tests_{tfm}_x64.log");
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        Assert.IsFalse(string.IsNullOrEmpty(File.ReadAllText(outputFileLog)), $"Content of file '{File.ReadAllText(outputFileLog)}'");
    }

    // We avoid to test the multitfm because it's already tested with the above tests and we don't want to have too heavy testing, msbuild is pretty heavy(a lot of processes started
    // due to the no nodereuse and makes tests flaky.
    // We test two functionality for the same reason, we don't want to load too much the CI only for UX reasons.
    [ArgumentsProvider(nameof(GetBuildMatrix), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Showing_Error_And_Do_Not_Capture_The_Output_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
    {
        // We test only failed but we don't want to have too much argument provider overload.
        if (testSucceeded)
        {
            return;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformShowTestsFailure=True -p:TestingPlatformCaptureOutput=False -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);
        Assert.Contains("error test failed: TestMethod2 (", compilationResult.StandardOutput);
        Assert.Contains("): Assert.IsTrue: Expected 'true', but got 'false'.", compilationResult.StandardOutput);
        Assert.Contains(".NET Testing Platform", compilationResult.StandardOutput);
    }

    private const string SourceCode = """
#file MSBuild Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        $TargetFrameworks$
        $PlatformTarget$
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        <RootNamespace>MSBuildTests</RootNamespace>
        <SuppressNETCoreSdkPreviewMessage>true</SuppressNETCoreSdkPreviewMessage>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <!-- TrxReport.Abstractions is only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using MSBuildTests;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new MSBuild_Tests.SourceGeneratedTestNodesBuilder());
builder.AddMSBuild();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace MSBuildTests;

[TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        Assert.IsTrue(true);
    }

    public void TestMethod2()
    {
        Assert.IsTrue($AssertValue$);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Platform.MSBuild;
""";
}

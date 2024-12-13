﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests_Test : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSBuildTests";

    public static string? FormatBuildMatrixEntry(MethodInfo method, object?[]? data)
        => $"{data![0]},{(string.Equals(TargetFrameworks.All.ToMSBuildTargetFrameworks(), data[1]) ? "multitfm" : data[1])},{data[2]},{((bool)data[3]! ? "Succeeded" : "Failed")}";

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrix()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                foreach (bool testSucceeded in new bool[] { true, false })
                {
                    foreach (string buildCommand in new string[]
                    {
                        "build -t:Test",
                        "test -p:TestingPlatformDotnetTestSupport=True",
                    })
                    {
                        yield return (buildCommand, tfm, compilationMode, testSucceeded);
                    }
                }
            }
        }
    }

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrixMultiTfm()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
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

    [DynamicData(nameof(GetBuildMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [DynamicData(nameof(GetBuildMatrixMultiTfm), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, multiTfm, true, TargetFrameworks.All, compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformCommandLineArguments=\"--results-directory %22{testResultFolder}%22\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

    [DynamicData(nameof(GetBuildMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [DynamicData(nameof(GetBuildMatrixMultiTfm), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, multiTfm, true, TargetFrameworks.All, compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult compilationResult = testCommand.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            ? await DotnetCli.RunAsync(
                $"{testCommand} -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\" -- --treenode-filter /*/*/*/TestMethod1 --results-directory \"{testResultFolder}\"",
                AcceptanceFixture.NuGetGlobalPackagesFolder.Path)
            : await DotnetCli.RunAsync(
                $"{testCommand} -p:TestingPlatformCommandLineArguments=\"--treenode-filter /*/*/*/TestMethod1 --results-directory \"{testResultFolder}\"\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
                AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

    [TestMethod]
    public async Task Invoke_DotnetTest_With_Arch_Switch_x86_Should_Work()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string root = RootFinder.Find();
        string x86Muxer = Path.Combine(root, ".dotnet", "x86");
        var dotnetRootX86 = new Dictionary<string, string?>
        {
            { "DOTNET_ROOT_X86", x86Muxer },
        };

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        await DotnetCli.RunAsync(
            $"test --arch x86 -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            environmentVariables: dotnetRootX86,
            failIfReturnValueIsNotZero: false);

        string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, $"MSBuild Tests_net9.0_x86.log", SearchOption.AllDirectories).Single();
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        string logFileContent = File.ReadAllText(outputFileLog);
        Assert.IsTrue(Regex.IsMatch(logFileContent, ".*win-x86.*"), logFileContent);
        Assert.IsTrue(Regex.IsMatch(logFileContent, @"\.dotnet\\x86\\dotnet\.exe"), logFileContent);
    }

    [TestMethod]
    public async Task Invoke_DotnetTest_With_Incompatible_Arch()
    {
        Architecture currentArchitecture = RuntimeInformation.ProcessArchitecture;
        string incompatibleArchitecture = currentArchitecture switch
        {
            Architecture.X86 or Architecture.X64 => "arm64",
            _ => "x64",
        };

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFramework>{TargetFrameworks.NetCurrent}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --arch {incompatibleArchitecture} -p:TestingPlatformDotnetTestSupport=True \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false);
        // The output looks like:
        /*
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error : Could not find 'dotnet.exe' host for the 'arm64' architecture. [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error :  [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error : You can resolve the problem by installing the 'arm64' .NET. [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error :  [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error : The specified framework can be found at: [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
            D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\tidOn\.packages\microsoft.testing.platform.msbuild\1.5.0-ci\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5): error :   - https://aka.ms/dotnet-download [D:\a\_work\1\s\artifacts\tmp\Debug\testsuite\vf8vR\MSBuildTests\MSBuild Tests.csproj]
         */
        // Assert each error line separately for simplicity.
        result.AssertOutputContains($"Could not find '{(OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")}' host for the '{incompatibleArchitecture}' architecture.");
        result.AssertOutputContains($"You can resolve the problem by installing the '{incompatibleArchitecture}' .NET.");
        result.AssertOutputContains("The specified framework can be found at:");
        result.AssertOutputContains("  - https://aka.ms/dotnet-download");
    }

    [TestMethod]
    public async Task Invoke_DotnetTest_With_DOTNET_HOST_PATH_Should_Work()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string root = RootFinder.Find();
        string dotnetHostPath = Path.Combine(root, ".dotnet", "dotnet.exe");
        var dotnetHostPathEnvVar = new Dictionary<string, string?>
        {
            { "DOTNET_HOST_PATH", dotnetHostPath },
        };

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        await DotnetCli.RunAsync(
            $"test -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            environmentVariables: dotnetHostPathEnvVar,
            failIfReturnValueIsNotZero: false);

        string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, "MSBuild Tests_net9.0_x64.log", SearchOption.AllDirectories).Single();
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        string logFileContent = File.ReadAllText(outputFileLog);
        Assert.IsTrue(Regex.IsMatch(logFileContent, @"\.dotnet\\dotnet\.exe"), logFileContent);
    }

    private static void CommonAssert(DotnetMuxerResult compilationResult, string tfm, bool testSucceeded, string testResultFolder)
    {
        Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $".*Run tests:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        if (testSucceeded)
        {
            Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $"Tests succeeded:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        }
        else
        {
            Assert.IsTrue(Regex.IsMatch(compilationResult.StandardOutput, $".*error : Tests failed:.* \\[{tfm}|x64\\]"), compilationResult.StandardOutput);
        }

        string outputFileLog = Path.Combine(testResultFolder, $"MSBuild Tests_{tfm}_x64.log");
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        Assert.IsFalse(string.IsNullOrEmpty(File.ReadAllText(outputFileLog)), $"Content of file '{File.ReadAllText(outputFileLog)}'");
    }

    // We avoid to test the multi-tfm because it's already tested with the above tests and we don't want to have too heavy testing,
    // msbuild is pretty heavy (a lot of processes started due to the no 'nodereuse') and makes tests flaky.
    // We test two functionality for the same reason, we don't want to load too much the CI only for UX reasons.
    [DynamicData(nameof(GetBuildMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
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
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformShowTestsFailure=True -p:TestingPlatformCaptureOutput=False -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

        compilationResult.AssertOutputContains("error test failed: TestMethod2 (");
        compilationResult.AssertOutputContains("Assert.IsTrue: Expected 'true', but got 'false'.");
        compilationResult.AssertOutputContains(".NET Testing Platform");
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
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
       context.Complete();
       return Task.CompletedTask;
    }
}
""";
}

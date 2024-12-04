// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests_Test : AcceptanceTestBase
{
    private const string AssetName = "MSBuildTests";
    private readonly AcceptanceFixture _acceptanceFixture;

    public MSBuildTests_Test(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    internal static TestArgumentsEntry<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> FormatBuildMatrixEntry(TestArgumentsContext ctx)
    {
        var entry = ((string, string, BuildConfiguration, bool))ctx.Arguments;
        return new TestArgumentsEntry<(string, string, BuildConfiguration, bool)>(entry, $"{entry.Item1},{(TargetFrameworks.All.ToMSBuildTargetFrameworks() == entry.Item2 ? "multitfm" : entry.Item2)},{entry.Item3},{(entry.Item4 ? "Succeeded" : "Failed")}");
    }

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
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
                        yield return (buildCommand, tfm.Arguments, compilationMode, testSucceeded);
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

    [ArgumentsProvider(nameof(GetBuildMatrix), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfm), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, multiTfm, true, TargetFrameworks.All.Select(x => x.Arguments).ToArray(), compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformCommandLineArguments=\"--results-directory %22{testResultFolder}%22\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfm), TestArgumentsEntryProviderMethodName = nameof(FormatBuildMatrixEntry))]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, multiTfm, true, TargetFrameworks.All.Select(x => x.Arguments).ToArray(), compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult compilationResult = testCommand.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            ? await DotnetCli.RunAsync(
                $"{testCommand} -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\" -- --treenode-filter /*/*/*/TestMethod1 --results-directory \"{testResultFolder}\"",
                _acceptanceFixture.NuGetGlobalPackagesFolder.Path)
            : await DotnetCli.RunAsync(
                $"{testCommand} -p:TestingPlatformCommandLineArguments=\"--treenode-filter /*/*/*/TestMethod1 --results-directory \"{testResultFolder}\"\" -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
                _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

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
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent.Arguments}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        await DotnetCli.RunAsync(
            $"test --arch x86 -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
            environmentVariables: dotnetRootX86,
            failIfReturnValueIsNotZero: false);

        string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, $"MSBuild Tests_net9.0_x86.log", SearchOption.AllDirectories).Single();
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        string logFileContent = File.ReadAllText(outputFileLog);
        Assert.IsTrue(Regex.IsMatch(logFileContent, ".*win-x86.*"), logFileContent);
        Assert.IsTrue(Regex.IsMatch(logFileContent, @"\.dotnet\\x86\\dotnet\.exe"), logFileContent);
    }

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
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFramework>{TargetFrameworks.NetCurrent.Arguments}</TargetFramework>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --arch {incompatibleArchitecture} -p:TestingPlatformDotnetTestSupport=True \"{testAsset.TargetAssetPath}\"",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
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
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent.Arguments}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        await DotnetCli.RunAsync(
            $"test -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false -bl:{binlogFile} \"{testAsset.TargetAssetPath}\"",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
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

    // We avoid to test the multi-tfm because it's already tested with the above tests and we don't want to have too heavy testing, msbuild is pretty heavy (a lot of processes started due to the no 'nodereuse') and makes tests flaky.
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
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformShowTestsFailure=True -p:TestingPlatformCaptureOutput=False -p:Configuration={compilationMode} -p:nodeReuse=false -bl:{binlogFile} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);
        Assert.Contains("error test failed: TestMethod2 (", compilationResult.StandardOutput);
        Assert.Contains("Assert.IsTrue: Expected 'true', but got 'false'.", compilationResult.StandardOutput);
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
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingInternalFrameworkVersion$" />
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

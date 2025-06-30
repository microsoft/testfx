// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests_Test : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSBuildTests";

    public static string? FormatBuildMatrixEntry(MethodInfo method, object?[]? data)
        => $"{data![0]},{(Equals(TargetFrameworks.All.ToMSBuildTargetFrameworks(), data[1]) ? "multitfm" : data[1])},{data[2]},{((bool)data[3]! ? "Succeeded" : "Failed")}";

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrix()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach ((string buildCommand, _, BuildConfiguration buildConfiguration, bool testSucceeded) in GetBuildMatrixMultiTfm())
            {
                yield return (buildCommand, tfm, buildConfiguration, testSucceeded);
            }
        }
    }

    internal static IEnumerable<(string BuildCommand, string TargetFramework, BuildConfiguration BuildConfiguration, bool TestSucceeded)> GetBuildMatrixMultiTfm()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
        {
            foreach (bool testSucceeded in new bool[] { true, false })
            {
                yield return ("build -t:Test", TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode, testSucceeded);
                yield return ("test -p:TestingPlatformDotnetTestSupport=True", TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode, testSucceeded);
            }
        }
    }

    [DynamicData(nameof(GetBuildMatrix), DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [DynamicData(nameof(GetBuildMatrixMultiTfm), DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(testCommand, multiTfm, true, TargetFrameworks.All, compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Execute_Tests_Without_Showing_Error_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<targetFramework>{tfm}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformCommandLineArguments=\"--results-directory %22{testResultFolder}%22\" -p:Configuration={compilationMode} -p:nodeReuse=false \"{testAsset.TargetAssetPath}\"", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: testAsset.TargetAssetPath, failIfReturnValueIsNotZero: false);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

    [DynamicData(nameof(GetBuildMatrix), DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_SingleTfm(string testCommand, string tfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, tfm, false, [tfm], compilationMode, testSucceeded);

    [DynamicData(nameof(GetBuildMatrixMultiTfm), DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
    [TestMethod]
    public async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_MultiTfm(string testCommand, string multiTfm, BuildConfiguration compilationMode, bool testSucceeded)
        => await InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(testCommand, multiTfm, true, TargetFrameworks.All, compilationMode, testSucceeded);

    private async Task InvokeTestingPlatform_Target_Should_Build_Without_Warnings_And_Execute_Passing_Test_And_Pass_TheRun_Detail(string testCommand, string tfm, bool isMultiTfm, string[] tfmsToAssert, BuildConfiguration compilationMode, bool testSucceeded)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{tfm}</TargetFrameworks>" : $"<targetFramework>{tfm}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        string testResultFolder = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult compilationResult = testCommand.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            ? await DotnetCli.RunAsync(
                $"{testCommand} -p:Configuration={compilationMode} -p:nodeReuse=false \"{testAsset.TargetAssetPath}\" -- --treenode-filter <whatever> --results-directory \"{testResultFolder}\"",
                AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
                workingDirectory: testAsset.TargetAssetPath)
            : await DotnetCli.RunAsync(
                $"{testCommand} -p:TestingPlatformCommandLineArguments=\"--treenode-filter <whatever> --results-directory \"{testResultFolder}\"\" -p:Configuration={compilationMode} -p:nodeReuse=false \"{testAsset.TargetAssetPath}\"",
                AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
                workingDirectory: testAsset.TargetAssetPath);

        foreach (string tfmToAssert in tfmsToAssert)
        {
            CommonAssert(compilationResult, tfmToAssert, testSucceeded, testResultFolder);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunUsingTestTargetWithNetfxMSBuild()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<targetFramework>{TargetFrameworks.NetCurrent}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        string msbuildExe = await FindMsbuildWithVsWhereAsync();
        var commandLine = new TestInfrastructure.CommandLine();
        string binlogFile = Path.Combine(TempDirectory.TestSuiteDirectory, $"{nameof(RunUsingTestTargetWithNetfxMSBuild)}.binlog");
        await commandLine.RunAsync($"\"{msbuildExe}\" {testAsset.TargetAssetPath} /t:Restore");
        await commandLine.RunAsync($"\"{msbuildExe}\" {testAsset.TargetAssetPath} /t:\"Build;Test\" /bl:\"{binlogFile}\"", environmentVariables: new Dictionary<string, string?>
        {
            ["DOTNET_ROOT"] = Path.Combine(RootFinder.Find(), ".dotnet"),
        });
        StringAssert.Contains(commandLine.StandardOutput, "Tests succeeded");
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

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        await DotnetCli.RunAsync(
            $"test --arch x86 -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            workingDirectory: testAsset.TargetAssetPath,
            environmentVariables: dotnetRootX86,
            failIfReturnValueIsNotZero: false);

        string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, "MSBuild Tests_net9.0_x86.log", SearchOption.AllDirectories).Single();
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        string logFileContent = File.ReadAllText(outputFileLog);
        Assert.IsTrue(Regex.IsMatch(logFileContent, ".*win-x86.*"), logFileContent);

        // This is the architecture part that's written by TerminalOutputDevice when there is no banner specified.
        Assert.Contains($"[win-x86 - {TargetFrameworks.NetCurrent}]", logFileContent);
    }

    [TestMethod]
    public async Task Invoke_DotnetTest_With_Incompatible_Arch()
    {
        // TODO: Test with both old and new dotnet test experience.
        Architecture currentArchitecture = RuntimeInformation.ProcessArchitecture;
        string incompatibleArchitecture = currentArchitecture switch
        {
            Architecture.X86 or Architecture.X64 => "arm64",
            _ => "x64",
        };

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<targetFramework>{TargetFrameworks.NetCurrent}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --arch {incompatibleArchitecture} -p:TestingPlatformDotnetTestSupport=True \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            workingDirectory: testAsset.TargetAssetPath,
            failIfReturnValueIsNotZero: false);

        // On Windows, we run the exe directly.
        // On other OSes, we run with dotnet exec.
        // This yields two different outputs, pointing to the same issue.
        string executableName = OperatingSystem.IsWindows() ? "MSBuild Tests.exe" : "MSBuild Tests";

        result.AssertOutputContains($"error MSB6003: The specified task executable \"{executableName}\" could not be run. System.ComponentModel.Win32Exception");
        result.AssertOutputContains("An error occurred trying to start process");

        if (OperatingSystem.IsWindows())
        {
            result.AssertOutputContains("The specified executable is not a valid application for this OS platform.");
        }
        else if (OperatingSystem.IsMacOS())
        {
            result.AssertOutputContains("Bad CPU type in executable");
        }
        else if (OperatingSystem.IsLinux())
        {
            result.AssertOutputContains("Exec format error");
        }
        else
        {
            // Unexpected OS.
            throw ApplicationStateGuard.Unreachable();
        }
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

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", string.Empty)
            .PatchCodeWithReplace("$TargetFrameworks$", $"<TargetFrameworks>{TargetFrameworks.NetCurrent}</TargetFrameworks>")
            .PatchCodeWithReplace("$AssertValue$", bool.TrueString.ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        await DotnetCli.RunAsync(
            $"test -p:TestingPlatformDotnetTestSupport=True -p:Configuration=Release -p:nodeReuse=false \"{testAsset.TargetAssetPath}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            workingDirectory: testAsset.TargetAssetPath,
            environmentVariables: dotnetHostPathEnvVar,
            failIfReturnValueIsNotZero: false);

        string outputFileLog = Directory.GetFiles(testAsset.TargetAssetPath, "MSBuild Tests_net9.0_x64.log", SearchOption.AllDirectories).Single();
        Assert.IsTrue(File.Exists(outputFileLog), $"Expected file '{outputFileLog}'");
        string logFileContent = File.ReadAllText(outputFileLog);
        // This is the architecture part that's written by TerminalOutputDevice when there is no banner specified.
        Assert.Contains($"[win-x64 - {TargetFrameworks.NetCurrent}]", logFileContent);
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
    [DynamicData(nameof(GetBuildMatrix), DynamicDataDisplayName = nameof(FormatBuildMatrixEntry))]
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
            .PatchCodeWithReplace("$TargetFrameworks$", $"<targetFramework>{tfm}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", testSucceeded.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{testCommand} -p:TestingPlatformShowTestsFailure=True -p:TestingPlatformCaptureOutput=False -p:Configuration={compilationMode} -p:nodeReuse=false {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: testAsset.TargetAssetPath, failIfReturnValueIsNotZero: false);

        compilationResult.AssertOutputContains("error test failed: Test2 (");
        compilationResult.AssertOutputContains("FAILED: Expected 'true', but got 'false'.");
        compilationResult.AssertOutputContains("Microsoft.Testing.Platform");
    }

    [TestMethod]
    public async Task TestingPlatformDisableCustomTestTarget_Should_Cause_UserDefined_Target_To_Run()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$PlatformTarget$", "<PlatformTarget>x64</PlatformTarget>")
            .PatchCodeWithReplace("$TargetFrameworks$", $"<targetFramework>{TargetFrameworks.NetCurrent}</targetFramework>")
            .PatchCodeWithReplace("$AssertValue$", "true")
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -p:TestingPlatformDisableCustomTestTarget=true -p:ImportUserDefinedTestTarget=true -t:\"Build;Test\"", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

        compilationResult.AssertOutputContains("Error from UserDefinedTestTarget.targets");
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

    <Import Project="UserDefinedTestTarget.targets" Condition="'$(ImportUserDefinedTestTarget)' == 'true'" />
</Project>

#file UserDefinedTestTarget.targets
<Project>
    <Target Name="Test">
        <Error Text="Error from UserDefinedTestTarget.targets" />
    </Target>
</Project>

#file dotnet.config
[dotnet.test.runner]
name= "VSTest"

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.MSBuild;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        MyExtension myExtension = new();
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,sp) => new DummyTestFramework(sp, myExtension));
        builder.AddTreeNodeFilterService(myExtension);
        builder.AddMSBuild();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class MyExtension : IExtension
{
    public string Uid => "MyExtension";
    public string Version => "1.0.0";
    public string DisplayName => "My Extension";
    public string Description => "My Extension Description";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    private IServiceProvider _sp;
    private MyExtension _myExtension;

    public DummyTestFramework(IServiceProvider sp, MyExtension myExtension)
    {
        _sp = sp;
        _myExtension = myExtension;
    }

    public string Uid => _myExtension.Uid;

    public string Version => _myExtension.Version;

    public string DisplayName => _myExtension.DisplayName;

    public string Description => _myExtension.Description;

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => _myExtension.IsEnabledAsync();

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode { Uid = "1", DisplayName = "Test1", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode { Uid = "1", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        if (!_sp.GetCommandLineOptions().TryGetOptionArgumentList("--treenode-filter", out _))
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode { Uid = "2", DisplayName = "Test2", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode { Uid = "2", DisplayName = "Test2", Properties = new($AssertValue$ ? PassedTestNodeStateProperty.CachedInstance : new FailedTestNodeStateProperty("FAILED: Expected 'true', but got 'false'.")) }));
        }

       context.Complete();
    }
}
""";
}

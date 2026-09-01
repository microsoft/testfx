// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Combinatorial.MSTest;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests : AcceptanceTestBase<NopAssetFixture>
{
    [CombinatorialData]
    [TestMethod]
    public async Task ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned([AllTargetFrameworks] string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned),
            SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        string buildOrPublishCommand = verb == Verb.publish ? $"publish -f {tfm}" : "build";
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{buildOrPublishCommand} -v:normal {testAsset.TargetAssetPath} -c {compilationMode}", cancellationToken: TestContext.CancellationToken);

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);
        string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testconfig.json");
        string generatedConfigurationFileInBuildOutput = Path.Combine(testAsset.TargetAssetPath, "bin", compilationMode.ToString(), tfm, "MSBuildTests.testconfig.json");
        Assert.IsTrue(File.Exists(generatedConfigurationFile));
        Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
        Assert.Contains("Microsoft Testing Platform configuration file written", compilationResult.StandardOutput);

        compilationResult = await DotnetCli.RunAsync($"{buildOrPublishCommand} -v:normal {testAsset.TargetAssetPath} -c {compilationMode}", cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(File.Exists(generatedConfigurationFile));
        Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
        // Assert is failing, probably the MSBuild regression which is being fixed in https://github.com/dotnet/msbuild/pull/12431 ?
        // compilationResult.AssertOutputContains("Microsoft Testing Platform configuration file written");
        Assert.IsTrue(Regex.IsMatch(
            compilationResult.StandardOutput,
            """
\s*_GenerateTestingPlatformConfigurationFileCore:
\s*Skipping target "_GenerateTestingPlatformConfigurationFileCore" because all output files are up\-to\-date with respect to the input files\.
"""));

        await DotnetCli.RunAsync($"{buildOrPublishCommand} -v:normal {testAsset.TargetAssetPath} -c {compilationMode} /p:GenerateTestingPlatformConfigurationFile=false", cancellationToken: TestContext.CancellationToken);
        Assert.IsFalse(File.Exists(generatedConfigurationFile));
        Assert.IsFalse(File.Exists(generatedConfigurationFileInBuildOutput));

        File.WriteAllText(generatedConfigurationFile, ConfigurationContent);
        if (verb == Verb.publish)
        {
            File.WriteAllText(generatedConfigurationFileInBuildOutput, ConfigurationContent);
        }

        File.Delete(Path.Combine(testAsset.TargetAssetPath, "testconfig.json"));
        await DotnetCli.RunAsync($"{buildOrPublishCommand} -v:normal {testAsset.TargetAssetPath} -c {compilationMode}", cancellationToken: TestContext.CancellationToken);
        Assert.IsFalse(File.Exists(generatedConfigurationFile));
        Assert.IsFalse(File.Exists(generatedConfigurationFileInBuildOutput));

        await DotnetCli.RunAsync($"clean -c {compilationMode} -v:normal {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);

        // dotnet clean doesn't clean the publish output folder
        if (verb == Verb.build)
        {
            Assert.IsFalse(File.Exists(generatedConfigurationFile));
        }
    }

    [CombinatorialData]
    [TestMethod]
    public async Task ConfigFileGeneration_NoConfigurationFile_TaskWontRun([AllTargetFrameworks] string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_NoConfigurationFile_TaskWontRun),
            SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        File.Delete(Path.Combine(testAsset.TargetAssetPath, "testconfig.json"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:diagnostic {testAsset.TargetAssetPath} -c {compilationMode}", cancellationToken: TestContext.CancellationToken);

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);

        // Working around MSBuild regression: waiting for fix https://github.com/dotnet/msbuild/pull/12431
        // After we insert a new SDK version that ships with a working MSBuild, the DoesNotContain assert will fail.
        // Then, remove the DoesNotContain line, and uncomment the Contains line.
        // Assert.Contains("Target \"_GenerateTestingPlatformConfigurationFileCore\" skipped, due to false condition;", compilationResult.StandardOutput);
        Assert.DoesNotContain("_GenerateTestingPlatformConfigurationFileCore", compilationResult.StandardOutput);

        string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testconfig.json");
        Assert.IsFalse(File.Exists(generatedConfigurationFile));
    }

    [TestMethod]
    public async Task ConfigFileGeneration_OptionDefaultsGenerateConfigurationWithoutSourceFile()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_OptionDefaultsGenerateConfigurationWithoutSourceFile),
            OptionDefaultsSourceCode
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        await DotnetCli.RunAsync($"build -v:normal {testAsset.TargetAssetPath} -c Release", cancellationToken: TestContext.CancellationToken);

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildOptionDefaults", "net8.0", buildConfiguration: BuildConfiguration.Release);
        string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildOptionDefaults.testconfig.json");
        Assert.IsTrue(File.Exists(generatedConfigurationFile));
        using var document = JsonDocument.Parse(File.ReadAllText(generatedConfigurationFile));
        JsonElement defaults = document.RootElement.GetProperty("commandLineOptionDefaults");
        Assert.AreEqual("{asm}.trx", defaults.GetProperty("report-trx-filename").GetString());
        Assert.AreSequenceEqual(
            ["first", "second"],
            defaults.GetProperty("filter-uid").EnumerateArray().Select(x => x.GetString()).ToArray());

        DotnetMuxerResult unchangedBuildResult = await DotnetCli.RunAsync(
            $"build -v:normal {testAsset.TargetAssetPath} -c Release",
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(Regex.IsMatch(
            unchangedBuildResult.StandardOutput,
            """
            \s*_GenerateTestingPlatformConfigurationFileCore:
            \s*Skipping target "_GenerateTestingPlatformConfigurationFileCore" because all output files are up\-to\-date with respect to the input files\.
            """));

        await DotnetCli.RunAsync(
            $"build -v:normal {testAsset.TargetAssetPath} -c Release /p:TrxFileName=changed.trx",
            cancellationToken: TestContext.CancellationToken);

        using var updatedDocument = JsonDocument.Parse(File.ReadAllText(generatedConfigurationFile));
        Assert.AreEqual(
            "changed.trx",
            updatedDocument.RootElement.GetProperty("commandLineOptionDefaults").GetProperty("report-trx-filename").GetString());
    }

    [TestMethod]
    [DataRow("duplicate", """{"value": 1, "value": 2}""")]
    [DataRow("trailing-content", """{} trailing""")]
    public async Task ConfigFileGeneration_PackagedTaskRejectsInvalidJson(string scenario, string json)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            $"{nameof(ConfigFileGeneration_PackagedTaskRejectsInvalidJson)}_{scenario}",
            InvalidJsonOptionDefaultsSourceCode
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$JsonContent$", json));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build -v:normal {testAsset.TargetAssetPath} -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIsNot(0);
        result.AssertOutputContains("Failed to parse Microsoft Testing Platform configuration file");
    }

    private const string ConfigurationContent = """
{
  "platformOptions": {
    "exitProcessOnUnhandledException": true
  }
}
""";

    private const string SourceCode = """
#file MSBuildTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file testconfig.json
$JsonContent$

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

    private const string OptionDefaultsSourceCode = """
    #file MSBuildOptionDefaults.csproj
    <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <OutputType>Exe</OutputType>
            <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
            <TrxFileName Condition="'$(TrxFileName)' == ''">{asm}.trx</TrxFileName>
        </PropertyGroup>
        <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
            <TestingPlatformCommandLineOptionDefault Include="report-trx-filename" Value="$(TrxFileName)" />
            <TestingPlatformCommandLineOptionDefault Include="filter-uid" Value="first" />
            <TestingPlatformCommandLineOptionDefault Include="filter-uid" Value="second" />
        </ItemGroup>
    </Project>

    #file Program.cs
    public static class Program
    {
        public static void Main()
        {
        }
    }
    """;

    private const string InvalidJsonOptionDefaultsSourceCode = """
    #file MSBuildInvalidOptionDefaults.csproj
    <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <OutputType>Exe</OutputType>
            <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        </PropertyGroup>
        <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
            <TestingPlatformCommandLineOptionDefault Include="report-trx-filename" Value="{asm}.trx" />
        </ItemGroup>
    </Project>

    #file testconfig.json
    $JsonContent$

    #file Program.cs
    public static class Program
    {
        public static void Main()
        {
        }
    }
    """;

    public TestContext TestContext { get; set; }
}

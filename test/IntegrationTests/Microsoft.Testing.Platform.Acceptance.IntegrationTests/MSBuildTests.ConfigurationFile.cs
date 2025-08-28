// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests : AcceptanceTestBase<NopAssetFixture>
{
    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    [TestMethod]
    public async Task ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned(string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned),
            SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:normal -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);
        string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testconfig.json");
        Assert.IsTrue(File.Exists(generatedConfigurationFile));
        Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
        Assert.Contains("Microsoft Testing Platform configuration file written", compilationResult.StandardOutput);

        compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:normal -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.IsTrue(File.Exists(generatedConfigurationFile));
        Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
        compilationResult.StandardOutput.Contains("Microsoft Testing Platform configuration file written");
        Assert.IsTrue(Regex.IsMatch(
            compilationResult.StandardOutput,
            """
\s*_GenerateTestingPlatformConfigurationFileCore:
\s*Skipping target "_GenerateTestingPlatformConfigurationFileCore" because all output files are up\-to\-date with respect to the input files\.
"""));
        await DotnetCli.RunAsync($"clean -c {compilationMode} -v:normal {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        // dotnet clean doesn't clean the publish output folder
        if (verb == Verb.build)
        {
            Assert.IsFalse(File.Exists(generatedConfigurationFile));
        }
    }

    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    [TestMethod]
    public async Task ConfigFileGeneration_NoConfigurationFile_TaskWontRun(string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_NoConfigurationFile_TaskWontRun),
            SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        File.Delete(Path.Combine(testAsset.TargetAssetPath, "testconfig.json"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:diagnostic -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);

        // Working around MSBuild regression: waiting for fix https://github.com/dotnet/msbuild/pull/12431
        // After we insert a new SDK version that ships with a working MSBuild, the DoesNotContain assert will fail.
        // Then, remove the DoesNotContain line, and uncomment the Contains line.
        // Assert.Contains("Target \"_GenerateTestingPlatformConfigurationFileCore\" skipped, due to false condition;", compilationResult.StandardOutput);
        Assert.DoesNotContain("_GenerateTestingPlatformConfigurationFileCore", compilationResult.StandardOutput);

        string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testconfig.json");
        Assert.IsFalse(File.Exists(generatedConfigurationFile));
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
}

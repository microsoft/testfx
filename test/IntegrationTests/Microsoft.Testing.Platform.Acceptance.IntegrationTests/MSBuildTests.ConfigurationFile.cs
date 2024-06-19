// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1636 // File header copyright text should match
// Copyright (c) Microsoft Corporation. All rights reserved.
#pragma warning restore SA1636 // File header copyright text should match

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests : AcceptanceTestBase
{
    private static readonly SemaphoreSlim Lock = new(1);

    public MSBuildTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned(string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        await Lock.WaitAsync();
        try
        {
            using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                nameof(ConfigFileGeneration_CorrectlyCreateAndCacheAndCleaned),
                SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));

            DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:normal -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

            var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);
            string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testingplatformconfig.json");
            Assert.IsTrue(File.Exists(generatedConfigurationFile));
            Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
            Assert.IsTrue(compilationResult.StandardOutput.Contains("Microsoft Testing Platform configuration file written"));

            compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:normal -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            Assert.IsTrue(File.Exists(generatedConfigurationFile));
            Assert.AreEqual(ConfigurationContent.Trim(), File.ReadAllText(generatedConfigurationFile).Trim());
            compilationResult.StandardOutput.Contains("Microsoft Testing Platform configuration file written");
            Assert.IsTrue(Regex.IsMatch(
                compilationResult.StandardOutput,
                """
\s*GenerateTestingPlatformConfigurationFile:
\s*Skipping target "GenerateTestingPlatformConfigurationFile" because all output files are up\-to\-date with respect to the input files\.
"""));
            compilationResult = await DotnetCli.RunAsync($"clean -c {compilationMode} -v:normal {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

            // dotnet clean doesn't clean the publish output folder
            if (verb == Verb.build)
            {
                Assert.IsFalse(File.Exists(generatedConfigurationFile));
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task ConfigFileGeneration_NoConfigurationFile_TaskWontRun(string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        await Lock.WaitAsync();
        try
        {
            using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(ConfigFileGeneration_NoConfigurationFile_TaskWontRun),
            SourceCode
            .PatchCodeWithReplace("$TargetFrameworks$", tfm)
            .PatchCodeWithReplace("$JsonContent$", ConfigurationContent)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));

            File.Delete(Path.Combine(testAsset.TargetAssetPath, "testingplatformconfig.json"));

            DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")} -v:diagnostic -nodeReuse:false {testAsset.TargetAssetPath} -c {compilationMode}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

            var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, "MSBuildTests", tfm, verb: verb, buildConfiguration: compilationMode);
            Assert.IsTrue(compilationResult.StandardOutput.Contains("Target \"GenerateTestingPlatformConfigurationFile\" skipped, due to false condition;"));
            string generatedConfigurationFile = Path.Combine(testHost.DirectoryName, "MSBuildTests.testingplatformconfig.json");
            Assert.IsFalse(File.Exists(generatedConfigurationFile));
        }
        finally
        {
            Lock.Release();
        }
    }

    private const string ConfigurationContent = """
{
  "testingplatform": {
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
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
    </ItemGroup>
</Project>

#file testingplatformconfig.json
$JsonContent$

#file Program.cs
using MSBuildTests;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
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
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
""";

    private readonly AcceptanceFixture _acceptanceFixture;
}

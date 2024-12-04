// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests_Solution : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    public MSBuildTests_Solution(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    internal static IEnumerable<TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)> entry in GetBuildMatrixSingleAndMultiTfmBuildConfiguration())
        {
            foreach (string command in new string[]
            {
                "build --no-restore -t:Test -p:UseMSBuildTestInfrastructure=true",
                "test --no-restore",
            })
            {
                yield return new TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>(
                (entry.Arguments.SingleTfmOrMultiTfm, entry.Arguments.BuildConfiguration, entry.Arguments.IsMultiTfm, command), $"{(entry.Arguments.IsMultiTfm ? "multitfm" : entry.Arguments.SingleTfmOrMultiTfm)},{entry.Arguments.BuildConfiguration},{command}");
            }
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix))]
    public async Task MSBuildTests_UseMSBuildTestInfrastructure_Should_Run_Solution_Tests(string singleTfmOrMultiTfm, BuildConfiguration _, bool isMultiTfm, string command)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{singleTfmOrMultiTfm}</TargetFrameworks>" : $"<TargetFramework>{singleTfmOrMultiTfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
            .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));

        string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSBuildTests.csproj", SearchOption.AllDirectories).Single());
        string programSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Program.cs", SearchOption.AllDirectories).Single());
        string unitTestSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "UnitTest1.cs", SearchOption.AllDirectories).Single());
        string usingsSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Usings.cs", SearchOption.AllDirectories).Single());
        string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "NuGet.config", SearchOption.AllDirectories).Single());

        // Create a solution with 3 projects
        using TempDirectory tempDirectory = new();
        string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
        VSSolution solution = new(solutionFolder, "MSTestSolution");
        string nugetFile = solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
        for (int i = 0; i < 3; i++)
        {
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("Program.cs", programSourceContent.PatchCodeWithReplace("$ProjectName$", $"TestProject{i}"));
            project.AddOrUpdateFileContent("UnitTest1.cs", unitTestSourceContent);
            project.AddOrUpdateFileContent("Usings.cs", usingsSourceContent);

            CSharpProject project2 = solution.CreateCSharpProject($"Project{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            project.AddProjectReference(project2.ProjectFile);
        }

        // Build the solution
        DotnetMuxerResult restoreResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {solution.SolutionFile} --configfile {nugetFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        restoreResult.AssertOutputNotContains("An approximate best match of");
        DotnetMuxerResult testResult = await DotnetCli.RunAsync($"{command} -nodeReuse:false {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        if (isMultiTfm)
        {
            foreach (string tfm in singleTfmOrMultiTfm.Split(';'))
            {
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{tfm}\|x64\]");
            }
        }
        else
        {
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{singleTfmOrMultiTfm}\|x64\]");
        }
    }

    private const string SourceCode = """
#file MSBuildTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        $TargetFrameworks$
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <PlatformTarget>x64</PlatformTarget>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
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
using $ProjectName$;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
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
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Platform.MSBuild;
""";
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildMSTestRunnerTests : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    public MSBuildMSTestRunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    //[ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    //public async Task MSBuildTestTarget_SingleTfm_Should_Run_Solution_Tests(string tfm, BuildConfiguration buildConfiguration)
    //{
    //    // Get the template project
    //    TestAsset generator = await TestAsset.GenerateAssetAsync(
    //       AssetName,
    //       CurrentMSTestSourceCode
    //       .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
    //       .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
    //       .PatchCodeWithReplace("$MSTestVersion$", MSTestCurrentVersion)
    //       .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
    //       .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>"),
    //       addPublicFeeds: true);
    //    string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSTestProject.csproj", SearchOption.AllDirectories).Single());
    //    string testSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "UnitTest1.cs", SearchOption.AllDirectories).Single());
    //    string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Nuget.config", SearchOption.AllDirectories).Single());

    //    // Create a solution with 3 projects
    //    TempDirectory tempDirectory = new();
    //    string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
    //    VSSolution solution = new(solutionFolder, "MSTestSolution");
    //    solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
    //    for (int i = 0; i < 3; i++)
    //    {
    //        CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", tfm);
    //        File.WriteAllText(project.ProjectFile, projectContent);
    //        project.AddOrUpdateFileContent("UnitTest1.cs", testSourceContent);
    //    }

    //    // Build the solution
    //    await DotnetCli.RunAsync($"build {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    //    var testResult = await DotnetCli.RunAsync($"msbuild /t:Test {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    //    testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{tfm}\|x64\]");
    //    testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{tfm}\|x64\]");
    //    testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{tfm}\|x64\]");
    //}

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmBuildConfiguration))]
    public async Task MSBuildTestTarget_SingleMultiTfm_Should_Run_Solution_Tests(string multiTfm, BuildConfiguration buildConfiguration)
    {
        // Get the template project
        TestAsset generator = await TestAsset.GenerateAssetAsync(
           AssetName,
           CurrentMSTestSourceCode
           .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
           .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
           .PatchCodeWithReplace("$MSTestVersion$", MSTestCurrentVersion)
           .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
           .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>"),
           addPublicFeeds: true);
        string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSTestProject.csproj", SearchOption.AllDirectories).Single());
        string testSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "UnitTest1.cs", SearchOption.AllDirectories).Single());
        string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Nuget.config", SearchOption.AllDirectories).Single());

        // Create a solution with 3 projects
        TempDirectory tempDirectory = new();
        string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
        VSSolution solution = new(solutionFolder, "MSTestSolution");
        solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
        for (int i = 0; i < 3; i++)
        {
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", multiTfm.Split(';'));
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("UnitTest1.cs", testSourceContent);
        }

        // Build the solution
        await DotnetCli.RunAsync($"build {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testResult = await DotnetCli.RunAsync($"msbuild /t:Test {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        //testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{tfm}\|x64\]");
        //testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{tfm}\|x64\]");
        //testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{tfm}\|x64\]");
    }
}

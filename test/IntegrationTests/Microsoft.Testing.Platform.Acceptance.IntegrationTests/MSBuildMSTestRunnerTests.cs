// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task MSBuildTestTarget_Should_Run_Solution_Tests(string tfm, BuildConfiguration buildConfiguration)
    {
        // Get the template project
        TestAsset generator = await TestAsset.GenerateAssetAsync(
           AssetName,
           CurrentMSTestSourceCode
           .PatchCodeWithReplace("$TargetFramework$", tfm)
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
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", tfm);
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("UnitTest1.cs", testSourceContent);
        }

        // Build the solution
        var buildResult = await DotnetCli.RunAsync($"build {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testRestult = await DotnetCli.RunAsync($"msbuild /t:Test {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    }
}

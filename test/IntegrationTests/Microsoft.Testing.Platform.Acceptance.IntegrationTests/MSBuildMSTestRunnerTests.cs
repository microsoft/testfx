// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

// [TestGroup]
public class MSBuildMSTestRunnerTests : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";
    private const string DotnetTestVerb = "test";

    public MSBuildMSTestRunnerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    internal static IEnumerable<TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)> entry in GetBuildMatrixSingleAndMultiTfmBuildConfiguration())
        {
            foreach (var command in new string[]
            {
                "build --no-restore /t:Test",
                DotnetTestVerb,
            })
            {
                yield return new TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>(
                (entry.Arguments.SingleTfmOrMultiTfm, entry.Arguments.BuildConfiguration, entry.Arguments.IsMultiTfm, command), $"{(entry.Arguments.IsMultiTfm ? "multitfm" : entry.Arguments.SingleTfmOrMultiTfm)},{entry.Arguments.BuildConfiguration},{command}");
            }
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix))]
    public async Task MSBuildTestTarget_SingleAndMultiTfm_Should_Run_Solution_Tests(string singleTfmOrMultiTfm, BuildConfiguration buildConfiguration, bool isMultiTfm, string command)
    {
        // Get the template project
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
           AssetName,
           CurrentMSTestSourceCode
           .PatchCodeWithReplace("$TargetFramework$", isMultiTfm ? $"<TargetFrameworks>{singleTfmOrMultiTfm}</TargetFrameworks>" : $"<TargetFramework>{singleTfmOrMultiTfm}</TargetFramework>")
           .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
           .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
           .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
           .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
           .PatchCodeWithReplace("$Extra$", command == DotnetTestVerb ?
"""
           <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
           <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
""" :
           string.Empty),
           addPublicFeeds: true);

        string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSTestProject.csproj", SearchOption.AllDirectories).Single());
        string testSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "UnitTest1.cs", SearchOption.AllDirectories).Single());
        string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "NuGet.config", SearchOption.AllDirectories).Single());

        // Create a solution with 3 projects
        using TempDirectory tempDirectory = new();
        string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
        VSSolution solution = new(solutionFolder, "MSTestSolution");
        string nugetFile = solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
        for (int i = 0; i < 3; i++)
        {
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : new[] { singleTfmOrMultiTfm });
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("UnitTest1.cs", testSourceContent);
        }

        // Build the solution
        var restoreResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {solution.SolutionFile} --configfile {nugetFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        restoreResult.AssertOutputNotContains("An approximate best match of");
        var testResult = await DotnetCli.RunAsync($"{command} -nodeReuse:false {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

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
}

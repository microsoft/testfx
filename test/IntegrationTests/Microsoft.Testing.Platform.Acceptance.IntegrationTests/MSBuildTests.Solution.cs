// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests_Solution : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestProject";

    internal static IEnumerable<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)> GetBuildMatrix()
    {
        foreach ((string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm) entry in GetBuildMatrixSingleAndMultiTfmBuildConfiguration())
        {
            foreach (string command in new string[]
            {
                "build --no-restore -t:Test -p:UseMSBuildTestInfrastructure=true",
                "test --no-restore",
            })
            {
                yield return new(entry.SingleTfmOrMultiTfm, entry.BuildConfiguration, entry.IsMultiTfm, command);
            }
        }
    }

    [DynamicData(nameof(GetBuildMatrix))]
    [TestMethod]
    public async Task MSBuildTests_UseMSBuildTestInfrastructure_Should_Run_Solution_Tests(string singleTfmOrMultiTfm, BuildConfiguration _, bool isMultiTfm, string command)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{singleTfmOrMultiTfm}</TargetFrameworks>" : $"<TargetFramework>{singleTfmOrMultiTfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSBuildTests.csproj", SearchOption.AllDirectories).Single());
        string programSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Program.cs", SearchOption.AllDirectories).Single());
        string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "NuGet.config", SearchOption.AllDirectories).Single());

        // Create a solution with 3 projects
        using TempDirectory tempDirectory = new();
        string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
        VSSolution solution = new(solutionFolder, "MSTestSolution");
        string nugetFile = solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
        solution.AddOrUpdateFileContent("global.json", """
            {
              "test": {
                "runner": "VSTest"
              }
            }
            """);
        for (int i = 0; i < 3; i++)
        {
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("Program.cs", programSourceContent.PatchCodeWithReplace("$ProjectName$", $"TestProject{i}"));

            CSharpProject project2 = solution.CreateCSharpProject($"Project{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            project.AddProjectReference(project2.ProjectFile);
        }

        // Build the solution
        DotnetMuxerResult restoreResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {solution.SolutionFile} --configfile {nugetFile}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, cancellationToken: TestContext.CancellationToken);
        restoreResult.AssertOutputDoesNotContain("An approximate best match of");
        DotnetMuxerResult testResult = await DotnetCli.RunAsync($"{command} -nodeReuse:false {solution.SolutionFile}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: solution.FolderPath, cancellationToken: TestContext.CancellationToken);

        if (isMultiTfm)
        {
            foreach (string tfm in singleTfmOrMultiTfm.Split(';'))
            {
                testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject0\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject1\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject2\..*' \[{tfm}\|x64\]");
            }
        }
        else
        {
            testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject0\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject1\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputMatchesRegex($@"Tests succeeded: '.*TestProject2\..*' \[{singleTfmOrMultiTfm}\|x64\]");
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
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.MSBuild;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        builder.AddMSBuild();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
    new TestNode() { Uid = "1", DisplayName = "Test1", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
    new TestNode() { Uid = "1", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        context.Complete();
    }
}
""";

    public TestContext TestContext { get; set; }
}

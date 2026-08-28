// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal class DotnetMuxer : IStep<SingleProject, BuildArtifact>
{
    private readonly BuildConfiguration _buildConfiguration;

    public DotnetMuxer(BuildConfiguration buildConfiguration = BuildConfiguration.Release) => _buildConfiguration = buildConfiguration;

    public string Description => "dotnet build";

    public async Task<BuildArtifact> ExecuteAsync(SingleProject payload, IContext context)
    {
        if (payload.Tfms.Length > 1)
        {
            throw new NotSupportedException();
        }

        string binlogPath = Path.Combine(payload.TestAsset.TargetAssetPath, "Build.binlog");
        string buildCommand = $"build \"{payload.TestAsset.TargetAssetPath}\" -c {_buildConfiguration} -bl:\"{binlogPath}\"";
        Console.WriteLine($"Building: '{buildCommand}'");
        await DotnetCli.RunAsync(buildCommand);
        TestHost? testHost = payload.TestPlatform == TestPlatform.Mtp
            ? TestHost.LocateFrom(payload.TestAsset.TargetAssetPath, payload.AssetName, payload.Tfms.Single(), buildConfiguration: _buildConfiguration)
            : null;

        return new BuildArtifact(testHost, payload, _buildConfiguration);
    }
}

internal class BuildArtifact : IPayload
{
    public BuildArtifact(TestHost? testHost, SingleProject project, BuildConfiguration buildConfiguration)
    {
        TestHost = testHost;
        Project = project;
        BuildConfiguration = buildConfiguration;
    }

    public TestHost? TestHost { get; }

    public SingleProject Project { get; }

    public BuildConfiguration BuildConfiguration { get; }

    public TestAsset TestAsset => Project.TestAsset;

    public string ResultFilePath => Path.Combine(Project.TestAsset.TargetAssetPath, "Result.json");

    public TestHost GetRequiredTestHost()
        => TestHost ?? throw new InvalidOperationException($"Pipeline '{Project.AssetName}' requires an executable MTP test host.");
}

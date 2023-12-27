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

        string nugetRestoreFolder = Path.Combine(payload.TestAsset.TargetAssetPath, ".packages");
        string buildCommand = $"build {payload.TestAsset.TargetAssetPath} -c {_buildConfiguration}";
        Console.WriteLine($"Building: '{buildCommand}'");
        await DotnetCli.RunAsync(buildCommand, nugetRestoreFolder);
        var testHost = TestHost.LocateFrom(payload.TestAsset.TargetAssetPath, payload.AssetName, payload.Tfms.Single(), buildConfiguration: _buildConfiguration);
        return new BuildArtifact(testHost, payload.TestAsset);
    }
}

internal class BuildArtifact : IPayload
{
    public BuildArtifact(TestHost testHost, TestAsset testAsset)
    {
        TestHost = testHost;
        TestAsset = testAsset;
    }

    public TestHost TestHost { get; }

    public TestAsset TestAsset { get; }
}

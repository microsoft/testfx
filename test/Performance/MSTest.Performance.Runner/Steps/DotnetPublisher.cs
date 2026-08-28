// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal sealed class DotnetPublisher : IStep<SingleProject, BuildArtifact>
{
    public string Description => "dotnet publish (NativeAOT)";

    public async Task<BuildArtifact> ExecuteAsync(SingleProject payload, IContext context)
    {
        if (payload.SourceGenerationMode != MSTestSourceGenerationMode.NativeAot)
        {
            throw new InvalidOperationException("DotnetPublisher is only valid for the NativeAOT benchmark mode.");
        }

        EnsureVsWhereOnPath();

        string runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
        string binlogPath = Path.Combine(payload.TestAsset.TargetAssetPath, "Publish.binlog");
        string publishCommand =
            $"publish \"{payload.TestAsset.TargetAssetPath}\" -c {BuildConfiguration.Release} -r {runtimeIdentifier} " +
            $"--self-contained -p:PublishAot=true -p:MSTestSourceGenMode=ReflectionFree -bl:\"{binlogPath}\"";
        Console.WriteLine($"Publishing: '{publishCommand}'");
        await DotnetCli.RunAsync(publishCommand);

        var testHost = TestHost.LocateFrom(
            payload.TestAsset.TargetAssetPath,
            payload.AssetName,
            payload.Tfms.Single(),
            runtimeIdentifier,
            Verb.publish,
            BuildConfiguration.Release);

        return new BuildArtifact(testHost, payload, BuildConfiguration.Release);
    }

    private static void EnsureVsWhereOnPath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string installerDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer");
        if (!File.Exists(Path.Combine(installerDirectory, "vswhere.exe")))
        {
            return;
        }

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Split(Path.PathSeparator).Contains(installerDirectory, StringComparer.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{installerDirectory}{Path.PathSeparator}{path}");
        }
    }
}

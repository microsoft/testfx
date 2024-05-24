// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal class DotnetTrace : IStep<BuildArtifact, Files>
{
    private readonly string _arguments;
    private readonly string _reportFileName;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run under dotnet-trace";

    public DotnetTrace(string arguments, string reportFileName, CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _arguments = arguments;
        _reportFileName = reportFileName;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        string nugetRestoreFolder = Path.Combine(payload.TestAsset.TargetAssetPath, ".packages");
        await DotnetCli.RunAsync($"tool install --tool-path \"{payload.TestAsset.TargetAssetPath}\" dotnet-trace", nugetRestoreFolder);

        string dotnetTrace = Path.Combine(payload.TestAsset.TargetAssetPath, "dotnet-trace" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty));

        ProcessStartInfo processStartInfo =
        new(dotnetTrace, $" collect {_arguments} -- \"{payload.TestHost.FullName}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = payload.TestAsset.TargetAssetPath,
        };

        Console.WriteLine($"dotnet-trace command: '{processStartInfo.FileName} {processStartInfo.Arguments}'");

        Process process = Process.Start(processStartInfo)!;
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);

        process.WaitForExit();

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files(new[] { sample });
    }
}

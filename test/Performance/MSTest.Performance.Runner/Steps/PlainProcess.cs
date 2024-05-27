// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace MSTest.Performance.Runner.Steps;

internal class PlainProcess : IStep<BuildArtifact, Files>
{
    private readonly string _reportFileName;
    private readonly int _numberOfRun;
    private readonly string _argument;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run plain Process.Start";

    public PlainProcess(string reportFileName, int numberOfRun = 3, string argument = "", CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _reportFileName = reportFileName;
        _numberOfRun = numberOfRun;
        _argument = argument;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        ProcessStartInfo processStartInfo =
           new(payload.TestHost.FullName, _argument)
           {
               UseShellExecute = false,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               RedirectStandardInput = true,
           };

        Console.WriteLine($"Process command: '{processStartInfo.FileName} {processStartInfo.Arguments.Trim()}' for {_numberOfRun} times");

        List<object> results = new();
        for (int i = 0; i < _numberOfRun; i++)
        {
            Process process = Process.Start(processStartInfo)!;
            await process.WaitForExitAsync();
            var result = new
            {
                ElapsedTime = process.ExitTime - process.StartTime,
                process.TotalProcessorTime,
                Environment.ProcessorCount,
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            };

            results.Add(result);
        }

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        await File.AppendAllTextAsync(Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "Result.json"), JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions() { WriteIndented = true }));
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");

        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files([sample]);
    }
}

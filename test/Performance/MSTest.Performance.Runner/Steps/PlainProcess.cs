// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal class PlainProcess : IStep<BuildArtifact, Files>
{
    private readonly string _reportFileName;
    private readonly int _numberOfRun;
    private readonly int _warmupCount;
    private readonly string _argument;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run plain Process.Start";

    public PlainProcess(string reportFileName, int numberOfRun = 5, int warmupCount = 1, string argument = "", CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _reportFileName = reportFileName;
        _numberOfRun = numberOfRun;
        _warmupCount = warmupCount;
        _argument = argument;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        TestHost testHost = payload.TestHost
            ?? throw new InvalidOperationException("A standalone process benchmark requires an MTP executable test host.");

        ProcessStartInfo processStartInfo =
           new(testHost.FullName, $"--no-ansi --progress off {_argument}".Trim())
           {
               UseShellExecute = false,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               WorkingDirectory = testHost.DirectoryName,
           };

        ProcessBenchmarkRunner.ConfigureEnvironment(processStartInfo);
        await ProcessBenchmarkRunner.RunAsync(
            processStartInfo,
            payload,
            context,
            executionKind: "MTP standalone",
            processResourceScope: "test host",
            captureProcessResources: true,
            numberOfRuns: _numberOfRun,
            warmupCount: _warmupCount);

        if (string.Equals(Environment.GetEnvironmentVariable("MSTEST_PERFORMANCE_SKIP_ARCHIVE"), "1", StringComparison.Ordinal))
        {
            return new Files([payload.ResultFilePath]);
        }

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");

        await ZipFile.CreateFromDirectoryAsync(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files([sample]);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

/// <summary>
/// Runs the test project via <c>dotnet test --no-build</c> and records wall-clock
/// timing using the same plain <see cref="Process"/> metrics as <see cref="PlainProcess"/>.
/// </summary>
/// <remarks>
/// <para>
/// With <c>EnableMSTestRunner=true</c> (MTP native mode), <c>dotnet test</c> invokes the
/// compiled test host in server mode, passing <c>--server --protocol dotnet-test-protocol</c>.
/// The host then communicates results back via a named pipe / TCP socket rather than running
/// standalone. This exercises the serialisation, JSON-RPC framing, and pipe I/O paths that
/// the plain-process scenario does not cover. With <c>EnableMSTestRunner=false</c>, the same
/// step exercises the VSTest runner selected by the generated asset's <c>global.json</c>.
/// </para>
/// <para>
/// <b>Measurement note:</b> <see cref="Process.TotalProcessorTime"/> reflects only the
/// <c>dotnet test</c> parent process; the spawned test-host child's CPU time is not included.
/// Wall-clock time is the primary metric and represents the end-to-end time a user observes
/// when running <c>dotnet test</c>.
/// </para>
/// </remarks>
internal class DotnetTestProcess : IStep<BuildArtifact, Files>
{
    private readonly string _reportFileName;
    private readonly BuildConfiguration _buildConfiguration;
    private readonly int _numberOfRun;
    private readonly int _warmupCount;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run dotnet test";

    public DotnetTestProcess(string reportFileName, BuildConfiguration buildConfiguration = BuildConfiguration.Release, int numberOfRun = 5, int warmupCount = 1, CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _reportFileName = reportFileName;
        _buildConfiguration = buildConfiguration;
        _numberOfRun = numberOfRun;
        _warmupCount = warmupCount;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        if (_buildConfiguration != payload.BuildConfiguration)
        {
            throw new InvalidOperationException(
                $"The dotnet test configuration '{_buildConfiguration}' must match the built configuration '{payload.BuildConfiguration}'.");
        }

        string root = RootFinder.Find();
        string dotnet = Path.Combine(root, ".dotnet", $"dotnet{Constants.ExecutableExtension}");
        string projectDir = payload.TestAsset.TargetAssetPath;

        // Use the repo-local SDK consistently with the build step (DotnetMuxer). The
        // configuration must match the one used by DotnetMuxer so that --no-build finds the
        // binaries that were actually produced. --no-restore is added because the build step
        // already restored; restoring here would fold NuGet work into the measured wall-clock
        // time and skew the server-mode signal. -p:SuppressNETCoreSdkPreviewMessage=true keeps
        // output consistent with DotnetCli when running a preview SDK. WorkingDirectory is pinned
        // to the test asset so relative outputs (TestResults, logs, temp files) stay inside the
        // generated asset rather than polluting the runner's current directory between scenarios.
        ProcessStartInfo psi = new(dotnet, $"test \"{projectDir}\" --no-build --no-restore --configuration {_buildConfiguration} -p:SuppressNETCoreSdkPreviewMessage=true")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = projectDir,
        };

        ProcessBenchmarkRunner.ConfigureEnvironment(psi);
        await ProcessBenchmarkRunner.RunAsync(
            psi,
            payload,
            context,
            executionKind: "dotnet test",
            processResourceScope: "dotnet test parent only; test-host children excluded",
            captureProcessResources: false,
            numberOfRuns: _numberOfRun,
            warmupCount: _warmupCount);

        if (string.Equals(Environment.GetEnvironmentVariable("MSTEST_PERFORMANCE_SKIP_ARCHIVE"), "1", StringComparison.Ordinal))
        {
            return new Files([payload.ResultFilePath]);
        }

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files([sample]);
    }
}

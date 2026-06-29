// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text.Json;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

/// <summary>
/// Runs the test project via <c>dotnet test --no-build</c> to exercise the MTP
/// server-mode (JSON-RPC / named-pipe) path, and records wall-clock timing using
/// the same plain <see cref="Process"/> metrics as <see cref="PlainProcess"/>.
/// </summary>
/// <remarks>
/// <para>
/// When <c>EnableMSTestRunner=true</c> (MTP native mode), <c>dotnet test</c> invokes the
/// compiled test host in server mode, passing <c>--server --protocol dotnet-test-protocol</c>.
/// The host then communicates results back via a named pipe / TCP socket rather than running
/// standalone. This exercises the serialisation, JSON-RPC framing, and pipe I/O paths that
/// the plain-process scenario does not cover.
/// </para>
/// <para>
/// <b>Measurement note:</b> <see cref="Process.TotalProcessorTime"/> reflects only the
/// <c>dotnet test</c> parent process; the spawned test-host child's CPU time is not included.
/// <see cref="Process.ExitTime"/> minus <see cref="Process.StartTime"/> (wall-clock) is the
/// primary metric and represents the end-to-end time a user observes when running
/// <c>dotnet test</c>.
/// </para>
/// </remarks>
internal class DotnetTestProcess : IStep<BuildArtifact, Files>
{
    private readonly string _reportFileName;
    private readonly BuildConfiguration _buildConfiguration;
    private readonly int _numberOfRun;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run dotnet test (MTP server mode)";

    public DotnetTestProcess(string reportFileName, BuildConfiguration buildConfiguration = BuildConfiguration.Debug, int numberOfRun = 3, CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _reportFileName = reportFileName;
        _buildConfiguration = buildConfiguration;
        _numberOfRun = numberOfRun;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
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

        // Strip ambient environment variables that the test infrastructure isolates (diagnostics,
        // dotnet-test execution IDs, NO_COLOR, LLM/agent detection, minidump, telemetry, ...) so the
        // measured run is reproducible and not influenced by the shell or CI agent it runs under,
        // matching the isolation DotnetCli applies.
        foreach (string toSkip in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            psi.EnvironmentVariables.Remove(toSkip);
        }

        psi.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.EnvironmentVariables["DOTNET_ROOT"] = Path.Combine(root, ".dotnet");
        psi.EnvironmentVariables["DOTNET_INSTALL_DIR"] = Path.Combine(root, ".dotnet");
        psi.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        psi.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        Console.WriteLine($"Process command: '{psi.FileName} {psi.Arguments.Trim()}' for {_numberOfRun} times");

        List<object> results = [];
        for (int i = 0; i < _numberOfRun; i++)
        {
            using Process process = Process.Start(psi)!;
            // Drain stdout/stderr asynchronously to prevent buffer deadlocks.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            // Fail fast on a non-zero exit code: `dotnet test` has many infrastructure failure
            // modes (restore issues, SDK mismatch, missing build artefacts) that would otherwise
            // record timings for an invalid run and silently corrupt the perf baseline.
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"'dotnet test' exited with code {process.ExitCode}.{Environment.NewLine}" +
                    $"stdout:{Environment.NewLine}{await stdoutTask}{Environment.NewLine}" +
                    $"stderr:{Environment.NewLine}{await stderrTask}");
            }

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
        await File.AppendAllTextAsync(
            Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "Result.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
#pragma warning restore CA1869

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files([sample]);
    }
}

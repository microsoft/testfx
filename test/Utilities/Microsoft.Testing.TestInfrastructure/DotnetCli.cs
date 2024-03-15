// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Microsoft.Testing.TestInfrastructure;

public static class DotnetCli
{
    private static readonly string[] CodeCoverageEnvironmentVariables = new[]
{
        "MicrosoftInstrumentationEngine_ConfigPath32_VanguardInstrumentationProfiler",
        "MicrosoftInstrumentationEngine_ConfigPath64_VanguardInstrumentationProfiler",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "COR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "CODE_COVERAGE_SESSION_NAME",
        "CODE_COVERAGE_PIPE_PATH",
        "MicrosoftInstrumentationEngine_LogLevel",
        "MicrosoftInstrumentationEngine_DisableCodeSignatureValidation",
        "MicrosoftInstrumentationEngine_FileLogPath",
};

    private static int s_maxOutstandingCommand = Environment.ProcessorCount;
    private static SemaphoreSlim s_maxOutstandingCommands_semaphore = new(s_maxOutstandingCommand, s_maxOutstandingCommand);

    public static int MaxOutstandingCommands
    {
        get
        {
            return s_maxOutstandingCommand;
        }

        set
        {
            s_maxOutstandingCommand = value;
            s_maxOutstandingCommands_semaphore.Dispose();
            s_maxOutstandingCommands_semaphore = new SemaphoreSlim(s_maxOutstandingCommand, s_maxOutstandingCommand);
        }
    }

    public static bool DoNotRetry { get; set; }

    public static async Task<DotnetMuxerResult> RunAsync(
        string args,
        string nugetGlobalPackagesFolder,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        bool failIfReturnValueIsNotZero = true,
        bool disableTelemetry = true,
        int timeoutInSeconds = 50,
        bool disableCodeCoverage = true)
    {
        await s_maxOutstandingCommands_semaphore.WaitAsync();
        try
        {
            environmentVariables ??= new Dictionary<string, string>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                // Skip all unwanted environment variables.
                if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (disableCodeCoverage)
                {
                    // Disable the code coverage during the build.
                    if (CodeCoverageEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                environmentVariables.Add(entry.Key!.ToString()!, entry.Value!.ToString()!);
            }

            if (disableTelemetry)
            {
                environmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            }

            environmentVariables["NUGET_PACKAGES"] = nugetGlobalPackagesFolder;

            if (DoNotRetry)
            {
                if (args.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Command should not start with 'dotnet'");
                }

                using var dotnet = new DotnetMuxer(environmentVariables);
                int exitCode = await dotnet.Args(args, workingDirectory, timeoutInSeconds);

                return exitCode != 0 && failIfReturnValueIsNotZero
                    ? throw new InvalidOperationException($"Command 'dotnet {args}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}")
                    : new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines);
            }
            else
            {
                var delay = Backoff.ExponentialBackoff(TimeSpan.FromSeconds(3), retryCount: 5, factor: 1.5);
                return await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(delay)
                    .ExecuteAsync(async () =>
                    {
                        if (args.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Command should not start with 'dotnet'");
                        }

                        using var dotnet = new DotnetMuxer(environmentVariables);
                        int exitCode = await dotnet.Args(args, workingDirectory, timeoutInSeconds);

                        return exitCode != 0 && failIfReturnValueIsNotZero
                            ? throw new InvalidOperationException($"Command 'dotnet {args}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}")
                            : new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines);
                    });
            }
        }
        finally
        {
            s_maxOutstandingCommands_semaphore.Release();
        }
    }
}
